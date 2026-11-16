using ArturRios.Output;
using ArturRios.Util.WebApi.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArturRios.Util.WebApi.Tests.Configuration;

[Trait("Category", "Unit")]
public class ServiceCollectionExtensionsTests
{
    private static IActionResult Respond(Action<ActionContext> invalidate)
    {
        var services = new ServiceCollection();
        Assert.Same(services, services.AddInvalidModelStateEnvelope());

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value.InvalidModelStateResponseFactory;
        var context = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());

        invalidate(context);

        return factory(context);
    }

    [Fact]
    public void GivenAnInvalidModel_WhenTheResponseIsMade_ThenA400EnvelopeListsEachInvalidParameterWithItsFirstError()
    {
        var result = Respond(context =>
        {
            context.ModelState.AddModelError("Name", "The Name field is required.");
            context.ModelState.AddModelError("Name", "A second error on Name.");
            context.ModelState.AddModelError("Age", "Age must be positive.");
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);

        var output = Assert.IsType<DataOutput<string>>(badRequest.Value);
        Assert.False(output.Success);
        Assert.Equal(string.Empty, output.Data);
        Assert.Equal(2, output.Errors.Count);
        Assert.Contains("Parameter: Name | Error: The Name field is required.", output.Errors);
        Assert.Contains("Parameter: Age | Error: Age must be positive.", output.Errors);
    }

    [Fact]
    public void GivenAParameterThatHasAValueButNoError_WhenTheResponseIsMade_ThenItIsNotListed()
    {
        var result = Respond(context =>
        {
            context.ModelState.SetModelValue("Valid", "value", "value");
            context.ModelState.AddModelError("Name", "The Name field is required.");
        });

        var output = Assert.IsType<DataOutput<string>>(Assert.IsType<BadRequestObjectResult>(result).Value);
        Assert.Equal(["Parameter: Name | Error: The Name field is required."], output.Errors);
    }
}
