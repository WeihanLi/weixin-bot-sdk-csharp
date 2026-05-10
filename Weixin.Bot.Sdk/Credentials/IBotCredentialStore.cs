using Weixin.Bot.Sdk.Models;

namespace Weixin.Bot.Sdk.Credentials;

/// <summary>
/// Loads and persists reusable bot credentials for a <see cref="Bot.WeixinBot"/> instance.
/// </summary>
public interface IBotCredentialStore
{
    /// <summary>
    /// Loads saved bot credentials, if any.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the load operation.</param>
    /// <returns>The saved credentials, or <see langword="null"/> when no credentials are available.</returns>
    ValueTask<BotCredentials?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists bot credentials after a successful login.
    /// </summary>
    /// <param name="credentials">The credentials to persist.</param>
    /// <param name="cancellationToken">A token that can cancel the save operation.</param>
    /// <returns>A task that completes when the credentials have been saved.</returns>
    ValueTask SaveAsync(BotCredentials credentials, CancellationToken cancellationToken = default);
}
