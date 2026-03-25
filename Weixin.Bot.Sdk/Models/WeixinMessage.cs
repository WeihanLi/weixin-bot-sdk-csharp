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

internal sealed class WeixinMedia
{
    internal WeixinMedia(string encryptQueryParam, string? aesKey, int? encryptType)
    {
        EncryptQueryParam = encryptQueryParam;
        AesKey = aesKey;
        EncryptType = encryptType;
    }

    internal string EncryptQueryParam { get; }

    internal string? AesKey { get; }

    internal int? EncryptType { get; }
}

/// <summary>
/// Represents an inbound image attachment.
/// </summary>
public sealed class WeixinImage
{
    internal WeixinImage(WeixinMedia media, string? midSize, string? aesKey)
    {
        Media = media;
        MidSize = midSize;
        AesKey = aesKey;
    }

    internal WeixinMedia Media { get; }

    /// <summary>
    /// Gets the mid-size image payload length reported by the platform.
    /// </summary>
    public string? MidSize { get; }

    internal string? AesKey { get; }
}

/// <summary>
/// Represents an inbound voice attachment.
/// </summary>
public sealed class WeixinVoice
{
    internal WeixinVoice(WeixinMedia media, VoiceEncodeType encodeType, int sampleRate, int bitsPerSample, int playtime, string? text)
    {
        Media = media;
        EncodeType = encodeType;
        SampleRate = sampleRate;
        BitsPerSample = bitsPerSample;
        Playtime = playtime;
        Text = text;
    }

    internal WeixinMedia Media { get; }

    /// <summary>
    /// Gets the voice encoding format.
    /// </summary>
    public VoiceEncodeType EncodeType { get; }

    /// <summary>
    /// Gets the voice sample rate in hertz.
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    /// Gets the number of bits per audio sample.
    /// </summary>
    public int BitsPerSample { get; }

    /// <summary>
    /// Gets the reported playback duration.
    /// </summary>
    public int Playtime { get; }

    /// <summary>
    /// Gets an optional speech-to-text transcription.
    /// </summary>
    public string? Text { get; }
}

/// <summary>
/// Represents an inbound file attachment.
/// </summary>
public sealed class WeixinFile
{
    internal WeixinFile(WeixinMedia media, string? fileName, string? length)
    {
        Media = media;
        FileName = fileName;
        Length = length;
    }

    internal WeixinMedia Media { get; }

    /// <summary>
    /// Gets the original file name, when available.
    /// </summary>
    public string? FileName { get; }

    /// <summary>
    /// Gets the reported file length.
    /// </summary>
    public string? Length { get; }
}

/// <summary>
/// Represents an inbound video attachment.
/// </summary>
public sealed class WeixinVideo
{
    internal WeixinVideo(WeixinMedia media, string? videoSize)
    {
        Media = media;
        VideoSize = videoSize;
    }

    internal WeixinMedia Media { get; }

    /// <summary>
    /// Gets the reported video size.
    /// </summary>
    public string? VideoSize { get; }
}

/// <summary>
/// Represents a quoted message reference included with a message.
/// </summary>
public sealed class WeixinQuotedMessage
{
    internal WeixinQuotedMessage(string? title, string? text)
    {
        Title = title;
        Text = text;
    }

    /// <summary>
    /// Gets the quoted message title, when available.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// Gets a plain-text summary of the quoted content.
    /// </summary>
    public string? Text { get; }
}

/// <summary>
/// Represents a parsed inbound WeChat iLink message.
/// </summary>
public sealed class WeixinMessage
{
    internal WeixinMessage(
        string messageId,
        string fromUserId,
        string toUserId,
        DateTimeOffset timestamp,
        string? contextToken,
        string text,
        string textWithQuote,
        MessageContentKind contentKind,
        WeixinImage? image,
        WeixinVideo? video,
        WeixinFile? file,
        WeixinVoice? voice,
        WeixinQuotedMessage? quotedMessage)
    {
        MessageId = messageId;
        FromUserId = fromUserId;
        ToUserId = toUserId;
        Timestamp = timestamp;
        ContextToken = contextToken;
        Text = text;
        TextWithQuote = textWithQuote;
        ContentKind = contentKind;
        Image = image;
        Video = video;
        File = file;
        Voice = voice;
        QuotedMessage = quotedMessage;
    }

    /// <summary>
    /// Gets the platform message identifier.
    /// </summary>
    public string MessageId { get; }

    /// <summary>
    /// Gets the sender user identifier.
    /// </summary>
    public string FromUserId { get; }

    /// <summary>
    /// Gets the recipient user identifier.
    /// </summary>
    public string ToUserId { get; }

    /// <summary>
    /// Gets the message timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the context token used for replies and typing indicators.
    /// </summary>
    public string? ContextToken { get; }

    /// <summary>
    /// Gets the primary message text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the text content including quote context when present.
    /// </summary>
    public string TextWithQuote { get; }

    /// <summary>
    /// Gets the detected content kind.
    /// </summary>
    public MessageContentKind ContentKind { get; }

    /// <summary>
    /// Gets the parsed image payload, when present.
    /// </summary>
    public WeixinImage? Image { get; }

    /// <summary>
    /// Gets the parsed video payload, when present.
    /// </summary>
    public WeixinVideo? Video { get; }

    /// <summary>
    /// Gets the parsed file payload, when present.
    /// </summary>
    public WeixinFile? File { get; }

    /// <summary>
    /// Gets the parsed voice payload, when present.
    /// </summary>
    public WeixinVoice? Voice { get; }

    /// <summary>
    /// Gets the parsed quoted message metadata, when present.
    /// </summary>
    public WeixinQuotedMessage? QuotedMessage { get; }
}
