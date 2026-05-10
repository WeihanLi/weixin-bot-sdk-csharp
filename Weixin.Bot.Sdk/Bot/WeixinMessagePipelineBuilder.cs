using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Weixin.Bot.Sdk.Models;

namespace Weixin.Bot.Sdk.Bot;

/// <summary>
/// Builds a composable message-handling pipeline from a sequence of <see cref="IMessageMiddleware"/> components
/// wrapping a terminal <see cref="IWeixinMessageHandler"/>.
/// </summary>
/// <remarks>
/// Middleware runs in registration order: the first registered component is the outermost wrapper.
/// Each component receives the message and a <see cref="MessageHandlerDelegate"/> that calls the next
/// component in the chain. The terminal handler is called last.
/// </remarks>
public sealed class WeixinMessagePipelineBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<Func<IServiceProvider, IMessageMiddleware>> _factories = [];

    internal WeixinMessagePipelineBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Adds <typeparamref name="TMiddleware"/> as the next component in the pipeline.
    /// The type is registered as a transient service so it receives constructor dependencies from the container.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware component type.</typeparam>
    /// <returns>The same builder for chaining.</returns>
    public WeixinMessagePipelineBuilder Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, IMessageMiddleware
    {
        _services.AddTransient<TMiddleware>();
        _factories.Add(static sp => sp.GetRequiredService<TMiddleware>());
        return this;
    }

    /// <summary>
    /// Adds an inline middleware component using a delegate.
    /// </summary>
    /// <param name="middleware">
    /// A delegate that receives the message, the next pipeline step, and a cancellation token.
    /// Call <paramref name="middleware"/>'s second argument to continue to the next component.
    /// </param>
    /// <returns>The same builder for chaining.</returns>
    public WeixinMessagePipelineBuilder Use(Func<WeixinMessage, MessageHandlerDelegate, CancellationToken, Task> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _factories.Add(_ => new DelegateMiddleware(middleware));
        return this;
    }

    /// <summary>
    /// Builds the pipeline, wrapping <paramref name="terminal"/> with all registered middleware in registration order.
    /// </summary>
    internal IWeixinMessageHandler Build(IServiceProvider provider, IWeixinMessageHandler terminal)
    {
        MessageHandlerDelegate pipeline = terminal.HandleMessageAsync;
        for (int i = _factories.Count - 1; i >= 0; i--)
        {
            IMessageMiddleware mw = _factories[i](provider);
            MessageHandlerDelegate next = pipeline;
            pipeline = (msg, ct) => mw.InvokeAsync(msg, next, ct);
        }
        return new MessageHandlerPipeline(pipeline);
    }

    private sealed class DelegateMiddleware(Func<WeixinMessage, MessageHandlerDelegate, CancellationToken, Task> invoke) : IMessageMiddleware
    {
        public Task InvokeAsync(WeixinMessage message, MessageHandlerDelegate next, CancellationToken cancellationToken)
            => invoke(message, next, cancellationToken);
    }

    private sealed class MessageHandlerPipeline(MessageHandlerDelegate pipeline) : IWeixinMessageHandler
    {
        public Task HandleMessageAsync(WeixinMessage message, CancellationToken cancellationToken)
            => pipeline(message, cancellationToken);
    }
}
