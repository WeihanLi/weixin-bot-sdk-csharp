namespace Weixin.Bot.Sdk.Models.Wire;

internal sealed class QrCodeResponse
{
    [JsonPropertyName("qrcode")] public string? QrCode { get; set; }
    [JsonPropertyName("qrcode_img_content")] public string? QrCodeImageContent { get; set; }
}

internal sealed class QrStatusResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("bot_token")] public string? BotToken { get; set; }
    [JsonPropertyName("ilink_bot_id")] public string? BotId { get; set; }
    [JsonPropertyName("baseurl")] public string? BaseUrl { get; set; }
    [JsonPropertyName("ilink_user_id")] public string? UserId { get; set; }
}

internal sealed class UploadUrlResponse
{
    [JsonPropertyName("upload_param")] public string? UploadParam { get; set; }
}

internal sealed class ConfigResponse
{
    [JsonPropertyName("typing_ticket")] public string? TypingTicket { get; set; }
}
