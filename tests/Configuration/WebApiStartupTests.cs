using ArturRios.Configuration.Enums;
using ArturRios.Util.WebApi.Configuration;
using ArturRios.Util.WebApi.Middleware;
using ArturRios.Configuration.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Configuration;

[Trait("Category", "Unit")]
public class WebApiStartupTests
{
    private sealed class TestStartup(string[] args) : WebApiStartup(args)
    {
        public WebApplicationBuilder PublicBuilder => Builder;

        public WebApplication PublicApp => App;

        public bool SwaggerAllowed(EnvironmentType[]? allowedEnvironments = null) => IsSwaggerAllowed(allowedEnvironments);

        public override void Build() { }

        public override void ConfigureApp() { }
    }

    [Fact]
    public void GivenAnEnvironmentArgument_WhenTheStartupIsCreated_ThenTheHostUsesThatEnvironment()
    {
        var startup = new TestStartup(["Environment:Staging"]);

        Assert.Equal("Staging", startup.PublicBuilder.Environment.EnvironmentName);
    }

    [Fact]
    public void GivenTheDevelopmentEnvironment_WhenCheckingSwagger_ThenItIsAllowedByDefault()
    {
        var startup = new TestStartup(["Environment:Development"]);

        Assert.True(startup.SwaggerAllowed());
    }

    [Fact]
    public void GivenTheProductionEnvironment_WhenCheckingSwagger_ThenItIsNotAllowedByDefault()
    {
        var startup = new TestStartup(["Environment:Production"]);

        Assert.False(startup.SwaggerAllowed());
    }

    [Fact]
    public void GivenExplicitAllowedEnvironments_WhenCheckingSwagger_ThenTheyWinOverTheArguments()
    {
        var startup = new TestStartup(["Environment:Production", "SwaggerEnvironments:[Development]"]);

        Assert.True(startup.SwaggerAllowed([EnvironmentType.Production]));
    }

    [Fact]
    public void GivenSwaggerDocsDisabled_WhenCheckingSwagger_ThenItIsNeverAllowed()
    {
        var startup = new TestStartup(["Environment:Development", "EnableSwaggerDocs:false"]);

        Assert.False(startup.SwaggerAllowed());
        Assert.False(startup.SwaggerAllowed([EnvironmentType.Development]));
    }

    [Fact]
    public void GivenTheAppWasNotBuilt_WhenRunning_ThenAClearErrorIsThrown()
    {
        var startup = new TestStartup([]);

        Assert.Throws<InvalidOperationException>(startup.Run);
    }

    [Fact]
    public void GivenATypeThatIsNotAWebApiMiddleware_WhenAddingMiddlewares_ThenRegistrationFails()
    {
        var startup = new TestStartup([]);
        startup.BuildApp();

        Assert.Throws<ArgumentException>(() => startup.AddMiddlewares(typeof(string)));
    }

    [Fact]
    public void GivenWebApiMiddlewares_WhenAddingMiddlewares_ThenTheyAreRegistered()
    {
        var startup = new TestStartup([]);
        startup.BuildApp();

        var exception = Record.Exception(() =>
            startup.AddMiddlewares(typeof(TraceActivityMiddleware), typeof(ExceptionMiddleware)));

        Assert.Null(exception);
    }

    [Fact]
    public void GivenSwaggerIsServed_WhenTheAppReadsItsConfiguration_ThenTheSwaggerMarkerIsSet()
    {
        var startup = new TestStartup(["Environment:Development"]);
        startup.BuildApp();

        startup.UseSwagger();

        var configuration = startup.PublicApp.Services.GetRequiredService<IConfiguration>();
        Assert.True(new SettingsProvider(configuration).GetBool(AppSettingsKeys.SwaggerEnabled));
    }
}
