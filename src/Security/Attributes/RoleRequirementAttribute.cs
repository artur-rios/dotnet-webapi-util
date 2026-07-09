using ArturRios.Util.WebApi.Security.Filters;
using Microsoft.AspNetCore.Mvc;

namespace ArturRios.Util.WebApi.Security.Attributes;

/// <summary>Restricts an action or controller to users whose role is one of <see cref="RoleRequirementFilter"/>'s
/// authorized roles, applied declaratively via <see cref="TypeFilterAttribute"/>.</summary>
public class RoleRequirementAttribute : TypeFilterAttribute
{
    /// <summary>Initializes the attribute with the roles allowed to access the decorated action or controller.</summary>
    /// <param name="authorizedRoles">The role values permitted to access the resource.</param>
    public RoleRequirementAttribute(params int[] authorizedRoles) : base(typeof(RoleRequirementFilter))
    {
        object[] arguments = [authorizedRoles];

        Arguments = arguments;
    }
}
