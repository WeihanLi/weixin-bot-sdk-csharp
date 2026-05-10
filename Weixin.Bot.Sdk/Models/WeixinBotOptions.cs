using Weixin.Bot.Sdk.Bot;
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

    /// <summary>
    /// Gets the logger factory used to create loggers for SDK components.
    /// When <see langword="null"/>, a no-op logger is used.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    /// Gets a handler invoked when the polling loop starts.
    /// </summary>
    public EventHandler<WeixinBotStateChangedEventArgs>? OnStarted { get; set; }

    /// <summary>
    /// Gets a handler invoked when the polling loop stops.
    /// </summary>
    public EventHandler<WeixinBotStateChangedEventArgs>? OnStopped { get; set; }

    /// <summary>
    /// Gets a handler invoked after a successful login completes.
    /// </summary>
    public EventHandler<LoginSucceededEventArgs>? OnLoggedIn { get; set; }

    /// <summary>
    /// Gets a handler invoked when credentials are loaded from persistent storage.
    /// </summary>
    public EventHandler<CredentialsEventArgs>? OnCredentialsLoaded { get; set; }

    /// <summary>
    /// Gets a handler invoked when the remote session becomes invalid and the bot starts reauthentication.
    /// </summary>
    public EventHandler<SessionExpiredEventArgs>? OnSessionExpired { get; set; }

    /// <summary>
    /// Gets a handler invoked when the SDK encounters an exception during background processing.
    /// </summary>
    public EventHandler<WeixinBotErrorEventArgs>? OnError { get; set; }
}
