namespace ArturRios.Util.WebApi.Security.Constants;

/// <summary>
/// The claim keys used to embed and read authenticated-user data in JSON Web Tokens.
/// </summary>
public static class TokenClaimKeys
{
    /// <summary>The claim key holding the user's id.</summary>
    public const string Id = "id";

    /// <summary>The claim key holding the user's role.</summary>
    public const string Role = "role";
}
