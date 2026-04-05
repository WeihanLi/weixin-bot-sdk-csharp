using Weixin.Bot.Sdk.Api;
using Weixin.Bot.Sdk.Models;

namespace Weixin.Bot.Sdk.Media;

internal static class UploadPreparation
{
    public static async Task<PreparedUpload> PrepareAsync(
        WeixinBotApi api,
        ReadOnlyMemory<byte> buffer,
        string toUserId,
        UploadMediaType mediaType,
        CdnClient cdnClient,
        CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
        {
            throw new ArgumentException("Buffer must contain at least one byte", nameof(buffer));
        }

        var rawSize = buffer.Length;
        var rawMd5 = Convert.ToHexStringLower(MD5.HashData(buffer.Span));
        var paddedSize = Crypto.AesEcb.GetPaddedSize(rawSize);
        var fileKeyBytes = RandomNumberGenerator.GetBytes(16);
        var fileKeyHex = Convert.ToHexStringLower(fileKeyBytes);
        var aesKeyBytes = RandomNumberGenerator.GetBytes(16);
        var aesKeyHex = Convert.ToHexStringLower(aesKeyBytes);

        var uploadInfo = await api.GetUploadUrlAsync(
            fileKeyHex,
            mediaType,
            toUserId,
            rawSize,
            rawMd5,
            paddedSize,
            aesKeyHex,
            cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(uploadInfo.UploadParam))
        {
            throw new InvalidOperationException("getuploadurl response did not include upload_param");
        }

        var downloadParam = await cdnClient.UploadAsync(buffer, uploadInfo.UploadParam!, fileKeyHex, aesKeyBytes, api.CdnUrl, cancellationToken).ConfigureAwait(false);

        return new PreparedUpload(fileKeyHex, downloadParam, aesKeyHex, rawSize, paddedSize);
    }
}

internal sealed record PreparedUpload(
    string FileKey,
    string DownloadEncryptedQueryParam,
    string AesKeyHex,
    int FileSize,
    int FileSizeCiphertext
);
