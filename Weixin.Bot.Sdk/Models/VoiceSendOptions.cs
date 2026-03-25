namespace Weixin.Bot.Sdk.Models;

public sealed record VoiceSendOptions
{
    public VoiceEncodeType EncodeType { get; init; } = VoiceEncodeType.Silk;
    public int SampleRate { get; init; } = 24000;
    public int BitsPerSample { get; init; } = 16;
    public int Playtime { get; init; }
}
