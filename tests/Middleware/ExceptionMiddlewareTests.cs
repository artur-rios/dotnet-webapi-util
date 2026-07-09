using ArturRios.Util.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArturRios.Util.WebApi.Tests.Middleware;

public class ExceptionMiddlewareTests
{
    [Fact]
    public async Task UnhandledException_Writes500_WithGenericMessage()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        RequestDelegate next = _ => throw new InvalidOperationException("boom");
        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("Internal server error", body);
        Assert.DoesNotContain("boom", body); // no internal leak
    }
}
