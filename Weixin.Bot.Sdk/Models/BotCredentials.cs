namespace Weixin.Bot.Sdk.Models;

public sealed record BotCredentials
{
    [JsonPropertyName("botToken")] public string? BotToken { get; init; }
    [JsonPropertyName("botId")] public string? BotId { get; init; }
    [JsonPropertyName("baseUrl")] public string? BaseUrl { get; init; }
    [JsonPropertyName("userId")] public string? UserId { get; init; }
    [JsonPropertyName("savedAt")] public DateTimeOffset? SavedAt { get; init; }
}
