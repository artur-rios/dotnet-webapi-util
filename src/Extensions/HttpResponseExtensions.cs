using ArturRios.Output;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.WebApi.Extensions;

/// <summary>Extension methods for writing output envelopes directly to an <see cref="HttpResponse"/>, outside MVC.</summary>
internal static class HttpResponseExtensions
{
    /// <summary>Sets the status code and writes <paramref name="output"/> as JSON, using the app's configured
    /// <c>Microsoft.AspNetCore.Http.Json.JsonOptions</c> (camelCase by default), so envelopes written by
    /// middlewares have the same shape as the ones MVC writes for controller results.</summary>
    /// <param name="response">The response to write to.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="output">The envelope to serialize.</param>
    public static Task WriteOutputAsync<TOutput>(this HttpResponse response, int statusCode, TOutput output)
        where TOutput : ProcessOutput
    {
        response.StatusCode = statusCode;

        return response.WriteAsJsonAsync(output);
    }
}
