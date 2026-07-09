using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ArturRios.Util.WebApi.Middleware;

/// <summary>
/// Ensures every request is associated with a W3C-format <see cref="Activity"/>, propagating or creating one as
/// needed, and exposes its trace id on <see cref="HttpContext.TraceIdentifier"/>, <c>HttpContext.Items["TraceId"]</c>
/// and the response's <c>traceparent</c> header.
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="logger">Used to log the start and end of each request's trace.</param>
public class TraceActivityMiddleware(RequestDelegate next, ILogger<TraceActivityMiddleware> logger) : WebApiMiddleware
{
    private const string TraceParentHeader = "traceparent";

    static TraceActivityMiddleware()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
    }

    /// <summary>Attaches the current (or a newly created) trace activity to the context and response, then invokes the next middleware.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var createdActivity = false;
        var activity = Activity.Current;

        if (activity == null)
        {
            activity = new Activity("ServerReceive").SetIdFormat(ActivityIdFormat.W3C).Start();
            createdActivity = true;
        }

        var traceId = activity.TraceId.ToString();

        context.TraceIdentifier = traceId;
        context.Items["TraceId"] = traceId;


        var tp = $"00-{activity.TraceId}-{activity.SpanId}-{(activity.Recorded ? "01" : "00")}";

        context.Response.Headers[TraceParentHeader] = tp;

        logger.LogTrace("Started request with TraceId {TraceId}", traceId);

        try
        {
            await next(context);
        }
        finally
        {
            logger.LogTrace("Ending request with TraceId {TraceId}", traceId);

            if (createdActivity && Activity.Current == activity)
            {
                activity.Stop();
            }
        }
    }
}
