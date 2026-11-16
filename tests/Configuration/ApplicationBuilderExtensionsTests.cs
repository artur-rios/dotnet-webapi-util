using ArturRios.Util.WebApi.Configuration;
using ArturRios.Util.WebApi.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Tests.Configuration;

[Trait("Category", "Unit")]
public class ApplicationBuilderExtensionsTests
{
    private const string OrderKey = "order";

    private sealed class FirstMiddleware(RequestDelegate next) : WebApiMiddleware
    {
        public Task InvokeAsync(HttpContext context) => Record(context, "first", next);
    }

    private sealed class SecondMiddleware(RequestDelegate next) : WebApiMiddleware
    {
        public Task InvokeAsync(HttpContext context) => Record(context, "second", next);
    }

    /// <summary>A service provider that knows no service at all, not even <see cref="IServiceProviderIsService"/>.</summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private static Task Record(HttpContext context, string name, RequestDelegate next)
    {
        ((List<string>)context.Items[OrderKey]!).Add(name);

        return next(context);
    }

    private static async Task<List<string>> Run(IApplicationBuilder app)
    {
        var order = new List<string>();
        var context = new DefaultHttpContext();
        context.Items[OrderKey] = order;

        await app.Build()(context);

        return order;
    }

    [Fact]
    public async Task GivenSeveralMiddlewares_WhenTheyAreRegistered_ThenTheyRunInTheGivenOrder()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);

        Assert.Same(app, app.UseWebApiMiddlewares(typeof(SecondMiddleware), typeof(FirstMiddleware)));

        Assert.Equal(["second", "first"], await Run(app));
    }

    [Fact]
    public async Task GivenNoMiddlewares_WhenTheyAreRegistered_ThenThePipelineIsUnchanged()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);

        app.UseWebApiMiddlewares();

        Assert.Empty(await Run(app));
    }

    [Fact]
    public async Task GivenATypeThatIsNotAWebApiMiddleware_WhenRegistering_ThenItFailsNamingTheTypeAndRegistersNoneOfThem()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);

        var exception = Assert.Throws<ArgumentException>(() =>
            app.UseWebApiMiddlewares(typeof(FirstMiddleware), typeof(string)));

        Assert.Contains(typeof(string).FullName!, exception.Message);
        Assert.Equal("middlewares", exception.ParamName);
        Assert.Empty(await Run(app));
    }

    [Fact]
    public void GivenTheMarkerBaseClassItself_WhenRegistering_ThenItIsRejected()
    {
        using var services = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<ArgumentException>(() =>
            new ApplicationBuilder(services).UseWebApiMiddlewares(typeof(WebApiMiddleware)));
    }

    [Fact]
    public void GivenANullList_WhenRegisteringMiddlewares_ThenItThrows()
    {
        using var services = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<ArgumentNullException>(() =>
            new ApplicationBuilder(services).UseWebApiMiddlewares((IEnumerable<Type>)null!));
    }

    [Fact]
    public async Task GivenNoSwaggerGenerator_WhenUsingSwagger_ThenNothingIsAdded()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);

        Assert.Same(app, app.UseWebApiSwagger());

        Assert.Empty(await Run(app));
    }

    [Fact]
    public async Task GivenAContainerThatCannotTellWhatItHolds_WhenUsingSwagger_ThenNothingIsAdded()
    {
        var app = new ApplicationBuilder(new EmptyServiceProvider());

        app.UseWebApiSwagger();

        Assert.Empty(await Run(app));
    }
}
