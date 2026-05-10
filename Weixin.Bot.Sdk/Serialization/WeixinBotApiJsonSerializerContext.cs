using Weixin.Bot.Sdk.Models.Wire;

namespace Weixin.Bot.Sdk.Serialization;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(QrCodeResponse))]
[JsonSerializable(typeof(QrStatusResponse))]
[JsonSerializable(typeof(GetUpdatesResponse))]
[JsonSerializable(typeof(UploadUrlResponse))]
[JsonSerializable(typeof(ConfigResponse))]
[JsonSerializable(typeof(GetUpdatesRequest))]
[JsonSerializable(typeof(SendMessageRequest))]
[JsonSerializable(typeof(SendTypingRequest))]
[JsonSerializable(typeof(GetUploadUrlRequest))]
[JsonSerializable(typeof(GetConfigRequest))]
[JsonSerializable(typeof(MessageItemPayload[]))]
internal sealed partial class WeixinBotApiJsonSerializerContext : JsonSerializerContext;
