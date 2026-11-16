using System.Text.Json;
using ArturRios.Output;
using ArturRios.Util.WebApi.EndpointToggle;
using ArturRios.Util.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArturRios.Util.WebApi.Tests.Middleware;

/// <summary>
/// What <see cref="ExceptionMiddleware"/> answers for each kind of exception. The 500 envelope used to carry its text
/// under <c>Messages</c>, which left <c>Success</c> computed from an empty <c>Errors</c> list and therefore
/// <see langword="true"/> — a client checking the envelope saw a successful result on a 500.
/// <c>AuthenticationMiddleware</c> already enveloped its 401 the other way.
/// </summary>
[Trait("Category", "Unit")]
public class ExceptionMiddlewareTests
{
    private static async Task<DataOutput<string>> Run(RequestDelegate next)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance).InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);

        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        return JsonSerializer.Deserialize<DataOutput<string>>(body, JsonSerializerOptions.Web)!;
    }

    [Fact]
    public async Task GivenAnUnhandledException_WhenTheRequestIsProcessed_ThenA500ComesBackWithAGenericMessage()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        RequestDelegate next = _ => throw new InvalidOperationException("boom");
        var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Internal server error", body);
        Assert.DoesNotContain("boom", body); // no internal leak
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

    [Fact]
    public async Task GivenAnEndpointDisabledException_WhenTheRequestIsProcessed_ThenItsOwnStatusCodeIsUsed()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await new ExceptionMiddleware(_ => throw new EndpointDisabledException(["off"], 503),
            NullLogger<ExceptionMiddleware>.Instance).InvokeAsync(context);

        Assert.Equal(503, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, "Bad Request")]
    [InlineData(StatusCodes.Status413PayloadTooLarge, "Payload Too Large")]
    public async Task GivenABadHttpRequestException_WhenTheRequestIsProcessed_ThenItsOwnStatusCodeIsUsed(
        int statusCode, string reason)
    {
        // Kestrel throws it for client faults - a body over MaxRequestBodySize, a malformed chunked body - and
        // answers with its StatusCode when it escapes the app. Turning it into a 500 blamed the server.
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await new ExceptionMiddleware(
            _ => throw new BadHttpRequestException("Request body too large. The max is 10 bytes.", statusCode),
            NullLogger<ExceptionMiddleware>.Instance).InvokeAsync(context);

        Assert.Equal(statusCode, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var output = JsonSerializer.Deserialize<DataOutput<string>>(
            await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken), JsonSerializerOptions.Web)!;

        Assert.False(output.Success);
        Assert.Equal([reason], output.Errors);
    }

    [Fact]
    public async Task GivenAnUnhandledException_WhenTheEnvelopeIsWritten_ThenItsPropertiesAreCamelCasedLikeMvcs()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await new ExceptionMiddleware(_ => throw new InvalidOperationException("boom"),
            NullLogger<ExceptionMiddleware>.Instance).InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);

        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"success\":false", body);
        Assert.Contains("\"errors\":", body);
    }

    [Fact]
    public async Task GivenTheResponseHasStarted_WhenAnExceptionIsThrown_ThenItIsRethrownRatherThanSwallowed()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());

        var middleware = new ExceptionMiddleware(_ => throw new InvalidOperationException("boom"),
            NullLogger<ExceptionMiddleware>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    private sealed class TestCustomException(string[] messages) : CustomException(messages);

    private static async Task<(int StatusCode, string Body)> Process(RequestDelegate next, bool clientAborted = false)
    {
        using var aborted = new CancellationTokenSource();

        if (clientAborted)
        {
            await aborted.CancelAsync();
        }

        var context = new DefaultHttpContext { RequestAborted = aborted.Token, Response = { Body = new MemoryStream() } };

        await new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance).InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);

        return (context.Response.StatusCode,
            await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(typeof(OperationCanceledException))]
    [InlineData(typeof(TaskCanceledException))]
    public async Task GivenTheClientAbortedTheRequest_WhenTheWorkIsCancelled_ThenNothingIsWrittenAndNothingIsThrown(
        Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        var (statusCode, body) = await Process(_ => throw exception, clientAborted: true);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Empty(body);
    }

    [Fact]
    public async Task GivenAnOperationCancelledWhileTheClientIsStillConnected_WhenTheRequestIsProcessed_ThenA500EnvelopeComesBack()
    {
        var (statusCode, body) = await Process(_ => throw new OperationCanceledException());

        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);

        var output = JsonSerializer.Deserialize<DataOutput<string>>(body, JsonSerializerOptions.Web)!;

        Assert.False(output.Success);
        Assert.Equal(["Internal server error, please try again later"], output.Errors);
    }

    [Fact]
    public async Task GivenTheClientAbortedTheRequest_WhenAnotherExceptionIsThrown_ThenNoErrorResponseIsWritten()
    {
        var (statusCode, body) = await Process(_ => throw new InvalidOperationException("boom"), clientAborted: true);

        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Empty(body);
    }

    [Fact]
    public async Task GivenABadHttpRequestExceptionWithAStatusThatHasNoReasonPhrase_WhenTheRequestIsProcessed_ThenTheErrorSaysBadRequest()
    {
        const int statusCode = 450;

        var (actualStatusCode, body) = await Process(_ => throw new BadHttpRequestException("odd request", statusCode));

        Assert.Equal(statusCode, actualStatusCode);
        Assert.Equal(["Bad Request"], JsonSerializer.Deserialize<DataOutput<string>>(body, JsonSerializerOptions.Web)!.Errors);
    }

    [Fact]
    public async Task GivenAnEndpointDisabledExceptionWithTheDefaultStatus_WhenTheRequestIsProcessed_ThenA404CarriesItsMessages()
    {
        var (statusCode, body) = await Process(_ => throw new EndpointDisabledException(["turned off", "try later"]));

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);

        var output = JsonSerializer.Deserialize<DataOutput<string>>(body, JsonSerializerOptions.Web)!;

        Assert.False(output.Success);
        Assert.Equal(["turned off", "try later"], output.Errors);
    }
}
