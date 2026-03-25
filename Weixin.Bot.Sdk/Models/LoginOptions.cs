namespace Weixin.Bot.Sdk.Models;

/// <summary>
/// Configures the login flow for QR-code based authentication.
/// </summary>
public sealed class LoginOptions
{
    /// <summary>
    /// Gets the callback invoked when a QR code URL or payload is generated.
    /// </summary>
    public Func<string, ValueTask>? OnQrCode { get; set; }

    /// <summary>
    /// Gets the callback invoked when the QR code status changes.
    /// </summary>
    public Func<string, ValueTask>? OnStatusChanged { get; set; }

    /// <summary>
    /// Gets the bot type sent during QR-code generation.
    /// </summary>
    public string BotType { get; set; } = "3";

    /// <summary>
    /// Gets the overall login timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the number of times an expired QR code may be refreshed before the login fails.
    /// </summary>
    public int MaxQrRefresh { get; set; } = 3;
}

/// <summary>
/// Represents the outcome of a successful login.
/// </summary>
public sealed class LoginResult
{
    internal LoginResult(string botToken, string? botId, string? baseUrl, string? userId)
    {
        BotToken = botToken;
        BotId = botId;
        BaseUrl = baseUrl;
        UserId = userId;
    }

    /// <summary>
    /// Gets the authenticated bot token.
    /// </summary>
    public string BotToken { get; }

    /// <summary>
    /// Gets the bot identifier, when returned.
    /// </summary>
    public string? BotId { get; }

    /// <summary>
    /// Gets the resolved API base URL, when returned.
    /// </summary>
    public string? BaseUrl { get; }

    /// <summary>
    /// Gets the associated user identifier, when returned.
    /// </summary>
    public string? UserId { get; }
}
