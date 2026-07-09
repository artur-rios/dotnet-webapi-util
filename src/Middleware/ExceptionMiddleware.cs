using ArturRios.Output;
using ArturRios.Util.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ArturRios.Util.WebApi.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger) : WebApiMiddleware
{
    public async Task Invoke(HttpContext httpContext)
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

        var messages = new[] { "Internal server error, please try again later" };

        if (exception is CustomException customException)
        {
            messages = customException.Messages;
        }

        logger.LogError("Exception: {ExceptionMessage}", exception.Message);
        logger.LogError("Stack: {ExceptionStackTrace}", exception.StackTrace);

        foreach (var message in messages)
        {
            logger.LogError("Message: {Message}", message);
        }

        if (exception.InnerException is not null)
        {
            logger.LogError("Inner exception on request: {InnerExceptionMessage}", exception.InnerException.Message);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = HttpStatusCodes.InternalServerError;

        var output = DataOutput<string>.New
            .WithData(string.Empty)
            .WithMessages(messages);

        await context.Response.WriteAsync(JsonConvert.SerializeObject(output));
    }
}
