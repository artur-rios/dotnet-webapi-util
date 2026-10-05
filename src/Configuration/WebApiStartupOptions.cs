using ArturRios.Util.WebApi.Middleware;

namespace ArturRios.Util.WebApi.Configuration;

/// <summary>Options that tune the standard sequence <see cref="WebApiStartup"/> builds the application with.</summary>
public class WebApiStartupOptions
{
    /// <summary>Whether and how Swagger is generated and served.</summary>
    public WebApiSwaggerOptions Swagger { get; } = new();

    /// <summary>Whether invalid model state is answered with a failed <c>DataOutput</c> envelope (see
    /// <see cref="ServiceCollectionExtensions.AddInvalidModelStateEnvelope"/>). Defaults to <see langword="true"/>.</summary>
    public bool UseInvalidModelStateEnvelope { get; set; } = true;

    /// <summary>The name of the CORS policy to apply, registered in <c>ConfigureServices</c> with <c>AddCors</c>.
    /// Applied before authentication so preflight requests pass. <see langword="null"/> (the default) for no CORS.</summary>
    public string? CorsPolicy { get; set; }

    /// <summary>Additional <see cref="WebApiMiddleware"/> types, added in order after the standard middlewares and
    /// before the endpoints.</summary>
    public List<Type> Middlewares { get; } = [];
}
