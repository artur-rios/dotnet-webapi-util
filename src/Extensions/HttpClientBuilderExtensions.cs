using ArturRios.Util.WebApi.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArturRios.Util.WebApi.Extensions;

/// <summary>Extension methods for wiring the library's handlers into named or typed <see cref="HttpClient"/>s.</summary>
public static class HttpClientBuilderExtensions
{
    /// <summary>Adds <see cref="TracePropagationHandler"/> to the client's handler pipeline, so its outgoing requests
    /// carry the current W3C <c>traceparent</c>. Registers the handler with the container if it isn't already.</summary>
    /// <param name="builder">The builder of the named or typed <see cref="HttpClient"/>.</param>
    /// <returns>The same <paramref name="builder"/> instance, for chaining.</returns>
    public static IHttpClientBuilder AddTracePropagation(this IHttpClientBuilder builder)
    {
        builder.Services.TryAddTransient<TracePropagationHandler>();

        return builder.AddHttpMessageHandler<TracePropagationHandler>();
    }
}
