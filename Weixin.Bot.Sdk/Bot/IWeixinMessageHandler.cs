using Weixin.Bot.Sdk.Models;

namespace Weixin.Bot.Sdk.Bot;

/// <summary>
/// Processes inbound messages received by the bot.
/// </summary>
public interface IWeixinMessageHandler
{
    /// <summary>
    /// Processes an inbound <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The received message.</param>
    /// <param name="cancellationToken">A token that becomes cancelled when the bot stops.</param>
    Task HandleMessageAsync(WeixinMessage message, CancellationToken cancellationToken);
}
