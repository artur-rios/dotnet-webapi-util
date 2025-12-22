using ArturRios.Util.WebApi.Security.Filters;
using Microsoft.AspNetCore.Mvc;

namespace ArturRios.Util.WebApi.Security.Attributes;

public class RoleRequirementAttribute : TypeFilterAttribute
{
    public RoleRequirementAttribute(params int[] authorizedRoles) : base(typeof(RoleRequirementFilter))
    {
        object[] arguments = [authorizedRoles];

        Arguments = arguments;
    }
}
