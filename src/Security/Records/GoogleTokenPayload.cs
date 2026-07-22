namespace ArturRios.Util.WebApi.Security.Records;

/// <summary>The verified subset of a Google ID token used to resolve the app user.</summary>
/// <param name="Email">The token's verified email claim.</param>
/// <param name="Subject">Google's stable subject identifier (<c>sub</c>).</param>
public record GoogleTokenPayload(string Email, string Subject);
