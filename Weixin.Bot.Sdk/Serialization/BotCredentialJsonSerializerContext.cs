using Weixin.Bot.Sdk.Models;

namespace Weixin.Bot.Sdk.Serialization;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BotCredentials))]
internal sealed partial class BotCredentialJsonSerializerContext : JsonSerializerContext;
