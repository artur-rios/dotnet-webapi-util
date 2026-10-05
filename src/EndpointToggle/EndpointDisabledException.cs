using ArturRios.Output;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.EndpointToggle;

/// <summary>Exception thrown by <see cref="EndpointToggleAttribute"/> when a disabled endpoint is reached and its
/// disabled behavior is configured as <c>OutputType.Exception</c>. Carries one or more messages describing why the
/// endpoint is unavailable and the status code to answer with, letting it be handled by the exception pipeline (for
/// example <see cref="Middleware.ExceptionMiddleware"/>, which honors <see cref="StatusCode"/>).</summary>
public class EndpointDisabledException : CustomException
{
    /// <summary>Initializes a new instance answered with 404 Not Found.</summary>
    /// <param name="messages">The messages describing why the endpoint is disabled.</param>
    public EndpointDisabledException(string[] messages) : this(messages, StatusCodes.Status404NotFound) { }

    /// <summary>Initializes a new instance answered with <paramref name="statusCode"/>.</summary>
    /// <param name="messages">The messages describing why the endpoint is disabled.</param>
    /// <param name="statusCode">The HTTP status code the disabled endpoint should answer with.</param>
    public EndpointDisabledException(string[] messages, int statusCode) : base(messages) => StatusCode = statusCode;

    /// <summary>The HTTP status code the disabled endpoint should answer with.</summary>
    public int StatusCode { get; }
}
