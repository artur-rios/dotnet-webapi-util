using System.Net;
using ArturRios.Configuration.Enums;
using ArturRios.Output;
using ArturRios.Util.WebApi.EndpointToggle;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.EndpointToggle;

[Trait("Category", "Unit")]
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
    public void GivenDisabledToggleWithObjectOutput_WhenActionExecuting_ThenReturnsAFailedProcessOutputWithTheMessageAndStatusCode()
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
        Assert.Contains("Temporarily off", output.Errors);
        Assert.False(output.Success);
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

    [Fact]
    public void GivenDisabledToggleWithExceptionOutput_WhenActionExecuting_ThenTheExceptionCarriesTheDisabledStatusCode()
    {
        var context = BuildContext(nameof(SampleController.IntAction));
        var attribute = new EndpointToggleAttribute(
            isEnabled: false,
            disabledStatusCode: HttpStatusCode.ServiceUnavailable,
            disabledOutputType: OutputType.Exception);

        var exception = Assert.Throws<EndpointDisabledException>(() => attribute.OnActionExecuting(context));
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    [Theory]
    [InlineData(nameof(SampleController.TaskIntAction))]
    [InlineData(nameof(SampleController.ValueTaskIntAction))]
    [InlineData(nameof(SampleController.ActionResultIntAction))]
    [InlineData(nameof(SampleController.TaskActionResultIntAction))]
    public void GivenDisabledToggleWithDefaultOutputAndAWrappedValueTypeReturn_WhenActionExecuting_ThenReturnsTheWrappedTypesDefault(
        string actionName)
    {
        var context = BuildContext(actionName);
        var attribute = new EndpointToggleAttribute(isEnabled: false, disabledOutputType: OutputType.Default);

        attribute.OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(0, result.Value);
    }

    [Theory]
    [InlineData(nameof(SampleController.TaskAction))]
    [InlineData(nameof(SampleController.ValueTaskAction))]
    public void GivenDisabledToggleWithDefaultOutputAndAnAsyncVoidReturn_WhenActionExecuting_ThenReturnsStatusCodeResult(
        string actionName)
    {
        var context = BuildContext(actionName);
        var attribute = new EndpointToggleAttribute(isEnabled: false, disabledOutputType: OutputType.Default);

        attribute.OnActionExecuting(context);

        var result = Assert.IsType<StatusCodeResult>(context.Result);
        Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
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

    // ---- Further shapes, sources and keys ----

    [Fact]
    public void GivenTheDefaultDisabledMessage_WhenRead_ThenItSaysTheEndpointIsDisabled()
    {
        Assert.Equal("This endpoint is currently disabled", EndpointToggleAttribute.DefaultDisabledMessage);
    }

    [Fact]
    public void GivenADisabledToggleWithTheDefaults_WhenActionExecuting_ThenA404EnvelopeCarriesTheDefaultMessage()
    {
        var context = BuildContext(nameof(SampleController.IntAction));

        new EndpointToggleAttribute(isEnabled: false).OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal([EndpointToggleAttribute.DefaultDisabledMessage], Assert.IsType<ProcessOutput>(result.Value).Errors);
    }

    [Fact]
    public void GivenDisabledToggleWithPrimitiveOutput_WhenActionExecuting_ThenItAnswersLikeTheDefaultOutput()
    {
        var context = BuildContext(nameof(SampleController.IntAction));

        new EndpointToggleAttribute(isEnabled: false, HttpStatusCode.Gone, OutputType.Primitive).OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal((int)HttpStatusCode.Gone, result.StatusCode);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void GivenDisabledToggleWithAnUndefinedOutputType_WhenActionExecuting_ThenItFallsBackToTheEnvelope()
    {
        var context = BuildContext(nameof(SampleController.IntAction));

        new EndpointToggleAttribute(isEnabled: false, HttpStatusCode.NotFound, (OutputType)99, "off")
            .OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal(["off"], Assert.IsType<ProcessOutput>(result.Value).Errors);
    }

    [Fact]
    public void GivenDisabledToggleWithDefaultOutputOnANonControllerAction_WhenActionExecuting_ThenAnEmptyStatusCodeComesBack()
    {
        var context = NonControllerContext(configuration: null);

        new EndpointToggleAttribute(isEnabled: false, HttpStatusCode.Gone, OutputType.Default).OnActionExecuting(context);

        Assert.Equal((int)HttpStatusCode.Gone, Assert.IsType<StatusCodeResult>(context.Result).StatusCode);
    }

    [Fact]
    public void GivenAConfigurationToggleWithNoKeyOnANonControllerAction_WhenActionExecuting_ThenNoKeyCanBeDerivedAndTheEndpointIsEnabled()
    {
        var context = NonControllerContext(BuildConfiguration(("Endpoints", "false")));

        new EndpointToggleAttribute(ConfigurationSourceType.AppSettings).OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void GivenACustomKeyPrefix_WhenActionExecuting_ThenTheKeyIsDerivedFromIt()
    {
        var context = BuildContext(nameof(SampleController.IntAction),
            BuildConfiguration(($"Features:{nameof(SampleController.IntAction)}", "false")));

        new EndpointToggleAttribute(ConfigurationSourceType.AppSettings, keyPrefix: "Features").OnActionExecuting(context);

        Assert.NotNull(context.Result);
    }

    [Fact]
    public void GivenACustomKeyPrefixWithTheControllerPlaceholder_WhenActionExecuting_ThenTheControllerNameIsFilledIn()
    {
        var context = BuildContext(nameof(SampleController.IntAction),
            BuildConfiguration(($"Toggles:{ControllerName}:{nameof(SampleController.IntAction)}", "false")));

        new EndpointToggleAttribute(ConfigurationSourceType.AppSettings, keyPrefix: "Toggles:[Controller]")
            .OnActionExecuting(context);

        Assert.NotNull(context.Result);
    }

    [Fact]
    public void GivenAnActionWithNoControllerName_WhenTheKeyIsDerived_ThenTheControllerSegmentIsLeftOut()
    {
        var context = BuildContext(nameof(SampleController.IntAction),
            BuildConfiguration(($"Endpoints:{nameof(SampleController.IntAction)}", "false")));
        ((ControllerActionDescriptor)context.ActionDescriptor).ControllerName = null!;

        new EndpointToggleAttribute(ConfigurationSourceType.AppSettings).OnActionExecuting(context);

        Assert.NotNull(context.Result);
    }

    [Fact]
    public void GivenNoRequestServices_WhenReadingAnAppSettingsToggle_ThenTheEndpointIsEnabled()
    {
        var context = BuildContext(nameof(SampleController.IntAction));
        context.HttpContext.RequestServices = null!;

        new EndpointToggleAttribute(ConfigurationSourceType.AppSettings, key: "Features:Toggle").OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void GivenTheEnvFileSourceAndADerivedKey_WhenTheEnvironmentVariableIsFalse_ThenTheEndpointIsDisabled()
    {
        // The env file is loaded into environment variables; the derived key uses '_' as its separator.
        var suffix = Guid.NewGuid().ToString("N");
        var key = $"Endpoints_{ControllerName}_{nameof(SampleController.IntAction)}_{suffix}";
        Environment.SetEnvironmentVariable(key, "false");

        try
        {
            var context = BuildContext(nameof(SampleController.IntAction),
                BuildConfiguration(($"Endpoints:{ControllerName}:{nameof(SampleController.IntAction)}:{suffix}", "true")));

            new EndpointToggleAttribute(ConfigurationSourceType.EnvFile, keySuffix: suffix).OnActionExecuting(context);

            Assert.NotNull(context.Result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void GivenAnUndefinedSourceAndTheAppSettingsValue_WhenActionExecuting_ThenAppSettingsAreReadFirst()
    {
        var key = $"ENDPOINT_TOGGLE_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(key, "true");

        try
        {
            var context = BuildContext(nameof(SampleController.IntAction), BuildConfiguration((key, "false")));

            new EndpointToggleAttribute((ConfigurationSourceType)99, key: key).OnActionExecuting(context);

            Assert.NotNull(context.Result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void GivenAnUndefinedSourceAndOnlyAnEnvironmentVariable_WhenActionExecuting_ThenTheEnvironmentVariableIsRead()
    {
        var key = $"ENDPOINT_TOGGLE_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(key, "false");

        try
        {
            var context = BuildContext(nameof(SampleController.IntAction), BuildConfiguration());

            new EndpointToggleAttribute((ConfigurationSourceType)99, key: key).OnActionExecuting(context);

            Assert.NotNull(context.Result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void GivenAnUndefinedSourceAndNoValueAnywhere_WhenActionExecuting_ThenTheEndpointIsEnabled()
    {
        var context = BuildContext(nameof(SampleController.IntAction), BuildConfiguration());

        new EndpointToggleAttribute((ConfigurationSourceType)99, key: $"ENDPOINT_TOGGLE_{Guid.NewGuid():N}")
            .OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void GivenANullContext_WhenActionExecuting_ThenItThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new EndpointToggleAttribute().OnActionExecuting(null!));
    }

    // ---- Helpers ----

    private static ActionExecutingContext NonControllerContext(IConfiguration? configuration)
    {
        var services = new ServiceCollection();

        if (configuration is not null)
        {
            services.AddSingleton(configuration);
        }

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(),
            controller: new object());
    }

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

        public Task<int> TaskIntAction() => Task.FromResult(0);

        public ValueTask<int> ValueTaskIntAction() => ValueTask.FromResult(0);

        public ActionResult<int> ActionResultIntAction() => 0;

        public Task<ActionResult<int>> TaskActionResultIntAction() => Task.FromResult<ActionResult<int>>(0);

        public Task TaskAction() => Task.CompletedTask;

        public ValueTask ValueTaskAction() => ValueTask.CompletedTask;
    }
}
