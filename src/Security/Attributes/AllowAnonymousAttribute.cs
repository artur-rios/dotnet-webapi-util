using Microsoft.AspNetCore.Authorization;

namespace ArturRios.Util.WebApi.Security.Attributes;

/// <summary>Marks an action method or a whole controller as exempt from authentication, letting
/// <see cref="Security.Middleware.AuthenticationMiddleware"/>, <see cref="AuthorizeAttribute"/> and
/// <see cref="RoleRequirementAttribute"/> skip it. Implements <see cref="IAllowAnonymous"/>, so ASP.NET Core's own
/// <c>[AllowAnonymous]</c> is honored by this library too, and this attribute by ASP.NET Core's authorization.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AllowAnonymousAttribute : Attribute, IAllowAnonymous;
