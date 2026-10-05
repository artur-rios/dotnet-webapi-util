using System.Diagnostics;
using ArturRios.Util.WebApi.Extensions;
using ArturRios.Util.WebApi.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Handlers;

[Trait("Category", "Unit")]
public class TracePropagationHandlerTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;

            return Task.FromResult(new HttpResponseMessage());
        }
    }

    private static async Task<HttpRequestMessage> Send(HttpRequestMessage request)
    {
        var inner = new CapturingHandler();
        using var client = new HttpClient(new TracePropagationHandler { InnerHandler = inner });

        await client.SendAsync(request);

        return inner.Request!;
    }

    [Fact]
    public async Task GivenAnAmbientActivity_WhenSending_ThenItsTraceparentAndTracestateAreAdded()
    {
        using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();
        activity.TraceStateString = "vendor=value";

        var request = await Send(new HttpRequestMessage(HttpMethod.Get, "https://example.test"));

        Assert.Equal(activity.ToTraceParent(), request.Headers.GetValues("traceparent").Single());
        Assert.Equal("vendor=value", request.Headers.GetValues("tracestate").Single());
    }

    [Fact]
    public async Task GivenARequestThatAlreadyCarriesATraceparent_WhenSending_ThenItIsLeftAlone()
    {
        using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();
        var original = new HttpRequestMessage(HttpMethod.Get, "https://example.test");
        original.Headers.Add("traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");

        var request = await Send(original);

        Assert.Equal("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
            request.Headers.GetValues("traceparent").Single());
    }

    [Fact]
    public void GivenAClientBuilder_WhenAddingTracePropagation_ThenTheHandlerIsRegistered()
    {
        var services = new ServiceCollection();

        services.AddHttpClient("downstream").AddTracePropagation();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TracePropagationHandler));
    }
}
