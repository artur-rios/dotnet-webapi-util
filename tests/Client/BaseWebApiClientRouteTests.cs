using System.Net.Http;
using ArturRios.Util.Http;
using ArturRios.Util.WebApi.Client;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Tests.Client;

[Trait("Category", "Unit")]
public class BaseWebApiClientRouteTests
{
    private sealed class TestRoute(HttpGateway gateway) : BaseWebApiClientRoute(gateway)
    {
        public override string BaseUrl => "/test";
        public void CallAuthorize(string token) => Authorize(token);

        public Task CallAuthenticateAndAuthorizeAsync() =>
            AuthenticateAndAuthorizeAsync(new Credentials("user@example.test", "password123"), "/auth");

        public Task<Authentication> CallAuthenticateAsync() =>
            AuthenticateAsync(new Credentials("user@example.test", "password123"), "/auth");
    }

    private sealed class CapturingJsonHandler(string json) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public string? Path { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
    }

    private static (TestRoute Route, HttpClient Client) RouteAnswering(string json)
    {
        var httpClient = new HttpClient(new JsonHandler(json)) { BaseAddress = new Uri("https://example.test") };

        return (new TestRoute(new HttpGateway(httpClient)), httpClient);
    }

    [Fact]
    public async Task GivenAnInvalidAuthentication_WhenAuthenticatingAndAuthorizing_ThenItFailsAndNoHeaderIsSet()
    {
        var (route, httpClient) = RouteAnswering(
            """{"Token":null,"Valid":false,"CreatedAt":"","Expiration":""}""");

        await Assert.ThrowsAsync<WebApiClientException>(route.CallAuthenticateAndAuthorizeAsync);
        Assert.Null(httpClient.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task GivenAValidAuthentication_WhenAuthenticatingAndAuthorizing_ThenTheTokenIsSetAsBearer()
    {
        var (route, httpClient) = RouteAnswering(
            """{"Token":"issued.jwt.token","Valid":true,"CreatedAt":"","Expiration":""}""");

        await route.CallAuthenticateAndAuthorizeAsync();

        Assert.Equal("issued.jwt.token", httpClient.DefaultRequestHeaders.Authorization?.Parameter);
    }

    [Fact]
    public void GivenARouteAuthorizedTwice_WhenInspected_ThenOneBearerSchemeIsSet()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("https://example.test") };
        var route = new TestRoute(new HttpGateway(httpClient));

        route.CallAuthorize("token-one");
        route.CallAuthorize("token-two");

        Assert.NotNull(httpClient.DefaultRequestHeaders.Authorization);
        Assert.Equal("Bearer", httpClient.DefaultRequestHeaders.Authorization!.Scheme);
        Assert.Equal("token-two", httpClient.DefaultRequestHeaders.Authorization.Parameter);
    }

    [Fact]
    public async Task GivenAnAuthenticationResponseWithNoBody_WhenAuthenticating_ThenItFailsSayingSo()
    {
        var (route, httpClient) = RouteAnswering("null");

        var exception = await Assert.ThrowsAsync<WebApiClientException>(route.CallAuthenticateAndAuthorizeAsync);

        Assert.Contains("no body", exception.Message);
        Assert.Null(httpClient.DefaultRequestHeaders.Authorization);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    public async Task GivenAValidAuthenticationWithABlankToken_WhenAuthenticatingAndAuthorizing_ThenItFailsAndNoHeaderIsSet(
        string token)
    {
        var (route, httpClient) = RouteAnswering(
            $$"""{"Token":{{token}},"Valid":true,"CreatedAt":"","Expiration":""}""");

        var exception = await Assert.ThrowsAsync<WebApiClientException>(route.CallAuthenticateAndAuthorizeAsync);

        Assert.Contains("no valid token", exception.Message);
        Assert.Null(httpClient.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task GivenCredentials_WhenAuthenticating_ThenTheyArePostedToTheRouteAndTheResponseComesBack()
    {
        var handler = new CapturingJsonHandler(
            """{"Token":"issued.jwt.token","Valid":true,"CreatedAt":"","Expiration":""}""");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var route = new TestRoute(new HttpGateway(httpClient));

        var authentication = await route.CallAuthenticateAsync();

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/auth", handler.Path);
        Assert.Contains("user@example.test", handler.Body);
        Assert.Equal("issued.jwt.token", authentication.Token);
        Assert.True(authentication.Valid);
        Assert.Null(httpClient.DefaultRequestHeaders.Authorization);
    }
}
