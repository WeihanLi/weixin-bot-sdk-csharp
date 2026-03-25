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
