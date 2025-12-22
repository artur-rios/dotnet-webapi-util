using System.Net;
using ArturRios.Configuration.Enums;
using ArturRios.Output;
using ArturRios.Util.Http;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArturRios.Util.WebApi.Test;

public class WebApiTest<T> where T : class
{
    private readonly WebApplicationFactory<T> _factory = new();
    protected readonly HttpGateway Gateway;

    protected WebApiTest(EnvironmentType environment)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment.ToString().ToLower());

        Gateway = new HttpGateway(_factory.CreateClient());
    }

    public async Task<Authentication> AuthenticateAsync(Credentials credentials, string authRoute)
    {
        var output = await Gateway.PostAsync<DataOutput<Authentication>>(authRoute, credentials);

        var authError = output.StatusCode != HttpStatusCode.OK
                        || output.Body is null
                        || !output.Body.Success
                        || output.Body.Data is null
                        || string.IsNullOrEmpty(output.Body.Data.Token);

        return authError ? throw new Exception("Could not authenticate") : output.Body!.Data!;
    }

    public void Authorize(string authToken) =>
        Gateway.Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {authToken}");

    public async Task AuthenticateAndAuthorizeAsync(Credentials credentials, string authRoute)
    {
        var authentication = await AuthenticateAsync(credentials, authRoute);

        Authorize(authentication.Token!);
    }
}
