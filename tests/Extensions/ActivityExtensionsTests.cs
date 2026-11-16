using System.Diagnostics;
using ArturRios.Util.WebApi.Extensions;

namespace ArturRios.Util.WebApi.Tests.Extensions;

[Trait("Category", "Unit")]
public class ActivityExtensionsTests
{
    [Theory]
    [InlineData(ActivityTraceFlags.Recorded, "01")]
    [InlineData(ActivityTraceFlags.None, "00")]
    public void GivenAnActivity_WhenFormattingItsTraceparent_ThenItIsTheW3CHeaderWithItsSampledFlag(
        ActivityTraceFlags flags, string expectedFlags)
    {
        using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();
        activity.ActivityTraceFlags = flags;

        var traceparent = activity.ToTraceParent();

        Assert.Equal($"00-{activity.TraceId}-{activity.SpanId}-{expectedFlags}", traceparent);
        Assert.Matches("^00-[0-9a-f]{32}-[0-9a-f]{16}-0[01]$", traceparent);
    }
}
