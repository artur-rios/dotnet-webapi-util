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

namespace ArturRios.Util.WebApi.Tests.EndpointToggle;

/// <summary>
/// ASP.NET Core reuses one filter-attribute instance for every request to the action it decorates. The
/// attribute used to stash the executing context in an instance field, so two concurrent requests
/// overwrote each other's and one could end up writing its result onto the other's context.
/// </summary>
[Trait("Category", "Unit")]
public class EndpointToggleConcurrencyTests
{
    private sealed class SampleController
    {
        public string First() => string.Empty;

        public string Second() => string.Empty;
    }

    private static ActionExecutingContext ContextFor(string actionName)
    {
        var descriptor = new ControllerActionDescriptor
        {
            ControllerName = nameof(SampleController),
            ActionName = actionName,
            MethodInfo = typeof(SampleController).GetMethod(actionName)!
        };

        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor);

        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), controller: null!);
    }

    [Fact]
    public void GivenOneAttributeSharedByManyConcurrentRequests_WhenTheyExecute_ThenEachResultLandsOnItsOwnContext()
    {
        var attribute = new EndpointToggleAttribute(isEnabled: false, HttpStatusCode.ServiceUnavailable);

        var contexts = Enumerable.Range(0, 200)
            .Select(i => ContextFor(i % 2 == 0 ? nameof(SampleController.First) : nameof(SampleController.Second)))
            .ToArray();

        Parallel.ForEach(contexts, context => attribute.OnActionExecuting(context));

        Assert.All(contexts, context =>
        {
            var result = Assert.IsType<ObjectResult>(context.Result);

            Assert.Equal((int)HttpStatusCode.ServiceUnavailable, result.StatusCode);
        });
    }

    [Fact]
    public void GivenTheConfigurationForm_WhenManyConcurrentRequestsExecute_ThenEveryOneIsShortCircuited()
    {
        var key = "ARTURRIOS_TOGGLE_" + Guid.NewGuid().ToString("N");

        Environment.SetEnvironmentVariable(key, "false");

        try
        {
            var attribute = new EndpointToggleAttribute(ConfigurationSourceType.EnvironmentVariables, key);

            var contexts = Enumerable.Range(0, 200).Select(_ => ContextFor(nameof(SampleController.First))).ToArray();

            Parallel.ForEach(contexts, context => attribute.OnActionExecuting(context));

            Assert.All(contexts, context => Assert.IsType<ObjectResult>(context.Result));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void GivenACustomDisabledMessage_WhenTheExceptionOutputIsUsed_ThenTheExceptionCarriesThatMessage()
    {
        const string message = "This endpoint is retired.";

        var attribute = new EndpointToggleAttribute(
            isEnabled: false,
            HttpStatusCode.NotFound,
            OutputType.Exception,
            message);

        var exception = Assert.Throws<EndpointDisabledException>(
            () => attribute.OnActionExecuting(ContextFor(nameof(SampleController.First))));

        Assert.Equal(new[] { message }, exception.Messages);
    }

    [Fact]
    public void GivenACustomDisabledMessage_WhenTheObjectOutputIsUsed_ThenTheEnvelopeCarriesThatMessage()
    {
        const string message = "This endpoint is retired.";

        var attribute = new EndpointToggleAttribute(
            isEnabled: false,
            HttpStatusCode.NotFound,
            OutputType.Object,
            message);

        var context = ContextFor(nameof(SampleController.First));

        attribute.OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        var output = Assert.IsType<ProcessOutput>(result.Value);

        Assert.Contains(message, output.Messages);
    }

    [Fact]
    public void GivenNoContext_WhenExecuting_ThenArgumentNullExceptionIsThrown()
    {
        var attribute = new EndpointToggleAttribute(isEnabled: false);

        Assert.Throws<ArgumentNullException>(() => attribute.OnActionExecuting(null!));
    }
}
