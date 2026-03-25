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
});

bot.CredentialsLoaded += (_, args) =>
{
    Console.WriteLine($"Loaded credentials for bot {args.Credentials.BotId ?? "(unknown)"}.");
};

bot.LoggedIn += (_, result) =>
{
    Console.WriteLine($"Logged in as bot {result.BotId ?? "(unknown)"}.");
};

bot.Started += (_, _) => Console.WriteLine("Polling started.");
bot.Stopped += (_, _) => Console.WriteLine("Polling stopped.");
bot.SessionExpired += (_, code) => Console.WriteLine($"Session expired with errcode {code}.");
bot.Error += (_, ex) => Console.WriteLine($"SDK error: {ex.Message}");

bot.MessageReceived += (_, args) =>
{
    _ = Task.Run(() => HandleMessageAsync(bot, args.Message, shutdown.Token), shutdown.Token);
};

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
