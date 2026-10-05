using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;

namespace ArturRios.Util.WebApi.Security.Extensions;

/// <summary>Detects endpoints exempt from authentication, marked with this library's
/// <see cref="Attributes.AllowAnonymousAttribute"/> or ASP.NET Core's own <c>[AllowAnonymous]</c>.</summary>
internal static class EndpointMetadataExtensions
{
    /// <param name="endpoint">The endpoint the request was routed to, if any.</param>
    extension(Endpoint? endpoint)
    {
        /// <summary>Whether the endpoint carries <see cref="IAllowAnonymous"/> metadata.</summary>
        public bool AllowsAnonymous() =>
            endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
    }

    /// <param name="actionDescriptor">The MVC action being authorized.</param>
    extension(ActionDescriptor actionDescriptor)
    {
        /// <summary>Whether the action carries <see cref="IAllowAnonymous"/> metadata, at action or controller level.</summary>
        public bool AllowsAnonymous() =>
            actionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
    }
}
