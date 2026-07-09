using System.Net.Http.Headers;
using ArturRios.Util.Http;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Client;

/// <summary>Base class for a group of related routes (endpoints) on a remote web API, sharing a base URL and gateway.</summary>
/// <param name="gateway">The HTTP gateway used to issue requests.</param>
public abstract class BaseWebApiClientRoute(HttpGateway gateway)
{
    /// <summary>The HTTP gateway used to issue requests to this route group.</summary>
    protected readonly HttpGateway Gateway = gateway;

    /// <summary>The base URL of the routes exposed by this class.</summary>
    public abstract string BaseUrl { get; }

    /// <summary>Authenticates against <paramref name="authRoute"/> using <paramref name="credentials"/> and returns the resulting token.</summary>
    /// <param name="credentials">The credentials to authenticate with.</param>
    /// <param name="authRoute">The relative authentication route.</param>
    /// <returns>The <see cref="Authentication"/> response returned by the API.</returns>
    /// <exception cref="WebApiClientException">Thrown when the authentication response contains no body.</exception>
    protected async Task<Authentication> AuthenticateAsync(Credentials credentials, string authRoute)
    {
        var output = await Gateway.PostAsync<Authentication>(authRoute, credentials);

        return output.Body ?? throw new WebApiClientException("Could not authenticate: the authentication response contained no body.");
    }

    /// <summary>Sets the bearer token used for subsequent requests made through <see cref="Gateway"/>.</summary>
    /// <param name="authToken">The JWT to send as a Bearer token.</param>
    protected void Authorize(string authToken) =>
        Gateway.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

    /// <summary>Authenticates against <paramref name="authRoute"/> and applies the resulting token to the gateway's default headers.</summary>
    /// <param name="credentials">The credentials to authenticate with.</param>
    /// <param name="authRoute">The relative authentication route.</param>
    protected async Task AuthenticateAndAuthorizeAsync(Credentials credentials, string authRoute)
    {
        var authentication = await AuthenticateAsync(credentials, authRoute);

        Authorize(authentication.Token!);
    }
}
