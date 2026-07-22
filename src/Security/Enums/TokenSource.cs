namespace ArturRios.Util.WebApi.Security.Enums;

/// <summary>Where <c>AuthenticationMiddleware</c> reads the raw authentication token from.</summary>
public enum TokenSource
{
    /// <summary>Read the token from the <c>Authorization: Bearer &lt;token&gt;</c> header only.</summary>
    Header,

    /// <summary>Read the token from the configured cookie only.</summary>
    Cookie,

    /// <summary>Read the token from the header first, then fall back to the cookie.</summary>
    Either
}
