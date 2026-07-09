namespace ArturRios.Util.WebApi.Security.Records;

/// <summary>The result of an authentication attempt, as returned by a web API's authentication route.</summary>
/// <param name="Token">The issued JWT, or <c>null</c> if authentication failed.</param>
/// <param name="Valid">Whether the authentication attempt succeeded.</param>
/// <param name="CreatedAt">When the token was issued.</param>
/// <param name="Expiration">When the token expires.</param>
public record Authentication(string? Token, bool Valid, string CreatedAt, string Expiration);
