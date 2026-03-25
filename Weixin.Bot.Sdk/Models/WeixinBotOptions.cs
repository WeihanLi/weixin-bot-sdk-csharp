namespace Weixin.Bot.Sdk.Models;

/// <summary>
/// Configures a <see cref="Bot.WeixinBot"/> instance.
/// </summary>
public sealed record WeixinBotOptions
{
    /// <summary>
    /// Gets the API base URL override.
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// Gets the CDN base URL override.
    /// </summary>
    public string? CdnUrl { get; init; }

    /// <summary>
    /// Gets the existing bot token to use instead of performing QR-code login.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Gets the channel version value sent to the API.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Gets the path used to load and persist credentials.
    /// </summary>
    public string? CredentialsPath { get; init; }

    /// <summary>
    /// Gets a shared <see cref="HttpClient"/> instance to use for API and CDN traffic.
    /// </summary>
    public HttpClient? HttpClient { get; init; }
}
