using Weixin.Bot.Sdk.Models.Wire;

namespace Weixin.Bot.Sdk.Models;

public enum MessageContentKind
{
    Unknown,
    Text,
    Image,
    Video,
    File,
    Voice
}

public sealed record WeixinQuotedMessage(string? Title, MessageItemPayload? Item, string? Text);

public sealed record WeixinMessage(
    string MessageId,
    string FromUserId,
    string ToUserId,
    DateTimeOffset Timestamp,
    string? ContextToken,
    string Text,
    string TextWithQuote,
    MessageContentKind ContentKind,
    ImageItemPayload? Image,
    VideoItemPayload? Video,
    FileItemPayload? File,
    VoiceItemPayload? Voice,
    WeixinQuotedMessage? QuotedMessage,
    MessagePayload Raw
);
