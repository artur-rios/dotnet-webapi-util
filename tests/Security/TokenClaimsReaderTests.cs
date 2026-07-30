using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ArturRios.Jwt;
using ArturRios.Util.WebApi.Security.Authentication;

namespace ArturRios.Util.WebApi.Tests.Security;

public class TokenClaimsReaderTests
{
    private const string Secret = "super-secret-signing-key-with-enough-length-1234567890";

    private static string CreateToken(Dictionary<string, string> claims) =>
        new JwtHandler().CreateToken(new JwtConfiguration(3600, "issuer", "audience", Secret, claims));

    [Fact]
    public void Read_ReturnsClaims_FromReadableToken()
    {
        var token = CreateToken(new Dictionary<string, string> { { "id", "abc" }, { "tenant", "acme" } });

        var claims = TokenClaimsReader.Read(token);

        Assert.NotNull(claims);
        Assert.Equal("abc", claims["id"]);
        Assert.Equal("acme", claims["tenant"]);
    }

    [Fact]
    public void Read_ReturnsNull_WhenTokenIsNotReadable()
    {
        Assert.Null(TokenClaimsReader.Read("not-a-jwt"));
    }

    [Fact]
    public void Read_ReturnsNull_WhenTokenIsEmpty()
    {
        Assert.Null(TokenClaimsReader.Read(string.Empty));
    }

    [Fact]
    public void Read_ReturnsNull_WhenTokenLooksLikeAJwtButIsMalformed()
    {
        Assert.Null(TokenClaimsReader.Read("aaa.bbb.ccc"));
    }

    [Fact]
    public void Read_KeepsFirstOccurrence_WhenClaimKeyIsRepeated()
    {
        var token = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(claims: [new Claim("dup", "first"), new Claim("dup", "second")]));

        var claims = TokenClaimsReader.Read(token);

        Assert.NotNull(claims);
        Assert.Equal("first", claims["dup"]);
    }
}
