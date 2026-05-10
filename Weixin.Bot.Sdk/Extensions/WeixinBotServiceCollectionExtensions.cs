using Microsoft.Extensions.DependencyInjection;
using Weixin.Bot.Sdk.Bot;
using Weixin.Bot.Sdk.Models;

namespace Weixin.Bot.Sdk.Extensions;

/// <summary>
/// Extension methods for registering Weixin Bot SDK services with an <see cref="IServiceCollection"/>.
/// </summary>
public static class WeixinBotServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="WeixinBot"/> singleton with the service collection.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">An optional delegate to configure <see cref="WeixinBotOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddWeixinBot(
        this IServiceCollection services,
        Action<WeixinBotOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(provider =>
        {
            WeixinBotOptions options = new();
            configure?.Invoke(options);
            options.LoggerFactory ??= provider.GetService<ILoggerFactory>();
            options.MessageHandler ??= provider.GetService<IWeixinMessageHandler>();
            return new WeixinBot(options);
        });
        services.AddSingleton<IWeixinBot>(provider => provider.GetRequiredService<WeixinBot>());

        return services;
    }
}
