namespace Weixin.Bot.Sdk.Models;

/// <summary>
/// Persisted bot credentials that can be reused to skip QR-code login.
/// </summary>
public sealed record BotCredentials
{
    /// <summary>
    /// Gets the authenticated bot token.
    /// </summary>
    [JsonPropertyName("botToken")] public string? BotToken { get; init; }

    /// <summary>
    /// Gets the bot identifier.
    /// </summary>
    [JsonPropertyName("botId")] public string? BotId { get; init; }

    /// <summary>
    /// Gets the resolved API base URL.
    /// </summary>
    [JsonPropertyName("baseUrl")] public string? BaseUrl { get; init; }

    /// <summary>
    /// Gets the associated user identifier.
    /// </summary>
    [JsonPropertyName("userId")] public string? UserId { get; init; }

    /// <summary>
    /// Gets the timestamp when the credentials were saved.
    /// </summary>
    [JsonPropertyName("savedAt")] public DateTimeOffset? SavedAt { get; init; }
}
