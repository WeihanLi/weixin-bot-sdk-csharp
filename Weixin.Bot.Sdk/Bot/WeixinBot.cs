using Weixin.Bot.Sdk.Api;
using Weixin.Bot.Sdk.Credentials;
using Weixin.Bot.Sdk.Media;
using Weixin.Bot.Sdk.Models;
using Weixin.Bot.Sdk.Models.Wire;

namespace Weixin.Bot.Sdk.Bot;

/// <summary>
/// High-level client for authenticating a WeChat iLink bot, receiving messages, and sending replies or media.
/// </summary>
public sealed class WeixinBot : IWeixinBot, IAsyncDisposable, IDisposable
{
    private const int ContextCacheLimit = 1000;
    private const int SessionExpiredErrorCode = -14;
    private const int SessionInvalidErrorCode = -13;
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    private readonly WeixinBotApi _api;
    private readonly CdnClient _cdnClient;
    private readonly HttpClient _sharedHttpClient;
    private readonly bool _ownsSharedHttpClient;
    private readonly IBotCredentialStore? _credentialStore;
    private readonly Dictionary<string, string> _contextTokens = new();
    private readonly Queue<string> _contextOrder = new();
    private readonly Lock _contextLock = new();
    private readonly ILogger<WeixinBot> _logger;
    private readonly EventHandler<WeixinBotStateChangedEventArgs>? _onStarted;
    private readonly EventHandler<WeixinBotStateChangedEventArgs>? _onStopped;
    private readonly EventHandler<LoginSucceededEventArgs>? _onLoggedIn;
    private readonly EventHandler<CredentialsEventArgs>? _onCredentialsLoaded;
    private readonly IWeixinMessageHandler? _messageHandler;
    private readonly EventHandler<SessionExpiredEventArgs>? _onSessionExpired;
    private readonly EventHandler<WeixinBotErrorEventArgs>? _onError;

    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private string _updatesBuffer = string.Empty;
    private BotCredentials? _credentials;
    private LoginOptions? _loginOptions;
    private bool _disposed;

    /// <summary>
    /// Initializes a new bot instance.
    /// </summary>
    /// <param name="messageHandler">The handler invoked for each inbound message.</param>
    /// <param name="options">Bot configuration.</param>
    public WeixinBot(IWeixinMessageHandler? messageHandler, WeixinBotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = options.LoggerFactory?.CreateLogger<WeixinBot>() ?? NullLogger<WeixinBot>.Instance;
        _onStarted = options.OnStarted;
        _onStopped = options.OnStopped;
        _onLoggedIn = options.OnLoggedIn;
        _onCredentialsLoaded = options.OnCredentialsLoaded;
        _messageHandler = messageHandler;
        _onSessionExpired = options.OnSessionExpired;
        _onError = options.OnError;
        _credentialStore = options.CredentialStore
            ?? (string.IsNullOrWhiteSpace(options.CredentialsPath) ? null : new FileBotCredentialStore(options.CredentialsPath));
        _sharedHttpClient = options.HttpClient ?? new HttpClient();
        _ownsSharedHttpClient = options.HttpClient is null;

        var apiOptions = new WeixinBotApiOptions
        {
            BaseUrl = options.BaseUrl,
            CdnUrl = options.CdnUrl,
            Token = options.Token,
            Version = options.Version,
            HttpClient = _sharedHttpClient,
            LoggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance,
        };
        _api = new(apiOptions);
        _cdnClient = new CdnClient(_sharedHttpClient, _api.CdnUrl, apiOptions.LoggerFactory);

        TryLoadCredentialsAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    internal CdnClient Cdn => _cdnClient;
    /// <summary>
    /// Gets a value indicating whether the polling loop is currently active.
    /// </summary>
    public bool IsRunning => _pollingTask is { IsCompleted: false };

    /// <summary>
    /// Logs in using <paramref name="loginOptions"/> if valid credentials are not already loaded,
    /// then starts the long-polling loop for receiving messages.
    /// </summary>
    /// <param name="loginOptions">Login options used when credentials are not already loaded.</param>
    /// <param name="cancellationToken">A token that can stop polling.</param>
    public async Task StartAsync(LoginOptions loginOptions, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsRunning)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(_api.Token))
        {
            await LoginCoreAsync(loginOptions, cancellationToken).ConfigureAwait(false);
        }
        if (_messageHandler is null)
        {
            throw new InvalidOperationException("No message handler is configured. Set WeixinBotOptions.MessageHandler before starting.");
        }

        _pollingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollingTask = Task.Run(() => PollLoopAsync(_pollingCts.Token), CancellationToken.None);
        _logger.LogInformation("Polling started");
        _onStarted?.Invoke(this, new WeixinBotStateChangedEventArgs(DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Stops the polling loop if it is running.
    /// </summary>
    /// <returns>A task that completes when shutdown finishes.</returns>
    public async Task StopAsync()
    {
        var cts = _pollingCts;
        var pollingTask = _pollingTask;

        if (cts is null || pollingTask is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The polling loop may already have completed and disposed the CTS.
        }
        try
        {
            if (Task.CurrentId != pollingTask.Id)
            {
                await pollingTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // expected during shutdown
        }
    }

    /// <summary>
    /// Sends a text reply using the context token from an inbound message.
    /// </summary>
    /// <param name="message">The inbound message to reply to.</param>
    /// <param name="text">The reply text to send.</param>
    /// <param name="cancellationToken">A token that can cancel the send.</param>
    /// <returns>The generated client message identifier.</returns>
    public Task<string> ReplyAsync(WeixinMessage message, string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return SendTextAsync(message.FromUserId, text, message.ContextToken, cancellationToken);
    }

    /// <summary>
    /// Sends a text message to a user.
    /// </summary>
    /// <param name="toUserId">The target user identifier.</param>
    /// <param name="text">The text to send.</param>
    /// <param name="contextToken">An optional context token. If omitted, a cached token for the user is used.</param>
    /// <param name="cancellationToken">A token that can cancel the send.</param>
    /// <returns>The generated client message identifier.</returns>
    public Task<string> SendTextAsync(string toUserId, string text, string? contextToken = null, CancellationToken cancellationToken = default)
    {
        var token = EnsureContextToken(toUserId, contextToken);
        return _api.SendTextAsync(toUserId, text ?? string.Empty, token, cancellationToken);
    }

    /// <summary>
    /// Sends an image to a user.
    /// </summary>
    /// <param name="toUserId">The target user identifier.</param>
    /// <param name="image">The raw image bytes.</param>
    /// <param name="caption">Optional caption text to send before the image.</param>
    /// <param name="contextToken">An optional context token. If omitted, a cached token for the user is used.</param>
    /// <param name="cancellationToken">A token that can cancel the send.</param>
    /// <returns>A task that completes when the image has been sent.</returns>
    public async Task SendImageAsync(string toUserId, ReadOnlyMemory<byte> image, string? caption = null, string? contextToken = null, CancellationToken cancellationToken = default)
    {
        await SendMediaAsync(
            toUserId,
            image,
            caption,
            contextToken,
            UploadMediaType.Image,
            prepared => new MessageItemPayload
            {
                Type = MessageItemType.Image,
                ImageItem = new ImageItemPayload
                {
                    Media = new MediaPayload
                    {
                        EncryptQueryParam = prepared.DownloadEncryptedQueryParam,
                        AesKey = HexToBase64(prepared.AesKeyHex),
                        EncryptType = 1,
                    },
                    MidSize = prepared.FileSizeCiphertext,
                },
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a video to a user.
    /// </summary>
    /// <param name="toUserId">The target user identifier.</param>
    /// <param name="video">The raw video bytes.</param>
    /// <param name="caption">Optional caption text to send before the video.</param>
    /// <param name="contextToken">An optional context token. If omitted, a cached token for the user is used.</param>
    /// <param name="cancellationToken">A token that can cancel the send.</param>
    /// <returns>A task that completes when the video has been sent.</returns>
    public async Task SendVideoAsync(string toUserId, ReadOnlyMemory<byte> video, string? caption = null, string? contextToken = null, CancellationToken cancellationToken = default)
    {
        await SendMediaAsync(
            toUserId,
            video,
            caption,
            contextToken,
            UploadMediaType.Video,
            prepared => new MessageItemPayload
            {
                Type = MessageItemType.Video,
                VideoItem = new VideoItemPayload
                {
                    Media = new MediaPayload
                    {
                        EncryptQueryParam = prepared.DownloadEncryptedQueryParam,
                        AesKey = HexToBase64(prepared.AesKeyHex),
                        EncryptType = 1,
                    },
                    VideoSize = prepared.FileSizeCiphertext,
                },
            },
            cancellationToken).ConfigureAwait(false);
    }

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
    public async Task SendFileAsync(string toUserId, ReadOnlyMemory<byte> file, string fileName, string? caption = null, string? contextToken = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required", nameof(fileName));
        }

        await SendMediaAsync(toUserId, file, caption, contextToken, UploadMediaType.File, prepared => new MessageItemPayload
        {
            Type = MessageItemType.File,
            FileItem = new FileItemPayload
            {
                Media = new MediaPayload
                {
                    EncryptQueryParam = prepared.DownloadEncryptedQueryParam,
                    AesKey = HexToBase64(prepared.AesKeyHex),
                    EncryptType = 1,
                },
                FileName = fileName,
                Length = prepared.FileSize.ToString(CultureInfo.InvariantCulture),
            },
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a voice message to a user.
    /// </summary>
    /// <param name="toUserId">The target user identifier.</param>
    /// <param name="voice">The raw voice payload bytes.</param>
    /// <param name="options">Optional voice metadata such as encoding and duration.</param>
    /// <param name="contextToken">An optional context token. If omitted, a cached token for the user is used.</param>
    /// <param name="cancellationToken">A token that can cancel the send.</param>
    /// <returns>A task that completes when the voice message has been sent.</returns>
    public async Task SendVoiceAsync(string toUserId, ReadOnlyMemory<byte> voice, VoiceSendOptions? options = null, string? contextToken = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        var token = EnsureContextToken(toUserId, contextToken);
        var prepared = await UploadPreparation.PrepareAsync(_api, voice, toUserId, UploadMediaType.Voice, _cdnClient, cancellationToken).ConfigureAwait(false);
        var item = new MessageItemPayload
        {
            Type = MessageItemType.Voice,
            VoiceItem = new VoiceItemPayload
            {
                Media = new MediaPayload
                {
                    EncryptQueryParam = prepared.DownloadEncryptedQueryParam,
                    AesKey = HexToBase64(prepared.AesKeyHex),
                    EncryptType = 1,
                },
                EncodeType = options.EncodeType,
                SampleRate = options.SampleRate,
                BitsPerSample = options.BitsPerSample,
                Playtime = options.Playtime,
            },
        };
        await _api.SendMessageAsync(toUserId, new[] { item }, token, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads and decrypts an inbound image payload.
    /// </summary>
    /// <param name="image">The image metadata from an inbound message.</param>
    /// <param name="cdnBaseUrl">Optional CDN base URL override.</param>
    /// <param name="cancellationToken">A token that can cancel the download.</param>
    /// <returns>The decrypted image bytes.</returns>
    public Task<byte[]> DownloadImageAsync(WeixinImage image, string? cdnBaseUrl = null, CancellationToken cancellationToken = default)
        => DownloadMediaAsync(image?.Media, image?.AesKey, cdnBaseUrl, cancellationToken);

    /// <summary>
    /// Downloads and decrypts an inbound voice payload.
    /// </summary>
    /// <param name="voice">The voice metadata from an inbound message.</param>
    /// <param name="cdnBaseUrl">Optional CDN base URL override.</param>
    /// <param name="cancellationToken">A token that can cancel the download.</param>
    /// <returns>The decrypted voice bytes.</returns>
    public Task<byte[]> DownloadVoiceAsync(WeixinVoice voice, string? cdnBaseUrl = null, CancellationToken cancellationToken = default)
        => DownloadMediaAsync(voice?.Media, null, cdnBaseUrl, cancellationToken);

    /// <summary>
    /// Downloads and decrypts an inbound file payload.
    /// </summary>
    /// <param name="file">The file metadata from an inbound message.</param>
    /// <param name="cdnBaseUrl">Optional CDN base URL override.</param>
    /// <param name="cancellationToken">A token that can cancel the download.</param>
    /// <returns>The decrypted file bytes.</returns>
    public Task<byte[]> DownloadFileAsync(WeixinFile file, string? cdnBaseUrl = null, CancellationToken cancellationToken = default)
        => DownloadMediaAsync(file?.Media, null, cdnBaseUrl, cancellationToken);

    /// <summary>
    /// Downloads and decrypts an inbound video payload.
    /// </summary>
    /// <param name="video">The video metadata from an inbound message.</param>
    /// <param name="cdnBaseUrl">Optional CDN base URL override.</param>
    /// <param name="cancellationToken">A token that can cancel the download.</param>
    /// <returns>The decrypted video bytes.</returns>
    public Task<byte[]> DownloadVideoAsync(WeixinVideo video, string? cdnBaseUrl = null, CancellationToken cancellationToken = default)
        => DownloadMediaAsync(video?.Media, null, cdnBaseUrl, cancellationToken);

    /// <summary>
    /// Downloads the raw encrypted payload from the CDN without decrypting it.
    /// </summary>
    /// <param name="encryptedQueryParam">The encrypted CDN query parameter.</param>
    /// <param name="cdnBaseUrl">Optional CDN base URL override.</param>
    /// <param name="cancellationToken">A token that can cancel the download.</param>
    /// <returns>The raw encrypted payload bytes.</returns>
    internal Task<byte[]> DownloadRawAsync(string encryptedQueryParam, string? cdnBaseUrl = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(encryptedQueryParam))
        {
            throw new ArgumentException("encryptedQueryParam is required", nameof(encryptedQueryParam));
        }
        return _cdnClient.DownloadRawAsync(encryptedQueryParam, cdnBaseUrl ?? _api.CdnUrl, cancellationToken);
    }

    /// <summary>
    /// Sends a typing indicator to a user.
    /// </summary>
    /// <param name="userId">The target user identifier.</param>
    /// <param name="contextToken">An optional context token. If omitted, a cached token for the user is used.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>A task that completes when the typing indicator has been sent.</returns>
    public async Task SendTypingAsync(string userId, string? contextToken = null, CancellationToken cancellationToken = default)
    {
        var token = EnsureContextToken(userId, contextToken);
        var config = await _api.GetConfigAsync(userId, token, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(config.TypingTicket))
        {
            await _api.SendTypingAsync(userId, config.TypingTicket!, TypingStatus.Typing, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Cancels the typing indicator for a user.
    /// </summary>
    /// <param name="userId">The target user identifier.</param>
    /// <param name="contextToken">An optional context token. If omitted, a cached token for the user is used.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>A task that completes when the typing indicator has been cancelled.</returns>
    public async Task CancelTypingAsync(string userId, string? contextToken = null, CancellationToken cancellationToken = default)
    {
        var token = EnsureContextToken(userId, contextToken);
        var config = await _api.GetConfigAsync(userId, token, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(config.TypingTicket))
        {
            await _api.SendTypingAsync(userId, config.TypingTicket!, TypingStatus.Cancel, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<LoginResult> LoginCoreAsync(LoginOptions? options, CancellationToken cancellationToken)
    {
        if (options is not null)
        {
            _loginOptions = CloneLoginOptions(options);
        }

        var result = await _api.LoginAsync(options, cancellationToken).ConfigureAwait(false);
        var creds = new BotCredentials
        {
            BotToken = result.BotToken,
            BotId = result.BotId,
            BaseUrl = result.BaseUrl,
            UserId = result.UserId,
            SavedAt = DateTimeOffset.UtcNow,
        };
        await SaveCredentialsAsync(creds, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Logged in as bot {BotId}", result.BotId ?? "(unknown)");
        _onLoggedIn?.Invoke(this, new LoginSucceededEventArgs(result));
        return result;
    }

    private static LoginOptions CloneLoginOptions(LoginOptions options)
        => new()
        {
            OnQrCode = options.OnQrCode,
            OnStatusChanged = options.OnStatusChanged,
            BotType = options.BotType,
            Timeout = options.Timeout,
            MaxQrRefresh = options.MaxQrRefresh,
        };

    private async Task SendMediaAsync(
        string toUserId,
        ReadOnlyMemory<byte> payload,
        string? caption,
        string? contextToken,
        UploadMediaType mediaType,
        Func<PreparedUpload, MessageItemPayload> itemFactory,
        CancellationToken cancellationToken)
    {
        var token = EnsureContextToken(toUserId, contextToken);
        var prepared = await UploadPreparation.PrepareAsync(_api, payload, toUserId, mediaType, _cdnClient, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(caption))
        {
            var captionItem = new MessageItemPayload
            {
                Type = MessageItemType.Text,
                TextItem = new TextItemPayload { Text = caption },
            };
            await _api.SendMessageAsync(toUserId, new[] { captionItem }, token, cancellationToken).ConfigureAwait(false);
        }

        var mediaItem = itemFactory(prepared);
        await _api.SendMessageAsync(toUserId, new[] { mediaItem }, token, cancellationToken).ConfigureAwait(false);
    }

    private Task<byte[]> DownloadMediaAsync(WeixinMedia? media, string? overrideHexKey, string? cdnBaseUrl, CancellationToken cancellationToken)
    {
        if (media?.EncryptQueryParam is null)
        {
            throw new ArgumentException("Media payload missing encrypt_query_param");
        }

        var aesKeyBase64 = !string.IsNullOrWhiteSpace(overrideHexKey)
            ? HexToBase64(overrideHexKey!)
            : media.AesKey ?? throw new InvalidOperationException("Media payload missing aes_key");

        return _cdnClient.DownloadAsync(media.EncryptQueryParam, aesKeyBase64, cdnBaseUrl ?? _api.CdnUrl, cancellationToken);
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        var backoff = InitialBackoff;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var response = await _api.GetUpdatesAsync(_updatesBuffer, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(response.GetUpdatesBuffer))
                    {
                        _updatesBuffer = response.GetUpdatesBuffer!;
                    }

                    if (response.ErrorCode is SessionExpiredErrorCode or SessionInvalidErrorCode)
                    {
                        _logger.LogWarning("Session expired with error code {ErrorCode}", response.ErrorCode);
                        _onSessionExpired?.Invoke(this, new SessionExpiredEventArgs(response.ErrorCode));
                        if (await TryReauthenticateAsync(cancellationToken).ConfigureAwait(false))
                        {
                            backoff = InitialBackoff;
                            continue;
                        }

                        return;
                    }

                    if (response.Messages is { Count: > 0 })
                    {
                        _logger.LogDebug("GetUpdates returned {MessageCount} message(s)", response.Messages.Count);
                        foreach (var raw in response.Messages)
                        {
                            if (!IsInboundMessage(raw))
                            {
                                continue;
                            }

                            CacheContextToken(raw.FromUserId, raw.ContextToken);
                            var parsed = ParseMessage(raw);
                            if (parsed is not null)
                            {
                                _logger.LogDebug("Message received from {FromUserId}, kind {ContentKind}", parsed.FromUserId, parsed.ContentKind);
                                _ = Task.Run(() => InvokeMessageHandlerAsync(parsed, cancellationToken), CancellationToken.None);
                            }
                        }
                    }

                    backoff = InitialBackoff;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during polling loop; retrying in {Backoff}", backoff);
                    _onError?.Invoke(this, new WeixinBotErrorEventArgs(ex));
                    await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
                    var next = backoff.TotalMilliseconds * 2;
                    backoff = TimeSpan.FromMilliseconds(Math.Min(next, MaxBackoff.TotalMilliseconds));
                }
            }
        }
        finally
        {
            var cts = _pollingCts;
            _pollingCts = null;
            _pollingTask = null;
            cts?.Dispose();
            _logger.LogInformation("Polling stopped");
            _onStopped?.Invoke(this, new WeixinBotStateChangedEventArgs(DateTimeOffset.UtcNow));
        }
    }

    private void CacheContextToken(string? userId, string? contextToken)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(contextToken))
        {
            return;
        }

        lock (_contextLock)
        {
            if (_contextTokens.ContainsKey(userId))
            {
                _contextTokens[userId] = contextToken;
                return;
            }
            _contextTokens[userId] = contextToken;
            _contextOrder.Enqueue(userId);
            if (_contextOrder.Count > ContextCacheLimit && _contextOrder.TryDequeue(out var evicted))
            {
                _contextTokens.Remove(evicted);
            }
        }
    }

    private string EnsureContextToken(string userId, string? contextToken)
    {
        if (!string.IsNullOrWhiteSpace(contextToken))
        {
            CacheContextToken(userId, contextToken);
            return contextToken;
        }

        lock (_contextLock)
        {
            if (_contextTokens.TryGetValue(userId, out var token))
            {
                return token;
            }
        }

        throw new InvalidOperationException($"No context token for user {userId}. Wait for a message before replying.");
    }

    private async Task<bool> TryReauthenticateAsync(CancellationToken cancellationToken)
    {
        if (_loginOptions is null)
        {
            _onError?.Invoke(this, new WeixinBotErrorEventArgs(
                new InvalidOperationException("Session expired and no login options are available for reauthentication.")));
            return false;
        }

        try
        {
            await LoginCoreAsync(CloneLoginOptions(_loginOptions), cancellationToken).ConfigureAwait(false);
            _updatesBuffer = string.Empty;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _onError?.Invoke(this, new WeixinBotErrorEventArgs(ex));
            return false;
        }
    }

    private async Task InvokeMessageHandlerAsync(WeixinMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await _messageHandler!.HandleMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown — do not log.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message handler threw an unhandled exception for message {MessageId}", message.MessageId);
            _onError?.Invoke(this, new WeixinBotErrorEventArgs(ex));
        }
    }

    private bool IsInboundMessage(MessagePayload payload)
    {
        var botUserId = _credentials?.UserId;
        var fromUserId = payload.FromUserId;
        var toUserId = payload.ToUserId;

        return payload.MessageType switch
        {
            MessageType.User => true,
            MessageType.Bot => false,
            _ => !string.IsNullOrWhiteSpace(fromUserId)
                && (!string.IsNullOrWhiteSpace(payload.ContextToken) || payload.Items is { Count: > 0 }),
        };
    }

    private static WeixinMessage? ParseMessage(MessagePayload payload)
    {
        if (payload.Items is null || payload.Items.Count == 0)
        {
            return new WeixinMessage(
                payload.MessageId ?? Guid.NewGuid().ToString("n"),
                payload.FromUserId ?? string.Empty,
                payload.ToUserId ?? string.Empty,
                payload.CreatedAtMs is long ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : DateTimeOffset.UtcNow,
                payload.ContextToken,
                string.Empty,
                string.Empty,
                MessageContentKind.Unknown,
                null,
                null,
                null,
                null,
                null);
        }

        var text = string.Empty;
        var textWithQuote = string.Empty;
        WeixinQuotedMessage? quoted = null;

        foreach (var item in payload.Items)
        {
            if (item.Type == MessageItemType.Text && item.TextItem?.Text is { } body)
            {
                text = body;
                textWithQuote = body;
                if (item.ReferencedMessage is { } reference)
                {
                    List<string> parts = [];
                    if (!string.IsNullOrWhiteSpace(reference.Title))
                    {
                        parts.Add(reference.Title!);
                    }
                    if (reference.MessageItem?.TextItem?.Text is { } quotedText)
                    {
                        parts.Add(quotedText);
                    }
                    var combined = parts.Count > 0 ? string.Join(" | ", parts) : null;
                    if (!string.IsNullOrEmpty(combined))
                    {
                        textWithQuote = $"[引用: {combined}]\n{text}";
                    }
                    quoted = new WeixinQuotedMessage(reference.Title, combined);
                }
                break;
            }
        }

        MessageContentKind kind = MessageContentKind.Text;
        WeixinImage? image = null;
        WeixinVideo? video = null;
        WeixinFile? file = null;
        WeixinVoice? voice = null;

        foreach (var item in payload.Items)
        {
            if (item.Type == MessageItemType.Image && item.ImageItem is { } img)
            {
                kind = MessageContentKind.Image;
                image = ToWeixinImage(img);
                break;
            }
            if (item.Type == MessageItemType.Video && item.VideoItem is { } vid)
            {
                kind = MessageContentKind.Video;
                video = ToWeixinVideo(vid);
                break;
            }
            if (item.Type == MessageItemType.File && item.FileItem is { } fileItem)
            {
                kind = MessageContentKind.File;
                file = ToWeixinFile(fileItem);
                break;
            }
            if (item.Type == MessageItemType.Voice && item.VoiceItem is { } voiceItem)
            {
                kind = MessageContentKind.Voice;
                voice = ToWeixinVoice(voiceItem);
                if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(voiceItem.Text))
                {
                    text = voiceItem.Text!;
                    textWithQuote = voiceItem.Text!;
                }
                break;
            }
        }

        if (payload.Items.All(i => i.Type != MessageItemType.Text))
        {
            textWithQuote = text;
        }

        return new WeixinMessage(
            payload.MessageId ?? Guid.NewGuid().ToString("n"),
            payload.FromUserId ?? string.Empty,
            payload.ToUserId ?? string.Empty,
            payload.CreatedAtMs is long created ? DateTimeOffset.FromUnixTimeMilliseconds(created) : DateTimeOffset.UtcNow,
            payload.ContextToken,
            text,
            string.IsNullOrEmpty(textWithQuote) ? text : textWithQuote,
            kind,
            image,
            video,
            file,
            voice,
            quoted);
    }

    private static WeixinImage ToWeixinImage(ImageItemPayload image)
        => new(ToWeixinMedia(image.Media), image.MidSize?.ToString(CultureInfo.InvariantCulture), image.AesKey);

    private static WeixinVoice ToWeixinVoice(VoiceItemPayload voice)
        => new(ToWeixinMedia(voice.Media), voice.EncodeType, voice.SampleRate, voice.BitsPerSample, voice.Playtime, voice.Text);

    private static WeixinFile ToWeixinFile(FileItemPayload file)
        => new(ToWeixinMedia(file.Media), file.FileName, file.Length);

    private static WeixinVideo ToWeixinVideo(VideoItemPayload video)
        => new(ToWeixinMedia(video.Media), video.VideoSize?.ToString(CultureInfo.InvariantCulture));

    private static WeixinMedia ToWeixinMedia(MediaPayload? media)
    {
        if (media?.EncryptQueryParam is null)
        {
            throw new InvalidOperationException("Media payload missing encrypt_query_param");
        }

        return new WeixinMedia(media.EncryptQueryParam, media.AesKey, media.EncryptType);
    }

    private async Task<bool> TryLoadCredentialsAsync(CancellationToken cancellationToken)
    {
        if (_credentialStore is null)
        {
            return false;
        }
        try
        {
            BotCredentials? creds = await _credentialStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (creds?.BotToken is { Length: > 0 })
            {
                _api.Token = creds.BotToken;
                if (!string.IsNullOrWhiteSpace(creds.BaseUrl))
                {
                    _api.BaseUrl = creds.BaseUrl!;
                }
                _credentials = creds;
                _logger.LogInformation("Credentials loaded for bot {BotId}", creds.BotId ?? "(unknown)");
                _onCredentialsLoaded?.Invoke(this, new CredentialsEventArgs(creds));
                return true;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _onError?.Invoke(this, new WeixinBotErrorEventArgs(ex));
        }

        return false;
    }

    private async Task SaveCredentialsAsync(BotCredentials credentials, CancellationToken cancellationToken)
    {
        _credentials = credentials;
        if (_credentialStore is null)
        {
            return;
        }
        try
        {
            BotCredentials payload = new()
            {
                BotToken = credentials.BotToken,
                BotId = credentials.BotId,
                BaseUrl = credentials.BaseUrl,
                UserId = credentials.UserId,
                SavedAt = credentials.SavedAt ?? DateTimeOffset.UtcNow,
            };
            await _credentialStore.SaveAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _onError?.Invoke(this, new WeixinBotErrorEventArgs(ex));
        }
    }

    private static string HexToBase64(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            throw new ArgumentException("Hex value is required", nameof(hex));
        }
        return Convert.ToBase64String(Convert.FromHexString(hex));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WeixinBot));
        }
    }

    /// <summary>
    /// Stops background work and asynchronously releases resources used by the bot.
    /// </summary>
    /// <returns>A task representing asynchronous disposal.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        await StopAsync().ConfigureAwait(false);
        if (_ownsSharedHttpClient)
        {
            _sharedHttpClient.Dispose();
        }
        _cdnClient.Dispose();
        _api.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Stops background work and releases resources used by the bot.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
