using ArturRios.Configuration.Enums;
using ArturRios.Util.WebApi.Configuration;

namespace ArturRios.Util.WebApi.Tests.Configuration;

[Trait("Category", "Unit")]
public class WebApiParametersTests
{
    [Fact]
    public void GivenNoArguments_WhenParsing_ThenTheDefaultsAreKept()
    {
        var parameters = new WebApiParameters([]);

        Assert.Equal(string.Empty, parameters.EnvironmentName);
        Assert.True(parameters.UseAppSettings);
        Assert.True(parameters.UseEnvFile);
        Assert.True(parameters.EnableSwaggerDocs);
        Assert.Empty(parameters.SwaggerEnvironments);
    }

    [Theory]
    [InlineData("UseAppSettings:false")]
    [InlineData("UseAppSetting:false")]
    [InlineData("useappsettings:false")]
    public void GivenEitherSpellingOfTheAppSettingsArgument_WhenParsing_ThenAppSettingsAreDisabled(string arg)
    {
        var parameters = new WebApiParameters([arg]);

        Assert.False(parameters.UseAppSettings);
    }

    [Fact]
    public void GivenKeysInAnyCase_WhenParsing_ThenTheyAreRecognized()
    {
        var parameters = new WebApiParameters(["environment:Staging", "USEENVFILE:false", "enableSwaggerDocs:false"]);

        Assert.Equal("Staging", parameters.EnvironmentName);
        Assert.False(parameters.UseEnvFile);
        Assert.False(parameters.EnableSwaggerDocs);
    }

    [Fact]
    public void GivenAnInvalidEnvironment_WhenParsing_ThenTheEnvironmentNameIsEmpty()
    {
        var parameters = new WebApiParameters(["Environment:Nowhere"]);

        Assert.Equal(string.Empty, parameters.EnvironmentName);
    }

    [Fact]
    public void GivenSwaggerEnvironments_WhenReadingThem_ThenOnlyValidOnesComeBack()
    {
        var parameters = new WebApiParameters(["SwaggerEnvironments:[Staging, Nowhere]"]);

        Assert.Equal(["Staging"], parameters.GetSwaggerEnvironments());
    }

    [Fact]
    public void GivenNoSwaggerEnvironments_WhenTheDefaultsAreMutated_ThenTheNextCallStillReturnsTheDefaults()
    {
        var parameters = new WebApiParameters([]);

        parameters.GetSwaggerEnvironments()[0] = "Production";

        Assert.Equal(["Development", "Local"], parameters.GetSwaggerEnvironments());
    }

    [Fact]
    public void GivenTheDevelopmentEnvironment_WhenCheckingSwagger_ThenItIsAllowedByDefault()
    {
        Assert.True(new WebApiParameters([]).IsSwaggerAllowed("Development"));
    }

    [Fact]
    public void GivenTheProductionEnvironment_WhenCheckingSwagger_ThenItIsNotAllowedByDefault()
    {
        Assert.False(new WebApiParameters([]).IsSwaggerAllowed("Production"));
    }

    [Fact]
    public void GivenExplicitAllowedEnvironments_WhenCheckingSwagger_ThenTheyWinOverTheArguments()
    {
        var parameters = new WebApiParameters(["SwaggerEnvironments:[Development]"]);

        Assert.True(parameters.IsSwaggerAllowed("Production", [EnvironmentType.Production]));
    }

    [Fact]
    public void GivenSwaggerDocsDisabled_WhenCheckingSwagger_ThenItIsNeverAllowed()
    {
        var parameters = new WebApiParameters(["EnableSwaggerDocs:false"]);

        Assert.False(parameters.IsSwaggerAllowed("Development"));
        Assert.False(parameters.IsSwaggerAllowed("Development", [EnvironmentType.Development]));
    }

    [Fact]
    public void GivenAnEnvironmentArgument_WhenBuildingWebApplicationOptions_ThenItIsTheEnvironmentAndTheArgsAreKept()
    {
        string[] args = ["Environment:Staging", "UseEnvFile:false"];

        var options = new WebApiParameters(args).ToWebApplicationOptions();

        Assert.Equal("Staging", options.EnvironmentName);
        Assert.Equal(args, options.Args);
    }

    [Fact]
    public void GivenNoEnvironmentArgument_WhenBuildingWebApplicationOptions_ThenTheHostDecidesTheEnvironment()
    {
        Assert.Null(new WebApiParameters([]).ToWebApplicationOptions().EnvironmentName);
    }
}
