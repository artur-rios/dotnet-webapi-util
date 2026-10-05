using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Extensions;

/// <summary>Extension methods for identifying the client that made the current request.</summary>
public static class HttpContextClientExtensions
{
    /// <param name="context">The current HTTP context.</param>
    extension(HttpContext context)
    {
        /// <summary>Gets the IP address of the client that made the request, from
        /// <see cref="ConnectionInfo.RemoteIpAddress"/>. An IPv4 address carried as IPv6 (<c>::ffff:203.0.113.7</c>)
        /// comes back in its IPv4 form.</summary>
        /// <remarks>Behind a reverse proxy or load balancer this is the proxy's address. Configure ASP.NET Core's
        /// <c>ForwardedHeadersOptions</c> with the proxies you trust (the standard middlewares run
        /// <c>UseForwardedHeaders</c> first), so the address is taken from <c>X-Forwarded-For</c>. The header is not
        /// read here directly, since any client can forge it.</remarks>
        /// <returns>The client's IP address, or <see langword="null"/> when the server doesn't know it (for example on
        /// an in-memory test server).</returns>
        public string? GetClientIpAddress()
        {
            var address = context.Connection.RemoteIpAddress;

            if (address is null)
            {
                return null;
            }

            return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
        }
    }
}
