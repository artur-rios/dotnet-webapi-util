using ArturRios.Output;
using ArturRios.Util.Http;
using Microsoft.AspNetCore.Mvc;

namespace ArturRios.Util.WebApi.AspNetCore;

/// <summary>Converts output envelopes from <c>ArturRios.Output</c> into ASP.NET Core <see cref="ActionResult{TValue}"/>
/// instances. The HTTP status is resolved in order: an explicit <c>statusCode</c>, then a lookup of the envelope's
/// first error (on failure) or first message (on success) in an optional <c>statusMap</c>, then a default of 200 on
/// success and 400 on failure.</summary>
public static class OutputActionResultExtensions
{
    /// <param name="paginatedOutput">The paginated result envelope to return.</param>
    extension<T>(PaginatedOutput<T> paginatedOutput)
    {
        /// <summary>Wraps a <see cref="PaginatedOutput{T}"/> in an <see cref="ActionResult{TValue}"/>. The HTTP status is
        /// resolved from <paramref name="statusCode"/>, then <paramref name="statusMap"/>, then the 200/400 default.</summary>
        /// <param name="statusCode">Optional explicit HTTP status code; when supplied it wins over the map and default.</param>
        /// <param name="statusMap">Optional map from the first error (on failure) or first message (on success) to an HTTP
        /// status code. The caller owns the dictionary and its key comparer.</param>
        public ActionResult<PaginatedOutput<T>> ToActionResult(int? statusCode = null,
            IReadOnlyDictionary<string, int>? statusMap = null) =>
            new ObjectResult(paginatedOutput) { StatusCode = paginatedOutput.ResolveStatusCode(statusCode, statusMap) };
    }

    /// <param name="dataOutput">The result envelope to return.</param>
    extension<T>(DataOutput<T?> dataOutput)
    {
        /// <summary>Wraps a <see cref="DataOutput{T}"/> in an <see cref="ActionResult{TValue}"/>. The HTTP status is
        /// resolved from <paramref name="statusCode"/>, then <paramref name="statusMap"/>, then the 200/400 default.</summary>
        /// <param name="statusCode">Optional explicit HTTP status code; when supplied it wins over the map and default.</param>
        /// <param name="statusMap">Optional map from the first error (on failure) or first message (on success) to an HTTP
        /// status code. The caller owns the dictionary and its key comparer.</param>
        public ActionResult<DataOutput<T?>> ToActionResult(int? statusCode = null,
            IReadOnlyDictionary<string, int>? statusMap = null) =>
            new ObjectResult(dataOutput) { StatusCode = dataOutput.ResolveStatusCode(statusCode, statusMap) };
    }

    /// <param name="output">The result envelope.</param>
    extension(ProcessOutput output)
    {
        /// <summary>Wraps a <see cref="ProcessOutput"/> in an <see cref="ActionResult{TValue}"/>. The HTTP status is
        /// resolved from <paramref name="statusCode"/>, then <paramref name="statusMap"/>, then the 200/400 default.</summary>
        /// <param name="statusCode">Optional explicit HTTP status code; when supplied it wins over the map and default.</param>
        /// <param name="statusMap">Optional map from the first error (on failure) or first message (on success) to an HTTP
        /// status code. The caller owns the dictionary and its key comparer.</param>
        public ActionResult<ProcessOutput> ToActionResult(int? statusCode = null,
            IReadOnlyDictionary<string, int>? statusMap = null) =>
            new ObjectResult(output) { StatusCode = output.ResolveStatusCode(statusCode, statusMap) };

        /// <summary>Resolves the HTTP status code for the envelope: <paramref name="statusCode"/> when supplied, then the
        /// <paramref name="statusMap"/> entry for the envelope's first error (on failure) or first message (on success),
        /// then 200 on success and 400 on failure.</summary>
        /// <param name="statusCode">Optional explicit HTTP status code; when supplied it wins over the map and default.</param>
        /// <param name="statusMap">Optional map from the first error or message to an HTTP status code.</param>
        public int ResolveStatusCode(int? statusCode = null, IReadOnlyDictionary<string, int>? statusMap = null)
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

            return output.Success ? HttpStatusCodes.Ok : HttpStatusCodes.BadRequest;
        }
    }
}
