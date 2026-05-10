using Weixin.Bot.Sdk.Models;

namespace Weixin.Bot.Sdk.Bot;

/// <summary>
/// Represents the next step in the message-handling pipeline.
/// </summary>
/// <param name="message">The message being processed.</param>
/// <param name="cancellationToken">A token that becomes cancelled when the bot stops.</param>
public delegate Task MessageHandlerDelegate(WeixinMessage message, CancellationToken cancellationToken);

/// <summary>
/// Defines a component in the message-handling middleware pipeline.
/// </summary>
public interface IMessageMiddleware
{
    /// <summary>
    /// Processes <paramref name="message"/> and optionally calls <paramref name="next"/> to continue the pipeline.
    /// </summary>
    /// <param name="message">The received message.</param>
    /// <param name="next">The next handler in the pipeline.</param>
    /// <param name="cancellationToken">A token that becomes cancelled when the bot stops.</param>
    Task InvokeAsync(WeixinMessage message, MessageHandlerDelegate next, CancellationToken cancellationToken);
}
