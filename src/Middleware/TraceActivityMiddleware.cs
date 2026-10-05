using System.Diagnostics;
using ArturRios.Util.WebApi.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ArturRios.Util.WebApi.Middleware;

/// <summary>
/// Ensures every request is associated with a W3C-format <see cref="Activity"/>, propagating or creating one as
/// needed, and exposes its trace id on <see cref="HttpContext.TraceIdentifier"/>, <c>HttpContext.Items["TraceId"]</c>
/// and the response's <c>traceparent</c> header. When it has to create the activity itself, an incoming
/// <c>traceparent</c>/<c>tracestate</c> pair becomes its parent, so the caller's trace continues.
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="logger">Used to log the start and end of each request's trace.</param>
public class TraceActivityMiddleware(RequestDelegate next, ILogger<TraceActivityMiddleware> logger) : WebApiMiddleware
{
    private const string ActivityName = "ServerReceive";
    private const string TraceIdItemKey = "TraceId";
    private const string TraceParentHeader = "traceparent";
    private const string TraceStateHeader = "tracestate";

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

        var activity = Activity.Current;
        var createdActivity = activity is null;

        activity ??= StartActivity(context.Request);

        var traceId = activity.TraceId.ToString();

        context.TraceIdentifier = traceId;
        context.Items[TraceIdItemKey] = traceId;
        context.Response.Headers[TraceParentHeader] = activity.ToTraceParent();

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

    private static Activity StartActivity(HttpRequest request)
    {
        var activity = new Activity(ActivityName).SetIdFormat(ActivityIdFormat.W3C);

        if (ActivityContext.TryParse(request.Headers[TraceParentHeader], request.Headers[TraceStateHeader],
                out var parent))
        {
            activity.SetParentId(parent.TraceId, parent.SpanId, parent.TraceFlags);
            activity.TraceStateString = parent.TraceState;
        }

        return activity.Start();
    }
}
