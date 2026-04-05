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

            using var httpClient = new HttpClient(handler);
            await using var bot = new WeixinBot(new WeixinBotOptions
            {
                CredentialsPath = credentialsPath,
                HttpClient = httpClient,
            });

            var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bot.MessageReceived += (_, _) => received.TrySetResult(true);

            using var cts = new CancellationTokenSource();
            bot.Start(cts.Token);
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
        await using var bot = new WeixinBot(new WeixinBotOptions
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
        await using var bot = new WeixinBot(new WeixinBotOptions
        {
            Token = "bot-token",
            HttpClient = new HttpClient(new ScriptedHttpMessageHandler()),
        });

        await Assert.ThrowsAsync<ArgumentException>(() => bot.SendFileAsync("user-1", new byte[] { 1, 2, 3 }, string.Empty, contextToken: "ctx"));
    }
}
