using ArturRios.Util.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArturRios.Util.WebApi.Tests.Middleware;

public class TraceActivityMiddlewareTests
{
    [Fact]
    public async Task SetsTraceIdAndTraceparentHeader()
    {
        var context = new DefaultHttpContext();
        var middleware = new TraceActivityMiddleware(_ => Task.CompletedTask, NullLogger<TraceActivityMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.False(string.IsNullOrEmpty(context.Items["TraceId"] as string));
        Assert.Equal(context.Items["TraceId"], context.TraceIdentifier);
        Assert.True(context.Response.Headers.ContainsKey("traceparent"));
    }
}
