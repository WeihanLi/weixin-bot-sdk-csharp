namespace Weixin.Bot.Sdk.Crypto;

/// <summary>Utility helpers for AES-128-ECB operations used by the WeChat CDN.</summary>
internal static class AesEcb
{
    private const int BlockSize = 16;

    public static byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> key)
    {
        using var aes = CreateCipher(key);
        using var encryptor = aes.CreateEncryptor();
        return PerformCryptography(encryptor, plaintext);
    }

    public static byte[] Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> key)
    {
        using var aes = CreateCipher(key);
        using var decryptor = aes.CreateDecryptor();
        return PerformCryptography(decryptor, ciphertext);
    }

    public static int GetPaddedSize(int plaintextSize)
    {
        if (plaintextSize < 0) throw new ArgumentOutOfRangeException(nameof(plaintextSize));
        var total = plaintextSize + 1; // match official client behavior
        var blocks = (total + BlockSize - 1) / BlockSize;
        return blocks * BlockSize;
    }

    public static byte[] ParseKey(string aesKeyBase64)
    {
        if (string.IsNullOrWhiteSpace(aesKeyBase64))
        {
            throw new ArgumentException("AES key must not be empty", nameof(aesKeyBase64));
        }

        var decoded = Convert.FromBase64String(aesKeyBase64);
        if (decoded.Length == BlockSize)
        {
            return decoded;
        }

        if (decoded.Length == BlockSize * 2)
        {
            var ascii = Encoding.ASCII.GetString(decoded);
            if (ascii.All(c => Uri.IsHexDigit(c)))
            {
                return Convert.FromHexString(ascii);
            }
        }

        throw new ArgumentException("Invalid aes_key payload: expected 16-byte raw key or 32-char hex", nameof(aesKeyBase64));
    }

    private static bool IsHex(string value)
    {
        foreach (var ch in value)
        {
            if (!Uri.IsHexDigit(ch))
            {
                return false;
            }
        }
        return value.Length > 0;
    }

    private static Aes CreateCipher(ReadOnlySpan<byte> key)
    {
        if (key.Length != BlockSize)
        {
            throw new ArgumentException($"Key must be {BlockSize} bytes", nameof(key));
        }

        var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key.ToArray();
        return aes;
    }

    private static byte[] PerformCryptography(ICryptoTransform transform, ReadOnlySpan<byte> data)
    {
        var input = data.ToArray();
        return transform.TransformFinalBlock(input, 0, input.Length);
    }
}
