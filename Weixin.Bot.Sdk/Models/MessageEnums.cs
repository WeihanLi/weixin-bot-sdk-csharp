namespace Weixin.Bot.Sdk.Models;

/// <summary>
/// Identifies whether a message originated from a user or the bot.
/// </summary>
public enum MessageType
{
    /// <summary>No message type was specified.</summary>
    None = 0,
    /// <summary>The message was sent by a user.</summary>
    User = 1,
    /// <summary>The message was sent by the bot.</summary>
    Bot = 2
}

/// <summary>
/// Identifies the content item type inside a message payload.
/// </summary>
public enum MessageItemType
{
    /// <summary>No item type was specified.</summary>
    None = 0,
    /// <summary>A text item.</summary>
    Text = 1,
    /// <summary>An image item.</summary>
    Image = 2,
    /// <summary>A voice item.</summary>
    Voice = 3,
    /// <summary>A file item.</summary>
    File = 4,
    /// <summary>A video item.</summary>
    Video = 5
}

/// <summary>
/// Represents the generation state of a bot message.
/// </summary>
public enum MessageState
{
    /// <summary>The message has not started generating.</summary>
    New = 0,
    /// <summary>The message is still being generated.</summary>
    Generating = 1,
    /// <summary>The message generation is complete.</summary>
    Finish = 2
}

/// <summary>
/// Identifies the upload media type used by the iLink API.
/// </summary>
public enum UploadMediaType
{
    /// <summary>Image content.</summary>
    Image = 1,
    /// <summary>Video content.</summary>
    Video = 2,
    /// <summary>File content.</summary>
    File = 3,
    /// <summary>Voice content.</summary>
    Voice = 4
}

/// <summary>
/// Represents typing indicator state transitions.
/// </summary>
public enum TypingStatus
{
    /// <summary>Show the typing indicator.</summary>
    Typing = 1,
    /// <summary>Clear the typing indicator.</summary>
    Cancel = 2
}

/// <summary>
/// Voice encoding formats accepted by the platform.
/// </summary>
public enum VoiceEncodeType
{
    /// <summary>PCM audio.</summary>
    Pcm = 1,
    /// <summary>ADPCM audio.</summary>
    Adpcm = 2,
    /// <summary>Feature-encoded audio.</summary>
    Feature = 3,
    /// <summary>Speex audio.</summary>
    Speex = 4,
    /// <summary>AMR audio.</summary>
    Amr = 5,
    /// <summary>Silk audio.</summary>
    Silk = 6,
    /// <summary>MP3 audio.</summary>
    Mp3 = 7,
    /// <summary>Ogg Speex audio.</summary>
    OggSpeex = 8
}
