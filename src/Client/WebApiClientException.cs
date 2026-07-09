namespace ArturRios.Util.WebApi.Client;

/// <summary>Raised when a <see cref="BaseWebApiClientRoute"/> operation fails, e.g. authentication returns no body.</summary>
public class WebApiClientException : Exception
{
    /// <summary>Initializes a new instance with an error message.</summary>
    public WebApiClientException(string message) : base(message) { }

    /// <summary>Initializes a new instance with an error message and inner exception.</summary>
    public WebApiClientException(string message, Exception innerException) : base(message, innerException) { }
}
