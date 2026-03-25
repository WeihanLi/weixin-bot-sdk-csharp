namespace Weixin.Bot.Sdk.Api;

public sealed record WeixinBotApiOptions
{
    public string? BaseUrl { get; init; }
    public string? CdnUrl { get; init; }
    public string? Token { get; init; }
    public string Version { get; init; } = "1.0.0";
    public HttpClient? HttpClient { get; init; }
}
