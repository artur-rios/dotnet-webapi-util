using ArturRios.Util.Http;
using ArturRios.Util.WebApi.Client;

namespace ArturRios.Util.WebApi.Tests.Client;

[Trait("Category", "Unit")]
public class BaseWebApiClientTests
{
    private sealed class TestClient : BaseWebApiClient
    {
        public TestClient(HttpClient httpClient) : base(httpClient) { }

        public TestClient(string baseUrl) : base(baseUrl) { }

        public int SetRoutesCalls { get; private set; }

        public HttpGateway ExposedGateway => Gateway;

        protected override void SetRoutes() => SetRoutesCalls++;
    }

    [Fact]
    public void GivenAnHttpClient_WhenTheClientIsCreated_ThenItsGatewayWrapsThatClientAndTheRoutesAreSetOnce()
    {
        using var httpClient = new HttpClient();

        var client = new TestClient(httpClient);

        Assert.Same(httpClient, client.ExposedGateway.Client);
        Assert.Equal(1, client.SetRoutesCalls);
    }

    [Fact]
    public void GivenABaseUrl_WhenTheClientIsCreated_ThenItsGatewayTargetsThatUrlAndTheRoutesAreSetOnce()
    {
        var client = new TestClient("https://api.example.test/v1/");

        Assert.Equal(new Uri("https://api.example.test/v1/"), client.ExposedGateway.Client.BaseAddress);
        Assert.Equal(1, client.SetRoutesCalls);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("")]
    public void GivenABaseUrlThatIsNotAnAbsoluteUri_WhenTheClientIsCreated_ThenItThrows(string baseUrl)
    {
        Assert.ThrowsAny<UriFormatException>(() => new TestClient(baseUrl));
    }
}
