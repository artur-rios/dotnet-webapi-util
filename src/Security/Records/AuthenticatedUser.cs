using ArturRios.Util.WebApi.Security.Interfaces;

namespace ArturRios.Util.WebApi.Security.Records;

/// <summary>The default <see cref="IAuthenticatedUser"/>, for apps that need nothing beyond an id and a role.
/// Attached to the current request after successful authentication.</summary>
/// <param name="Id">The user's id.</param>
/// <param name="RoleId">The user's role id.</param>
public record AuthenticatedUser(Guid Id, int RoleId) : IAuthenticatedUser;
