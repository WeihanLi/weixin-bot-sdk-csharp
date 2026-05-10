using Microsoft.Extensions.DependencyInjection;
using Weixin.Bot.Sdk.Bot;
using Weixin.Bot.Sdk.Extensions;
using Weixin.Bot.Sdk.Models;
using Xunit;

namespace Weixin.Bot.Sdk.Test;

public sealed class NamedBotTests
{
    private static Action<WeixinBotOptions> WithToken(string token) => options =>
    {
        options.Token = token;
        options.HttpClient = new HttpClient(new ScriptedHttpMessageHandler());
    };

    [Fact]
    public void AddWeixinBot_Named_RegistersFactory()
    {
        ServiceCollection services = new();
        services.AddWeixinBot<NoOpHandler>("bot-a", WithToken("ta"));

        ServiceProvider provider = services.BuildServiceProvider();

        IWeixinBotFactory factory = provider.GetRequiredService<IWeixinBotFactory>();
        Assert.NotNull(factory);
    }

    [Fact]
    public void AddWeixinBot_Named_DifferentNames_ResolveDifferentInstances()
    {
        ServiceCollection services = new();
        services.AddWeixinBot<NoOpHandler>("bot-a", WithToken("ta"));
        services.AddWeixinBot<NoOpHandler>("bot-b", WithToken("tb"));

        ServiceProvider provider = services.BuildServiceProvider();
        IWeixinBotFactory factory = provider.GetRequiredService<IWeixinBotFactory>();

        IWeixinBot botA = factory.GetBot("bot-a");
        IWeixinBot botB = factory.GetBot("bot-b");

        Assert.NotSame(botA, botB);
    }

    [Fact]
    public void AddWeixinBot_Named_SameName_ReturnsSameInstance()
    {
        ServiceCollection services = new();
        services.AddWeixinBot<NoOpHandler>("bot-a", WithToken("ta"));

        ServiceProvider provider = services.BuildServiceProvider();
        IWeixinBotFactory factory = provider.GetRequiredService<IWeixinBotFactory>();

        IWeixinBot first = factory.GetBot("bot-a");
        IWeixinBot second = factory.GetBot("bot-a");

        Assert.Same(first, second);
    }

    [Fact]
    public void AddWeixinBot_Named_UnknownName_Throws()
    {
        ServiceCollection services = new();
        services.AddWeixinBot<NoOpHandler>("bot-a", WithToken("ta"));

        ServiceProvider provider = services.BuildServiceProvider();
        IWeixinBotFactory factory = provider.GetRequiredService<IWeixinBotFactory>();

        Assert.Throws<InvalidOperationException>(() => factory.GetBot("does-not-exist"));
    }

    [Fact]
    public void AddWeixinBot_Named_TwoRegistrations_ShareOneFactory()
    {
        ServiceCollection services = new();
        services.AddWeixinBot<NoOpHandler>("bot-a", WithToken("ta"));
        services.AddWeixinBot<NoOpHandler>("bot-b", WithToken("tb"));

        ServiceProvider provider = services.BuildServiceProvider();

        // Factory is a singleton — both names come from the same factory
        IWeixinBotFactory f1 = provider.GetRequiredService<IWeixinBotFactory>();
        IWeixinBotFactory f2 = provider.GetRequiredService<IWeixinBotFactory>();
        Assert.Same(f1, f2);
    }

    private sealed class NoOpHandler : IWeixinMessageHandler
    {
        public Task HandleMessageAsync(WeixinMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
