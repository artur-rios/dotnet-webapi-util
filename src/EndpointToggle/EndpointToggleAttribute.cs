using System.Net;
using System.Reflection;
using ArturRios.Configuration.Enums;
using ArturRios.Output;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
    private const string ControllerPlaceholder = "[Controller]";
    private const string DefaultAppSettingsKeyPrefix = "Endpoints:" + ControllerPlaceholder;
    private const string DefaultEnvFileKeyPrefix = "Endpoints_" + ControllerPlaceholder;
    private const string DisabledMessage = "This endpoint is currently disabled";
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
        string disabledMessage = DisabledMessage
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
        string disabledMessage = DisabledMessage
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
    public static string DefaultDisabledMessage => DisabledMessage;

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
                throw new EndpointDisabledException([_disabledMessage], (int)_disabledStatusCode);
            default:
                ReturnObject(context);
                break;
        }
    }

    private void ReturnObject(ActionExecutingContext context)
    {
        // An error, not a message: Success is computed from Errors, so the disabled text under Messages
        // came back as a 404 whose envelope said "success": true.
        var output = ProcessOutput.New.WithError(_disabledMessage);

        context.Result = new ObjectResult(output) { StatusCode = (int)_disabledStatusCode };
    }

    private void ReturnDefault(ActionExecutingContext context)
    {
        var returnType = UnwrapReturnType(GetMethodInfo(context)?.ReturnType);

        if (returnType is null)
        {
            context.Result = new StatusCodeResult((int)_disabledStatusCode);

            return;
        }

        var defaultObj = returnType.IsValueType ? Activator.CreateInstance(returnType) : null;

        context.Result = new ObjectResult(defaultObj) { StatusCode = (int)_disabledStatusCode };
    }

    /// <summary>The type whose default the action would have produced: the <c>T</c> of <see cref="Task{TResult}"/>,
    /// <see cref="ValueTask{TResult}"/> or <see cref="ActionResult{TValue}"/>, or <see langword="null"/> when the
    /// action produces no value (<c>void</c>, <see cref="Task"/>, <see cref="ValueTask"/>).</summary>
    private static Type? UnwrapReturnType(Type? returnType)
    {
        if (returnType is null || returnType == typeof(void) || returnType == typeof(Task) ||
            returnType == typeof(ValueTask))
        {
            return null;
        }

        if (returnType.IsGenericType)
        {
            var definition = returnType.GetGenericTypeDefinition();

            if (definition == typeof(Task<>) || definition == typeof(ValueTask<>) ||
                definition == typeof(ActionResult<>))
            {
                return UnwrapReturnType(returnType.GetGenericArguments()[0]);
            }
        }

        return returnType;
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
        var config = context.HttpContext.RequestServices?.GetService<IConfiguration>();

        return ParseToggle(config?[key]);
    }

    private static bool? GetToggleFromEnvironmentVariables(string key) =>
        ParseToggle(Environment.GetEnvironmentVariable(key));

    private static bool? ParseToggle(string? value) => bool.TryParse(value, out var parsed) ? parsed : null;

    private string? GetDefaultKey(ActionExecutingContext context)
    {
        var methodInfo = GetMethodInfo(context);

        if (methodInfo is null)
        {
            return null;
        }

        var controllerName = (context.ActionDescriptor as ControllerActionDescriptor)?.ControllerName;

        var keyPrefix = controllerName is null
            ? _keyPrefix.Replace($"{_keySeparator}{ControllerPlaceholder}", string.Empty)
            : _keyPrefix.Replace(ControllerPlaceholder, controllerName);

        var key = $"{keyPrefix}{_keySeparator}{methodInfo.Name}";

        return string.IsNullOrWhiteSpace(_keySuffix) ? key : $"{key}{_keySeparator}{_keySuffix}";
    }

    private static MethodInfo? GetMethodInfo(ActionExecutingContext context) =>
        (context.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo;
}
