using System.Globalization;
using System.Text.Json;
using Weixin.Bot.Sdk.Api;
using Weixin.Bot.Sdk.Media;
using Weixin.Bot.Sdk.Models;
using Weixin.Bot.Sdk.Models.Wire;
using Weixin.Bot.Sdk.Utilities;

namespace Weixin.Bot.Sdk.Bot;

public sealed class WeixinBot : IAsyncDisposable, IDisposable
{
    private const int ContextCacheLimit = 1000;
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    private readonly WeixinBotApi _api;
    private readonly CdnClient _cdnClient;
    private readonly HttpClient _sharedHttpClient;
    private readonly bool _ownsSharedHttpClient;
    private readonly string? _credentialsPath;
    private readonly Dictionary<string, string> _contextTokens = new();
    private readonly Queue<string> _contextOrder = new();
    private readonly object _contextLock = new();
    private readonly JsonSerializerOptions _credentialSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private string _updatesBuffer = string.Empty;
    private BotCredentials? _credentials;
    private bool _disposed;

    public WeixinBot(WeixinBotOptions? options = null)
    {
        options ??= new();
        _credentialsPath = options.CredentialsPath;
        _sharedHttpClient = options.HttpClient ?? new HttpClient();
        _ownsSharedHttpClient = options.HttpClient is null;

        var apiOptions = new WeixinBotApiOptions
        {
            BaseUrl = options.BaseUrl,
            CdnUrl = options.CdnUrl,
            Token = options.Token,
            Version = options.Version,
            HttpClient = _sharedHttpClient,
        };
        _api = new(apiOptions);
        _cdnClient = new CdnClient(_sharedHttpClient, _api.CdnUrl)
        {
            BaseUrl = _api.CdnUrl,
        };

        TryLoadCredentials();
    }

    public WeixinBotApi Api => _api;
    internal CdnClient Cdn => _cdnClient;
    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(_api.Token);
    public bool IsRunning => _pollingTask is { IsCompleted: false };
    public BotCredentials? CurrentCredentials => _credentials;

    public event EventHandler? Started;
    public event EventHandler? Stopped;
    public event EventHandler<LoginResult>? LoggedIn;
    public event EventHandler<CredentialsEventArgs>? CredentialsLoaded;
    public event EventHandler<WeixinMessageEventArgs>? MessageReceived;
    public event EventHandler<GetUpdatesResponse>? PollCompleted;
    public event EventHandler<int>? SessionExpired;
    public event EventHandler<Exception>? Error;

    public Task<LoginResult> LoginAsync(LoginOptions? options = null, CancellationToken cancellationToken = default)
        => LoginCoreAsync(options, cancellationToken);

    public void Start(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsRunning)
        {
            return;
        }
        if (!IsLoggedIn)
        {
            throw new InvalidOperationException("Not logged in. Call LoginAsync first.");
        }
        _pollingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollingTask = Task.Run(() => PollLoopAsync(_pollingCts.Token), CancellationToken.None);
        Started?.Invoke(this, EventArgs.Empty);
    }

    public async Task StopAsync()
    {
        var cts = _pollingCts;
        var pollingTask = _pollingTask;

        if (cts is null || pollingTask is null)
        {
            return;
        }

        cts.Cancel();
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

    public Task<string> ReplyAsync(WeixinMessage message, string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return SendTextAsync(message.FromUserId, text, message.ContextToken, cancellationToken);
    }

    public Task<string> SendTextAsync(string toUserId, string text, string? contextToken = null, CancellationToken cancellationToken = default)
    {
        var token = EnsureContextToken(toUserId, contextToken);
        return _api.SendTextAsync(toUserId, text ?? string.Empty, token, cancellationToken);
    }

    public async Task SendImageAsync(string toUserId, ReadOnlyMemory<byte> image, string? caption = null, string? contextToken = null, CancellationToken cancellationToken = default)
    {
        await SendMediaAsync(toUserId, image, caption, contextToken, UploadMediaType.Image, prepared => new MessageItemPayload
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
                MidSize = prepared.FileSizeCiphertext.ToString(CultureInfo.InvariantCulture),
            },
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendVideoAsync(string toUserId, ReadOnlyMemory<byte> video, string? caption = null, string? contextToken = null, CancellationToken cancellationToken = default)
    {
        await SendMediaAsync(toUserId, video, caption, contextToken, UploadMediaType.Video, prepared => new MessageItemPayload
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
                VideoSize = prepared.FileSizeCiphertext.ToString(CultureInfo.InvariantCulture),
            },
        }, cancellationToken).ConfigureAwait(false);
    }

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

    public Task<byte[]> DownloadImageAsync(ImageItemPayload image, string? cdnBaseUrl = null, CancellationToken cancellationToken = default)
        => DownloadMediaAsync(image?.Media, image?.AesKey, cdnBaseUrl, cancellationToken);

    public Task<byte[]> DownloadVoiceAsync(VoiceItemPayload voice, string? cdnBaseUrl = null, CancellationToken cancellationToken = default)
        => DownloadMediaAsync(voice?.Media, null, cdnBaseUrl, cancellationToken);

    public Task<byte[]> DownloadFileAsync(FileItemPayload file, string? cdnBaseUrl = null, CancellationToken cancellationToken = default)
        => DownloadMediaAsync(file?.Media, null, cdnBaseUrl, cancellationToken);

    public Task<byte[]> DownloadVideoAsync(VideoItemPayload video, string? cdnBaseUrl = null, CancellationToken cancellationToken = default)
        => DownloadMediaAsync(video?.Media, null, cdnBaseUrl, cancellationToken);

    public Task<byte[]> DownloadRawAsync(string encryptedQueryParam, string? cdnBaseUrl = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(encryptedQueryParam))
        {
            throw new ArgumentException("encryptedQueryParam is required", nameof(encryptedQueryParam));
        }
        return _cdnClient.DownloadRawAsync(encryptedQueryParam, cdnBaseUrl ?? _api.CdnUrl, cancellationToken);
    }

    public async Task SendTypingAsync(string userId, string? contextToken = null, CancellationToken cancellationToken = default)
    {
        var token = EnsureContextToken(userId, contextToken);
        var config = await _api.GetConfigAsync(userId, token, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(config.TypingTicket))
        {
            await _api.SendTypingAsync(userId, config.TypingTicket!, TypingStatus.Typing, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task CancelTypingAsync(string userId, string? contextToken = null, CancellationToken cancellationToken = default)
    {
        var token = EnsureContextToken(userId, contextToken);
        var config = await _api.GetConfigAsync(userId, token, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(config.TypingTicket))
        {
            await _api.SendTypingAsync(userId, config.TypingTicket!, TypingStatus.Cancel, cancellationToken).ConfigureAwait(false);
        }
    }

    public static string MarkdownToPlainText(string text) => Markdown.ToPlainText(text);

    private async Task<LoginResult> LoginCoreAsync(LoginOptions? options, CancellationToken cancellationToken)
    {
        var result = await _api.LoginAsync(options, cancellationToken).ConfigureAwait(false);
        var creds = new BotCredentials
        {
            BotToken = result.BotToken,
            BotId = result.BotId,
            BaseUrl = result.BaseUrl,
            UserId = result.UserId,
            SavedAt = DateTimeOffset.UtcNow,
        };
        SaveCredentials(creds);
        LoggedIn?.Invoke(this, result);
        return result;
    }

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
        var items = new List<MessageItemPayload>();
        if (!string.IsNullOrWhiteSpace(caption))
        {
            items.Add(new MessageItemPayload
            {
                Type = MessageItemType.Text,
                TextItem = new TextItemPayload { Text = caption },
            });
        }
        items.Add(itemFactory(prepared));

        foreach (var item in items)
        {
            await _api.SendMessageAsync(toUserId, new[] { item }, token, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task<byte[]> DownloadMediaAsync(MediaPayload? media, string? overrideHexKey, string? cdnBaseUrl, CancellationToken cancellationToken)
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

                    if (response.ErrorCode is -14 or -13)
                    {
                        SessionExpired?.Invoke(this, response.ErrorCode);
                        _pollingCts?.Cancel();
                        return;
                    }

                    if (response.Messages is { Count: > 0 })
                    {
                        foreach (var raw in response.Messages)
                        {
                            if (raw.MessageType != MessageType.User)
                            {
                                continue;
                            }

                            CacheContextToken(raw.FromUserId, raw.ContextToken);
                            var parsed = ParseMessage(raw);
                            if (parsed is not null)
                            {
                                MessageReceived?.Invoke(this, new WeixinMessageEventArgs(parsed));
                            }
                        }
                    }

                    PollCompleted?.Invoke(this, response);
                    backoff = InitialBackoff;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Error?.Invoke(this, ex);
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
            Stopped?.Invoke(this, EventArgs.Empty);
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

    private WeixinMessage? ParseMessage(MessagePayload payload)
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
                null,
                payload);
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
                    var parts = new List<string>();
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
                    quoted = new WeixinQuotedMessage(reference.Title, reference.MessageItem, combined);
                }
                break;
            }
        }

        MessageContentKind kind = MessageContentKind.Text;
        ImageItemPayload? image = null;
        VideoItemPayload? video = null;
        FileItemPayload? file = null;
        VoiceItemPayload? voice = null;

        foreach (var item in payload.Items)
        {
            if (item.Type == MessageItemType.Image && item.ImageItem is { } img)
            {
                kind = MessageContentKind.Image;
                image = img;
                break;
            }
            if (item.Type == MessageItemType.Video && item.VideoItem is { } vid)
            {
                kind = MessageContentKind.Video;
                video = vid;
                break;
            }
            if (item.Type == MessageItemType.File && item.FileItem is { } fileItem)
            {
                kind = MessageContentKind.File;
                file = fileItem;
                break;
            }
            if (item.Type == MessageItemType.Voice && item.VoiceItem is { } voiceItem)
            {
                kind = MessageContentKind.Voice;
                voice = voiceItem;
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
            quoted,
            payload);
    }

    private void TryLoadCredentials()
    {
        if (string.IsNullOrWhiteSpace(_credentialsPath) || !File.Exists(_credentialsPath))
        {
            return;
        }
        try
        {
            var json = File.ReadAllText(_credentialsPath);
            var creds = JsonSerializer.Deserialize<BotCredentials>(json, _credentialSerializerOptions);
            if (creds?.BotToken is { Length: > 0 })
            {
                _api.Token = creds.BotToken;
                if (!string.IsNullOrWhiteSpace(creds.BaseUrl))
                {
                    _api.BaseUrl = creds.BaseUrl!;
                }
                _credentials = creds;
                CredentialsLoaded?.Invoke(this, new CredentialsEventArgs(creds));
            }
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
        }
    }

    private void SaveCredentials(BotCredentials credentials)
    {
        _credentials = credentials;
        if (string.IsNullOrWhiteSpace(_credentialsPath))
        {
            return;
        }
        try
        {
            var directory = Path.GetDirectoryName(_credentialsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var payload = credentials with { SavedAt = DateTimeOffset.UtcNow };
            var json = JsonSerializer.Serialize(payload, _credentialSerializerOptions);
            File.WriteAllText(_credentialsPath, json);
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, ex);
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

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
