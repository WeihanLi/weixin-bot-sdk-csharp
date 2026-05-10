using System.Diagnostics.CodeAnalysis;
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

        services.AddSingleton<IWeixinBot>(provider =>
        {
            WeixinBotOptions options = new();
            configure?.Invoke(options);
            options.LoggerFactory ??= provider.GetService<ILoggerFactory>();
            options.CredentialStore ??= provider.GetService<Credentials.IBotCredentialStore>();
            var handler = provider.GetRequiredService<IWeixinMessageHandler>();
            return new WeixinBot(handler, options);
        });

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="THandler"/> as <see cref="IWeixinMessageHandler"/> and a <see cref="WeixinBot"/> singleton.
    /// </summary>
    /// <typeparam name="THandler">The message handler type to register.</typeparam>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">An optional delegate to configure <see cref="WeixinBotOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddWeixinBot<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services,
        Action<WeixinBotOptions>? configure = null)
        where THandler : class, IWeixinMessageHandler
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IWeixinMessageHandler, THandler>();
        return services.AddWeixinBot(configure);
    }
}
