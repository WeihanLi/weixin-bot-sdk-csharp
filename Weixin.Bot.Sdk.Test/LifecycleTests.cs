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
    public void Constructor_LoadsCredentials_FromCredentialStore()
    {
        InMemoryCredentialStore credentialStore = new(new BotCredentials
        {
            BotToken = "store-token",
            BotId = "store-bot",
            BaseUrl = "https://unit.test/",
            UserId = "store-user",
            SavedAt = DateTimeOffset.UtcNow,
        });

        using var bot = new WeixinBot(new WeixinBotOptions
        {
            CredentialStore = credentialStore,
        });

        Assert.True(bot.IsLoggedIn);
        Assert.NotNull(bot.CurrentCredentials);
        Assert.Equal("store-user", bot.CurrentCredentials!.UserId);
        Assert.Equal(1, credentialStore.LoadCount);
    }

    [Fact]
    public async Task LoginAsync_SavesCredentials_ToCredentialStore()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.EnqueueJson("""
        {
          "qrcode": "qr-login",
          "qrcode_img_content": "https://qr/login"
        }
        """);
        handler.EnqueueJson("""
        {
          "status": "confirmed",
          "bot_token": "saved-token",
          "ilink_bot_id": "saved-bot",
          "baseurl": "https://unit.test/",
          "ilink_user_id": "saved-user"
        }
        """);

        InMemoryCredentialStore credentialStore = new(null);
        using var httpClient = new HttpClient(handler);
        await using var bot = new WeixinBot(new WeixinBotOptions
        {
            CredentialStore = credentialStore,
            HttpClient = httpClient,
        });

        await bot.LoginAsync(new LoginOptions
        {
            OnQrCode = _ => ValueTask.CompletedTask,
            OnStatusChanged = _ => ValueTask.CompletedTask,
        });

        Assert.NotNull(credentialStore.SavedCredentials);
        Assert.Equal("saved-token", credentialStore.SavedCredentials!.BotToken);
        Assert.Equal("saved-bot", credentialStore.SavedCredentials.BotId);
        Assert.Equal("saved-user", credentialStore.SavedCredentials.UserId);
        Assert.Equal(1, credentialStore.SaveCount);
    }

    [Fact]
    public async Task Start_RaisesStarted_AndStopAsync_RaisesStopped()
    {
        var credentialsPath = TestSupport.CreateCredentialsFile("bot-user");
        try
        {
            var started = new TaskCompletionSource<DateTimeOffset>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stopped = new TaskCompletionSource<DateTimeOffset>(TaskCreationOptions.RunContinuationsAsynchronously);
            var handler = new ScriptedHttpMessageHandler();
            using var httpClient = new HttpClient(handler);
            await using var bot = new WeixinBot(new WeixinBotOptions
            {
                CredentialsPath = credentialsPath,
                HttpClient = httpClient,
                OnStarted = (_, args) => started.TrySetResult(args.OccurredAt),
                OnStopped = (_, args) => stopped.TrySetResult(args.OccurredAt),
                MessageHandler = new DelegateMessageHandler((_, _) => Task.CompletedTask),
            });

            using var cts = new CancellationTokenSource();
            await bot.StartAsync(cts.Token);

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
    public async Task Start_WithoutLogin_ThrowsInvalidOperationException()
    {
        using var bot = new WeixinBot(new WeixinBotOptions());
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => bot.StartAsync());
        Assert.Contains("Not logged in", exception.Message);
    }

    [Fact]
    public async Task Start_WithoutMessageHandler_ThrowsInvalidOperationException()
    {
        var credentialsPath = TestSupport.CreateCredentialsFile("bot-user");
        try
        {
            using var bot = new WeixinBot(new WeixinBotOptions { CredentialsPath = credentialsPath });
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => bot.StartAsync());
            Assert.Contains("message handler", exception.Message);
        }
        finally
        {
            File.Delete(credentialsPath);
        }
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

            var expired = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var httpClient = new HttpClient(handler);
            await using var bot = new WeixinBot(new WeixinBotOptions
            {
                CredentialsPath = credentialsPath,
                HttpClient = httpClient,
                OnSessionExpired = (_, args) => expired.TrySetResult(args.ErrorCode),
                OnStopped = (_, _) => stopped.TrySetResult(true),
                MessageHandler = new DelegateMessageHandler((_, _) => Task.CompletedTask),
            });

            await bot.StartAsync();

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

        var expired = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<WeixinMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loginCount = 0;
        using var httpClient = new HttpClient(handler);
        await using var bot = new WeixinBot(new WeixinBotOptions
        {
            HttpClient = httpClient,
            OnSessionExpired = (_, args) => expired.TrySetResult(args.ErrorCode),
            OnLoggedIn = (_, _) => Interlocked.Increment(ref loginCount),
            MessageHandler = new DelegateMessageHandler((message, _) => { received.TrySetResult(message); return Task.CompletedTask; }),
        });

        await bot.LoginAsync(new LoginOptions
        {
            OnQrCode = _ => ValueTask.CompletedTask,
            OnStatusChanged = _ => ValueTask.CompletedTask,
        });

        using var cts = new CancellationTokenSource();
        await bot.StartAsync(cts.Token);

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

            var error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var httpClient = new HttpClient(handler);
            await using var bot = new WeixinBot(new WeixinBotOptions
            {
                CredentialsPath = credentialsPath,
                HttpClient = httpClient,
                OnError = (_, args) => error.TrySetResult(args.Exception),
                MessageHandler = new DelegateMessageHandler((_, _) => Task.CompletedTask),
            });

            using var cts = new CancellationTokenSource();
            await bot.StartAsync(cts.Token);

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
