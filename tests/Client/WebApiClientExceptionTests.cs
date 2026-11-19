using ArturRios.Util.WebApi.Client;

namespace ArturRios.Util.WebApi.Tests.Client;

[Trait("Category", "Unit")]
public class WebApiClientExceptionTests
{
    [Fact]
    public void GivenAMessage_WhenCreated_ThenItCarriesItWithNoInnerException()
    {
        var exception = new WebApiClientException("could not authenticate");

        Assert.Equal("could not authenticate", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void GivenAMessageAndAnInnerException_WhenCreated_ThenItCarriesBoth()
    {
        var inner = new HttpRequestException("connection refused");

        var exception = new WebApiClientException("could not authenticate", inner);

        Assert.Equal("could not authenticate", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }
}
