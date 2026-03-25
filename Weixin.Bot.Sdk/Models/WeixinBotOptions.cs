using Weixin.Bot.Sdk.Api;

namespace Weixin.Bot.Sdk.Models;

public sealed record WeixinBotOptions
{
    public string? BaseUrl { get; init; }
    public string? CdnUrl { get; init; }
    public string? Token { get; init; }
    public string Version { get; init; } = "1.0.0";
    public string? CredentialsPath { get; init; }
    public HttpClient? HttpClient { get; init; }
}
