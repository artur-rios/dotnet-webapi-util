using System.Net.Http.Headers;
using ArturRios.Util.Http;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Client;

public abstract class BaseWebApiClientRoute(HttpGateway gateway)
{
    protected readonly HttpGateway Gateway = gateway;
    public abstract string BaseUrl { get; }

    protected async Task<Authentication> AuthenticateAsync(Credentials credentials, string authRoute)
    {
        var output = await Gateway.PostAsync<Authentication>(authRoute, credentials);

        return output.Body ?? throw new WebApiClientException("Could not authenticate: the authentication response contained no body.");
    }

    protected void Authorize(string authToken) =>
        Gateway.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

    protected async Task AuthenticateAndAuthorizeAsync(Credentials credentials, string authRoute)
    {
        var authentication = await AuthenticateAsync(credentials, authRoute);

        Authorize(authentication.Token!);
    }
}
