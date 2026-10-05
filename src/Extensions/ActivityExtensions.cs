using System.Diagnostics;

namespace ArturRios.Util.WebApi.Extensions;

/// <summary>Extension methods for formatting an <see cref="Activity"/> as W3C trace-context headers.</summary>
public static class ActivityExtensions
{
    /// <summary>Formats the activity as a W3C <c>traceparent</c> header value:
    /// <c>00-&lt;trace-id&gt;-&lt;span-id&gt;-&lt;flags&gt;</c>.</summary>
    /// <param name="activity">The activity to format.</param>
    public static string ToTraceParent(this Activity activity) =>
        $"00-{activity.TraceId}-{activity.SpanId}-{(activity.Recorded ? "01" : "00")}";
}
