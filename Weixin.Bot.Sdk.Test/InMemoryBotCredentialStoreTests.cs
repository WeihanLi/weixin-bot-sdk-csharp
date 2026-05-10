using Weixin.Bot.Sdk.Credentials;
using Weixin.Bot.Sdk.Models;
using Xunit;

namespace Weixin.Bot.Sdk.Test;

public sealed class InMemoryBotCredentialStoreTests
{
    [Fact]
    public async Task LoadAsync_ReturnsNull_WhenEmpty()
    {
        InMemoryBotCredentialStore store = new();
        BotCredentials? result = await store.LoadAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_ReturnsSeededCredentials_WhenPreloaded()
    {
        BotCredentials creds = new() { BotToken = "t", BotId = "b", UserId = "u" };
        InMemoryBotCredentialStore store = new(creds);
        BotCredentials? result = await store.LoadAsync();
        Assert.Same(creds, result);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_ReturnsStoredCredentials()
    {
        InMemoryBotCredentialStore store = new();
        BotCredentials creds = new() { BotToken = "saved-token", BotId = "b", UserId = "u" };

        await store.SaveAsync(creds);
        BotCredentials? result = await store.LoadAsync();

        Assert.Same(creds, result);
    }

    [Fact]
    public async Task SaveAsync_Overwrites_PreviousCredentials()
    {
        BotCredentials first = new() { BotToken = "first" };
        BotCredentials second = new() { BotToken = "second" };
        InMemoryBotCredentialStore store = new(first);

        await store.SaveAsync(second);
        BotCredentials? result = await store.LoadAsync();

        Assert.Same(second, result);
    }

    [Fact]
    public async Task SaveAsync_ThrowsArgumentNullException_WhenCredentialsIsNull()
    {
        InMemoryBotCredentialStore store = new();
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.SaveAsync(null!).AsTask());
    }
}
