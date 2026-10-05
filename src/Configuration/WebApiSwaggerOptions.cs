using ArturRios.Configuration.Enums;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ArturRios.Util.WebApi.Configuration;

/// <summary>Options for <see cref="WebApplicationBuilderExtensions.AddWebApiSwagger"/>.</summary>
public class WebApiSwaggerOptions
{
    /// <summary>The environments in which Swagger is generated and served. When empty, the
    /// <c>SwaggerEnvironments</c> argument decides, falling back to <c>Development</c> and <c>Local</c>
    /// (see <see cref="WebApiParameters.IsSwaggerAllowed"/>).</summary>
    public EnvironmentType[] AllowedEnvironments { get; set; } = [];

    /// <summary>Whether the generated document declares a JWT bearer security scheme, so Swagger UI offers an
    /// "Authorize" button. Defaults to <see langword="false"/>.</summary>
    public bool JwtAuthentication { get; set; }

    /// <summary>Optional callback to further configure the Swagger generator (documents, filters, XML comments).</summary>
    public Action<SwaggerGenOptions>? ConfigureGenerator { get; set; }
}
