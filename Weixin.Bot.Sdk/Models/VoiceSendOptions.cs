namespace Weixin.Bot.Sdk.Models;

/// <summary>
/// Metadata used when sending a voice message.
/// </summary>
public sealed record VoiceSendOptions
{
    /// <summary>
    /// Gets the voice encoding format.
    /// </summary>
    public VoiceEncodeType EncodeType { get; init; } = VoiceEncodeType.Silk;

    /// <summary>
    /// Gets the audio sample rate in hertz.
    /// </summary>
    public int SampleRate { get; init; } = 24000;

    /// <summary>
    /// Gets the number of bits per audio sample.
    /// </summary>
    public int BitsPerSample { get; init; } = 16;

    /// <summary>
    /// Gets the playback duration reported to the platform.
    /// </summary>
    public int Playtime { get; init; }
}
