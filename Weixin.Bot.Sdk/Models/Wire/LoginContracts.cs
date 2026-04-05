namespace Weixin.Bot.Sdk.Models.Wire;

internal sealed class QrCodeResponse
{
    [JsonPropertyName("qrcode"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? QrCode { get; set; }
    [JsonPropertyName("qrcode_img_content"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? QrCodeImageContent { get; set; }
}

internal sealed class QrStatusResponse
{
    [JsonPropertyName("status"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? Status { get; set; }
    [JsonPropertyName("bot_token"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? BotToken { get; set; }
    [JsonPropertyName("ilink_bot_id"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? BotId { get; set; }
    [JsonPropertyName("baseurl"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? BaseUrl { get; set; }
    [JsonPropertyName("ilink_user_id"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? UserId { get; set; }
}

internal sealed class UploadUrlResponse
{
    [JsonPropertyName("upload_param"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? UploadParam { get; set; }
}

internal sealed class ConfigResponse
{
    [JsonPropertyName("typing_ticket"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? TypingTicket { get; set; }
}
