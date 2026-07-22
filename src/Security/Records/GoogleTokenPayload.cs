namespace ArturRios.Util.WebApi.Security.Records;

/// <summary>The verified subset of a Google ID token used to resolve the app user.</summary>
/// <param name="Email">The token's email claim.</param>
/// <param name="Subject">Google's stable subject identifier (<c>sub</c>).</param>
/// <param name="EmailVerified">Whether Google has verified ownership of <paramref name="Email"/> (the token's <c>email_verified</c> claim).</param>
public record GoogleTokenPayload(string Email, string Subject, bool EmailVerified);
