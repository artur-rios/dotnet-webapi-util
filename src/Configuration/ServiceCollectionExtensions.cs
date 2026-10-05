using ArturRios.Output;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.WebApi.Configuration;

/// <summary>Service registrations shared by web APIs built with this library.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Replaces ASP.NET Core's default invalid-model-state response with a 400 result carrying a failed
    /// <see cref="DataOutput{T}"/> whose errors list each invalid parameter and its message, so validation failures
    /// have the same shape as every other failed response.</summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    public static IServiceCollection AddInvalidModelStateEnvelope(this IServiceCollection services) =>
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .Select(e => $"Parameter: {e.Key} | Error: {e.Value?.Errors.First().ErrorMessage}").ToArray();

                var output = DataOutput<string>.New
                    .WithData(string.Empty)
                    .WithErrors(errors);

                return new BadRequestObjectResult(output);
            };
        });
}
