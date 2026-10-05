using ArturRios.Configuration.Enums;
using ArturRios.Extensions;
using Microsoft.AspNetCore.Builder;

namespace ArturRios.Util.WebApi.Configuration;

/// <summary>Startup parameters parsed from command-line arguments (e.g. <c>Environment:Production</c>,
/// <c>EnableSwaggerDocs:false</c>), controlling how the application is configured by <see cref="WebApiStartup"/> or
/// by the <see cref="WebApplicationBuilderExtensions"/> it is built from.</summary>
public class WebApiParameters
{
    private static readonly string[] DefaultSwaggerEnvironments =
        [nameof(EnvironmentType.Development), nameof(EnvironmentType.Local)];

    /// <summary>Parses <paramref name="args"/> into the corresponding properties. Keys are case-insensitive;
    /// unrecognized or malformed entries are ignored, leaving the default values in place.</summary>
    /// <param name="args">The command-line arguments, each in <c>Key:Value</c> form.</param>
    public WebApiParameters(string[] args)
    {
        Args = args;

        if (args.IsEmpty())
        {
            return;
        }

        foreach (var arg in args)
        {
            var parts = arg.Split(':', 2, StringSplitOptions.TrimEntries);

            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0];
            var value = parts[1];

            switch (key.ToLowerInvariant())
            {
                case "environment":
                    EnvironmentName = value.IsValidEnumValue<EnvironmentType>() ? value : string.Empty;
                    break;
                case "enableswaggerdocs":
                    EnableSwaggerDocs = value.ParseToBoolOrDefault(true)!.Value;
                    break;
                case "useappsettings":
                case "useappsetting":
                    UseAppSettings = value.ParseToBoolOrDefault(true)!.Value;
                    break;
                case "useenvfile":
                    UseEnvFile = value.ParseToBoolOrDefault(true)!.Value;
                    break;
                case "swaggerenvironments":
                    if (value.StartsWith('[') && value.EndsWith(']'))
                    {
                        SwaggerEnvironments = value.Trim('[', ']').Split(
                            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    }

                    break;
            }
        }
    }

    /// <summary>The command-line arguments these parameters were parsed from.</summary>
    public string[] Args { get; }

    /// <summary>The environment name (e.g. <c>Development</c>, <c>Production</c>), if a valid one was supplied.
    /// When set, <see cref="ToWebApplicationOptions"/> makes it the host environment, overriding
    /// <c>ASPNETCORE_ENVIRONMENT</c>.</summary>
    public string EnvironmentName { get; set; } = string.Empty;

    /// <summary>Whether <c>appsettings.json</c> should be loaded. Defaults to <c>true</c>. Parsed from the
    /// <c>UseAppSettings</c> argument (or its legacy spelling, <c>UseAppSetting</c>).</summary>
    public bool UseAppSettings { get; set; } = true;

    /// <summary>Whether a <c>.env</c> file should be loaded. Defaults to <c>true</c>.</summary>
    public bool UseEnvFile { get; set; } = true;

    /// <summary>Parsed from the <c>EnableSwaggerDocs</c> argument. When <c>false</c>, Swagger is neither generated
    /// nor served in any environment, whatever <see cref="SwaggerEnvironments"/> says (see
    /// <see cref="IsSwaggerAllowed"/>). Defaults to <c>true</c>, leaving Swagger gated by environment.</summary>
    public bool EnableSwaggerDocs { get; set; } = true;

    /// <summary>The environment names in which Swagger should be enabled, as parsed from the <c>SwaggerEnvironments</c> argument.</summary>
    public string[] SwaggerEnvironments { get; set; } = [];

    /// <summary>Returns the configured Swagger environments, falling back to <c>Development</c> and <c>Local</c>
    /// when none were supplied or none are valid.</summary>
    public string[] GetSwaggerEnvironments()
    {
        if (SwaggerEnvironments.IsEmpty())
        {
            return [.. DefaultSwaggerEnvironments];
        }

        var validEnvs = SwaggerEnvironments.Where(env => env.IsValidEnumValue<EnvironmentType>()).ToArray();

        return validEnvs.IsNotEmpty() ? validEnvs : [.. DefaultSwaggerEnvironments];
    }

    /// <summary>Whether Swagger is allowed in <paramref name="environmentName"/>. Never when the
    /// <c>EnableSwaggerDocs:false</c> argument was supplied; otherwise when the environment is in
    /// <paramref name="allowedEnvironments"/>, if non-empty, or else in <see cref="GetSwaggerEnvironments"/>.</summary>
    /// <param name="environmentName">The current host environment name.</param>
    /// <param name="allowedEnvironments">Optional explicit list of environments in which Swagger is allowed.</param>
    public bool IsSwaggerAllowed(string environmentName, EnvironmentType[]? allowedEnvironments = null)
    {
        if (!EnableSwaggerDocs)
        {
            return false;
        }

        var environments = allowedEnvironments.IsNotEmpty()
            ? allowedEnvironments!.Select(env => env.ToString())
            : GetSwaggerEnvironments();

        return environments.Contains(environmentName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Builds the <see cref="WebApplicationOptions"/> for <c>WebApplication.CreateBuilder</c>: the
    /// original <see cref="Args"/>, and <see cref="EnvironmentName"/> as the environment when one was supplied.</summary>
    public WebApplicationOptions ToWebApplicationOptions() =>
        new()
        {
            Args = Args,
            EnvironmentName = string.IsNullOrWhiteSpace(EnvironmentName) ? null : EnvironmentName
        };
}
