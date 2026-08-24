using System.Net;
using ArturRios.Configuration.Enums;
using ArturRios.Output;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace ArturRios.Util.WebApi.EndpointToggle;

/// <summary>Action filter attribute that enables or disables a single endpoint, either from a compile-time flag or from
/// a runtime configuration value. When the endpoint is disabled the request is short-circuited before the action runs
/// and the response is shaped according to <see cref="OutputType"/>: an empty status code, the action's default return
/// value, a <see cref="ProcessOutput"/> envelope, or a thrown <see cref="EndpointDisabledException"/>.</summary>
/// <remarks>
/// Two forms are available. The static form (<see cref="EndpointToggleAttribute(bool, HttpStatusCode, OutputType, string)"/>)
/// fixes the toggle at compile time. The configuration form
/// (<see cref="EndpointToggleAttribute(ConfigurationSourceType, string, string, string, HttpStatusCode, OutputType, string)"/>)
/// resolves the toggle on each request from <c>appsettings.json</c> and/or environment variables, so an endpoint can be
/// turned on or off without redeploying.
/// </remarks>
/// <remarks>
/// ASP.NET Core reuses a single filter-attribute instance for every request to the action it decorates, so
/// nothing per-request is kept on the instance: the executing context is threaded through the helpers
/// instead. Keeping it in a field let two concurrent requests overwrite each other's.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public class EndpointToggleAttribute : ActionFilterAttribute
{
    private const string DefaultAppSettingsKeyPrefix = "Endpoints:[Controller]";
    private const string DefaultEnvFileKeyPrefix = "Endpoints_[Controller]";
    private readonly ConfigurationSourceType _configurationSource;
    private readonly string _disabledMessage;
    private readonly OutputType _disabledOutputType;
    private readonly HttpStatusCode _disabledStatusCode;
    private readonly bool _isEnabled;
    private readonly string _key = string.Empty;
    private readonly string _keyPrefix = string.Empty;
    private readonly string _keySeparator = string.Empty;
    private readonly string _keySuffix = string.Empty;
    private readonly bool _useConfigurationFile;

    /// <summary>Creates a toggle whose enabled state is fixed at compile time.</summary>
    /// <param name="isEnabled">Whether the endpoint is enabled. When <c>false</c> the action is short-circuited on every request.</param>
    /// <param name="disabledStatusCode">The HTTP status code returned when the endpoint is disabled. Defaults to 404 Not Found.</param>
    /// <param name="disabledOutputType">How the disabled response is shaped. Defaults to <see cref="OutputType.Object"/>.</param>
    /// <param name="disabledMessage">The message included in the disabled response when <paramref name="disabledOutputType"/> is <see cref="OutputType.Object"/>.</param>
    public EndpointToggleAttribute(
        bool isEnabled = true,
        HttpStatusCode disabledStatusCode = HttpStatusCode.NotFound,
        OutputType disabledOutputType = OutputType.Object,
        string disabledMessage = "This endpoint is currently disabled"
    )
    {
        _isEnabled = isEnabled;
        _disabledStatusCode = disabledStatusCode;
        _disabledMessage = disabledMessage;
        _disabledOutputType = disabledOutputType;
        _useConfigurationFile = false;
    }

    /// <summary>Creates a toggle whose enabled state is resolved on each request from configuration.</summary>
    /// <param name="configurationSource">Where the toggle value is read from: <c>AppSettings</c>, an env file / environment
    /// variables, or both. This also selects the key separator (<c>:</c> for app settings, <c>_</c> otherwise).</param>
    /// <param name="key">The full configuration key to read. When empty, a key is derived from the key prefix, the
    /// controller name, the action name and the optional suffix.</param>
    /// <param name="keyPrefix">The prefix for the derived key. When empty a default is used
    /// (<c>Endpoints:[Controller]</c> for app settings, <c>Endpoints_[Controller]</c> otherwise), with <c>[Controller]</c>
    /// replaced by the current controller name.</param>
    /// <param name="keySuffix">An optional suffix appended to the derived key.</param>
    /// <param name="disabledStatusCode">The HTTP status code returned when the endpoint is disabled. Defaults to 404 Not Found.</param>
    /// <param name="disabledOutputType">How the disabled response is shaped. Defaults to <see cref="OutputType.Object"/>.</param>
    /// <param name="disabledMessage">The message included in the disabled response when <paramref name="disabledOutputType"/> is <see cref="OutputType.Object"/>.</param>
    public EndpointToggleAttribute(
        ConfigurationSourceType configurationSource,
        string key = "",
        string keyPrefix = "",
        string keySuffix = "",
        HttpStatusCode disabledStatusCode = HttpStatusCode.NotFound,
        OutputType disabledOutputType = OutputType.Object,
        string disabledMessage = "This endpoint is currently disabled"
    )
    {
        _configurationSource = configurationSource;
        _key = key;
        _disabledStatusCode = disabledStatusCode;
        _disabledMessage = disabledMessage;
        _disabledOutputType = disabledOutputType;
        _useConfigurationFile = true;
        _keySuffix = keySuffix;
        _keySeparator = configurationSource == ConfigurationSourceType.AppSettings ? ":" : "_";

        if (string.IsNullOrWhiteSpace(keyPrefix))
        {
            _keyPrefix = configurationSource == ConfigurationSourceType.AppSettings
                ? DefaultAppSettingsKeyPrefix
                : DefaultEnvFileKeyPrefix;
        }
        else
        {
            _keyPrefix = keyPrefix;
        }
    }

    /// <summary>The default message describing a disabled endpoint: <c>"This endpoint is currently disabled"</c>.</summary>
    public static string DefaultDisabledMessage => "This endpoint is currently disabled";

    /// <summary>Evaluates the toggle before the action runs and, when the endpoint is disabled, short-circuits the
    /// pipeline with a response shaped according to the configured <see cref="OutputType"/>.</summary>
    /// <param name="context">The executing-action context for the current request.</param>
    /// <exception cref="EndpointDisabledException">Thrown when the endpoint is disabled and its disabled output type is <see cref="OutputType.Exception"/>.</exception>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var isEnabled = _useConfigurationFile ? GetToggleFromFile(context) : _isEnabled;

        if (isEnabled)
        {
            return;
        }

        switch (_disabledOutputType)
        {
            case OutputType.Void:
                context.Result = new StatusCodeResult((int)_disabledStatusCode);
                break;
            case OutputType.Default:
            case OutputType.Primitive:
                ReturnDefault(context);
                break;
            case OutputType.Object:
                ReturnObject(context);
                break;
            case OutputType.Exception:
                throw new EndpointDisabledException([_disabledMessage]);
            default:
                ReturnObject(context);
                break;
        }
    }

    private void ReturnObject(ActionExecutingContext context)
    {
        var output = ProcessOutput.New.WithMessage(_disabledMessage);

        context.Result = new ObjectResult(output) { StatusCode = (int)_disabledStatusCode };
    }

    private void ReturnDefault(ActionExecutingContext context)
    {
        var methodInfo = (context.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo;
        var returnType = methodInfo?.ReturnType;

        if (returnType == null || returnType == typeof(void))
        {
            context.Result = new StatusCodeResult((int)_disabledStatusCode);

            return;
        }

        var defaultObj = returnType.IsValueType ? Activator.CreateInstance(returnType) : null;

        context.Result = new ObjectResult(defaultObj) { StatusCode = (int)_disabledStatusCode };
    }

    private bool GetToggleFromFile(ActionExecutingContext context)
    {
        var toggleKey = string.IsNullOrWhiteSpace(_key) ? GetDefaultKey(context) : _key;

        if (string.IsNullOrWhiteSpace(toggleKey))
        {
            return true;
        }

        return _configurationSource switch
        {
            ConfigurationSourceType.AppSettings => GetToggleFromAppSettings(context, toggleKey) ?? true,
            ConfigurationSourceType.EnvFile or ConfigurationSourceType.EnvironmentVariables =>
                GetToggleFromEnvironmentVariables(toggleKey) ?? true,
            _ => GetToggleFromAppSettings(context, toggleKey) ??
                 GetToggleFromEnvironmentVariables(toggleKey) ?? true
        };
    }

    private static bool? GetToggleFromAppSettings(ActionExecutingContext context, string key)
    {
        if (context.HttpContext.RequestServices.GetService(typeof(IConfiguration)) is not IConfiguration config)
        {
            return null;
        }

        var value = config[key];

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool? GetToggleFromEnvironmentVariables(string key)
    {
        var envValue = Environment.GetEnvironmentVariable(key);

        if (string.IsNullOrEmpty(envValue))
        {
            return null;
        }

        if (bool.TryParse(envValue, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private string? GetDefaultKey(ActionExecutingContext context)
    {
        var methodInfo = (context.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo;
        var methodName = methodInfo?.Name;

        var keyPrefix = GetKeyPrefix(context, _key);

        return methodInfo is not null ? AddKeySuffix($"{keyPrefix}{_keySeparator}{methodName}") : null;
    }

    private static string? GetControllerName(ActionExecutingContext context) =>
        (context.ActionDescriptor as ControllerActionDescriptor)?.ControllerName;

    private string GetKeyPrefix(ActionExecutingContext context, string key)
    {
        if (string.IsNullOrWhiteSpace(_keyPrefix))
        {
            return key;
        }

        var controllerName = GetControllerName(context);

        return controllerName is null
            ? _keyPrefix.Replace($"{_keySeparator}[Controller]", string.Empty)
            : _keyPrefix.Replace("[Controller]", controllerName);
    }

    private string AddKeySuffix(string key) =>
        string.IsNullOrWhiteSpace(_keySuffix) ? key : $"{key}{_keySeparator}{_keySuffix}";
}
