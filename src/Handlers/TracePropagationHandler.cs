using System.Diagnostics;

namespace ArturRios.Util.WebApi.Handlers;

/// <summary>
///     Propagates the W3C traceparent header on outgoing HttpClient requests.
///     Register this as an HttpMessageHandler for typed/named HttpClients.
/// </summary>
public class TracePropagationHandler : DelegatingHandler
{
    private const string TraceParentHeader = "traceparent";

    /// <summary>Adds the current <see cref="Activity"/>'s W3C <c>traceparent</c> header to the outgoing
    /// request, if one isn't already present, before delegating to the inner handler.</summary>
    /// <param name="request">The outgoing HTTP request.</param>
    /// <param name="cancellationToken">A token to cancel the send operation.</param>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var activity = Activity.Current;

        if (activity is null)
        {
            return base.SendAsync(request, cancellationToken);
        }

        var traceParent = $"00-{activity.TraceId}-{activity.SpanId}-{(activity.Recorded ? "01" : "00")}";

        if (!request.Headers.Contains(TraceParentHeader))
        {
            request.Headers.Add(TraceParentHeader, traceParent);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
