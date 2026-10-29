using ArturRios.Output;
using ArturRios.Util.Http;
using ArturRios.Util.WebApi.EndpointToggle;
using ArturRios.Util.WebApi.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace ArturRios.Util.WebApi.Middleware;

/// <summary>
/// Catches unhandled exceptions raised further down the pipeline and converts them into a JSON error response,
/// logging the exception and quietly ignoring client-initiated request cancellations. An
/// <see cref="EndpointDisabledException"/> is answered with its own status code rather than 500, and so is a
/// <see cref="BadHttpRequestException"/> (a client fault such as a request body over the size limit), with its
/// status's reason phrase as the error. When the response
/// has already started, the exception is rethrown so the server aborts the response instead of leaving the client
/// with a truncated body that looks complete.
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="logger">Used to log unhandled exceptions and cancellations.</param>
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger) : WebApiMiddleware
{
    private const string InternalServerError = "Internal server error, please try again later";
    private const string BadRequestMessage = "Bad Request";

    /// <summary>Invokes the next middleware, catching any unhandled exception and writing an error response instead of propagating it.</summary>
    /// <param name="httpContext">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await next(httpContext);
        }
        catch (OperationCanceledException oce) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            // TaskCanceledException derives from OperationCanceledException, so this covers both.
            logger.LogDebug("Request was canceled by the client: {Message}", oce.Message);
        }
        catch (Exception ex) when (!httpContext.Response.HasStarted)
        {
            await HandleException(httpContext, ex);
        }
    }

    private async Task HandleException(HttpContext context, Exception exception)
    {
        if (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(exception, "Cannot write an error response because the request was aborted.");

            return;
        }

        int statusCode;
        string[] errors;

        switch (exception)
        {
            case EndpointDisabledException disabled:
                logger.LogInformation("Request reached a disabled endpoint: {Path}", context.Request.Path);
                statusCode = disabled.StatusCode;
                errors = disabled.Messages;
                break;
            case BadHttpRequestException badRequest:
                // A client fault Kestrel or a binder detected (e.g. a body over MaxRequestBodySize is a 413);
                // Kestrel answers with its StatusCode when nothing catches it, so this must not become a 500.
                logger.LogInformation("Rejected a bad request with {StatusCode}: {Message}", badRequest.StatusCode,
                    badRequest.Message);
                statusCode = badRequest.StatusCode;
                errors = [ReasonPhrases.GetReasonPhrase(badRequest.StatusCode) is { Length: > 0 } reason
                    ? reason
                    : BadRequestMessage];
                break;
            case CustomException custom:
                logger.LogError(exception, "Unhandled exception while processing the request.");
                statusCode = HttpStatusCodes.InternalServerError;
                errors = custom.Messages;
                break;
            default:
                logger.LogError(exception, "Unhandled exception while processing the request.");
                statusCode = HttpStatusCodes.InternalServerError;
                errors = [InternalServerError];
                break;
        }

        // Errors, not messages: Success is computed from Errors, so a 500 whose text sat under Messages
        // came back with "success": true. AuthenticationMiddleware already envelopes its 401 this way.
        var output = DataOutput<string>.New
            .WithData(string.Empty)
            .WithErrors(errors);

        await context.Response.WriteOutputAsync(statusCode, output);
    }
}
