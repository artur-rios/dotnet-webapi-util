using ArturRios.Util.WebApi.Security.Authentication;

namespace ArturRios.Util.WebApi.Tests.Security;

[Trait("Category", "Unit")]
public class GoogleTokenVerifierTests
{
    private static readonly string[] Audiences = ["client-id.apps.googleusercontent.com"];

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-jwt")]
    [InlineData("a.b.c")]
    [InlineData("!!!.@@@.###")]
    [InlineData("eyJhbGciOiJSUzI1NiJ9.bm90LWpzb24.c2ln")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6IjEifQ.c2lnbmF0dXJl")]
    public async Task GivenAMalformedOrForeignToken_WhenVerifying_ThenNullIsReturnedRatherThanAnException(string token)
    {
        var payload = await new GoogleTokenVerifier().VerifyAsync(token, Audiences);

        Assert.Null(payload);
    }
}
