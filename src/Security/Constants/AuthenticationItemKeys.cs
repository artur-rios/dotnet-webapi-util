namespace ArturRios.Util.WebApi.Security.Constants;

/// <summary>The <c>HttpContext.Items</c> keys used by the authentication pipeline.</summary>
internal static class AuthenticationItemKeys
{
    /// <summary>The key under which the authenticated user is attached to the current request.</summary>
    public const string User = "User";
}
