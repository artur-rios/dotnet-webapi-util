using ArturRios.Util.WebApi.Middleware;
using ArturRios.Util.WebApi.Security.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using TokenAuthenticationOptions = ArturRios.Util.WebApi.Security.Configuration.AuthenticationOptions;

namespace ArturRios.Util.WebApi.Configuration;

/// <summary>Pipeline-side steps of the standard web API setup. <see cref="WebApiStartup"/> runs them in order, and
/// each one can be used on its own with a plain <see cref="WebApplication"/>.</summary>
public static class ApplicationBuilderExtensions
{
    /// <param name="app">The application builder.</param>
    extension(IApplicationBuilder app)
    {
        /// <summary>Adds the standard middlewares, in order: ASP.NET Core's forwarded headers (a no-op until
        /// <c>ForwardedHeadersOptions</c> is configured, so the client IP behind a trusted proxy is the real one),
        /// <see cref="TraceActivityMiddleware"/>,
        /// <see cref="ExceptionMiddleware"/>, Swagger (see <see cref="UseWebApiSwagger"/>), CORS when
        /// <paramref name="corsPolicy"/> is given, then <see cref="AuthenticationMiddleware"/> when token authentication
        /// was registered with <c>AddTokenAuthentication</c>. Swagger and CORS come before authentication, so the
        /// Swagger UI and CORS preflight requests are never rejected for lacking a token.</summary>
        /// <param name="corsPolicy">The name of a CORS policy registered with <c>AddCors</c>, or <see langword="null"/>
        /// for no CORS.</param>
        /// <returns>The same <paramref name="app"/> instance, for chaining.</returns>
        public IApplicationBuilder UseStandardMiddlewares(string? corsPolicy = null)
        {
            app.UseForwardedHeaders();
            app.UseMiddleware<TraceActivityMiddleware>();
            app.UseMiddleware<ExceptionMiddleware>();
            app.UseWebApiSwagger();

            if (!string.IsNullOrWhiteSpace(corsPolicy))
            {
                app.UseCors(corsPolicy);
            }

            if (app.ApplicationServices.GetService<TokenAuthenticationOptions>() is not null)
            {
                app.UseMiddleware<AuthenticationMiddleware>();
            }

            return app;
        }

        /// <summary>Registers each given middleware type on the pipeline, in order.</summary>
        /// <param name="middlewares">The middleware types to register. Each must derive from <see cref="WebApiMiddleware"/>.</param>
        /// <returns>The same <paramref name="app"/> instance, for chaining.</returns>
        /// <exception cref="ArgumentException">A type does not derive from <see cref="WebApiMiddleware"/>.</exception>
        public IApplicationBuilder UseWebApiMiddlewares(params IEnumerable<Type> middlewares)
        {
            ArgumentNullException.ThrowIfNull(middlewares);

            var types = middlewares.ToArray();
            var invalid = types.FirstOrDefault(middleware => !middleware.IsSubclassOf(typeof(WebApiMiddleware)));

            if (invalid is not null)
            {
                throw new ArgumentException(
                    $"{invalid.FullName} does not derive from {nameof(WebApiMiddleware)}.", nameof(middlewares));
            }

            foreach (var middleware in types)
            {
                app.UseMiddleware(middleware);
            }

            return app;
        }

        /// <summary>Serves the Swagger JSON endpoint and UI when the Swagger generator was registered, typically by
        /// <see cref="WebApplicationBuilderExtensions.AddWebApiSwagger"/> in an environment that allows it; otherwise does
        /// nothing, so the generator and the UI can never disagree.</summary>
        /// <returns>The same <paramref name="app"/> instance, for chaining.</returns>
        public IApplicationBuilder UseWebApiSwagger()
        {
            var isService = app.ApplicationServices.GetService<IServiceProviderIsService>();

            if (isService?.IsService(typeof(ISwaggerProvider)) is not true)
            {
                return app;
            }

            app.UseSwagger();
            app.UseSwaggerUI();

            return app;
        }
    }
}
