using System.Buffers.Binary;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Weixin.Bot.Sdk.Media;
using Weixin.Bot.Sdk.Models;
using Weixin.Bot.Sdk.Models.Wire;

namespace Weixin.Bot.Sdk.Api;

internal sealed class WeixinBotApi : IDisposable
{
    private const string DefaultBaseUrl = "https://ilinkai.weixin.qq.com";
    private static readonly TimeSpan DefaultLongPollTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultApiTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

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

    private async Task<QrCodeResponse> GetQrCodeAsync(string botType = "3", CancellationToken cancellationToken = default)
    {
        var url = BuildAbsoluteUrl($"ilink/bot/get_bot_qrcode?bot_type={Uri.EscapeDataString(botType)}");
        var qrResponse = await _httpClient.GetFromJsonAsync<QrCodeResponse>(url, _serializerOptions, cancellationToken).ConfigureAwait(false);
        return qrResponse ?? throw new InvalidOperationException("QR code response was empty");
    }

    private async Task<QrStatusResponse> PollQrStatusAsync(string qrcode, CancellationToken cancellationToken = default)
    {
        var url = BuildAbsoluteUrl($"ilink/bot/get_qrcode_status?qrcode={Uri.EscapeDataString(qrcode)}");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(DefaultLongPollTimeout);
        try
        {
            var response = await _httpClient.GetFromJsonAsync<QrStatusResponse>(url, _serializerOptions, cts.Token).ConfigureAwait(false);
            return response ?? new QrStatusResponse { Status = "wait" };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new QrStatusResponse { Status = "wait" };
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
                case "confirmed":
                    Token = status.BotToken ?? throw new InvalidOperationException("Login succeeded but bot_token missing");
                    if (!string.IsNullOrWhiteSpace(status.BaseUrl))
                    {
                        BaseUrl = status.BaseUrl!;
                    }
                    return new LoginResult(Token, status.BotId, status.BaseUrl, status.UserId);
                case "expired":
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
                case "wait":
                    break;
                default:
                    break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Login timed out before confirmation");
    }

    internal async Task<GetUpdatesResponse> GetUpdatesAsync(string? updatesBuffer = "", CancellationToken cancellationToken = default)
    {
        try
        {
            return await PostAsync<GetUpdatesResponse>("ilink/bot/getupdates", new
            {
                get_updates_buf = updatesBuffer ?? string.Empty,
                base_info = BaseInfo(),
            }, DefaultLongPollTimeout, cancellationToken).ConfigureAwait(false);
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

    internal async Task<string> SendMessageAsync<TItem>(string toUserId, IEnumerable<TItem> items, string contextToken, CancellationToken cancellationToken = default)
    {
        var clientId = $"wx-bot-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8))}";
        await PostWithoutResponseAsync("ilink/bot/sendmessage", new
        {
            msg = new
            {
                from_user_id = string.Empty,
                to_user_id = toUserId,
                client_id = clientId,
                message_type = (int)MessageType.Bot,
                message_state = (int)MessageState.Finish,
                item_list = items,
                context_token = contextToken,
            },
            base_info = BaseInfo(),
        }, cancellationToken).ConfigureAwait(false);
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
        return PostWithoutResponseAsync("ilink/bot/sendtyping", new
        {
            ilink_user_id = userId,
            typing_ticket = typingTicket,
            status = (int)status,
            base_info = BaseInfo(),
        }, cancellationToken);
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
        return PostAsync<UploadUrlResponse>("ilink/bot/getuploadurl", new
        {
            filekey = fileKey,
            media_type = (int)mediaType,
            to_user_id = toUserId,
            rawsize = rawSize,
            rawfilemd5 = rawFileMd5,
            filesize = paddedFileSize,
            no_need_thumb = true,
            aeskey = aesKeyHex,
            base_info = BaseInfo(),
        }, DefaultApiTimeout, cancellationToken);
    }

    internal Task<ConfigResponse> GetConfigAsync(string userId, string contextToken, CancellationToken cancellationToken = default)
    {
        return PostAsync<ConfigResponse>("ilink/bot/getconfig", new
        {
            ilink_user_id = userId,
            context_token = contextToken,
            base_info = BaseInfo(),
        }, TimeSpan.FromSeconds(10), cancellationToken);
    }

    private string BuildAbsoluteUrl(string endpoint)
    {
        var baseUri = new Uri(BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/");
        return new Uri(baseUri, endpoint).ToString();
    }

    private Dictionary<string, string> BaseInfo() => new() { ["channel_version"] = Version };

    private async Task<T> PostAsync<T>(string endpoint, object body, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            throw new InvalidOperationException("API token is not set. Authenticate first.");
        }

        var url = BuildAbsoluteUrl(endpoint);
        var json = JsonSerializer.Serialize(body, _serializerOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("AuthorizationType", "ilink_bot_token");
        request.Headers.TryAddWithoutValidation("X-WECHAT-UIN", RandomWechatUin());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        return await SendAsync<T>(request, timeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task PostWithoutResponseAsync(string endpoint, object body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            throw new InvalidOperationException("API token is not set. Authenticate first.");
        }

        var url = BuildAbsoluteUrl(endpoint);
        var json = JsonSerializer.Serialize(body, _serializerOptions);
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

    private async Task<T> SendAsync<T>(HttpRequestMessage request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<T>(_serializerOptions, cancellationToken).ConfigureAwait(false);
            return result ?? throw new InvalidOperationException("API response deserialized to null");
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"API request to {request.RequestUri} timed out after {timeout.TotalSeconds:N0}s", ex);
        }
    }

    private static string RandomWechatUin()
    {
        // 1. Generate 4 random bytes
        Span<byte> buffer = stackalloc byte[4];
        RandomNumberGenerator.Fill(buffer);

        // 2. Read as UInt32 (big-endian)
        uint uint32 = BinaryPrimitives.ReadUInt32BigEndian(buffer);

        // 3. Convert to string
        string str = uint32.ToString();

        // 4. Encode as UTF-8 bytes
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(str);

        // 5. Convert to Base64
        return Convert.ToBase64String(utf8Bytes);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
