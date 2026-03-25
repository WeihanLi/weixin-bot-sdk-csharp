namespace Weixin.Bot.Sdk.Models;

/// <summary>
/// Configures the login flow for QR-code based authentication.
/// </summary>
public sealed record LoginOptions
{
    /// <summary>
    /// Gets the callback invoked when a QR code URL or payload is generated.
    /// </summary>
    public Func<string, ValueTask>? OnQrCode { get; init; }

    /// <summary>
    /// Gets the callback invoked when the QR code status changes.
    /// </summary>
    public Func<string, ValueTask>? OnStatusChanged { get; init; }

    /// <summary>
    /// Gets the bot type sent during QR-code generation.
    /// </summary>
    public string BotType { get; init; } = "3";

    /// <summary>
    /// Gets the overall login timeout.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the number of times an expired QR code may be refreshed before the login fails.
    /// </summary>
    public int MaxQrRefresh { get; init; } = 3;
}

/// <summary>
/// Represents the outcome of a successful login.
/// </summary>
/// <param name="BotToken">The authenticated bot token.</param>
/// <param name="BotId">The bot identifier, when returned.</param>
/// <param name="BaseUrl">The resolved API base URL, when returned.</param>
/// <param name="UserId">The associated user identifier, when returned.</param>
public sealed record LoginResult(
    string BotToken,
    string? BotId,
    string? BaseUrl,
    string? UserId
);
