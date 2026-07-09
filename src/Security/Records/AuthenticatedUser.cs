namespace ArturRios.Util.WebApi.Security.Records;

/// <summary>Represents the user identity attached to the current request after successful JWT authentication.</summary>
/// <param name="Id">The user's id.</param>
/// <param name="Role">The user's role.</param>
public record AuthenticatedUser(int Id, int Role);
