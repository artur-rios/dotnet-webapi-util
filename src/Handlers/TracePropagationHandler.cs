using System.Diagnostics;
using ArturRios.Util.WebApi.Extensions;

namespace ArturRios.Util.WebApi.Handlers;

/// <summary>
///     Propagates the W3C <c>traceparent</c> (and <c>tracestate</c>, when present) headers on outgoing HttpClient
///     requests. Register it on typed/named HttpClients with
///     <see cref="WebApiBuilderExtensions.AddTracePropagation"/>.
/// </summary>
public class TracePropagationHandler : DelegatingHandler
{
    private const string TraceParentHeader = "traceparent";
    private const string TraceStateHeader = "tracestate";

    /// <summary>Adds the current <see cref="Activity"/>'s W3C <c>traceparent</c> and <c>tracestate</c> headers to the
    /// outgoing request, if they aren't already present, before delegating to the inner handler.</summary>
    /// <param name="request">The outgoing HTTP request.</param>
    /// <param name="cancellationToken">A token to cancel the send operation.</param>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var activity = Activity.Current;

        if (activity is not null && !request.Headers.Contains(TraceParentHeader))
        {
            request.Headers.TryAddWithoutValidation(TraceParentHeader, activity.ToTraceParent());

            if (!string.IsNullOrEmpty(activity.TraceStateString))
            {
                request.Headers.TryAddWithoutValidation(TraceStateHeader, activity.TraceStateString);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
