namespace Weixin.Bot.Sdk.Credentials;

/// <summary>
/// An in-memory <see cref="IBotCredentialStore"/> suitable for containerised deployments and testing scenarios
/// where file-system persistence is unavailable or undesirable.
/// </summary>
/// <remarks>
/// Credentials are held entirely in memory and are lost when the process exits.
/// Seed initial credentials by passing them to the constructor or by calling <see cref="SaveAsync"/>
/// before starting the bot (for example, from an environment variable or secrets manager).
/// </remarks>
public sealed class InMemoryBotCredentialStore : IBotCredentialStore
{
    private volatile Models.BotCredentials? _credentials;

    /// <summary>
    /// Initializes a new empty store. The bot will perform QR-code login on first start.
    /// </summary>
    public InMemoryBotCredentialStore() { }

    /// <summary>
    /// Initializes a store pre-seeded with existing credentials, skipping QR-code login.
    /// </summary>
    /// <param name="credentials">The credentials to pre-load.</param>
    public InMemoryBotCredentialStore(Models.BotCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        _credentials = credentials;
    }

    /// <inheritdoc />
    public ValueTask<Models.BotCredentials?> LoadAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_credentials);

    /// <inheritdoc />
    public ValueTask SaveAsync(Models.BotCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        _credentials = credentials;
        return ValueTask.CompletedTask;
    }
}
