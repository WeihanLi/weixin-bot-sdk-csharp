using System.Text.Json;
using Weixin.Bot.Sdk.Bot;
using Weixin.Bot.Sdk.Models;
using Xunit;

namespace Weixin.Bot.Sdk.Test;

public sealed class SendTests
{
    [Fact]
    public async Task SendTextAsync_UsesCachedContextToken_FromInboundMessage()
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
                  "message_type": 1,
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
            handler.Enqueue((_, _) => Task.FromResult(TestSupport.Json("""{ "ret": 0 }""")));

            var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var httpClient = new HttpClient(handler);
            await using var bot = new WeixinBot(new DelegateMessageHandler((_, _) => { received.TrySetResult(true); return Task.CompletedTask; }), new WeixinBotOptions
            {
                CredentialsPath = credentialsPath,
                HttpClient = httpClient,
            });

            using var cts = new CancellationTokenSource();
            await bot.StartAsync(new(), cts.Token);
            Assert.True(await received.Task.WaitAsync(TimeSpan.FromSeconds(3)));

            await bot.SendTextAsync("user-1", "pong");

            cts.Cancel();
            await bot.StopAsync();

            var sendRequest = Assert.Single(handler.Requests, x => x.Uri?.AbsoluteUri.Contains("/ilink/bot/sendmessage") == true);
            using var document = JsonDocument.Parse(sendRequest.Body!);
            var root = document.RootElement;
            Assert.Equal("user-1", root.GetProperty("msg").GetProperty("to_user_id").GetString());
            Assert.Equal("ctx-1", root.GetProperty("msg").GetProperty("context_token").GetString());
            Assert.Equal("pong", root.GetProperty("msg").GetProperty("item_list")[0].GetProperty("text_item").GetProperty("text").GetString());
        }
        finally
        {
            File.Delete(credentialsPath);
        }
    }

    [Fact]
    public async Task ReplyAsync_UsesMessageContextToken()
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
                  "context_token": "ctx-reply",
                  "message_type": 1,
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
            handler.Enqueue((_, _) => Task.FromResult(TestSupport.Json("""{ "ret": 0 }""")));

            var received = new TaskCompletionSource<WeixinMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var httpClient = new HttpClient(handler);
            await using var bot = new WeixinBot(new DelegateMessageHandler((message, _) => { received.TrySetResult(message); return Task.CompletedTask; }), new WeixinBotOptions
            {
                CredentialsPath = credentialsPath,
                HttpClient = httpClient,
            });

            using var cts = new CancellationTokenSource();
            await bot.StartAsync(new(), cts.Token);
            var inbound = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));

            await bot.ReplyAsync(inbound, "reply");

            cts.Cancel();
            await bot.StopAsync();

            var sendRequest = Assert.Single(handler.Requests, x => x.Uri?.AbsoluteUri.Contains("/ilink/bot/sendmessage") == true);
            using var document = JsonDocument.Parse(sendRequest.Body!);
            Assert.Equal("ctx-reply", document.RootElement.GetProperty("msg").GetProperty("context_token").GetString());
            Assert.Equal("user-1", document.RootElement.GetProperty("msg").GetProperty("to_user_id").GetString());
        }
        finally
        {
            File.Delete(credentialsPath);
        }
    }

    [Fact]
    public async Task SendTextAsync_WithoutContextToken_Throws()
    {
        await using var bot = new WeixinBot(null, new WeixinBotOptions
        {
            Token = "bot-token",
            HttpClient = new HttpClient(new ScriptedHttpMessageHandler()),
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => bot.SendTextAsync("user-1", "hello"));
        Assert.Contains("No context token", exception.Message);
    }

    [Fact]
    public async Task SendFileAsync_WithBlankFileName_Throws()
    {
        await using var bot = new WeixinBot(null, new WeixinBotOptions
        {
            Token = "bot-token",
            HttpClient = new HttpClient(new ScriptedHttpMessageHandler()),
        });

        await Assert.ThrowsAsync<ArgumentException>(() => bot.SendFileAsync("user-1", new byte[] { 1, 2, 3 }, string.Empty, contextToken: "ctx"));
    }

    [Fact]
    public async Task SendTextAsync_AllowsEmptySendMessageResponseBody()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue((_, _) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));

        await using var bot = new WeixinBot(null, new WeixinBotOptions
        {
            Token = "bot-token",
            HttpClient = new HttpClient(handler),
        });

        var clientId = await bot.SendTextAsync("user-1", "hello", "ctx");

        Assert.StartsWith("wx-bot-", clientId);
        var sendRequest = Assert.Single(handler.Requests, x => x.Uri?.AbsoluteUri.Contains("/ilink/bot/sendmessage") == true);
        using var document = JsonDocument.Parse(sendRequest.Body!);
        Assert.Equal("ctx", document.RootElement.GetProperty("msg").GetProperty("context_token").GetString());
    }

    [Fact]
    public async Task SendImageAsync_SerializesMidSizeAsNumber()
    {
        var handler = new ScriptedHttpMessageHandler();
        handler.Enqueue((_, _) => Task.FromResult(TestSupport.Json("""{ "upload_param": "upload-token" }""")));
        handler.Enqueue((_, _) =>
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            response.Headers.TryAddWithoutValidation("x-encrypted-param", "download-token");
            return Task.FromResult(response);
        });
        handler.Enqueue((_, _) => Task.FromResult(TestSupport.Json("""{ "ret": 0 }""")));

        await using var bot = new WeixinBot(null, new WeixinBotOptions
        {
            Token = "bot-token",
            HttpClient = new HttpClient(handler),
        });

        await bot.SendImageAsync("user-1", new byte[] { 1, 2, 3 }, contextToken: "ctx");

        var sendRequest = Assert.Single(handler.Requests, x => x.Uri?.AbsoluteUri.Contains("/ilink/bot/sendmessage") == true);
        using var document = JsonDocument.Parse(sendRequest.Body!);
        var midSize = document.RootElement.GetProperty("msg").GetProperty("item_list")[0].GetProperty("image_item").GetProperty("mid_size");
        Assert.Equal(JsonValueKind.Number, midSize.ValueKind);
    }
}
