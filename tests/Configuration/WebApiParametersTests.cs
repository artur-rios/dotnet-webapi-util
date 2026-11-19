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

    [Theory]
    [InlineData("Production")]
    [InlineData("UseEnvFile")]
    [InlineData("")]
    public void GivenAnArgumentWithoutAColon_WhenParsing_ThenItIsIgnored(string arg)
    {
        var parameters = new WebApiParameters([arg, "UseEnvFile:false"]);

        Assert.Equal(string.Empty, parameters.EnvironmentName);
        Assert.False(parameters.UseEnvFile);
        Assert.True(parameters.UseAppSettings);
    }

    [Fact]
    public void GivenAnUnknownKey_WhenParsing_ThenItIsIgnored()
    {
        var parameters = new WebApiParameters(["Colour:blue"]);

        Assert.Equal(string.Empty, parameters.EnvironmentName);
        Assert.True(parameters.UseAppSettings);
        Assert.True(parameters.UseEnvFile);
        Assert.True(parameters.EnableSwaggerDocs);
        Assert.Empty(parameters.SwaggerEnvironments);
    }

    [Theory]
    [InlineData("UseEnvFile:maybe")]
    [InlineData("UseEnvFile:")]
    public void GivenABooleanArgumentThatIsNotABoolean_WhenParsing_ThenItDefaultsToTrue(string arg)
    {
        Assert.True(new WebApiParameters([arg]).UseEnvFile);
    }

    [Fact]
    public void GivenSpacesAroundTheKeyAndValue_WhenParsing_ThenTheyAreTrimmed()
    {
        var parameters = new WebApiParameters([" Environment : Staging ", " UseEnvFile : false "]);

        Assert.Equal("Staging", parameters.EnvironmentName);
        Assert.False(parameters.UseEnvFile);
    }

    [Theory]
    [InlineData("SwaggerEnvironments:Development,Staging")]
    [InlineData("SwaggerEnvironments:[Development,Staging")]
    [InlineData("SwaggerEnvironments:Development,Staging]")]
    public void GivenSwaggerEnvironmentsWithoutBothBrackets_WhenParsing_ThenTheyAreIgnored(string arg)
    {
        Assert.Empty(new WebApiParameters([arg]).SwaggerEnvironments);
    }

    [Fact]
    public void GivenOnlyInvalidSwaggerEnvironments_WhenReadingThem_ThenTheDefaultsComeBack()
    {
        var parameters = new WebApiParameters(["SwaggerEnvironments:[Nowhere, Mars]"]);

        Assert.Equal(["Nowhere", "Mars"], parameters.SwaggerEnvironments);
        Assert.Equal(new WebApiParameters([]).GetSwaggerEnvironments(), parameters.GetSwaggerEnvironments());
    }

    [Fact]
    public void GivenTheEnvironmentNameInAnotherCase_WhenCheckingSwagger_ThenItStillMatches()
    {
        Assert.True(new WebApiParameters([]).IsSwaggerAllowed("development"));
    }
}
