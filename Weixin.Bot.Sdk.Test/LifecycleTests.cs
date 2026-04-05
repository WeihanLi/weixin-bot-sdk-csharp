using Weixin.Bot.Sdk.Bot;
using Weixin.Bot.Sdk.Models;
using Xunit;

namespace Weixin.Bot.Sdk.Test;

public sealed class LifecycleTests
{
    [Fact]
    public void Constructor_LoadsCredentials_AndRaisesCredentialsLoaded()
    {
        var credentialsPath = TestSupport.CreateCredentialsFile("bot-user");
        try
        {
            using var bot = new WeixinBot(new WeixinBotOptions
            {
                CredentialsPath = credentialsPath,
            });

            Assert.True(bot.IsLoggedIn);
            Assert.NotNull(bot.CurrentCredentials);
            Assert.Equal("bot-user", bot.CurrentCredentials!.UserId);
        }
        finally
        {
            File.Delete(credentialsPath);
        }
    }

    [Fact]
    public async Task Start_RaisesStarted_AndStopAsync_RaisesStopped()
    {
        var credentialsPath = TestSupport.CreateCredentialsFile("bot-user");
        try
        {
            var handler = new ScriptedHttpMessageHandler();
            using var httpClient = new HttpClient(handler);
            await using var bot = new WeixinBot(new WeixinBotOptions
            {
                CredentialsPath = credentialsPath,
                HttpClient = httpClient,
            });

            var started = new TaskCompletionSource<DateTimeOffset>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stopped = new TaskCompletionSource<DateTimeOffset>(TaskCreationOptions.RunContinuationsAsynchronously);
            bot.Started += (_, args) => started.TrySetResult(args.OccurredAt);
            bot.Stopped += (_, args) => stopped.TrySetResult(args.OccurredAt);

            using var cts = new CancellationTokenSource();
            bot.Start(cts.Token);

            Assert.True(await started.Task.WaitAsync(TimeSpan.FromSeconds(3)) > DateTimeOffset.MinValue);

            cts.Cancel();
            await bot.StopAsync();

            Assert.True(await stopped.Task.WaitAsync(TimeSpan.FromSeconds(3)) > DateTimeOffset.MinValue);
        }
        finally
        {
            File.Delete(credentialsPath);
        }
    }

    [Fact]
    public void Start_WithoutLogin_ThrowsInvalidOperationException()
    {
        using var bot = new WeixinBot(new WeixinBotOptions());
        var exception = Assert.Throws<InvalidOperationException>(() => bot.Start());
        Assert.Contains("Not logged in", exception.Message);
    }

    [Fact]
    public async Task Start_WhenSessionExpires_RaisesSessionExpired_AndStops()
    {
        var credentialsPath = TestSupport.CreateCredentialsFile("bot-user");
        try
        {
            var handler = new ScriptedHttpMessageHandler();
            handler.EnqueueJson("""
            {
              "ret": 0,
              "errcode": -14,
              "get_updates_buf": "expired",
              "msgs": []
            }
            """);

            using var httpClient = new HttpClient(handler);
            await using var bot = new WeixinBot(new WeixinBotOptions
            {
                CredentialsPath = credentialsPath,
                HttpClient = httpClient,
            });

            var expired = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bot.SessionExpired += (_, args) => expired.TrySetResult(args.ErrorCode);
            bot.Stopped += (_, _) => stopped.TrySetResult(true);

            bot.Start();

            Assert.Equal(-14, await expired.Task.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.True(await stopped.Task.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.False(bot.IsRunning);
        }
        finally
        {
            File.Delete(credentialsPath);
        }
    }

    [Fact]
    public async Task Start_WhenSessionExpires_AndLoginOptionsExist_RelogsAndContinuesPolling()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.EnqueueJson("""
        {
          "qrcode": "qr-initial",
          "qrcode_img_content": "https://qr/initial"
        }
        """);
        handler.EnqueueJson("""
        {
          "status": "confirmed",
          "bot_token": "bot-token-1",
          "ilink_bot_id": "bot-id",
          "baseurl": "https://unit.test/",
          "ilink_user_id": "bot-user"
        }
        """);
        handler.EnqueueJson("""
        {
          "ret": 0,
          "errcode": -14,
          "get_updates_buf": "expired",
          "msgs": []
        }
        """);
        handler.EnqueueJson("""
        {
          "qrcode": "qr-relogin",
          "qrcode_img_content": "https://qr/relogin"
        }
        """);
        handler.EnqueueJson("""
        {
          "status": "confirmed",
          "bot_token": "bot-token-2",
          "ilink_bot_id": "bot-id",
          "baseurl": "https://unit.test/",
          "ilink_user_id": "bot-user"
        }
        """);
        handler.EnqueueJson("""
        {
          "ret": 0,
          "errcode": 0,
          "get_updates_buf": "next-buffer",
          "msgs": [
            {
              "message_id": "msg-1",
              "from_user_id": "user-1",
              "to_user_id": "bot-user",
              "create_time_ms": 1710000000000,
              "context_token": "ctx-1",
              "message_type": 1,
              "message_state": 2,
              "item_list": [
                {
                  "type": 1,
                  "text_item": {
                    "text": "hello after relogin"
                  }
                }
              ]
            }
          ]
        }
        """);

        using var httpClient = new HttpClient(handler);
        await using var bot = new WeixinBot(new WeixinBotOptions
        {
            HttpClient = httpClient,
        });

        var expired = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<WeixinMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loginCount = 0;
        bot.SessionExpired += (_, args) => expired.TrySetResult(args.ErrorCode);
        bot.LoggedIn += (_, _) => Interlocked.Increment(ref loginCount);
        bot.MessageReceived += (_, args) => received.TrySetResult(args.Message);

        await bot.LoginAsync(new LoginOptions
        {
            OnQrCode = _ => ValueTask.CompletedTask,
            OnStatusChanged = _ => ValueTask.CompletedTask,
        });

        using var cts = new CancellationTokenSource();
        bot.Start(cts.Token);

        Assert.Equal(-14, await expired.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("hello after relogin", message.Text);
        Assert.Equal(2, loginCount);
        Assert.True(bot.IsRunning);

        cts.Cancel();
        await bot.StopAsync();
    }

    [Fact]
    public async Task Start_WhenPollingThrows_RaisesError()
    {
        var credentialsPath = TestSupport.CreateCredentialsFile("bot-user");
        try
        {
            var handler = new ScriptedHttpMessageHandler();
            handler.Enqueue((_, _) => throw new HttpRequestException("boom"));

            using var httpClient = new HttpClient(handler);
            await using var bot = new WeixinBot(new WeixinBotOptions
            {
                CredentialsPath = credentialsPath,
                HttpClient = httpClient,
            });

            var error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            bot.Error += (_, args) => error.TrySetResult(args.Exception);

            using var cts = new CancellationTokenSource();
            bot.Start(cts.Token);

            var exception = await error.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.IsType<HttpRequestException>(exception);
            Assert.Equal("boom", exception.Message);

            cts.Cancel();
            await bot.StopAsync();
        }
        finally
        {
            File.Delete(credentialsPath);
        }
    }
}
