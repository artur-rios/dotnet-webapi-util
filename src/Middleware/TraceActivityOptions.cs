namespace ArturRios.Util.WebApi.Middleware;

/// <summary>Options for <see cref="TraceActivityMiddleware"/>. Register them with
/// <c>services.Configure&lt;TraceActivityOptions&gt;(...)</c>, or through <c>WebApiStartupOptions.TraceActivity</c>.</summary>
public class TraceActivityOptions
{
    /// <summary>Whether the client's IP address is included in the "Started request" log entry. When
    /// <see langword="false"/>, the entry carries only the trace id. Defaults to <see langword="true"/>.</summary>
    public bool LogClientIp { get; set; } = true;

    /// <summary>Whether the client's IP address is tagged on the request's activity as <c>client.address</c>, so
    /// tracing backends receive it. Defaults to <see langword="true"/>.</summary>
    public bool TagClientAddress { get; set; } = true;
}
