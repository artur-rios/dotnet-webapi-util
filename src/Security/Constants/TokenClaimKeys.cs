namespace ArturRios.Util.WebApi.Security.Constants;

/// <summary>
/// The claim keys used by the library's default authenticated-user mapper to embed and read
/// authenticated-user data in JSON Web Tokens. A custom mapper may use any keys it likes.
/// </summary>
public static class TokenClaimKeys
{
    /// <summary>The claim key holding the user's id.</summary>
    public const string Id = "id";

    /// <summary>The claim key holding the user's role id.</summary>
    public const string RoleId = "role";
}
