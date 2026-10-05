using ArturRios.Output;
using ArturRios.Util.Http;
using Microsoft.AspNetCore.Mvc;

namespace ArturRios.Util.WebApi.Security.Filters;

/// <summary>The failed-envelope results the authorization filters short-circuit with, so a 401 or 403 from a filter
/// has the same shape as the one <see cref="Middleware.AuthenticationMiddleware"/> writes.</summary>
internal static class AuthorizationResults
{
    public const string UnauthorizedMessage = "Unauthorized";

    public const string ForbiddenMessage = "You do not have permission to access this resource";

    public static ObjectResult Unauthorized() =>
        new(ProcessOutput.New.WithError(UnauthorizedMessage)) { StatusCode = HttpStatusCodes.Unauthorized };

    public static ObjectResult Forbidden() =>
        new(ProcessOutput.New.WithError(ForbiddenMessage)) { StatusCode = HttpStatusCodes.Forbidden };
}
