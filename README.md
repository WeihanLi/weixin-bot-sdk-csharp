# Weixin Bot SDK for CSharp

Modern .NET SDK for the WeChat iLink Bot protocol.

This repository contains:

- `Weixin.Bot.Sdk`: the reusable client library
- `Weixin.Bot.Sdk.Sample`: a small console bot showing login, polling, and replies

## Features

- QR-code login flow for iLink bots
- Configurable credential persistence through file, API, database, or custom stores
- Long-polling for inbound user messages
- Strongly typed message models for text, image, video, file, and voice messages
- Send helpers for text, image, video, file, and voice content
- Reply helpers that reuse the message context token automatically
- Typing indicator support
- Media download helpers for image, video, file, and voice payloads
- Configurable API base URL, CDN URL, version header, and `HttpClient`

## Requirements

- .NET 10

## Development Workflow

Restore, build, and test with strict warning gates enabled by default:

```powershell
dotnet restore .\weixin-bot-sdk-csharp.slnx
dotnet build .\weixin-bot-sdk-csharp.slnx -c Release --no-restore
dotnet test .\Weixin.Bot.Sdk.Test\Weixin.Bot.Sdk.Test.csproj -c Release --no-build
```

Generate test result and coverage artifacts:

```powershell
dotnet test .\Weixin.Bot.Sdk.Test\Weixin.Bot.Sdk.Test.csproj -c Release --no-build --logger "trx;LogFileName=test-results.trx" --results-directory .\artifacts\TestResults --collect:"XPlat Code Coverage"
```

Pack the SDK:

```powershell
dotnet pack .\Weixin.Bot.Sdk\Weixin.Bot.Sdk.csproj -c Release --no-build -o .\artifacts\packages
```

## Project Structure

```text
.
|-- Weixin.Bot.Sdk
|-- Weixin.Bot.Sdk.Sample
`-- README.md
```

## Getting Started

Add the SDK project reference from this repository:

```xml
<ItemGroup>
  <ProjectReference Include="..\Weixin.Bot.Sdk\Weixin.Bot.Sdk.csproj" />
</ItemGroup>
```

If you publish the package to a feed, the package id is `Weixin.Bot.Sdk`.

## Quick Start

```csharp
using Weixin.Bot.Sdk.Bot;
using Weixin.Bot.Sdk.Models;

var credentialsPath = Path.Combine(AppContext.BaseDirectory, "weixin-bot.credentials.json");

await using var bot = new WeixinBot(new WeixinBotOptions
{
    CredentialsPath = credentialsPath,
});

bot.MessageReceived += (_, args) =>
{
    _ = Task.Run(() => bot.ReplyAsync(args.Message, $"Echo: {args.Message.Text}"));
};

if (!bot.IsLoggedIn)
{
    await bot.LoginAsync(new LoginOptions
    {
        OnQrCode = qr =>
        {
            Console.WriteLine("Scan this QR code URL:");
            Console.WriteLine(qr);
            return ValueTask.CompletedTask;
        },
        OnStatusChanged = status =>
        {
            Console.WriteLine($"QR status: {status}");
            return ValueTask.CompletedTask;
        },
    });
}

using var shutdown = new CancellationTokenSource();
bot.Start(shutdown.Token);

Console.WriteLine("Bot is running. Press Ctrl+C to stop.");
await Task.Delay(Timeout.Infinite, shutdown.Token);
```

## Credentials

`WeixinBot` can load and save bot credentials automatically through a credential store.
For local file storage, set `CredentialsPath` or configure `CredentialStore` with `FileBotCredentialStore`.

Saved credentials use this shape:

```json
{
  "botToken": "your-bot-token",
  "botId": "your-bot-id",
  "baseUrl": "https://ilinkai.weixin.qq.com",
  "userId": "your-user-id",
  "savedAt": "2026-03-25T00:00:00+00:00"
}
```

If a valid credential file exists, the bot can skip QR login and start polling immediately.

Custom stores can load credentials from an API, database, secrets manager, or any other backing store:

```csharp
using Weixin.Bot.Sdk.Credentials;
using Weixin.Bot.Sdk.Models;

public sealed class DatabaseCredentialStore : IBotCredentialStore
{
    public async ValueTask<BotCredentials?> LoadAsync(CancellationToken cancellationToken = default)
    {
        // Query your database or API and map the result to BotCredentials.
        return await LoadFromDatabaseAsync(cancellationToken);
    }

    public async ValueTask SaveAsync(BotCredentials credentials, CancellationToken cancellationToken = default)
    {
        // Upsert the credential record after a successful QR login.
        await SaveToDatabaseAsync(credentials, cancellationToken);
    }
}

await using var bot = new WeixinBot(new WeixinBotOptions
{
    CredentialStore = new DatabaseCredentialStore(),
});
```

## Handling Messages

Inbound messages are exposed as `WeixinMessage` instances through the `MessageReceived` event.

Supported content kinds:

- `MessageContentKind.Text`
- `MessageContentKind.Image`
- `MessageContentKind.Video`
- `MessageContentKind.File`
- `MessageContentKind.Voice`

Example:

```csharp
bot.MessageReceived += async (_, args) =>
{
    var message = args.Message;

    Console.WriteLine($"[{message.Timestamp:O}] {message.FromUserId}: {message.TextWithQuote}");

    switch (message.ContentKind)
    {
        case MessageContentKind.Text:
            await bot.ReplyAsync(message, $"Echo: {message.Text}");
            break;
        case MessageContentKind.Image:
            await bot.ReplyAsync(message, "Received an image.");
            break;
        case MessageContentKind.Voice:
            await bot.ReplyAsync(message, $"Transcript: {message.Text}");
            break;
        default:
            await bot.ReplyAsync(message, "Received your message.");
            break;
    }
};
```

## Sending Messages

### Text

```csharp
await bot.SendTextAsync(toUserId, "Hello from .NET", contextToken, cancellationToken);
```

### Reply

```csharp
await bot.ReplyAsync(message, "Thanks for your message", cancellationToken);
```

### Typing Indicator

```csharp
await bot.SendTypingAsync(toUserId, contextToken, cancellationToken);
await bot.CancelTypingAsync(toUserId, contextToken, cancellationToken);
```

### Media

```csharp
await bot.SendImageAsync(toUserId, imageBytes, "Optional caption", contextToken, cancellationToken);
await bot.SendVideoAsync(toUserId, videoBytes, "Optional caption", contextToken, cancellationToken);
await bot.SendFileAsync(toUserId, fileBytes, "report.pdf", "Optional caption", contextToken, cancellationToken);
await bot.SendVoiceAsync(toUserId, voiceBytes, new VoiceSendOptions
{
    EncodeType = VoiceEncodeType.Silk,
    SampleRate = 24000,
    BitsPerSample = 16,
    Playtime = 3,
}, contextToken, cancellationToken);
```

Notes:

- Outbound sends require a valid `contextToken`
- `ReplyAsync` uses `message.ContextToken`
- `SendTextAsync` and other direct send methods can also reuse a cached context token after a previous inbound message from the same user

## Downloading Media

```csharp
bot.MessageReceived += async (_, args) =>
{
    var message = args.Message;

    if (message.Image is not null)
    {
        var bytes = await bot.DownloadImageAsync(message.Image);
        await File.WriteAllBytesAsync("image.bin", bytes);
    }

    if (message.File is not null)
    {
        var bytes = await bot.DownloadFileAsync(message.File);
        await File.WriteAllBytesAsync("file.bin", bytes);
    }
};
```

## Events

`WeixinBot` exposes lifecycle and error events:

- `CredentialsLoaded`
- `LoggedIn`
- `Started`
- `Stopped`
- `MessageReceived`
- `SessionExpired`
- `Error`

## Configuration

`WeixinBotOptions` supports:

- `BaseUrl`
- `CdnUrl`
- `Token`
- `Version`
- `CredentialsPath`
- `CredentialStore`
- `HttpClient`

Defaults:

- API base URL: `https://ilinkai.weixin.qq.com`
- CDN base URL: `https://novac2c.cdn.weixin.qq.com/c2c`
- Version header: `1.0.0`

## Running the Sample

The sample project is a console echo bot.

Use a saved credential file:

```powershell
$env:WEIXIN_BOT_CREDENTIALS="C:\path\to\weixin-bot.credentials.json"
dotnet run --project .\Weixin.Bot.Sdk.Sample
```

Or run without credentials and scan the QR code URL printed to the console:

```powershell
dotnet run --project .\Weixin.Bot.Sdk.Sample
```

## Current Scope

This SDK currently focuses on the core iLink bot workflow:

- authenticate
- poll for updates
- parse inbound messages
- send replies and media
- download inbound media

If you need additional protocol coverage, extend the internal transport layer first and then expose higher-level helpers from `WeixinBot`.
