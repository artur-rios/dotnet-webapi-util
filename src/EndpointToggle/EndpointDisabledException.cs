using ArturRios.Output;

namespace ArturRios.Util.WebApi.EndpointToggle;

/// <summary>Exception thrown by <see cref="EndpointToggleAttribute"/> when a disabled endpoint is reached and its
/// disabled behavior is configured as <c>OutputType.Exception</c>. Carries one or more messages describing why the
/// endpoint is unavailable, letting it be handled by the exception pipeline (for example
/// <see cref="Middleware.ExceptionMiddleware"/>) like any other <see cref="CustomException"/>.</summary>
/// <param name="messages">The messages describing why the endpoint is disabled.</param>
public class EndpointDisabledException(string[] messages) : CustomException(messages);
