using ArturRios.Util.Http;

namespace ArturRios.Util.WebApi.Client;

/// <summary>Base class for strongly-typed clients that call a single web API through an <see cref="HttpGateway"/>.</summary>
public abstract class BaseWebApiClient
{
    /// <summary>The HTTP gateway used to issue requests to the target API.</summary>
    protected readonly HttpGateway Gateway;

    /// <summary>Initializes the client from an existing <see cref="HttpClient"/> and lets the derived class configure its routes.</summary>
    /// <param name="httpClient">The HTTP client to wrap.</param>
    protected BaseWebApiClient(HttpClient httpClient)
    {
        Gateway = new HttpGateway(httpClient);

        SetRoutes();
    }

    /// <summary>Initializes the client with a new <see cref="HttpClient"/> pointed at <paramref name="baseUrl"/> and lets the derived class configure its routes.</summary>
    /// <param name="baseUrl">The base address of the target API.</param>
    protected BaseWebApiClient(string baseUrl)
    {
        var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };

        Gateway = new HttpGateway(httpClient);

        SetRoutes();
    }

    /// <summary>Configures the routes (endpoints) exposed by this client. Called once during construction.</summary>
    protected abstract void SetRoutes();
}
