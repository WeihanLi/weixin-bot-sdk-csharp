namespace Weixin.Bot.Sdk.Models;

public enum MessageType
{
    None = 0,
    User = 1,
    Bot = 2
}

public enum MessageItemType
{
    None = 0,
    Text = 1,
    Image = 2,
    Voice = 3,
    File = 4,
    Video = 5
}

public enum MessageState
{
    New = 0,
    Generating = 1,
    Finish = 2
}

public enum UploadMediaType
{
    Image = 1,
    Video = 2,
    File = 3,
    Voice = 4
}

public enum TypingStatus
{
    Typing = 1,
    Cancel = 2
}

/// <summary>Voice encode_type constants.</summary>
public enum VoiceEncodeType
{
    Pcm = 1,
    Adpcm = 2,
    Feature = 3,
    Speex = 4,
    Amr = 5,
    Silk = 6,
    Mp3 = 7,
    OggSpeex = 8
}
