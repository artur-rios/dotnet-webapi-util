using ArturRios.Util.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
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
}
