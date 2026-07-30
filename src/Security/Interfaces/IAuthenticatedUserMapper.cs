namespace ArturRios.Util.WebApi.Security.Interfaces;

/// <summary>
/// Translates between the app's <see cref="IAuthenticatedUser"/> and the claims carried by its JSON Web
/// Tokens. One implementation owns both directions, so the claims written when a token is issued and the
/// claims read when it is validated cannot drift apart.
/// </summary>
/// <remarks>Implementations must return <see langword="null"/> for claims they cannot interpret rather
/// than throwing. Use <c>TryParse</c>, not <c>Parse</c>.</remarks>
public interface IAuthenticatedUserMapper
{
    /// <summary>Builds the claims to embed in a token for the given user, called by the app at login.</summary>
    /// <param name="user">The user the token will represent.</param>
    /// <returns>The claims to embed, keyed by claim name.</returns>
    Dictionary<string, string> ToClaims(IAuthenticatedUser user);

    /// <summary>Rebuilds the user from a token's claims, without any data-store lookup.</summary>
    /// <param name="claims">The claims read from the token.</param>
    /// <returns>The user, or <see langword="null"/> if the claims cannot produce one.</returns>
    IAuthenticatedUser? FromClaims(IReadOnlyDictionary<string, string> claims);

    /// <summary>Reads just the user id from a token's claims, used when the rest of the user is resolved
    /// from the data store. Override this when the token carries more than <see cref="FromClaims"/> requires.</summary>
    /// <param name="claims">The claims read from the token.</param>
    /// <returns>The user id, or <see langword="null"/> if the claims do not carry one.</returns>
    Guid? IdFromClaims(IReadOnlyDictionary<string, string> claims) => FromClaims(claims)?.Id;
}
