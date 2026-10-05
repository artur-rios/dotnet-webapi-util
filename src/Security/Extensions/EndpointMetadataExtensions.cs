using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;

namespace ArturRios.Util.WebApi.Security.Extensions;

/// <summary>Detects endpoints exempt from authentication, marked with this library's
/// <see cref="Attributes.AllowAnonymousAttribute"/> or ASP.NET Core's own <c>[AllowAnonymous]</c>.</summary>
internal static class EndpointMetadataExtensions
{
    /// <summary>Whether the endpoint carries <see cref="IAllowAnonymous"/> metadata.</summary>
    public static bool AllowsAnonymous(this Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null;

    /// <summary>Whether the action carries <see cref="IAllowAnonymous"/> metadata, at action or controller level.</summary>
    public static bool AllowsAnonymous(this ActionDescriptor actionDescriptor) =>
        actionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
}
