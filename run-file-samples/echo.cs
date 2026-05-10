#:project ../Weixin.Bot.Sdk/Weixin.Bot.Sdk.csproj
#:package Microsoft.Extensions.Logging.Console

using Microsoft.Extensions.Logging;
using Weixin.Bot.Sdk.Bot;
using Weixin.Bot.Sdk.Models;

var credentialsPath = Environment.GetEnvironmentVariable("WEIXIN_BOT_CREDENTIALS");
if (string.IsNullOrWhiteSpace(credentialsPath))
{
    credentialsPath = Path.Combine(AppContext.BaseDirectory, "weixin-bot.credentials.json");
}

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Debug);
});

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    shutdown.Cancel();
};

var echoHandler = new EchoMessageHandler();
var weixinBotOptions = new WeixinBotOptions
{
    CredentialsPath = credentialsPath,
    LoggerFactory = loggerFactory,
    OnCredentialsLoaded = (_, args) =>
    {
        Console.WriteLine($"Loaded credentials for bot {args.Credentials.BotId ?? "(unknown)"}.");
    },
    OnLoggedIn = (_, args) =>
    {
        Console.WriteLine($"Logged in as bot {args.Result.BotId ?? "(unknown)"}.");
    },
    OnStarted = (_, _) => Console.WriteLine("Polling started."),
    OnStopped = (_, _) => Console.WriteLine("Polling stopped."),
    OnSessionExpired = (_, code) => Console.WriteLine($"Session expired with errcode {code}."),
    OnError = (_, args) => Console.WriteLine($"SDK error: {args.Exception.Message}, {args.Exception}"),
};

await using var bot = new WeixinBot(echoHandler, weixinBotOptions);
echoHandler.Bot = bot;

await bot.StartAsync(new LoginOptions
{
    OnQrCode = qr =>
    {
        Console.WriteLine("No saved credentials found. Scan the QR URL below to log in.");
        Console.WriteLine();
        Console.WriteLine("QR code URL:");
        Console.WriteLine(qr);
        Console.WriteLine();
        return ValueTask.CompletedTask;
    },
    OnStatusChanged = status =>
    {
        Console.WriteLine($"QR status: {status}");
        return ValueTask.CompletedTask;
    },
}, shutdown.Token);

Console.WriteLine("Bot is running. Press Ctrl+C to stop.");

try
{
    await Task.Delay(Timeout.Infinite, shutdown.Token);
}
catch (OperationCanceledException)
{
}

await bot.StopAsync();

sealed class EchoMessageHandler : IWeixinMessageHandler
{
    public IWeixinBot? Bot { get; set; }

    public async Task HandleMessageAsync(WeixinMessage message, CancellationToken cancellationToken)
    {
        if (Bot is null) return;
        try
        {
            var displayText = string.IsNullOrWhiteSpace(message.TextWithQuote) ? "(non-text message)" : message.TextWithQuote;
            Console.WriteLine($"[{message.Timestamp:O}] {message.FromUserId}: {displayText}");

            await Bot.SendTypingAsync(message.FromUserId, message.ContextToken, cancellationToken).ConfigureAwait(false);

            var reply = message.ContentKind switch
            {
                MessageContentKind.Text => $"Echo: {message.Text}",
                MessageContentKind.Image => "Received an image.",
                MessageContentKind.Video => "Received a video.",
                MessageContentKind.File => "Received a file.",
                MessageContentKind.Voice => $"Received a voice message. Transcript: {message.Text}",
                _ => "Received a message.",
            };

            await Bot.ReplyAsync(message, reply, cancellationToken).ConfigureAwait(false);
            await Bot.CancelTypingAsync(message.FromUserId, message.ContextToken, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to handle message: {ex.Message}");
        }
    }
}
