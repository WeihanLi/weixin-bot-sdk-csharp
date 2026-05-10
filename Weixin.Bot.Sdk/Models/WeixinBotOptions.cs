using Weixin.Bot.Sdk.Credentials;

namespace Weixin.Bot.Sdk.Models;

/// <summary>
/// Configures a <see cref="Bot.WeixinBot"/> instance.
/// </summary>
public sealed class WeixinBotOptions
{
    /// <summary>
    /// Gets the API base URL override.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Gets the CDN base URL override.
    /// </summary>
    public string? CdnUrl { get; set; }

    /// <summary>
    /// Gets the existing bot token to use instead of performing QR-code login.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Gets the channel version value sent to the API.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets the path used to load and persist credentials.
    /// </summary>
    /// <remarks>
    /// This is a convenience shortcut for configuring <see cref="CredentialStore"/> with a
    /// <see cref="FileBotCredentialStore"/>. Set <see cref="CredentialStore"/> directly to load
    /// credentials from an API, database, secrets manager, or another backing store.
    /// </remarks>
    public string? CredentialsPath { get; set; }

    /// <summary>
    /// Gets the credential store used to load and persist reusable bot credentials.
    /// </summary>
    public IBotCredentialStore? CredentialStore { get; set; }

    /// <summary>
    /// Gets a shared <see cref="HttpClient"/> instance to use for API and CDN traffic.
    /// </summary>
    public HttpClient? HttpClient { get; set; }
}
