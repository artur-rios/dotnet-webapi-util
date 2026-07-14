using ArturRios.Output;
using ArturRios.Util.WebApi.EndpointToggle;

namespace ArturRios.Util.WebApi.Tests.EndpointToggle;

public class EndpointDisabledExceptionTests
{
    [Fact]
    public void GivenEndpointDisabledException_WhenInspectingType_ThenItIsCustomException()
    {
        var exception = new EndpointDisabledException(["disabled"]);

        Assert.IsAssignableFrom<CustomException>(exception);
    }

    [Fact]
    public void GivenMessages_WhenCreatingException_ThenItCarriesGivenMessages()
    {
        var messages = new[] { "first", "second" };

        var exception = new EndpointDisabledException(messages);

        Assert.Equal(messages, exception.Messages);
    }
}
