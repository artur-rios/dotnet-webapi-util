using ArturRios.Configuration.Enums;
using ArturRios.Extensions;

namespace ArturRios.Util.WebApi.Api.Configuration;

public class WebApiParameters
{
    private readonly string[] _defaultSwaggerEnvironments =
        [nameof(EnvironmentType.Development), nameof(EnvironmentType.Local)];

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

    public string EnvironmentName { get; set; } = string.Empty;
    public bool UseAppSettings { get; set; } = true;
    public bool UseEnvFile { get; set; } = true;
    public bool EnableSwaggerDocs { get; set; } = true;
    public string[] SwaggerEnvironments { get; set; } = [];

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
