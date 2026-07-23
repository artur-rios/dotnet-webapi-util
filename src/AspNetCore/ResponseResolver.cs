using ArturRios.Output;
using ArturRios.Util.Http;
using Microsoft.AspNetCore.Mvc;

namespace ArturRios.Util.WebApi.AspNetCore;

/// <summary>Converts output envelopes from <c>ArturRios.Output</c> into ASP.NET Core <see cref="ActionResult{TValue}"/>
/// instances. The HTTP status is resolved in order: an explicit <c>statusCode</c>, then a lookup of the envelope's
/// first error (on failure) or first message (on success) in an optional <c>statusMap</c>, then a default of 200 on
/// success and 400 on failure.</summary>
public static class ResponseResolver
{
    /// <summary>Wraps a <see cref="PaginatedOutput{T}"/> in an <see cref="ActionResult{TValue}"/>. The HTTP status is
    /// resolved from <paramref name="statusCode"/>, then <paramref name="statusMap"/>, then the 200/400 default.</summary>
    /// <param name="paginatedOutput">The paginated result envelope to return.</param>
    /// <param name="statusCode">Optional explicit HTTP status code; when supplied it wins over the map and default.</param>
    /// <param name="statusMap">Optional map from the first error (on failure) or first message (on success) to an HTTP
    /// status code. The caller owns the dictionary and its key comparer.</param>
    public static ActionResult<PaginatedOutput<T>> Resolve<T>(PaginatedOutput<T> paginatedOutput,
        int? statusCode = null, IReadOnlyDictionary<string, int>? statusMap = null)
    {
        var httpStatusCode = ResolveStatusCode(paginatedOutput, statusCode, statusMap);

        return new ObjectResult(paginatedOutput) { StatusCode = httpStatusCode };
    }

    /// <summary>Wraps a <see cref="DataOutput{T}"/> in an <see cref="ActionResult{TValue}"/>. The HTTP status is
    /// resolved from <paramref name="statusCode"/>, then <paramref name="statusMap"/>, then the 200/400 default.</summary>
    /// <param name="dataOutput">The result envelope to return.</param>
    /// <param name="statusCode">Optional explicit HTTP status code; when supplied it wins over the map and default.</param>
    /// <param name="statusMap">Optional map from the first error (on failure) or first message (on success) to an HTTP
    /// status code. The caller owns the dictionary and its key comparer.</param>
    public static ActionResult<DataOutput<T?>> Resolve<T>(DataOutput<T?> dataOutput, int? statusCode = null,
        IReadOnlyDictionary<string, int>? statusMap = null)
    {
        var httpStatusCode = ResolveStatusCode(dataOutput, statusCode, statusMap);

        return new ObjectResult(dataOutput) { StatusCode = httpStatusCode };
    }

    /// <summary>Wraps a <see cref="ProcessOutput"/> in an <see cref="ActionResult{TValue}"/>. The HTTP status is
    /// resolved from <paramref name="statusCode"/>, then <paramref name="statusMap"/>, then the 200/400 default.</summary>
    /// <param name="processOutput">The result envelope to return.</param>
    /// <param name="statusCode">Optional explicit HTTP status code; when supplied it wins over the map and default.</param>
    /// <param name="statusMap">Optional map from the first error (on failure) or first message (on success) to an HTTP
    /// status code. The caller owns the dictionary and its key comparer.</param>
    public static ActionResult<ProcessOutput> Resolve(ProcessOutput processOutput, int? statusCode = null,
        IReadOnlyDictionary<string, int>? statusMap = null)
    {
        var httpStatusCode = ResolveStatusCode(processOutput, statusCode, statusMap);

        return new ObjectResult(processOutput) { StatusCode = httpStatusCode };
    }

    private static int ResolveStatusCode(ProcessOutput output, int? statusCode,
        IReadOnlyDictionary<string, int>? statusMap)
    {
        if (statusCode.HasValue)
        {
            return statusCode.Value;
        }

        if (statusMap is not null)
        {
            var key = output.Success ? output.Messages.FirstOrDefault() : output.Errors.FirstOrDefault();

            if (key is not null && statusMap.TryGetValue(key, out var mapped))
            {
                return mapped;
            }
        }

        return GetDefaultStatusCode(output.Success);
    }

    private static int GetDefaultStatusCode(bool success) => success ? HttpStatusCodes.Ok : HttpStatusCodes.BadRequest;
}
