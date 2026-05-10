using System.Buffers.Binary;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using Weixin.Bot.Sdk.Media;
using Weixin.Bot.Sdk.Models;
using Weixin.Bot.Sdk.Models.Wire;
using Weixin.Bot.Sdk.Serialization;

namespace Weixin.Bot.Sdk.Api;

internal sealed class WeixinBotApi : IDisposable
{
    private const string DefaultBotType = "3";
    private const string QrStatusWait = "wait";
    private const string QrStatusConfirmed = "confirmed";
    private const string QrStatusExpired = "expired";
    private static readonly TimeSpan DefaultQrPollInterval = TimeSpan.FromSeconds(1);
    private const string DefaultBaseUrl = "https://ilinkai.weixin.qq.com";
    private static readonly TimeSpan DefaultLongPollTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultApiTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    internal WeixinBotApi(WeixinBotApiOptions? options = null)
    {
        options ??= new();
        BaseUrl = options.BaseUrl ?? DefaultBaseUrl;
        CdnUrl = options.CdnUrl ?? CdnClient.DefaultBaseUrl;
        Token = options.Token;
        Version = options.Version;
        _httpClient = options.HttpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("iLink-App-ClientVersion", "1");
        _ownsHttpClient = options.HttpClient is null;
    }

    internal string BaseUrl { get; set; }
    internal string CdnUrl { get; set; }
    internal string? Token { get; set; }
    internal string Version { get; set; }

    private async Task<QrCodeResponse> GetQrCodeAsync(string botType = DefaultBotType, CancellationToken cancellationToken = default)
    {
        var url = BuildAbsoluteUrl($"ilink/bot/get_bot_qrcode?bot_type={Uri.EscapeDataString(botType)}");
        var qrResponse = await _httpClient.GetFromJsonAsync(
            url,
            WeixinBotApiJsonSerializerContext.Default.QrCodeResponse,
            cancellationToken).ConfigureAwait(false);
        return qrResponse ?? throw new InvalidOperationException("QR code response was empty");
    }

    private async Task<QrStatusResponse> PollQrStatusAsync(string qrcode, CancellationToken cancellationToken = default)
    {
        var url = BuildAbsoluteUrl($"ilink/bot/get_qrcode_status?qrcode={Uri.EscapeDataString(qrcode)}");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(DefaultLongPollTimeout);
        try
        {
            var status = await _httpClient.GetFromJsonAsync(
                url,
                WeixinBotApiJsonSerializerContext.Default.QrStatusResponse,
                cts.Token).ConfigureAwait(false);
            return status ?? new QrStatusResponse { Status = QrStatusWait };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new QrStatusResponse { Status = QrStatusWait };
        }
    }

    internal async Task<LoginResult> LoginAsync(LoginOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        var qr = await GetQrCodeAsync(options.BotType, cancellationToken).ConfigureAwait(false);
        if (options.OnQrCode is { } onQr && !string.IsNullOrWhiteSpace(qr.QrCodeImageContent))
        {
            await onQr(qr.QrCodeImageContent!);
        }

        var deadline = DateTimeOffset.UtcNow + options.Timeout;
        var refreshCount = 0;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(qr.QrCode))
            {
                throw new InvalidOperationException("QR code token missing from response");
            }

            var status = await PollQrStatusAsync(qr.QrCode, cancellationToken).ConfigureAwait(false);
            if (options.OnStatusChanged is { } onStatus && !string.IsNullOrWhiteSpace(status.Status))
            {
                await onStatus(status.Status!);
            }

            switch (status.Status)
            {
                case QrStatusConfirmed:
                    Token = status.BotToken ?? throw new InvalidOperationException("Login succeeded but bot_token missing");
                    if (!string.IsNullOrWhiteSpace(status.BaseUrl))
                    {
                        BaseUrl = status.BaseUrl!;
                    }
                    return new LoginResult(Token, status.BotId, status.BaseUrl, status.UserId);
                case QrStatusExpired:
                    refreshCount++;
                    if (refreshCount >= options.MaxQrRefresh)
                    {
                        throw new TimeoutException($"QR code expired {options.MaxQrRefresh} times");
                    }
                    qr = await GetQrCodeAsync(options.BotType, cancellationToken).ConfigureAwait(false);
                    if (options.OnQrCode is { } onQrRefresh && !string.IsNullOrWhiteSpace(qr.QrCodeImageContent))
                    {
                        await onQrRefresh(qr.QrCodeImageContent!);
                    }
                    break;
                case QrStatusWait:
                    break;
                default:
                    break;
            }

            await Task.Delay(DefaultQrPollInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Login timed out before confirmation");
    }

    internal async Task<GetUpdatesResponse> GetUpdatesAsync(string? updatesBuffer = "", CancellationToken cancellationToken = default)
    {
        try
        {
            return await PostAsync("ilink/bot/getupdates", new GetUpdatesRequest
            {
                GetUpdatesBuffer = updatesBuffer ?? string.Empty,
                BaseInfo = BaseInfo(),
            }, WeixinBotApiJsonSerializerContext.Default.GetUpdatesRequest, WeixinBotApiJsonSerializerContext.Default.GetUpdatesResponse, DefaultLongPollTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return new GetUpdatesResponse
            {
                ReturnCode = 0,
                Messages = new(),
                GetUpdatesBuffer = updatesBuffer,
            };
        }
        catch (JsonException)
        {
            return new GetUpdatesResponse
            {
                ReturnCode = -1,
                Messages = new(),
                GetUpdatesBuffer = updatesBuffer,
            };
        }
    }

    internal async Task<string> SendMessageAsync(string toUserId, IEnumerable<MessageItemPayload> items, string contextToken, CancellationToken cancellationToken = default)
    {
        var clientId = $"wx-bot-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8))}";
        await PostWithoutResponseAsync("ilink/bot/sendmessage", new SendMessageRequest
        {
            Message = new OutboundMessagePayload
            {
                ToUserId = toUserId,
                ClientId = clientId,
                MessageType = (int)MessageType.Bot,
                MessageState = (int)MessageState.Finish,
                Items = items as MessageItemPayload[] ?? items.ToArray(),
                ContextToken = contextToken,
            },
            BaseInfo = BaseInfo(),
        }, WeixinBotApiJsonSerializerContext.Default.SendMessageRequest, cancellationToken).ConfigureAwait(false);
        return clientId;
    }

    internal Task<string> SendTextAsync(string toUserId, string text, string contextToken, CancellationToken cancellationToken = default)
    {
        var payload = new[]
        {
            new MessageItemPayload
            {
                Type = MessageItemType.Text,
                TextItem = new TextItemPayload { Text = text ?? string.Empty },
            },
        };
        return SendMessageAsync(toUserId, payload, contextToken, cancellationToken);
    }

    internal Task SendTypingAsync(string userId, string typingTicket, TypingStatus status, CancellationToken cancellationToken = default)
    {
        return PostWithoutResponseAsync("ilink/bot/sendtyping", new SendTypingRequest
        {
            UserId = userId,
            TypingTicket = typingTicket,
            Status = (int)status,
            BaseInfo = BaseInfo(),
        }, WeixinBotApiJsonSerializerContext.Default.SendTypingRequest, cancellationToken);
    }

    internal Task<UploadUrlResponse> GetUploadUrlAsync(
        string fileKey,
        UploadMediaType mediaType,
        string toUserId,
        int rawSize,
        string rawFileMd5,
        int paddedFileSize,
        string aesKeyHex,
        CancellationToken cancellationToken = default)
    {
        return PostAsync("ilink/bot/getuploadurl", new GetUploadUrlRequest
        {
            FileKey = fileKey,
            MediaType = (int)mediaType,
            ToUserId = toUserId,
            RawSize = rawSize,
            RawFileMd5 = rawFileMd5,
            FileSize = paddedFileSize,
            AesKey = aesKeyHex,
            BaseInfo = BaseInfo(),
        }, WeixinBotApiJsonSerializerContext.Default.GetUploadUrlRequest, WeixinBotApiJsonSerializerContext.Default.UploadUrlResponse, DefaultApiTimeout, cancellationToken);
    }

    internal Task<ConfigResponse> GetConfigAsync(string userId, string contextToken, CancellationToken cancellationToken = default)
    {
        return PostAsync("ilink/bot/getconfig", new GetConfigRequest
        {
            UserId = userId,
            ContextToken = contextToken,
            BaseInfo = BaseInfo(),
        }, WeixinBotApiJsonSerializerContext.Default.GetConfigRequest, WeixinBotApiJsonSerializerContext.Default.ConfigResponse, TimeSpan.FromSeconds(10), cancellationToken);
    }

    private string BuildAbsoluteUrl(string endpoint)
    {
        var baseUri = new Uri(BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/");
        return new Uri(baseUri, endpoint).ToString();
    }

    private BaseInfoPayload BaseInfo() => new() { ChannelVersion = Version };

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest body,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            throw new InvalidOperationException("API token is not set. Authenticate first.");
        }

        var url = BuildAbsoluteUrl(endpoint);
        var json = JsonSerializer.Serialize(body, requestTypeInfo);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("AuthorizationType", "ilink_bot_token");
        request.Headers.TryAddWithoutValidation("X-WECHAT-UIN", RandomWechatUin());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        return await SendAsync(request, responseTypeInfo, timeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task PostWithoutResponseAsync<TRequest>(
        string endpoint,
        TRequest body,
        JsonTypeInfo<TRequest> requestTypeInfo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            throw new InvalidOperationException("API token is not set. Authenticate first.");
        }

        var url = BuildAbsoluteUrl(endpoint);
        var json = JsonSerializer.Serialize(body, requestTypeInfo);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("AuthorizationType", "ilink_bot_token");
        request.Headers.TryAddWithoutValidation("X-WECHAT-UIN", RandomWechatUin());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        
        try
        {
            using var cts =  CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(DefaultApiTimeout);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"API request to {request.RequestUri} timed out", ex);
        }
    }

    private async Task<T> SendAsync<T>(
        HttpRequestMessage request,
        JsonTypeInfo<T> responseTypeInfo,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync(responseTypeInfo, cancellationToken).ConfigureAwait(false);
            return result ?? throw new InvalidOperationException("API response deserialized to null");
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"API request to {request.RequestUri} timed out after {timeout.TotalSeconds:N0}s", ex);
        }
    }

    private static string RandomWechatUin()
    {
        Span<byte> buffer = stackalloc byte[4];
        RandomNumberGenerator.Fill(buffer);
        var uint32 = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(uint32.ToString()));
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
