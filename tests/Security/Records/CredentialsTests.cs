using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.WebApi.Tests.Security.Records;

[Trait("Category", "Unit")]
public class CredentialsTests
{
    [Fact]
    public void GivenTheParameterlessConstructor_WhenCreated_ThenTheEmailAndPasswordAreEmptyRatherThanNull()
    {
        var credentials = new Credentials();

        Assert.Equal(string.Empty, credentials.Email);
        Assert.Equal(string.Empty, credentials.Password);
        Assert.Equal(new Credentials(string.Empty, string.Empty), credentials);
    }
}
