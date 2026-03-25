using System.Globalization;
using System.Net.Http.Headers;
using Weixin.Bot.Sdk.Media;
using Weixin.Bot.Sdk.Models;
using Weixin.Bot.Sdk.Models.Wire;

namespace Weixin.Bot.Sdk.Api;

internal sealed class WeixinBotApi : IDisposable
{
    internal const string DefaultBaseUrl = "https://ilinkai.weixin.qq.com";
    private static readonly TimeSpan DefaultLongPollTimeout = TimeSpan.FromSeconds(35);
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
        _ownsHttpClient = options.HttpClient is null;
    }

    internal string BaseUrl { get; set; }
    internal string CdnUrl { get; set; }
    internal string? Token { get; set; }
    internal string Version { get; set; }

    internal async Task<QrCodeResponse> GetQrCodeAsync(string botType = "3", CancellationToken cancellationToken = default)
    {
        var url = BuildAbsoluteUrl($"ilink/bot/get_bot_qrcode?bot_type={Uri.EscapeDataString(botType)}");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<QrCodeResponse>(stream, _serializerOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("QR code response was empty");
    }

    internal async Task<QrStatusResponse> PollQrStatusAsync(string qrcode, CancellationToken cancellationToken = default)
    {
        var url = BuildAbsoluteUrl($"ilink/bot/get_qrcode_status?qrcode={Uri.EscapeDataString(qrcode)}");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("iLink-App-ClientVersion", "1");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(DefaultLongPollTimeout);
        try
        {
            using var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<QrStatusResponse>(stream, _serializerOptions, cancellationToken).ConfigureAwait(false)
                ?? new QrStatusResponse { Status = "wait" };
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

    internal async Task<string> SendMessageAsync(string toUserId, IEnumerable<MessageItemPayload> items, string contextToken, CancellationToken cancellationToken = default)
    {
        var clientId = $"wx-bot-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant()}";
        await PostAsync<object>("ilink/bot/sendmessage", new
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
        }, DefaultApiTimeout, cancellationToken).ConfigureAwait(false);
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
        return PostAsync<object>("ilink/bot/sendtyping", new
        {
            ilink_user_id = userId,
            typing_ticket = typingTicket,
            status = (int)status,
            base_info = BaseInfo(),
        }, TimeSpan.FromSeconds(10), cancellationToken);
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

    private Dictionary<string, string> BaseInfo() => new() { ["channel_version"] = Version }; // simple structure

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

    private async Task<T> SendAsync<T>(HttpRequestMessage request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var result = await JsonSerializer.DeserializeAsync<T>(stream, _serializerOptions, cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                throw new InvalidOperationException("API response deserialized to null");
            }
            return result;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"API request to {request.RequestUri} timed out after {timeout.TotalSeconds:N0}s", ex);
        }
    }

    private static string RandomWechatUin()
    {
        Span<byte> random = stackalloc byte[4];
        RandomNumberGenerator.Fill(random);
        var value = BitConverter.ToUInt32(random);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value.ToString(CultureInfo.InvariantCulture)));
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
