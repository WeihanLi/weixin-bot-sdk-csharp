# Agent Instructions

## Project Overview

This repository contains a modern .NET SDK for the WeChat iLink Bot protocol.

- `Weixin.Bot.Sdk` is the reusable client library.
- `Weixin.Bot.Sdk.Sample` is a console echo bot that demonstrates login, polling, replies, and credential reuse.
- `Weixin.Bot.Sdk.Test` contains xUnit tests for message parsing, lifecycle behavior, polling, and send operations.
- `run-file-samples` contains small script-style samples used by the build process.

The solution file is `weixin-bot-sdk-csharp.slnx`. The SDK targets `net10.0`, and `global.json` pins the .NET SDK to `10.0.100` with `latestFeature` roll-forward.

## Build And Test Commands

Use the repository root as the working directory.

```sh
dotnet restore .\weixin-bot-sdk-csharp.slnx
dotnet build .\weixin-bot-sdk-csharp.slnx -c Release --no-restore
dotnet test .\Weixin.Bot.Sdk.Test\Weixin.Bot.Sdk.Test.csproj -c Release --no-build
```

Generate test results and coverage artifacts:

```sh
dotnet test .\Weixin.Bot.Sdk.Test\Weixin.Bot.Sdk.Test.csproj -c Release
```

Pack the SDK after a successful Release build:

```sh
dotnet pack .\Weixin.Bot.Sdk\Weixin.Bot.Sdk.csproj -c Release -o .\artifacts\packages
```

Run the sample bot:

```pwsh
$env:WEIXIN_BOT_CREDENTIALS="C:\path\to\weixin-bot.credentials.json"
dotnet run --project .\Weixin.Bot.Sdk.Sample
```

CI runs restore, Release tests, and a Release build of the sample project. Keep local validation aligned with `.github/workflows/build.yml`.

## Code Style Guidelines

- Follow `.editorconfig`; it is the source of truth for formatting, naming, and analyzer preferences.
- Nullable reference types and implicit usings are enabled through `Directory.Build.props`.
- Treat warnings as errors. Fix analyzer warnings instead of suppressing them unless there is a clear reason.
- Use file-scoped namespaces for new C# files.
- Prefer explicit types over `var`, matching the current `.editorconfig` preference.
- Use 4 spaces for C# indentation and 2 spaces for project/config files where `.editorconfig` applies.
- Keep public SDK APIs small, typed, and cancellation-token friendly.
- Preserve central package management in `Directory.Packages.props`; do not add package versions directly to individual project files.

## Testing Instructions

- Add or update xUnit tests in `Weixin.Bot.Sdk.Test` for behavior changes.
- Prefer deterministic unit tests. Avoid live WeChat/iLink network calls in tests.
- Mock or stub HTTP interactions instead of depending on real credentials, QR login, CDN downloads, or external API availability.
- Cover parsing changes with representative wire JSON and expected `WeixinMessage` model values.
- Cover send/polling changes by asserting request shape, endpoint behavior, cancellation, and error handling.
- Run `dotnet test .\Weixin.Bot.Sdk.Test\Weixin.Bot.Sdk.Test.csproj -c Release --no-build` before handing off changes that affect SDK behavior.

## Security Considerations

- Never commit bot tokens, bot IDs, user IDs, QR login artifacts, saved credential files, or downloaded private media.
- Treat files such as `weixin-bot.credentials.json` as secrets, even in samples and tests.
- Do not log credential values, authorization headers, context tokens, raw cookies, or full private payloads.
- Be careful with media download helpers: validate filenames and paths in samples or new APIs to avoid accidental overwrite or path traversal issues.
- Keep default API and CDN URLs configurable through options. Do not hard-code environment-specific secrets or endpoints into library code.
- For crypto-related changes, keep behavior covered by tests and avoid replacing established primitives with ad hoc implementations.

## Change Guidelines

- Keep changes scoped to the SDK, sample, tests, or build files required by the task.
- Update `README.md` when public usage, configuration, supported message types, or development commands change.
- Update tests together with behavior changes. If a change cannot be tested locally, document the gap in the handoff.
- Do not commit generated artifacts from `artifacts/`, test results, packages, credential files, or local IDE state.
- Use clear commit messages, preferably in the form `area: concise change summary`, for example `sdk: handle voice message payloads`.
- Pull requests should summarize the behavior change, list validation commands run, and call out any protocol, compatibility, or security impact.
