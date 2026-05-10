using Microsoft.Extensions.DependencyInjection;
using Weixin.Bot.Sdk.Bot;
using Weixin.Bot.Sdk.Extensions;
using Weixin.Bot.Sdk.Models;
using Xunit;

namespace Weixin.Bot.Sdk.Test;

public sealed class MessagePipelineTests
{
    private static WeixinMessage MakeMessage() => new(
        "mid", "from", "to", DateTimeOffset.UtcNow, "ctx",
        "hello", "hello", MessageContentKind.Text, null, null, null, null, null);

    [Fact]
    public async Task Pipeline_NoMiddleware_CallsTerminalHandler()
    {
        ServiceCollection services = new();
        WeixinMessagePipelineBuilder builder = new(services);
        ServiceProvider provider = services.BuildServiceProvider();

        List<string> calls = [];
        DelegateMessageHandler terminal = new((_, _) => { calls.Add("terminal"); return Task.CompletedTask; });

        IWeixinMessageHandler handler = builder.Build(provider, terminal);
        await handler.HandleMessageAsync(MakeMessage(), CancellationToken.None);

        Assert.Equal(["terminal"], calls);
    }

    [Fact]
    public async Task Pipeline_SingleMiddleware_RunsBeforeTerminal()
    {
        List<string> calls = [];
        ServiceCollection services = new();
        WeixinMessagePipelineBuilder builder = new(services);
        builder.Use((msg, next, ct) =>
        {
            calls.Add("middleware");
            return next(msg, ct);
        });

        ServiceProvider provider = services.BuildServiceProvider();
        DelegateMessageHandler terminal = new((_, _) => { calls.Add("terminal"); return Task.CompletedTask; });

        IWeixinMessageHandler handler = builder.Build(provider, terminal);
        await handler.HandleMessageAsync(MakeMessage(), CancellationToken.None);

        Assert.Equal(["middleware", "terminal"], calls);
    }

    [Fact]
    public async Task Pipeline_MultipleMiddleware_RunsInRegistrationOrder()
    {
        List<string> calls = [];
        ServiceCollection services = new();
        WeixinMessagePipelineBuilder builder = new(services);
        builder.Use((msg, next, ct) => { calls.Add("mw-1"); return next(msg, ct); });
        builder.Use((msg, next, ct) => { calls.Add("mw-2"); return next(msg, ct); });
        builder.Use((msg, next, ct) => { calls.Add("mw-3"); return next(msg, ct); });

        ServiceProvider provider = services.BuildServiceProvider();
        DelegateMessageHandler terminal = new((_, _) => { calls.Add("terminal"); return Task.CompletedTask; });

        IWeixinMessageHandler handler = builder.Build(provider, terminal);
        await handler.HandleMessageAsync(MakeMessage(), CancellationToken.None);

        Assert.Equal(["mw-1", "mw-2", "mw-3", "terminal"], calls);
    }

    [Fact]
    public async Task Pipeline_MiddlewareCanShortCircuit_WithoutCallingNext()
    {
        List<string> calls = [];
        ServiceCollection services = new();
        WeixinMessagePipelineBuilder builder = new(services);
        builder.Use((_, _, _) => { calls.Add("short-circuit"); return Task.CompletedTask; }); // does not call next
        builder.Use((msg, next, ct) => { calls.Add("should-not-run"); return next(msg, ct); });

        ServiceProvider provider = services.BuildServiceProvider();
        DelegateMessageHandler terminal = new((_, _) => { calls.Add("terminal"); return Task.CompletedTask; });

        IWeixinMessageHandler handler = builder.Build(provider, terminal);
        await handler.HandleMessageAsync(MakeMessage(), CancellationToken.None);

        Assert.Equal(["short-circuit"], calls);
    }

    [Fact]
    public async Task Pipeline_TypedMiddleware_ResolvedFromServiceProvider()
    {
        List<string> calls = [];
        ServiceCollection services = new();
        services.AddSingleton(calls); // inject the list into the middleware
        WeixinMessagePipelineBuilder builder = new(services);
        builder.Use<RecordingMiddleware>();

        ServiceProvider provider = services.BuildServiceProvider();
        DelegateMessageHandler terminal = new((_, _) => { calls.Add("terminal"); return Task.CompletedTask; });

        IWeixinMessageHandler handler = builder.Build(provider, terminal);
        await handler.HandleMessageAsync(MakeMessage(), CancellationToken.None);

        Assert.Equal(["RecordingMiddleware", "terminal"], calls);
    }

    [Fact]
    public async Task Pipeline_MessageIsPropagated_ToTerminalHandler()
    {
        WeixinMessage? received = null;
        ServiceCollection services = new();
        WeixinMessagePipelineBuilder builder = new(services);
        builder.Use((msg, next, ct) => next(msg, ct)); // pass-through

        ServiceProvider provider = services.BuildServiceProvider();
        DelegateMessageHandler terminal = new((msg, _) => { received = msg; return Task.CompletedTask; });

        IWeixinMessageHandler handler = builder.Build(provider, terminal);
        WeixinMessage sent = MakeMessage();
        await handler.HandleMessageAsync(sent, CancellationToken.None);

        Assert.Same(sent, received);
    }

    [Fact]
    public void AddWeixinBot_WithPipeline_BuildsHandlerChain()
    {
        List<string> calls = [];
        ServiceCollection services = new();
        services.AddSingleton(calls);
        services.AddWeixinBot<TerminalRecordingHandler>(
            configure: options =>
            {
                options.Token = "test-token";
                options.HttpClient = new HttpClient(new ScriptedHttpMessageHandler());
            },
            configurePipeline: pipeline => pipeline.Use<RecordingMiddleware>());

        ServiceProvider provider = services.BuildServiceProvider();

        // Resolving IWeixinBot builds the pipeline
        IWeixinBot bot = provider.GetRequiredService<IWeixinBot>();
        Assert.NotNull(bot);
    }

    private sealed class RecordingMiddleware(List<string> calls) : IMessageMiddleware
    {
        public Task InvokeAsync(WeixinMessage message, MessageHandlerDelegate next, CancellationToken cancellationToken)
        {
            calls.Add("RecordingMiddleware");
            return next(message, cancellationToken);
        }
    }

    private sealed class TerminalRecordingHandler(List<string> calls) : IWeixinMessageHandler
    {
        public Task HandleMessageAsync(WeixinMessage message, CancellationToken cancellationToken)
        {
            calls.Add("terminal");
            return Task.CompletedTask;
        }
    }
}
