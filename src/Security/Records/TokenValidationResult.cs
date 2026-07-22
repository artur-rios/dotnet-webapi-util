namespace ArturRios.Util.WebApi.Security.Records;

/// <summary>The outcome of an <c>ITokenValidator</c> attempt: a resolved user, or an error describing why validation failed.</summary>
/// <param name="User">The authenticated user when validation succeeded; otherwise <see langword="null"/>.</param>
/// <param name="Error">A human-readable error when <paramref name="User"/> is <see langword="null"/>; otherwise <see langword="null"/>.</param>
public readonly record struct TokenValidationResult(AuthenticatedUser? User, string? Error);
