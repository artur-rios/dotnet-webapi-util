using System.Text.Json;
using ArturRios.Output;
using ArturRios.Util.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArturRios.Util.WebApi.Tests.Middleware;

/// <summary>
/// The 500 envelope used to carry its text under <c>Messages</c>, which left <c>Success</c> computed from
/// an empty <c>Errors</c> list and therefore <see langword="true"/> — a client checking the envelope saw a
/// successful result on a 500. <c>AuthenticationMiddleware</c> already enveloped its 401 the other way.
/// </summary>
[Trait("Category", "Unit")]
public class ExceptionEnvelopeTests
{
    private static async Task<DataOutput<string>> Run(RequestDelegate next)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance).InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);

        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        return JsonSerializer.Deserialize<DataOutput<string>>(body)!;
    }

    [Fact]
    public async Task GivenAnUnhandledException_WhenTheEnvelopeIsRead_ThenItReportsFailure()
    {
        var output = await Run(_ => throw new InvalidOperationException("boom"));

        Assert.False(output.Success);
    }

    [Fact]
    public async Task GivenAnUnhandledException_WhenTheEnvelopeIsRead_ThenTheTextIsUnderErrors()
    {
        var output = await Run(_ => throw new InvalidOperationException("boom"));

        Assert.Contains(output.Errors, error => error.Contains("Internal server error"));
        Assert.Empty(output.Messages);
    }

    [Fact]
    public async Task GivenAnUnhandledException_WhenTheEnvelopeIsRead_ThenNoInternalDetailLeaks()
    {
        var output = await Run(_ => throw new InvalidOperationException("boom"));

        Assert.DoesNotContain(output.Errors, error => error.Contains("boom"));
    }

    [Fact]
    public async Task GivenACustomException_WhenTheEnvelopeIsRead_ThenItsOwnMessagesAreUnderErrors()
    {
        var output = await Run(_ => throw new TestCustomException(["first problem", "second problem"]));

        Assert.False(output.Success);
        Assert.Equal(new[] { "first problem", "second problem" }, output.Errors);
    }

    [Fact]
    public async Task GivenNoException_WhenTheRequestIsProcessed_ThenTheMiddlewareWritesNothing()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await new ExceptionMiddleware(_ => Task.CompletedTask, NullLogger<ExceptionMiddleware>.Instance)
            .InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    private sealed class TestCustomException(string[] messages) : CustomException(messages);
}
