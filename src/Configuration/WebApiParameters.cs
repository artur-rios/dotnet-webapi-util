using ArturRios.Configuration.Enums;
using ArturRios.Extensions;

namespace ArturRios.Util.WebApi.Configuration;

/// <summary>Startup parameters parsed from command-line arguments (e.g. <c>Environment:Production</c>,
/// <c>EnableSwaggerDocs:false</c>), controlling how <see cref="WebApiStartup"/> configures the application.</summary>
public class WebApiParameters
{
    private readonly string[] _defaultSwaggerEnvironments =
        [nameof(EnvironmentType.Development), nameof(EnvironmentType.Local)];

    /// <summary>Parses <paramref name="args"/> into the corresponding properties. Unrecognized or malformed
    /// entries are ignored, leaving the default values in place.</summary>
    /// <param name="args">The command-line arguments, each in <c>Key:Value</c> form.</param>
    public WebApiParameters(string[] args)
    {
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

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            switch (key)
            {
                case "Environment":
                    EnvironmentName = value.IsValidEnumValue<EnvironmentType>() ? value : string.Empty;
                    break;
                case "EnableSwaggerDocs":
                    EnableSwaggerDocs = value.ParseToBoolOrDefault(true)!.Value;
                    break;
                case "UseAppSetting":
                    UseAppSettings = value.ParseToBoolOrDefault(true)!.Value;
                    break;
                case "UseEnvFile":
                    UseEnvFile = value.ParseToBoolOrDefault(true)!.Value;
                    break;
                case "SwaggerEnvironments":
                    if (value.StartsWith('[') && value.EndsWith(']'))
                    {
                        var envs = value.Trim('[', ']').Split(
                            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        SwaggerEnvironments = envs;
                    }

                    break;
            }
        }
    }

    /// <summary>The environment name (e.g. <c>Development</c>, <c>Production</c>), if a valid one was supplied.</summary>
    public string EnvironmentName { get; set; } = string.Empty;

    /// <summary>Whether <c>appsettings.json</c> should be loaded. Defaults to <c>true</c>.</summary>
    public bool UseAppSettings { get; set; } = true;

    /// <summary>Whether a <c>.env</c> file should be loaded. Defaults to <c>true</c>.</summary>
    public bool UseEnvFile { get; set; } = true;

    /// <summary>Whether Swagger generation/UI should be enabled. Defaults to <c>true</c>.</summary>
    public bool EnableSwaggerDocs { get; set; } = true;

    /// <summary>The environment names in which Swagger should be enabled, as parsed from the <c>SwaggerEnvironments</c> argument.</summary>
    public string[] SwaggerEnvironments { get; set; } = [];

    /// <summary>Returns the configured Swagger environments, falling back to <c>Development</c> and <c>Local</c>
    /// when none were supplied or none are valid.</summary>
    public string[] GetSwaggerEnvironments()
    {
        if (SwaggerEnvironments.IsEmpty())
        {
            return _defaultSwaggerEnvironments;
        }

        var validEnvs = SwaggerEnvironments.Where(env => env.IsValidEnumValue<EnvironmentType>()).ToArray();

        return validEnvs.IsNotEmpty() ? validEnvs : _defaultSwaggerEnvironments;
    }
}
