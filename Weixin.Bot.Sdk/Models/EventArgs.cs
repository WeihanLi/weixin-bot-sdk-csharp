using Weixin.Bot.Sdk.Models.Wire;

namespace Weixin.Bot.Sdk.Models;

public sealed class WeixinMessageEventArgs : EventArgs
{
    public WeixinMessageEventArgs(WeixinMessage message)
    {
        Message = message;
    }

    public WeixinMessage Message { get; }
    public MessagePayload Raw => Message.Raw;
}

public sealed class CredentialsEventArgs : EventArgs
{
    public CredentialsEventArgs(BotCredentials credentials)
    {
        Credentials = credentials;
    }

    public BotCredentials Credentials { get; }
}
