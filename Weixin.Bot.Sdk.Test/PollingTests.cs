using System.Net;
using System.Text;
using Weixin.Bot.Sdk.Bot;
using Weixin.Bot.Sdk.Models;
using Xunit;

namespace Weixin.Bot.Sdk.Test;

public sealed class PollingTests
{
    [Fact]
    public async Task Start_RaisesMessageReceived_ForInboundMessageAddressedToBot()
    {
        var credentialsPath = CreateCredentialsFile("bot-user");
        try
        {
            var handler = new QueueHttpMessageHandler(
            [
                Json("""
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
                """),
            ]);

            using var httpClient = new HttpClient(handler);
            await using var bot = new WeixinBot(new WeixinBotOptions
            {
                CredentialsPath = credentialsPath,
                HttpClient = httpClient,
            });

            var received = new TaskCompletionSource<WeixinMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            bot.MessageReceived += (_, args) => received.TrySetResult(args.Message);

            using var cts = new CancellationTokenSource();
            bot.Start(cts.Token);

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
        var credentialsPath = CreateCredentialsFile("bot-user");
        try
        {
            var handler = new QueueHttpMessageHandler(
            [
                Json("""
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
                      "message_type": 0,
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
                """),
            ]);

            using var httpClient = new HttpClient(handler);
            await using var bot = new WeixinBot(new WeixinBotOptions
            {
                CredentialsPath = credentialsPath,
                HttpClient = httpClient,
            });

            var received = false;
            bot.MessageReceived += (_, _) => received = true;

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            bot.Start(cts.Token);

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
        var handler = new QueueHttpMessageHandler(
        [
            Json("""
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
            """),
        ]);

        using var httpClient = new HttpClient(handler);
        await using var bot = new WeixinBot(new WeixinBotOptions
        {
            Token = "bot-token",
            HttpClient = httpClient,
        });

        var received = new TaskCompletionSource<WeixinMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        bot.MessageReceived += (_, args) => received.TrySetResult(args.Message);

        using var cts = new CancellationTokenSource();
        bot.Start(cts.Token);

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(received.Task, completed);
        Assert.Equal("fallback", (await received.Task).Text);

        cts.Cancel();
        await bot.StopAsync();
    }

    private static string CreateCredentialsFile(string userId)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.weixin-bot.credentials.json");
        File.WriteAllText(path, $$"""
        {
          "botToken": "bot-token",
          "botId": "bot-id",
          "baseUrl": "https://unit.test/",
          "userId": "{{userId}}",
          "savedAt": "2026-04-05T00:00:00+00:00"
        }
        """);
        return path;
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private sealed class QueueHttpMessageHandler(IReadOnlyCollection<HttpResponseMessage> initialResponses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(initialResponses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count > 0)
            {
                return Task.FromResult(_responses.Dequeue());
            }

            return Task.FromResult(Json("""
            {
              "ret": 0,
              "errcode": 0,
              "get_updates_buf": "",
              "msgs": []
            }
            """));
        }
    }
}
