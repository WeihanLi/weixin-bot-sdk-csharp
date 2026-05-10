using Microsoft.Extensions.DependencyInjection;

namespace Weixin.Bot.Sdk.Bot;

internal sealed class WeixinBotFactory(IServiceProvider provider) : IWeixinBotFactory
{
    public IWeixinBot GetBot(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return provider.GetRequiredKeyedService<IWeixinBot>(name);
    }
}
