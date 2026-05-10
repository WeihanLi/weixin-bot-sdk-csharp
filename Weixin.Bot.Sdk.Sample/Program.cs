using Weixin.Bot.Sdk.Bot;
using Weixin.Bot.Sdk.Models;

var credentialsPath = Environment.GetEnvironmentVariable("WEIXIN_BOT_CREDENTIALS");
if (string.IsNullOrWhiteSpace(credentialsPath))
{
    credentialsPath = Path.Combine(AppContext.BaseDirectory, "weixin-bot.credentials.json");
}

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    shutdown.Cancel();
};

await using var bot = new WeixinBot(new WeixinBotOptions
{
    CredentialsPath = credentialsPath,
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
    OnMessageReceived = async (sender, args) =>
    {
        await HandleMessageAsync((WeixinBot)sender!, args.Message, shutdown.Token);
    },
});

if (!bot.IsLoggedIn)
{
    Console.WriteLine("No saved credentials found. Scan the QR URL below to log in.");

    await bot.LoginAsync(new LoginOptions
    {
        OnQrCode = qr =>
        {
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
}

bot.Start(shutdown.Token);

Console.WriteLine("Bot is running. Press Ctrl+C to stop.");

try
{
    await Task.Delay(Timeout.Infinite, shutdown.Token);
}
catch (OperationCanceledException)
{
}

await bot.StopAsync();

static async Task HandleMessageAsync(WeixinBot bot, WeixinMessage message, CancellationToken cancellationToken)
{
    try
    {
        var displayText = string.IsNullOrWhiteSpace(message.TextWithQuote) ? "(non-text message)" : message.TextWithQuote;
        Console.WriteLine($"[{message.Timestamp:O}] {message.FromUserId}: {displayText}");

        await bot.SendTypingAsync(message.FromUserId, message.ContextToken, cancellationToken).ConfigureAwait(false);

        var reply = message.ContentKind switch
        {
            MessageContentKind.Text => $"Echo: {message.Text}",
            MessageContentKind.Image => "Received an image.",
            MessageContentKind.Video => "Received a video.",
            MessageContentKind.File => "Received a file.",
            MessageContentKind.Voice => $"Received a voice message. Transcript: {message.Text}",
            _ => "Received a message.",
        };

        await bot.ReplyAsync(message, reply, cancellationToken).ConfigureAwait(false);
        await bot.CancelTypingAsync(message.FromUserId, message.ContextToken, cancellationToken).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to handle message: {ex.Message}");
    }
}
