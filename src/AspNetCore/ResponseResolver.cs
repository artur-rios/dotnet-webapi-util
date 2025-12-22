using ArturRios.Output;
using ArturRios.Util.Http;
using Microsoft.AspNetCore.Mvc;

namespace ArturRios.Util.WebApi.AspNetCore;

public static class ResponseResolver
{
    public static ActionResult<PaginatedOutput<T>> Resolve<T>(PaginatedOutput<T> paginatedOutput,
        int? statusCode = null)
    {
        var httpStatusCode = statusCode ?? GetDefaultStatusCode(paginatedOutput.Success);

        return new ObjectResult(paginatedOutput) { StatusCode = httpStatusCode };
    }

    public static ActionResult<DataOutput<T?>> Resolve<T>(DataOutput<T?> dataOutput, int? statusCode = null)
    {
        var httpStatusCode = statusCode ?? GetDefaultStatusCode(dataOutput.Success);

        return new ObjectResult(dataOutput) { StatusCode = httpStatusCode };
    }

    public static ActionResult<ProcessOutput> Resolve(ProcessOutput processOutput, int? statusCode = null)
    {
        var httpStatusCode = statusCode ?? GetDefaultStatusCode(processOutput.Success);

        return new ObjectResult(processOutput) { StatusCode = httpStatusCode };
    }

    private static int GetDefaultStatusCode(bool success) => success ? HttpStatusCodes.Ok : HttpStatusCodes.BadRequest;
}
