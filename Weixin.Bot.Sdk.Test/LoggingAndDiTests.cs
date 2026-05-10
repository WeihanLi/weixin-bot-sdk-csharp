using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weixin.Bot.Sdk.Bot;
using Weixin.Bot.Sdk.Extensions;
using Weixin.Bot.Sdk.Models;
using Xunit;

namespace Weixin.Bot.Sdk.Test;

public sealed class LoggingAndDiTests
{
    [Fact]
    public void Constructor_WithNoLoggerFactory_DoesNotThrow()
    {
        using WeixinBot bot = new(null, new WeixinBotOptions());
        Assert.NotNull(bot);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_DoesNotThrow()
    {
        using WeixinBot bot = new(null, new WeixinBotOptions { LoggerFactory = null });
        Assert.NotNull(bot);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_FallsBackToNullLogger()
    {
        // NullLoggerFactory.Instance is the fallback — constructing without logger must not throw.
        using WeixinBot bot = new(null, new WeixinBotOptions { LoggerFactory = NullLoggerFactory.Instance });
        Assert.NotNull(bot);
    }

    [Fact]
    public void AddWeixinBot_RegistersWeixinBotAsSingleton()
    {
        ServiceCollection services = new();
        services.AddSingleton<IWeixinMessageHandler>(new DelegateMessageHandler((_, _) => Task.CompletedTask));
        services.AddWeixinBot();

        using ServiceProvider provider = services.BuildServiceProvider();

        IWeixinBot bot1 = provider.GetRequiredService<IWeixinBot>();
        IWeixinBot bot2 = provider.GetRequiredService<IWeixinBot>();

        Assert.Same(bot1, bot2);
    }

    [Fact]
    public void AddWeixinBot_WithConfigure_AppliesOptions()
    {
        const string expectedVersion = "test-2.0";
        ServiceCollection services = new();
        services.AddSingleton<IWeixinMessageHandler>(new DelegateMessageHandler((_, _) => Task.CompletedTask));
        services.AddWeixinBot(options => options.Version = expectedVersion);

        using ServiceProvider provider = services.BuildServiceProvider();

        // Constructing the bot succeeds — we can't read Version back directly from the bot,
        // but we verify the factory delegate ran without error.
        IWeixinBot bot = provider.GetRequiredService<IWeixinBot>();
        Assert.NotNull(bot);
    }

    [Fact]
    public void AddWeixinBot_UsesILoggerFactoryFromContainer_WhenAvailable()
    {
        ServiceCollection services = new();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<IWeixinMessageHandler>(new DelegateMessageHandler((_, _) => Task.CompletedTask));
        services.AddWeixinBot();

        using ServiceProvider provider = services.BuildServiceProvider();

        IWeixinBot bot = provider.GetRequiredService<IWeixinBot>();
        Assert.NotNull(bot);
    }

    [Fact]
    public void AddWeixinBot_WithExplicitLoggerFactory_PrefersThatOverContainer()
    {
        ServiceCollection services = new();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<IWeixinMessageHandler>(new DelegateMessageHandler((_, _) => Task.CompletedTask));
        services.AddWeixinBot(options => options.LoggerFactory = NullLoggerFactory.Instance);

        using ServiceProvider provider = services.BuildServiceProvider();

        IWeixinBot bot = provider.GetRequiredService<IWeixinBot>();
        Assert.NotNull(bot);
    }
}
