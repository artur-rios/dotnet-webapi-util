using ArturRios.Util.WebApi.Handlers;
using ArturRios.Util.WebApi.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArturRios.Util.WebApi.Extensions;

/// <summary>Extension methods for wiring the library's middlewares and handlers into an application.</summary>
public static class WebApiBuilderExtensions
{
    /// <summary>Registers each given middleware type on the pipeline, in order.</summary>
    /// <param name="app">The application builder.</param>
    /// <param name="middlewares">The middleware types to register. Each must derive from <see cref="WebApiMiddleware"/>.</param>
    /// <returns>The same <paramref name="app"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentException">A type does not derive from <see cref="WebApiMiddleware"/>.</exception>
    public static IApplicationBuilder UseWebApiMiddlewares(this IApplicationBuilder app, params Type[] middlewares)
    {
        ArgumentNullException.ThrowIfNull(middlewares);

        var invalid = middlewares.FirstOrDefault(middleware => !middleware.IsSubclassOf(typeof(WebApiMiddleware)));

        if (invalid is not null)
        {
            throw new ArgumentException(
                $"{invalid.FullName} does not derive from {nameof(WebApiMiddleware)}.", nameof(middlewares));
        }

        foreach (var middleware in middlewares)
        {
            app.UseMiddleware(middleware);
        }

        return app;
    }

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
