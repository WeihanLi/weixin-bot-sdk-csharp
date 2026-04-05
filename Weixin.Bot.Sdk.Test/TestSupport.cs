using System.Net;
using System.Text;

namespace Weixin.Bot.Sdk.Test;

internal static class TestSupport
{
    internal static string CreateCredentialsFile(string userId)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.weixin-bot.credentials.json");
        File.WriteAllText(path, $$"""
        {
          "botToken": "bot-token",
          "botId": "bot-id",
          "baseUrl": "https://unit.test/",
          "userId": "{{userId}}",
          "savedAt": "2026-04-05T00:00:00+00:00"
        }
        """);
        return path;
    }

    internal static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
}

internal sealed class ScriptedHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _steps = new();

    public List<(HttpMethod Method, Uri? Uri, string? Body)> Requests { get; } = [];

    public void EnqueueJson(string body)
    {
        _steps.Enqueue((_, _) => Task.FromResult(TestSupport.Json(body)));
    }

    public void Enqueue(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> step)
    {
        _steps.Enqueue(step);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        Requests.Add((request.Method, request.RequestUri, body));

        if (_steps.Count > 0)
        {
            return await _steps.Dequeue()(request, cancellationToken).ConfigureAwait(false);
        }

        return TestSupport.Json("""
        {
          "ret": 0,
          "errcode": 0,
          "get_updates_buf": "",
          "msgs": []
        }
        """);
    }
}
