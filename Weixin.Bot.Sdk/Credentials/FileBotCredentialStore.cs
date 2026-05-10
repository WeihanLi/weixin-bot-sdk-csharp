using Weixin.Bot.Sdk.Models;
using Weixin.Bot.Sdk.Serialization;

namespace Weixin.Bot.Sdk.Credentials;

/// <summary>
/// Loads and persists bot credentials as JSON in a local file.
/// </summary>
public sealed class FileBotCredentialStore : IBotCredentialStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileBotCredentialStore"/> class.
    /// </summary>
    /// <param name="path">The JSON file path used for credential persistence.</param>
    public FileBotCredentialStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Credential file path is required.", nameof(path));
        }

        Path = path;
    }

    /// <summary>
    /// Gets the JSON file path used for credential persistence.
    /// </summary>
    public string Path { get; }

    /// <inheritdoc />
    public async ValueTask<BotCredentials?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(Path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, BotCredentialJsonSerializerContext.Default.BotCredentials);
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(BotCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        string? directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        BotCredentials payload = new()
        {
            BotToken = credentials.BotToken,
            BotId = credentials.BotId,
            BaseUrl = credentials.BaseUrl,
            UserId = credentials.UserId,
            SavedAt = credentials.SavedAt ?? DateTimeOffset.UtcNow,
        };
        string json = JsonSerializer.Serialize(payload, BotCredentialJsonSerializerContext.Default.BotCredentials);
        await File.WriteAllTextAsync(Path, json, cancellationToken).ConfigureAwait(false);
    }
}
