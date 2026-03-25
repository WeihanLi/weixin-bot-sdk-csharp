namespace Weixin.Bot.Sdk.Models;

/// <summary>
/// Event data for a parsed inbound message.
/// </summary>
public sealed class WeixinMessageEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WeixinMessageEventArgs"/> class.
    /// </summary>
    /// <param name="message">The parsed inbound message.</param>
    public WeixinMessageEventArgs(WeixinMessage message)
    {
        Message = message;
    }

    /// <summary>
    /// Gets the parsed inbound message.
    /// </summary>
    public WeixinMessage Message { get; }
}

/// <summary>
/// Event data for bot lifecycle transitions.
/// </summary>
public sealed class WeixinBotStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WeixinBotStateChangedEventArgs"/> class.
    /// </summary>
    /// <param name="occurredAt">The time the transition occurred.</param>
    public WeixinBotStateChangedEventArgs(DateTimeOffset occurredAt)
    {
        OccurredAt = occurredAt;
    }

    /// <summary>
    /// Gets the time the transition occurred.
    /// </summary>
    public DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Event data for a successful login.
/// </summary>
public sealed class LoginSucceededEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LoginSucceededEventArgs"/> class.
    /// </summary>
    /// <param name="result">The login result.</param>
    public LoginSucceededEventArgs(LoginResult result)
    {
        Result = result;
    }

    /// <summary>
    /// Gets the login result.
    /// </summary>
    public LoginResult Result { get; }
}

/// <summary>
/// Event data for credentials loaded from persistent storage.
/// </summary>
public sealed class CredentialsEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialsEventArgs"/> class.
    /// </summary>
    /// <param name="credentials">The loaded credentials.</param>
    public CredentialsEventArgs(BotCredentials credentials)
    {
        Credentials = credentials;
    }

    /// <summary>
    /// Gets the loaded credentials.
    /// </summary>
    public BotCredentials Credentials { get; }
}

/// <summary>
/// Event data for an expired or invalid remote session.
/// </summary>
public sealed class SessionExpiredEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionExpiredEventArgs"/> class.
    /// </summary>
    /// <param name="errorCode">The platform error code that ended the session.</param>
    public SessionExpiredEventArgs(int errorCode)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the platform error code that ended the session.
    /// </summary>
    public int ErrorCode { get; }
}

/// <summary>
/// Event data for SDK background processing failures.
/// </summary>
public sealed class WeixinBotErrorEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WeixinBotErrorEventArgs"/> class.
    /// </summary>
    /// <param name="exception">The exception raised by the SDK.</param>
    public WeixinBotErrorEventArgs(Exception exception)
    {
        Exception = exception;
    }

    /// <summary>
    /// Gets the exception raised by the SDK.
    /// </summary>
    public Exception Exception { get; }
}
