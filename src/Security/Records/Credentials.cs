namespace ArturRios.Util.WebApi.Security.Records;

/// <summary>The email/password pair submitted to authenticate a user.</summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Password">The user's password.</param>
public record Credentials(string Email, string Password)
{
    /// <summary>Initializes a new, empty instance.</summary>
    public Credentials() : this(string.Empty, string.Empty) { }
}
