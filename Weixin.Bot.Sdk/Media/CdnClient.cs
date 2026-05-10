using Weixin.Bot.Sdk.Crypto;

namespace Weixin.Bot.Sdk.Media;

internal sealed class CdnClient : IDisposable
{
    public const string DefaultBaseUrl = "https://novac2c.cdn.weixin.qq.com/c2c";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);
    private const int UploadMaxRetries = 3;

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly ILogger<CdnClient> _logger;

    public CdnClient(HttpClient? httpClient = null, string? baseUrl = null, ILoggerFactory? loggerFactory = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsClient = httpClient is null;
        BaseUrl = baseUrl ?? DefaultBaseUrl;
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<CdnClient>();
    }

    public string BaseUrl { get; set; }

    public async Task<byte[]> DownloadAsync(string encryptedQueryParam, string aesKeyBase64, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        var key = AesEcb.ParseKey(aesKeyBase64);
        var encrypted = await DownloadRawAsync(encryptedQueryParam, baseUrl, cancellationToken).ConfigureAwait(false);
        return AesEcb.Decrypt(encrypted, key);
    }

    public async Task<byte[]> DownloadRawAsync(string encryptedQueryParam, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        var url = BuildDownloadUrl(encryptedQueryParam, baseUrl);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var data = await SendWithTimeoutAsync(request, DefaultTimeout, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("CDN download completed: {Bytes} bytes", data.Length);
        return data;
    }

    public async Task<string> UploadAsync(ReadOnlyMemory<byte> buffer, string uploadParam, string fileKey, byte[] aesKey, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
        {
            throw new ArgumentException("Buffer must not be empty", nameof(buffer));
        }
        if (aesKey.Length == 0)
        {
            throw new ArgumentException("AES key must not be empty", nameof(aesKey));
        }

        var url = BuildUploadUrl(uploadParam, fileKey, baseUrl);
        var ciphertext = AesEcb.Encrypt(buffer.Span, aesKey);
        Exception? lastError = null;
        for (var attempt = 1; attempt <= UploadMaxRetries; attempt++)
        {
            using var content = new ByteArrayContent(ciphertext);
            content.Headers.ContentType = new("application/octet-stream");
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

            try
            {
                using var response = await SendAsync(request, DefaultTimeout, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                    {
                        throw new HttpRequestException($"CDN upload client error {(int)response.StatusCode}", null, response.StatusCode);
                    }
                    throw new HttpRequestException($"CDN upload server error {(int)response.StatusCode}", null, response.StatusCode);
                }

                var downloadParam = response.Headers.TryGetValues("x-encrypted-param", out var values)
                    ? values.FirstOrDefault()
                    : null;

                if (string.IsNullOrWhiteSpace(downloadParam))
                {
                    throw new InvalidOperationException("CDN upload response is missing x-encrypted-param header");
                }

                _logger.LogDebug("CDN upload succeeded on attempt {Attempt}: {Bytes} bytes", attempt, buffer.Length);
                return downloadParam;
            }
            catch (Exception ex) when (attempt < UploadMaxRetries && !IsClientError(ex))
            {
                lastError = ex;
                _logger.LogWarning(ex, "CDN upload attempt {Attempt} of {MaxRetries} failed, retrying", attempt, UploadMaxRetries);
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        throw lastError ?? new InvalidOperationException("CDN upload failed");
    }

    private string BuildDownloadUrl(string encryptedQueryParam, string? baseUrl)
        => $"{(baseUrl ?? BaseUrl).TrimEnd('/')}/download?encrypted_query_param={Uri.EscapeDataString(encryptedQueryParam)}";

    private string BuildUploadUrl(string uploadParam, string fileKey, string? baseUrl)
    {
        var baseUri = (baseUrl ?? BaseUrl).TrimEnd('/');
        return $"{baseUri}/upload?encrypted_query_param={Uri.EscapeDataString(uploadParam)}&filekey={Uri.EscapeDataString(fileKey)}";
    }

    private async Task<byte[]> SendWithTimeoutAsync(HttpRequestMessage request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(request, timeout, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"CDN download failed {(int)response.StatusCode}", null, response.StatusCode);
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"CDN request timed out after {timeout.TotalSeconds:N0}s", ex);
        }
    }

    private static bool IsClientError(Exception exception)
        => exception is HttpRequestException { StatusCode: { } status }
        && (int)status >= 400
        && (int)status < 500;

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }
}
