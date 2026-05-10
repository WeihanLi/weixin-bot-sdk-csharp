using Weixin.Bot.Sdk.Models;

namespace Weixin.Bot.Sdk.Bot;

/// <summary>
/// Represents a WeChat iLink bot client capable of authenticating, receiving messages, and sending replies or media.
/// </summary>
public interface IWeixinBot
{
    /// <summary>
    /// Gets a value indicating whether the bot currently has an authenticated token.
    /// </summary>
    bool IsLoggedIn { get; }

    /// <summary>
    /// Gets a value indicating whether the polling loop is currently active.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the credentials currently loaded into the bot, if any.
    /// </summary>
    BotCredentials? CurrentCredentials { get; }

    /// <summary>
    /// Performs the login flow, including QR code generation and status polling.
    /// </summary>
    /// <param name="options">Optional login behavior overrides.</param>
    /// <param name="cancellationToken">A token that can cancel the login operation.</param>
    /// <returns>The authenticated login result.</returns>
    Task<LoginResult> LoginAsync(LoginOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads credentials from the configured credential store and applies them to this bot.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the load operation.</param>
    /// <returns><see langword="true"/> when valid credentials were loaded; otherwise, <see langword="false"/>.</returns>
    Task<bool> LoadCredentialsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the long-polling loop for receiving messages.
    /// </summary>
    /// <param name="cancellationToken">A token that can stop polling.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the polling loop if it is running.
    /// </summary>
    /// <returns>A task that completes when shutdown finishes.</returns>
    Task StopAsync();

    /// <summary>
    /// Sends a text reply using the context token from an inbound message.
    /// </summary>
    /// <param name="message">The inbound message to reply to.</param>
    /// <param name="text">The reply text to send.</param>
    /// <param name="cancellationToken">A token that can cancel the send.</param>
    /// <returns>The generated client message identifier.</returns>
    Task<string> ReplyAsync(WeixinMessage message, string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a text message to a user.
    /// </summary>
    /// <param name="toUserId">The target user identifier.</param>
    /// <param name="text">The text to send.</param>
    /// <param name="contextToken">An optional context token. If omitted, a cached token for the user is used.</param>
    /// <param name="cancellationToken">A token that can cancel the send.</param>
    /// <returns>The generated client message identifier.</returns>
    Task<string> SendTextAsync(string toUserId, string text, string? contextToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an image to a user.
    /// </summary>
    /// <param name="toUserId">The target user identifier.</param>
    /// <param name="image">The raw image bytes.</param>
    /// <param name="caption">Optional caption text to send before the image.</param>
    /// <param name="contextToken">An optional context token. If omitted, a cached token for the user is used.</param>
    /// <param name="cancellationToken">A token that can cancel the send.</param>
    /// <returns>A task that completes when the image has been sent.</returns>
    Task SendImageAsync(string toUserId, ReadOnlyMemory<byte> image, string? caption = null, string? contextToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a video to a user.
    /// </summary>
    /// <param name="toUserId">The target user identifier.</param>
    /// <param name="video">The raw video bytes.</param>
    /// <param name="caption">Optional caption text to send before the video.</param>
    /// <param name="contextToken">An optional context token. If omitted, a cached token for the user is used.</param>
    /// <param name="cancellationToken">A token that can cancel the send.</param>
    /// <returns>A task that completes when the video has been sent.</returns>
    Task SendVideoAsync(string toUserId, ReadOnlyMemory<byte> video, string? caption = null, string? contextToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a file to a user.
    /// </summary>
    /// <param name="toUserId">The target user identifier.</param>
    /// <param name="file">The raw file bytes.</param>
    /// <param name="fileName">The filename presented to the recipient.</param>
    /// <param name="caption">Optional caption text to send before the file.</param>
    /// <param name="contextToken">An optional context token. If omitted, a cached token for the user is used.</param>
    /// <param name="cancellationToken">A token that can cancel the send.</param>
    /// <returns>A task that completes when the file has been sent.</returns>
    Task SendFileAsync(string toUserId, ReadOnlyMemory<byte> file, string fileName, string? caption = null, string? contextToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a voice message to a user.
    /// </summary>
    /// <param name="toUserId">The target user identifier.</param>
    /// <param name="voice">The raw voice payload bytes.</param>
    /// <param name="options">Optional voice metadata such as encoding and duration.</param>
    /// <param name="contextToken">An optional context token. If omitted, a cached token for the user is used.</param>
    /// <param name="cancellationToken">A token that can cancel the send.</param>
    /// <returns>A task that completes when the voice message has been sent.</returns>
    Task SendVoiceAsync(string toUserId, ReadOnlyMemory<byte> voice, VoiceSendOptions? options = null, string? contextToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and decrypts an inbound image payload.
    /// </summary>
    /// <param name="image">The image metadata from an inbound message.</param>
    /// <param name="cdnBaseUrl">Optional CDN base URL override.</param>
    /// <param name="cancellationToken">A token that can cancel the download.</param>
    /// <returns>The decrypted image bytes.</returns>
    Task<byte[]> DownloadImageAsync(WeixinImage image, string? cdnBaseUrl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and decrypts an inbound voice payload.
    /// </summary>
    /// <param name="voice">The voice metadata from an inbound message.</param>
    /// <param name="cdnBaseUrl">Optional CDN base URL override.</param>
    /// <param name="cancellationToken">A token that can cancel the download.</param>
    /// <returns>The decrypted voice bytes.</returns>
    Task<byte[]> DownloadVoiceAsync(WeixinVoice voice, string? cdnBaseUrl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and decrypts an inbound file payload.
    /// </summary>
    /// <param name="file">The file metadata from an inbound message.</param>
    /// <param name="cdnBaseUrl">Optional CDN base URL override.</param>
    /// <param name="cancellationToken">A token that can cancel the download.</param>
    /// <returns>The decrypted file bytes.</returns>
    Task<byte[]> DownloadFileAsync(WeixinFile file, string? cdnBaseUrl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and decrypts an inbound video payload.
    /// </summary>
    /// <param name="video">The video metadata from an inbound message.</param>
    /// <param name="cdnBaseUrl">Optional CDN base URL override.</param>
    /// <param name="cancellationToken">A token that can cancel the download.</param>
    /// <returns>The decrypted video bytes.</returns>
    Task<byte[]> DownloadVideoAsync(WeixinVideo video, string? cdnBaseUrl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a typing indicator to a user.
    /// </summary>
    /// <param name="userId">The target user identifier.</param>
    /// <param name="contextToken">An optional context token. If omitted, a cached token for the user is used.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>A task that completes when the typing indicator has been sent.</returns>
    Task SendTypingAsync(string userId, string? contextToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the typing indicator for a user.
    /// </summary>
    /// <param name="userId">The target user identifier.</param>
    /// <param name="contextToken">An optional context token. If omitted, a cached token for the user is used.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>A task that completes when the typing indicator has been cancelled.</returns>
    Task CancelTypingAsync(string userId, string? contextToken = null, CancellationToken cancellationToken = default);
}
