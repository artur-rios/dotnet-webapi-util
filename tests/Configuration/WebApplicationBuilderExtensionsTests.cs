using ArturRios.Configuration.Enums;
using ArturRios.Configuration.Loaders;
using ArturRios.Configuration.Providers;
using ArturRios.Util.WebApi.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ArturRios.Util.WebApi.Tests.Configuration;

/// <summary>
/// The builder-side steps on their own. An empty builder is enough for them, so no host is started; the environment
/// file and app settings are looked up under the test's output directory, where only
/// <c>Settings/appsettings.LoadConfigurationTest.json</c> exists, for the environment of that name.
/// </summary>
[Trait("Category", "Unit")]
public class WebApplicationBuilderExtensionsTests
{
    private static WebApplicationBuilder Builder(string environment) =>
        WebApplication.CreateEmptyBuilder(new WebApplicationOptions { EnvironmentName = environment });

    [Fact]
    public void GivenBothSourcesOff_WhenLoadingConfiguration_ThenTheParametersAndSettingsAreRegisteredButNoEnvironmentProvider()
    {
        var builder = Builder("Development");
        var parameters = new WebApiParameters(["UseAppSettings:false", "UseEnvFile:false"]);

        Assert.Same(builder, builder.LoadConfiguration(parameters));
        builder.Services.AddLogging();

        using var provider = builder.Services.BuildServiceProvider();
        Assert.Same(parameters, provider.GetRequiredService<WebApiParameters>());
        Assert.NotNull(provider.GetService<SettingsProvider>());
        Assert.NotNull(provider.GetService<ConfigurationLoader>());
        Assert.Null(provider.GetService<EnvironmentProvider>());
    }

    [Fact]
    public void GivenTheEnvFileIsOn_WhenLoadingConfiguration_ThenTheEnvironmentProviderIsRegistered()
    {
        var builder = Builder("Development");

        builder.LoadConfiguration(new WebApiParameters(["UseAppSettings:false", "UseEnvFile:true"]));

        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(EnvironmentProvider));
    }

    [Fact]
    public void GivenAppSettingsAreOn_WhenLoadingConfiguration_ThenTheEnvironmentsSettingsFileIsRead()
    {
        var builder = Builder("LoadConfigurationTest");

        builder.LoadConfiguration(new WebApiParameters(["UseAppSettings:true", "UseEnvFile:false"]));

        Assert.Equal("from-settings", builder.Configuration["LoadConfigurationTest:Value"]);
    }

    [Fact]
    public void GivenAppSettingsAreOff_WhenLoadingConfiguration_ThenTheSettingsFileIsNotRead()
    {
        var builder = Builder("LoadConfigurationTest");

        builder.LoadConfiguration(new WebApiParameters(["UseAppSettings:false", "UseEnvFile:false"]));

        Assert.Null(builder.Configuration["LoadConfigurationTest:Value"]);
    }

    [Fact]
    public void GivenNullParameters_WhenLoadingConfiguration_ThenItThrows()
    {
        Assert.Throws<ArgumentNullException>(() => Builder("Development").LoadConfiguration(null!));
    }

    [Fact]
    public void GivenAnEnvironmentWhereSwaggerIsAllowed_WhenAddingSwagger_ThenTheGeneratorIsRegisteredAndMarkedEnabled()
    {
        var builder = Builder("Development");

        Assert.Same(builder, builder.AddWebApiSwagger(new WebApiParameters([])));

        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(ISwaggerProvider));
        Assert.Equal("true", builder.Configuration[AppSettingsKeys.SwaggerEnabled]);
    }

    [Fact]
    public void GivenAnEnvironmentWhereSwaggerIsNotAllowed_WhenAddingSwagger_ThenNothingIsRegistered()
    {
        var builder = Builder("Production");

        builder.AddWebApiSwagger(new WebApiParameters([]));

        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ServiceType == typeof(ISwaggerProvider));
        Assert.Null(builder.Configuration[AppSettingsKeys.SwaggerEnabled]);
    }

    [Fact]
    public void GivenAConfigureCallback_WhenAddingSwagger_ThenItsEnvironmentsAndGeneratorCallbackApply()
    {
        var builder = Builder("Production");
        var generatorConfigured = false;

        builder.AddWebApiSwagger(new WebApiParameters([]), options =>
        {
            options.AllowedEnvironments = [EnvironmentType.Production];
            options.ConfigureGenerator = _ => generatorConfigured = true;
        });

        using var provider = builder.Services.BuildServiceProvider();
        _ = provider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;

        Assert.True(generatorConfigured);
        Assert.Equal("true", builder.Configuration[AppSettingsKeys.SwaggerEnabled]);
    }

    [Fact]
    public void GivenNullParameters_WhenAddingSwagger_ThenItThrows()
    {
        Assert.Throws<ArgumentNullException>(() => Builder("Development").AddWebApiSwagger(null!));
    }
}
