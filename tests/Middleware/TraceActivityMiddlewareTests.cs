using System.Diagnostics;
using System.Net;
using ArturRios.Util.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArturRios.Util.WebApi.Tests.Middleware;

[Trait("Category", "Unit")]
public class TraceActivityMiddlewareTests
{
    [Fact]
    public async Task GivenARequest_WhenItPassesThroughTheMiddleware_ThenTheTraceIdAndTraceparentHeaderAreSet()
    {
        var context = new DefaultHttpContext();
        var middleware = new TraceActivityMiddleware(_ => Task.CompletedTask, NullLogger<TraceActivityMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.False(string.IsNullOrEmpty(context.Items["TraceId"] as string));
        Assert.Equal(context.Items["TraceId"], context.TraceIdentifier);
        Assert.True(context.Response.Headers.ContainsKey("traceparent"));
    }

    [Fact]
    public async Task GivenAnIncomingTraceparentAndNoAmbientActivity_WhenItPassesThroughTheMiddleware_ThenTheCallersTraceContinues()
    {
        const string traceId = "0af7651916cd43dd8448eb211c80319c";
        var context = new DefaultHttpContext();
        context.Request.Headers["traceparent"] = $"00-{traceId}-b7ad6b7169203331-01";
        var middleware = new TraceActivityMiddleware(_ => Task.CompletedTask, NullLogger<TraceActivityMiddleware>.Instance);

        System.Diagnostics.Activity.Current = null;

        await middleware.InvokeAsync(context);

        var traceparent = context.Response.Headers["traceparent"].ToString();

        Assert.Equal(traceId, context.TraceIdentifier);
        Assert.StartsWith($"00-{traceId}-", traceparent);
        Assert.EndsWith("-01", traceparent);
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    public async Task GivenAClientAddress_WhenTheRequestStarts_ThenItIsLoggedAndTaggedOnTheActivity(
        string remoteAddress, string expected)
    {
        var logger = new CapturingLogger();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        string? tagged = null;
        var middleware = new TraceActivityMiddleware(_ =>
        {
            tagged = Activity.Current?.GetTagItem("client.address") as string;

            return Task.CompletedTask;
        }, logger);

        Activity.Current = null;

        await middleware.InvokeAsync(context);

        var entry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains(expected, entry.Message);
        Assert.Equal(expected, entry.State["ClientIp"]);
        Assert.Equal(expected, tagged);
    }

    [Fact]
    public async Task GivenNoClientAddress_WhenTheRequestStarts_ThenTheClientIsLoggedAsUnknown()
    {
        var logger = new CapturingLogger();
        var middleware = new TraceActivityMiddleware(_ => Task.CompletedTask, logger);

        await middleware.InvokeAsync(new DefaultHttpContext());

        var entry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Equal("unknown", entry.State["ClientIp"]);
    }

    private sealed record LogEntry(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> State);

    private sealed class CapturingLogger : ILogger<TraceActivityMiddleware>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var values = state as IEnumerable<KeyValuePair<string, object?>> ?? [];

            Entries.Add(new LogEntry(logLevel, formatter(state, exception),
                values.ToDictionary(pair => pair.Key, pair => pair.Value)));
        }
    }
}
