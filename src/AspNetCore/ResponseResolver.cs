using ArturRios.Output;
using ArturRios.Util.Http;
using Microsoft.AspNetCore.Mvc;

namespace ArturRios.Util.WebApi.AspNetCore;

/// <summary>Converts output envelopes from <c>ArturRios.Output</c> into ASP.NET Core <see cref="ActionResult{TValue}"/>
/// instances, defaulting the HTTP status to 200 on success and 400 on failure unless overridden.</summary>
public static class ResponseResolver
{
    /// <summary>Wraps a <see cref="PaginatedOutput{T}"/> in an <see cref="ActionResult{TValue}"/>, defaulting the
    /// HTTP status to 200 on success and 400 on failure unless <paramref name="statusCode"/> is supplied.</summary>
    /// <param name="paginatedOutput">The paginated result envelope to return.</param>
    /// <param name="statusCode">Optional explicit HTTP status code.</param>
    public static ActionResult<PaginatedOutput<T>> Resolve<T>(PaginatedOutput<T> paginatedOutput,
        int? statusCode = null)
    {
        var httpStatusCode = statusCode ?? GetDefaultStatusCode(paginatedOutput.Success);

        return new ObjectResult(paginatedOutput) { StatusCode = httpStatusCode };
    }

    /// <summary>Wraps a <see cref="DataOutput{T}"/> in an <see cref="ActionResult{TValue}"/>, defaulting the
    /// HTTP status to 200 on success and 400 on failure unless <paramref name="statusCode"/> is supplied.</summary>
    /// <param name="dataOutput">The result envelope to return.</param>
    /// <param name="statusCode">Optional explicit HTTP status code.</param>
    public static ActionResult<DataOutput<T?>> Resolve<T>(DataOutput<T?> dataOutput, int? statusCode = null)
    {
        var httpStatusCode = statusCode ?? GetDefaultStatusCode(dataOutput.Success);

        return new ObjectResult(dataOutput) { StatusCode = httpStatusCode };
    }

    /// <summary>Wraps a <see cref="ProcessOutput"/> in an <see cref="ActionResult{TValue}"/>, defaulting the
    /// HTTP status to 200 on success and 400 on failure unless <paramref name="statusCode"/> is supplied.</summary>
    /// <param name="processOutput">The result envelope to return.</param>
    /// <param name="statusCode">Optional explicit HTTP status code.</param>
    public static ActionResult<ProcessOutput> Resolve(ProcessOutput processOutput, int? statusCode = null)
    {
        var httpStatusCode = statusCode ?? GetDefaultStatusCode(processOutput.Success);

        return new ObjectResult(processOutput) { StatusCode = httpStatusCode };
    }

    private static int GetDefaultStatusCode(bool success) => success ? HttpStatusCodes.Ok : HttpStatusCodes.BadRequest;
}
