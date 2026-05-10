namespace Weixin.Bot.Sdk.Bot;

/// <summary>
/// Resolves named <see cref="IWeixinBot"/> instances registered via
/// <c>AddWeixinBot(name, ...)</c> overloads.
/// </summary>
public interface IWeixinBotFactory
{
    /// <summary>
    /// Returns the named bot instance.
    /// </summary>
    /// <param name="name">The name used when the bot was registered.</param>
    /// <returns>The registered <see cref="IWeixinBot"/> instance.</returns>
    /// <exception cref="InvalidOperationException">No bot is registered with <paramref name="name"/>.</exception>
    IWeixinBot GetBot(string name);
}
