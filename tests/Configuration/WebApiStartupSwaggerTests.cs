using System.Net;
using System.Text.Json;
using ArturRios.Configuration.Providers;
using ArturRios.Util.WebApi.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Configuration;

/// <summary>
/// Swagger in the standard sequence: served only where the environment allows it, with the bearer scheme when
/// asked, and never behind token authentication.
/// </summary>
[Trait("Category", "Functional")]
public sealed class WebApiStartupSwaggerTests : WebApiStartupTestHost
{
    [Fact]
    public async Task GivenTheDevelopmentEnvironment_WhenTheSwaggerDocumentIsRequested_ThenItIsServed()
    {
        var client = await Start("Development");

        var response = await client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GivenTheProductionEnvironment_WhenTheSwaggerDocumentIsRequested_ThenItIsNotServed()
    {
        var client = await Start("Production");

        var response = await client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GivenSwaggerAllowedInProductionByTheOptions_WhenTheDocumentIsRequested_ThenItIsServed()
    {
        var client = await Start("Production",
            options => options.Swagger.AllowedEnvironments = [ArturRios.Configuration.Enums.EnvironmentType.Production]);

        var response = await client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GivenJwtAuthenticationInTheSwaggerOptions_WhenTheDocumentIsRequested_ThenItRequiresTheBearerScheme()
    {
        var client = await Start("Development", options => options.Swagger.JwtAuthentication = true);

        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken));
        var root = document.RootElement;

        Assert.Equal("http",
            root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("type")
                .GetString());

        // An empty requirement ({}) tells Swagger UI the operations need no credentials, so the token entered
        // under "Authorize" would never be sent.
        var requirement = Assert.Single(root.GetProperty("security").EnumerateArray());
        Assert.True(requirement.TryGetProperty("Bearer", out _), requirement.ToString());
    }

    [Fact]
    public async Task GivenTokenAuthenticationAndSwaggerInDevelopment_WhenTheDocumentIsRequested_ThenNoTokenIsNeeded()
    {
        var client = await Start("Development", configureServices: AddJwtAuthentication);

        var response = await client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void GivenSwaggerIsAllowed_WhenBuilding_ThenTheSwaggerMarkerIsSetForTheAuthenticationBypass()
    {
        var startup = new TestStartup(["Environment:Development", "UseAppSettings:false", "UseEnvFile:false"],
            _ => { }, _ => { });

        var app = startup.Build();
        Apps.Add(app);

        var settings = new SettingsProvider(app.Services.GetRequiredService<IConfiguration>());
        Assert.True(settings.GetBool(AppSettingsKeys.SwaggerEnabled));
    }
}
