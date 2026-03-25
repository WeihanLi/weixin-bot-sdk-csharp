namespace Weixin.Bot.Sdk.Models;

/// <summary>
/// Represents the primary content type of a parsed inbound message.
/// </summary>
public enum MessageContentKind
{
    /// <summary>No supported content type was identified.</summary>
    Unknown,
    /// <summary>The message contains text content.</summary>
    Text,
    /// <summary>The message contains an image.</summary>
    Image,
    /// <summary>The message contains a video.</summary>
    Video,
    /// <summary>The message contains a file.</summary>
    File,
    /// <summary>The message contains a voice payload.</summary>
    Voice
}

/// <summary>
/// Common encrypted media metadata used for CDN downloads.
/// </summary>
/// <param name="EncryptQueryParam">The encrypted query parameter used to request the media from the CDN.</param>
/// <param name="AesKey">The Base64-encoded AES key, when provided.</param>
/// <param name="EncryptType">The remote encryption type indicator.</param>
public sealed record WeixinMedia(
    string EncryptQueryParam,
    string? AesKey,
    int? EncryptType
);

/// <summary>
/// Represents an inbound image attachment.
/// </summary>
/// <param name="Media">Common encrypted media metadata.</param>
/// <param name="MidSize">The mid-size image payload length reported by the platform.</param>
/// <param name="AesKey">An image-specific AES key when returned by the platform.</param>
public sealed record WeixinImage(
    WeixinMedia Media,
    string? MidSize,
    string? AesKey
);

/// <summary>
/// Represents an inbound voice attachment.
/// </summary>
/// <param name="Media">Common encrypted media metadata.</param>
/// <param name="EncodeType">The voice encoding format.</param>
/// <param name="SampleRate">The voice sample rate in hertz.</param>
/// <param name="BitsPerSample">The number of bits per audio sample.</param>
/// <param name="Playtime">The reported playback duration.</param>
/// <param name="Text">An optional speech-to-text transcription.</param>
public sealed record WeixinVoice(
    WeixinMedia Media,
    VoiceEncodeType EncodeType,
    int SampleRate,
    int BitsPerSample,
    int Playtime,
    string? Text
);

/// <summary>
/// Represents an inbound file attachment.
/// </summary>
/// <param name="Media">Common encrypted media metadata.</param>
/// <param name="FileName">The original file name, when available.</param>
/// <param name="Length">The reported file length.</param>
public sealed record WeixinFile(
    WeixinMedia Media,
    string? FileName,
    string? Length
);

/// <summary>
/// Represents an inbound video attachment.
/// </summary>
/// <param name="Media">Common encrypted media metadata.</param>
/// <param name="VideoSize">The reported video size.</param>
public sealed record WeixinVideo(
    WeixinMedia Media,
    string? VideoSize
);

/// <summary>
/// Represents a quoted message reference included with a message.
/// </summary>
/// <param name="Title">The quoted message title, when available.</param>
/// <param name="Text">A plain-text summary of the quoted content.</param>
public sealed record WeixinQuotedMessage(
    string? Title,
    string? Text
);

/// <summary>
/// Represents a parsed inbound WeChat iLink message.
/// </summary>
/// <param name="MessageId">The platform message identifier.</param>
/// <param name="FromUserId">The sender user identifier.</param>
/// <param name="ToUserId">The recipient user identifier.</param>
/// <param name="Timestamp">The message timestamp.</param>
/// <param name="ContextToken">The context token used for replies and typing indicators.</param>
/// <param name="Text">The primary message text.</param>
/// <param name="TextWithQuote">The text content including quote context when present.</param>
/// <param name="ContentKind">The detected content kind.</param>
/// <param name="Image">The parsed image payload, when present.</param>
/// <param name="Video">The parsed video payload, when present.</param>
/// <param name="File">The parsed file payload, when present.</param>
/// <param name="Voice">The parsed voice payload, when present.</param>
/// <param name="QuotedMessage">The parsed quoted message metadata, when present.</param>
public sealed record WeixinMessage(
    string MessageId,
    string FromUserId,
    string ToUserId,
    DateTimeOffset Timestamp,
    string? ContextToken,
    string Text,
    string TextWithQuote,
    MessageContentKind ContentKind,
    WeixinImage? Image,
    WeixinVideo? Video,
    WeixinFile? File,
    WeixinVoice? Voice,
    WeixinQuotedMessage? QuotedMessage
);
