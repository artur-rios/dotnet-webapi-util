namespace ArturRios.Util.WebApi.Security.Attributes;

/// <summary>Marks an action method as exempt from JWT authentication, letting <see cref="Security.Middleware.JwtMiddleware"/> and <see cref="AuthorizeAttribute"/> skip it.</summary>
[AttributeUsage(AttributeTargets.Method)]
public class AllowAnonymousAttribute : Attribute;
