namespace Weixin.Bot.Sdk.Models;

public sealed record LoginOptions
{
    public Func<string, ValueTask>? OnQrCode { get; init; }
    public Func<string, ValueTask>? OnStatusChanged { get; init; }
    public string BotType { get; init; } = "3";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
    public int MaxQrRefresh { get; init; } = 3;
}

public sealed record LoginResult(
    string BotToken,
    string? BotId,
    string? BaseUrl,
    string? UserId
);
