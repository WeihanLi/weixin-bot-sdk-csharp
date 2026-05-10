using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Weixin.Bot.Sdk.Bot;
using Weixin.Bot.Sdk.Credentials;
using Weixin.Bot.Sdk.Models;

namespace Weixin.Bot.Sdk.Extensions;

/// <summary>
/// Extension methods for registering Weixin Bot SDK services with an <see cref="IServiceCollection"/>.
/// </summary>
public static class WeixinBotServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="WeixinBot"/> singleton, resolving <see cref="IWeixinMessageHandler"/> from the container.
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
            options.CredentialStore ??= provider.GetService<IBotCredentialStore>();
            IWeixinMessageHandler handler = provider.GetRequiredService<IWeixinMessageHandler>();
            return new WeixinBot(handler, options);
        });

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="THandler"/> as <see cref="IWeixinMessageHandler"/> and a
    /// <see cref="WeixinBot"/> singleton, optionally wrapping the handler with a middleware pipeline.
    /// </summary>
    /// <typeparam name="THandler">The terminal message handler type.</typeparam>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">An optional delegate to configure <see cref="WeixinBotOptions"/>.</param>
    /// <param name="configurePipeline">
    /// An optional delegate to register middleware components that wrap <typeparamref name="THandler"/>.
    /// Middleware runs in registration order; the first registered component is the outermost wrapper.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddWeixinBot<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services,
        Action<WeixinBotOptions>? configure = null,
        Action<WeixinMessagePipelineBuilder>? configurePipeline = null)
        where THandler : class, IWeixinMessageHandler
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IWeixinMessageHandler, THandler>();

        WeixinMessagePipelineBuilder? pipelineBuilder = null;
        if (configurePipeline is not null)
        {
            pipelineBuilder = new WeixinMessagePipelineBuilder(services);
            configurePipeline(pipelineBuilder);
        }

        services.AddSingleton<IWeixinBot>(provider =>
        {
            WeixinBotOptions options = new();
            configure?.Invoke(options);
            options.LoggerFactory ??= provider.GetService<ILoggerFactory>();
            options.CredentialStore ??= provider.GetService<IBotCredentialStore>();
            IWeixinMessageHandler terminal = provider.GetRequiredService<IWeixinMessageHandler>();
            IWeixinMessageHandler handler = pipelineBuilder is not null
                ? pipelineBuilder.Build(provider, terminal)
                : terminal;
            return new WeixinBot(handler, options);
        });

        return services;
    }

    /// <summary>
    /// Registers a named <see cref="WeixinBot"/> as a keyed singleton, accessible via
    /// <see cref="IWeixinBotFactory"/>. Also registers <see cref="IWeixinBotFactory"/> if not already present.
    /// </summary>
    /// <typeparam name="THandler">The terminal message handler type for this named bot.</typeparam>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="name">The unique name used to retrieve the bot from <see cref="IWeixinBotFactory"/>.</param>
    /// <param name="configure">An optional delegate to configure <see cref="WeixinBotOptions"/>.</param>
    /// <param name="configurePipeline">
    /// An optional delegate to register middleware components that wrap <typeparamref name="THandler"/>
    /// for this named bot. Middleware runs in registration order.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddWeixinBot<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services,
        string name,
        Action<WeixinBotOptions>? configure = null,
        Action<WeixinMessagePipelineBuilder>? configurePipeline = null)
        where THandler : class, IWeixinMessageHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        services.TryAddSingleton<IWeixinBotFactory, WeixinBotFactory>();

        // Each named bot gets its own keyed handler instance.
        services.Add(ServiceDescriptor.KeyedSingleton<IWeixinMessageHandler, THandler>(name));

        WeixinMessagePipelineBuilder? pipelineBuilder = null;
        if (configurePipeline is not null)
        {
            pipelineBuilder = new WeixinMessagePipelineBuilder(services);
            configurePipeline(pipelineBuilder);
        }

        services.Add(ServiceDescriptor.KeyedSingleton<IWeixinBot>(name, (provider, key) =>
        {
            WeixinBotOptions options = new();
            configure?.Invoke(options);
            options.LoggerFactory ??= provider.GetService<ILoggerFactory>();
            IWeixinMessageHandler terminal = provider.GetRequiredKeyedService<IWeixinMessageHandler>((string)key!);
            IWeixinMessageHandler handler = pipelineBuilder is not null
                ? pipelineBuilder.Build(provider, terminal)
                : terminal;
            return new WeixinBot(handler, options);
        }));

        return services;
    }
}
