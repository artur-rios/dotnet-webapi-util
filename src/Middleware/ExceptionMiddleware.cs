using ArturRios.Output;
using ArturRios.Util.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ArturRios.Util.WebApi.Middleware;

/// <summary>
/// Catches unhandled exceptions raised further down the pipeline and converts them into a JSON error response,
/// logging the exception and quietly ignoring client-initiated request cancellations.
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="logger">Used to log unhandled exceptions and cancellations.</param>
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger) : WebApiMiddleware
{
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
            logger.LogDebug("Request was canceled by the client: {OceMessage}", oce.Message);
        }
        catch (TaskCanceledException tce) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Request was canceled by the client (TaskCanceled): {TceMessage}", tce.Message);
        }
        catch (Exception ex)
        {
            await HandleException(httpContext, ex);
        }
    }

    private async Task HandleException(HttpContext context, Exception exception)
    {
        if (context.RequestAborted.IsCancellationRequested || context.Response.HasStarted)
        {
            logger.LogDebug(
                "Cannot write error response because the request was aborted or the response has already started.");

            return;
        }

        var errors = new[] { "Internal server error, please try again later" };

        if (exception is CustomException customException)
        {
            errors = customException.Messages;
        }

        logger.LogError(exception, "Unhandled exception while processing the request.");

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = HttpStatusCodes.InternalServerError;

        // Errors, not messages: Success is computed from Errors, so a 500 whose text sat under Messages
        // came back with "success": true. AuthenticationMiddleware already envelopes its 401 this way.
        var output = DataOutput<string>.New
            .WithData(string.Empty)
            .WithErrors(errors);

        await context.Response.WriteAsync(JsonSerializer.Serialize(output));
    }
}
