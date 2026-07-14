using System.Net;
using ArturRios.Configuration.Enums;
using ArturRios.Output;
using ArturRios.Util.WebApi.EndpointToggle;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.EndpointToggle;

public class EndpointToggleAttributeTests
{
    private const string ControllerName = "Samples";

    // ---- Static toggle ----

    [Fact]
    public void GivenEnabledToggle_WhenActionExecuting_ThenResultIsNotSet()
    {
        var context = BuildContext(nameof(SampleController.IntAction));
        var attribute = new EndpointToggleAttribute(isEnabled: true);

        attribute.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void GivenDisabledToggleWithVoidOutput_WhenActionExecuting_ThenReturnsStatusCodeResultWithDisabledStatusCode()
    {
        var context = BuildContext(nameof(SampleController.IntAction));
        var attribute = new EndpointToggleAttribute(
            isEnabled: false,
            disabledStatusCode: HttpStatusCode.ServiceUnavailable,
            disabledOutputType: OutputType.Void);

        attribute.OnActionExecuting(context);

        var result = Assert.IsType<StatusCodeResult>(context.Result);
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, result.StatusCode);
    }

    [Fact]
    public void GivenDisabledToggleWithObjectOutput_WhenActionExecuting_ThenReturnsProcessOutputWithMessageAndStatusCode()
    {
        var context = BuildContext(nameof(SampleController.IntAction));
        var attribute = new EndpointToggleAttribute(
            isEnabled: false,
            disabledStatusCode: HttpStatusCode.Forbidden,
            disabledOutputType: OutputType.Object,
            disabledMessage: "Temporarily off");

        attribute.OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        var output = Assert.IsType<ProcessOutput>(result.Value);
        Assert.Contains("Temporarily off", output.Messages);
    }

    [Fact]
    public void GivenDisabledToggleWithDefaultOutputAndValueTypeReturn_WhenActionExecuting_ThenReturnsDefaultValueWithDisabledStatusCode()
    {
        var context = BuildContext(nameof(SampleController.IntAction));
        var attribute = new EndpointToggleAttribute(isEnabled: false, disabledOutputType: OutputType.Default);

        attribute.OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void GivenDisabledToggleWithDefaultOutputAndReferenceTypeReturn_WhenActionExecuting_ThenReturnsNullValueWithDisabledStatusCode()
    {
        var context = BuildContext(nameof(SampleController.StringAction));
        var attribute = new EndpointToggleAttribute(isEnabled: false, disabledOutputType: OutputType.Default);

        attribute.OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public void GivenDisabledToggleWithDefaultOutputAndVoidReturn_WhenActionExecuting_ThenReturnsStatusCodeResultWithDisabledStatusCode()
    {
        var context = BuildContext(nameof(SampleController.VoidAction));
        var attribute = new EndpointToggleAttribute(isEnabled: false, disabledOutputType: OutputType.Default);

        attribute.OnActionExecuting(context);

        var result = Assert.IsType<StatusCodeResult>(context.Result);
        Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
    }

    [Fact]
    public void GivenDisabledToggleWithDefaultOutputAndCustomStatusCode_WhenActionExecuting_ThenHonorsDisabledStatusCode()
    {
        var context = BuildContext(nameof(SampleController.IntAction));
        var attribute = new EndpointToggleAttribute(
            isEnabled: false,
            disabledStatusCode: HttpStatusCode.ServiceUnavailable,
            disabledOutputType: OutputType.Default);

        attribute.OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, result.StatusCode);
    }

    [Fact]
    public void GivenDisabledToggleWithExceptionOutput_WhenActionExecuting_ThenThrowsEndpointDisabledException()
    {
        var context = BuildContext(nameof(SampleController.IntAction));
        var attribute = new EndpointToggleAttribute(isEnabled: false, disabledOutputType: OutputType.Exception);

        var exception = Assert.Throws<EndpointDisabledException>(() => attribute.OnActionExecuting(context));
        Assert.Contains(EndpointToggleAttribute.DefaultDisabledMessage, exception.Messages);
    }

    // ---- App settings toggle ----

    [Fact]
    public void GivenAppSettingsKeySetToTrue_WhenActionExecuting_ThenEndpointIsEnabled()
    {
        var context = BuildContext(
            nameof(SampleController.IntAction),
            configuration: BuildConfiguration(("Features:Toggle", "true")));
        var attribute = new EndpointToggleAttribute(ConfigurationSourceType.AppSettings, key: "Features:Toggle");

        attribute.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void GivenAppSettingsKeySetToFalse_WhenActionExecuting_ThenEndpointIsDisabled()
    {
        var context = BuildContext(
            nameof(SampleController.IntAction),
            configuration: BuildConfiguration(("Features:Toggle", "false")));
        var attribute = new EndpointToggleAttribute(ConfigurationSourceType.AppSettings, key: "Features:Toggle");

        attribute.OnActionExecuting(context);

        Assert.NotNull(context.Result);
    }

    [Fact]
    public void GivenAbsentAppSettingsKey_WhenActionExecuting_ThenEndpointIsEnabled()
    {
        var context = BuildContext(
            nameof(SampleController.IntAction),
            configuration: BuildConfiguration());
        var attribute = new EndpointToggleAttribute(ConfigurationSourceType.AppSettings, key: "Features:Missing");

        attribute.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void GivenNonBooleanAppSettingsValue_WhenActionExecuting_ThenEndpointIsEnabled()
    {
        var context = BuildContext(
            nameof(SampleController.IntAction),
            configuration: BuildConfiguration(("Features:Toggle", "not-a-bool")));
        var attribute = new EndpointToggleAttribute(ConfigurationSourceType.AppSettings, key: "Features:Toggle");

        attribute.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void GivenNoConfigurationService_WhenActionExecuting_ThenEndpointIsEnabled()
    {
        var context = BuildContext(nameof(SampleController.IntAction), configuration: null);
        var attribute = new EndpointToggleAttribute(ConfigurationSourceType.AppSettings, key: "Features:Toggle");

        attribute.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void GivenDerivedAppSettingsKeyFromControllerAndAction_WhenActionExecuting_ThenEndpointIsDisabled()
    {
        // No explicit key: the key is derived as "Endpoints:<Controller>:<Action>".
        var context = BuildContext(
            nameof(SampleController.IntAction),
            configuration: BuildConfiguration(($"Endpoints:{ControllerName}:{nameof(SampleController.IntAction)}", "false")));
        var attribute = new EndpointToggleAttribute(ConfigurationSourceType.AppSettings);

        attribute.OnActionExecuting(context);

        Assert.NotNull(context.Result);
    }

    [Fact]
    public void GivenDerivedAppSettingsKeyWithSuffix_WhenActionExecuting_ThenEndpointIsDisabled()
    {
        var context = BuildContext(
            nameof(SampleController.IntAction),
            configuration: BuildConfiguration(
                ($"Endpoints:{ControllerName}:{nameof(SampleController.IntAction)}:v2", "false")));
        var attribute = new EndpointToggleAttribute(ConfigurationSourceType.AppSettings, keySuffix: "v2");

        attribute.OnActionExecuting(context);

        Assert.NotNull(context.Result);
    }

    // ---- Environment variable toggle ----

    [Fact]
    public void GivenEnvironmentVariableSetToFalse_WhenActionExecuting_ThenEndpointIsDisabled()
    {
        var key = $"ENDPOINT_TOGGLE_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(key, "false");

        try
        {
            var context = BuildContext(nameof(SampleController.IntAction));
            var attribute = new EndpointToggleAttribute(ConfigurationSourceType.EnvironmentVariables, key: key);

            attribute.OnActionExecuting(context);

            Assert.NotNull(context.Result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void GivenEnvironmentVariableSetToTrue_WhenActionExecuting_ThenEndpointIsEnabled()
    {
        var key = $"ENDPOINT_TOGGLE_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(key, "true");

        try
        {
            var context = BuildContext(nameof(SampleController.IntAction));
            var attribute = new EndpointToggleAttribute(ConfigurationSourceType.EnvironmentVariables, key: key);

            attribute.OnActionExecuting(context);

            Assert.Null(context.Result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void GivenUnsetEnvironmentVariable_WhenActionExecuting_ThenEndpointIsEnabled()
    {
        var key = $"ENDPOINT_TOGGLE_{Guid.NewGuid():N}";
        var context = BuildContext(nameof(SampleController.IntAction));
        var attribute = new EndpointToggleAttribute(ConfigurationSourceType.EnvironmentVariables, key: key);

        attribute.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void GivenNonBooleanEnvironmentVariable_WhenActionExecuting_ThenEndpointIsEnabled()
    {
        var key = $"ENDPOINT_TOGGLE_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(key, "not-a-bool");

        try
        {
            var context = BuildContext(nameof(SampleController.IntAction));
            var attribute = new EndpointToggleAttribute(ConfigurationSourceType.EnvironmentVariables, key: key);

            attribute.OnActionExecuting(context);

            Assert.Null(context.Result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    // ---- Helpers ----

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static ActionExecutingContext BuildContext(string actionName, IConfiguration? configuration = null)
    {
        var httpContext = new DefaultHttpContext();

        var services = new ServiceCollection();

        if (configuration is not null)
        {
            services.AddSingleton(configuration);
        }

        httpContext.RequestServices = services.BuildServiceProvider();

        var methodInfo = typeof(SampleController).GetMethod(actionName)!;

        var descriptor = new ControllerActionDescriptor
        {
            MethodInfo = methodInfo,
            ControllerName = ControllerName
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), descriptor);

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object());
    }

    private class SampleController
    {
        public int IntAction() => 0;

        public string StringAction() => string.Empty;

        public void VoidAction()
        {
        }
    }
}
