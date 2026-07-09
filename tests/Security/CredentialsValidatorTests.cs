using ArturRios.Util.WebApi.Security.Records;
using ArturRios.Util.WebApi.Security.Validation;

namespace ArturRios.Util.WebApi.Tests.Security;

public class CredentialsValidatorTests
{
    private readonly CredentialsValidator _validator = new();

    [Theory]
    [InlineData("user@example.com", "password123", true)]
    [InlineData("not-an-email", "password123", false)]
    [InlineData("user@example.com", "short", false)]
    [InlineData("", "password123", false)]
    public void Validates(string email, string password, bool expected)
    {
        var result = _validator.Validate(new Credentials(email, password));
        Assert.Equal(expected, result.IsValid);
    }
}
