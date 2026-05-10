using Weixin.Bot.Sdk.Bot;
using Weixin.Bot.Sdk.Models;
using Xunit;

namespace Weixin.Bot.Sdk.Test;

public sealed class MessageParsingTests
{
    [Fact]
    public async Task Start_ParsesQuotedTextMessage()
    {
        var message = await ReceiveSingleMessageAsync("""
        {
          "message_id": "quoted-1",
          "from_user_id": "user-1",
          "to_user_id": "bot-user",
          "create_time_ms": 1710000000000,
          "context_token": "ctx-quoted",
          "message_type": 1,
          "message_state": 2,
          "item_list": [
            {
              "type": 1,
              "text_item": {
                "text": "reply body"
              },
              "ref_msg": {
                "title": "Original",
                "message_item": {
                  "type": 1,
                  "text_item": {
                    "text": "quoted text"
                  }
                }
              }
            }
          ]
        }
        """);

        Assert.Equal(MessageContentKind.Text, message.ContentKind);
        Assert.Equal("reply body", message.Text);
        Assert.Equal("[引用: Original | quoted text]\nreply body", message.TextWithQuote);
        Assert.NotNull(message.QuotedMessage);
        Assert.Equal("Original", message.QuotedMessage!.Title);
        Assert.Equal("Original | quoted text", message.QuotedMessage.Text);
    }

    [Fact]
    public async Task Start_ParsesImageMessage()
    {
        var message = await ReceiveSingleMessageAsync("""
        {
          "message_id": "image-1",
          "from_user_id": "user-1",
          "to_user_id": "bot-user",
          "create_time_ms": 1710000000000,
          "context_token": "ctx-image",
          "message_type": 1,
          "message_state": 2,
          "item_list": [
            {
              "type": 2,
              "image_item": {
                "media": {
                  "encrypt_query_param": "enc-image",
                  "aes_key": "YWVz",
                  "encrypt_type": 1
                },
                "mid_size": "1024",
                "aeskey": "001122"
              }
            }
          ]
        }
        """);

        Assert.Equal(MessageContentKind.Image, message.ContentKind);
        Assert.NotNull(message.Image);
        Assert.Empty(message.Text);
        Assert.Equal("1024", message.Image!.MidSize);
    }

    [Fact]
    public async Task Start_ParsesVideoMessage()
    {
        var message = await ReceiveSingleMessageAsync("""
        {
          "message_id": "video-1",
          "from_user_id": "user-1",
          "to_user_id": "bot-user",
          "create_time_ms": 1710000000000,
          "context_token": "ctx-video",
          "message_type": 1,
          "message_state": 2,
          "item_list": [
            {
              "type": 5,
              "video_item": {
                "media": {
                  "encrypt_query_param": "enc-video",
                  "aes_key": "YWVz",
                  "encrypt_type": 1
                },
                "video_size": "2048"
              }
            }
          ]
        }
        """);

        Assert.Equal(MessageContentKind.Video, message.ContentKind);
        Assert.NotNull(message.Video);
        Assert.Equal("2048", message.Video!.VideoSize);
    }

    [Fact]
    public async Task Start_ParsesFileMessage()
    {
        var message = await ReceiveSingleMessageAsync("""
        {
          "message_id": "file-1",
          "from_user_id": "user-1",
          "to_user_id": "bot-user",
          "create_time_ms": 1710000000000,
          "context_token": "ctx-file",
          "message_type": 1,
          "message_state": 2,
          "item_list": [
            {
              "type": 4,
              "file_item": {
                "media": {
                  "encrypt_query_param": "enc-file",
                  "aes_key": "YWVz",
                  "encrypt_type": 1
                },
                "file_name": "report.pdf",
                "len": "4096"
              }
            }
          ]
        }
        """);

        Assert.Equal(MessageContentKind.File, message.ContentKind);
        Assert.NotNull(message.File);
        Assert.Equal("report.pdf", message.File!.FileName);
        Assert.Equal("4096", message.File.Length);
    }

    [Fact]
    public async Task Start_ParsesVoiceMessage_AndUsesTranscriptAsText()
    {
        var message = await ReceiveSingleMessageAsync("""
        {
          "message_id": "voice-1",
          "from_user_id": "user-1",
          "to_user_id": "bot-user",
          "create_time_ms": 1710000000000,
          "context_token": "ctx-voice",
          "message_type": 1,
          "message_state": 2,
          "item_list": [
            {
              "type": 3,
              "voice_item": {
                "media": {
                  "encrypt_query_param": "enc-voice",
                  "aes_key": "YWVz",
                  "encrypt_type": 1
                },
                "encode_type": 6,
                "sample_rate": 24000,
                "bits_per_sample": 16,
                "playtime": 4,
                "text": "spoken text"
              }
            }
          ]
        }
        """);

        Assert.Equal(MessageContentKind.Voice, message.ContentKind);
        Assert.NotNull(message.Voice);
        Assert.Equal("spoken text", message.Text);
        Assert.Equal("spoken text", message.TextWithQuote);
        Assert.Equal(4, message.Voice!.Playtime);
    }

    [Fact]
    public async Task Start_ParsesEmptyItemList_AsUnknownMessage()
    {
        var message = await ReceiveSingleMessageAsync("""
        {
          "message_id": "unknown-1",
          "from_user_id": "user-1",
          "to_user_id": "bot-user",
          "create_time_ms": 1710000000000,
          "context_token": "ctx-empty",
          "message_type": 1,
          "message_state": 2,
          "item_list": []
        }
        """);

        Assert.Equal(MessageContentKind.Unknown, message.ContentKind);
        Assert.Empty(message.Text);
        Assert.Empty(message.TextWithQuote);
    }

    [Fact]
    public async Task Start_ParsesMessage_WhenWireTypesUseNumbersForStringFields()
    {
        var message = await ReceiveSingleMessageAsync("""
        {
          "message_id": 987654321,
          "from_user_id": 10001,
          "to_user_id": "bot-user",
          "create_time_ms": 1710000000000,
          "context_token": 12345,
          "message_type": 1,
          "message_state": 2,
          "item_list": [
            {
              "type": 1,
              "text_item": {
                "text": 67890
              }
            }
          ]
        }
        """);

        Assert.Equal("987654321", message.MessageId);
        Assert.Equal("10001", message.FromUserId);
        Assert.Equal("12345", message.ContextToken);
        Assert.Equal("67890", message.Text);
    }

    private static async Task<WeixinMessage> ReceiveSingleMessageAsync(string messageJson)
    {
        var credentialsPath = TestSupport.CreateCredentialsFile("bot-user");
        try
        {
            var handler = new ScriptedHttpMessageHandler();
            handler.EnqueueJson($$"""
            {
              "ret": 0,
              "errcode": 0,
              "get_updates_buf": "next-buffer",
              "msgs": [
                {{messageJson}}
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
            await bot.StartAsync(new LoginOptions(), cts.Token);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
            cts.Cancel();
            await bot.StopAsync();
            return message;
        }
        finally
        {
            File.Delete(credentialsPath);
        }
    }
}
