namespace ArturRios.Util.WebApi.Security.Interfaces;

/// <summary>The user identity the library needs to authenticate a request and enforce role-based access.
/// Consuming apps implement this on their own type, adding whatever else they need.</summary>
public interface IAuthenticatedUser
{
    /// <summary>The user's id, used to look the user up when re-validating against the data store, and as
    /// the provider cache key.</summary>
    Guid Id { get; }

    /// <summary>The user's role id, compared against the roles allowed by
    /// <see cref="Attributes.RoleRequirementAttribute"/>.</summary>
    int RoleId { get; }
}
