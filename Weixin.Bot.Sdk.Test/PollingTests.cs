using Weixin.Bot.Sdk.Bot;
using Weixin.Bot.Sdk.Models;
using Xunit;

namespace Weixin.Bot.Sdk.Test;

public sealed class PollingTests
{
    [Fact]
    public async Task Start_RaisesMessageReceived_ForInboundMessageAddressedToBot()
    {
        var credentialsPath = TestSupport.CreateCredentialsFile("bot-user");
        try
        {
            var handler = new ScriptedHttpMessageHandler();
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
                      "message_type": 0,
                      "message_state": 2,
                      "item_list": [
                        {
                          "type": 1,
                          "text_item": {
                            "text": "hello"
                          }
                        }
                      ]
                    }
                  ]
                }
                """);

            var received = new TaskCompletionSource<WeixinMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var httpClient = new HttpClient(handler);
            await using var bot = new WeixinBot(new DelegateMessageHandler((message, _) => { received.TrySetResult(message); return Task.CompletedTask; }), new WeixinBotOptions
            {
                CredentialsPath = credentialsPath,
                HttpClient = httpClient,
            });

            using var cts = new CancellationTokenSource();
            await bot.StartAsync(new(), cts.Token);

            var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(3)));
            Assert.Same(received.Task, completed);

            var message = await received.Task;
            Assert.Equal("msg-1", message.MessageId);
            Assert.Equal("user-1", message.FromUserId);
            Assert.Equal("bot-user", message.ToUserId);
            Assert.Equal("ctx-1", message.ContextToken);
            Assert.Equal("hello", message.Text);
            Assert.Equal(MessageContentKind.Text, message.ContentKind);

            cts.Cancel();
            await bot.StopAsync();
        }
        finally
        {
            File.Delete(credentialsPath);
        }
    }

    [Fact]
    public async Task Start_DoesNotRaiseMessageReceived_ForSelfAuthoredMessage()
    {
        var credentialsPath = TestSupport.CreateCredentialsFile("bot-user");
        try
        {
            var handler = new ScriptedHttpMessageHandler();
            handler.EnqueueJson("""
                {
                  "ret": 0,
                  "errcode": 0,
                  "get_updates_buf": "next-buffer",
                  "msgs": [
                    {
                      "message_id": "msg-2",
                      "from_user_id": "bot-user",
                      "to_user_id": "user-1",
                      "create_time_ms": 1710000000000,
                      "context_token": "ctx-2",
                      "message_type": 2,
                      "message_state": 2,
                      "item_list": [
                        {
                          "type": 1,
                          "text_item": {
                            "text": "echo"
                          }
                        }
                      ]
                    }
                  ]
                }
                """);

            var received = false;
            using var httpClient = new HttpClient(handler);
            await using var bot = new WeixinBot(new DelegateMessageHandler((_, _) => { received = true; return Task.CompletedTask; }), new WeixinBotOptions
            {
                CredentialsPath = credentialsPath,
                HttpClient = httpClient,
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            await bot.StartAsync(new(), cts.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(TimeSpan.FromSeconds(5), cts.Token));
            await bot.StopAsync();

            Assert.False(received);
        }
        finally
        {
            File.Delete(credentialsPath);
        }
    }

    [Fact]
    public async Task Start_RaisesMessageReceived_WhenMessageTypeIsUnknownButContextExists()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.EnqueueJson("""
            {
              "ret": 0,
              "errcode": 0,
              "get_updates_buf": "next-buffer",
              "msgs": [
                {
                  "message_id": "msg-3",
                  "from_user_id": "user-2",
                  "to_user_id": "",
                  "create_time_ms": 1710000000000,
                  "context_token": "ctx-3",
                  "message_type": 0,
                  "message_state": 2,
                  "item_list": [
                    {
                      "type": 1,
                      "text_item": {
                        "text": "fallback"
                      }
                    }
                  ]
                }
              ]
            }
            """);

        var received = new TaskCompletionSource<WeixinMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var httpClient = new HttpClient(handler);
        await using var bot = new WeixinBot(new DelegateMessageHandler((message, _) => { received.TrySetResult(message); return Task.CompletedTask; }), new WeixinBotOptions
        {
            Token = "bot-token",
            HttpClient = httpClient,
        });

        using var cts = new CancellationTokenSource();
        await bot.StartAsync(new(), cts.Token);

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(received.Task, completed);
        Assert.Equal("fallback", (await received.Task).Text);

        cts.Cancel();
        await bot.StopAsync();
    }
}
