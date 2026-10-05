using ArturRios.Output;
using ArturRios.Util.WebApi.AspNetCore;
using Microsoft.AspNetCore.Mvc;

namespace ArturRios.Util.WebApi.Tests.AspNetCore;

[Trait("Category", "Unit")]
public class OutputActionResultExtensionsTests
{
    // --- Behavior preserved when no map / no statusCode ---

    [Fact]
    public void GivenSuccessProcessOutput_WhenConvertingToAnActionResultWithoutArgs_ThenStatusIs200()
    {
        var output = ProcessOutput.New;

        var result = output.ToActionResult();

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(output, objectResult.Value);
    }

    [Fact]
    public void GivenFailedProcessOutput_WhenConvertingToAnActionResultWithoutArgs_ThenStatusIs400()
    {
        var output = ProcessOutput.New.WithError("boom");

        var result = output.ToActionResult();

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    // --- Explicit statusCode wins over map ---

    [Fact]
    public void GivenExplicitStatusCodeAndMatchingMap_WhenConvertingToAnActionResult_ThenStatusCodeWins()
    {
        var output = ProcessOutput.New.WithError("not-found");
        var map = new Dictionary<string, int> { ["not-found"] = 404 };

        var result = output.ToActionResult(statusCode: 409, statusMap: map);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(409, objectResult.StatusCode);
    }

    // --- Map hit on first error (failure) ---

    [Fact]
    public void GivenFailedOutputWhoseFirstErrorIsMapped_WhenConvertingToAnActionResult_ThenMappedStatusUsed()
    {
        var output = ProcessOutput.New.WithError("not-found").WithError("ignored");
        var map = new Dictionary<string, int> { ["not-found"] = 404 };

        var result = output.ToActionResult(statusMap: map);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    // --- Map hit on first message (success) ---

    [Fact]
    public void GivenSuccessOutputWhoseFirstMessageIsMapped_WhenConvertingToAnActionResult_ThenMappedStatusUsed()
    {
        var output = ProcessOutput.New.WithMessage("created");
        var map = new Dictionary<string, int> { ["created"] = 201 };

        var result = output.ToActionResult(statusMap: map);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(201, objectResult.StatusCode);
    }

    // --- Map miss falls back to default ---

    [Fact]
    public void GivenFailedOutputWhoseFirstErrorIsNotMapped_WhenConvertingToAnActionResult_ThenFallsBackTo400()
    {
        var output = ProcessOutput.New.WithError("unmapped");
        var map = new Dictionary<string, int> { ["not-found"] = 404 };

        var result = output.ToActionResult(statusMap: map);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    // --- Empty message list with map present falls back to default ---

    [Fact]
    public void GivenSuccessOutputWithNoMessages_WhenConvertingToAnActionResultWithMap_ThenFallsBackTo200()
    {
        var output = ProcessOutput.New;
        var map = new Dictionary<string, int> { ["created"] = 201 };

        var result = output.ToActionResult(statusMap: map);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    // --- Caller-controlled comparer (case-insensitive) ---

    [Fact]
    public void GivenCaseInsensitiveMap_WhenFirstErrorDiffersInCase_ThenMappedStatusUsed()
    {
        var output = ProcessOutput.New.WithError("Not-Found");
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["not-found"] = 404
        };

        var result = output.ToActionResult(statusMap: map);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    // --- Per-envelope wiring: DataOutput ---

    [Fact]
    public void GivenFailedDataOutputWhoseFirstErrorIsMapped_WhenConvertingToAnActionResult_ThenMappedStatusUsed()
    {
        var output = DataOutput<string?>.New.WithError("conflict");
        var map = new Dictionary<string, int> { ["conflict"] = 409 };

        var result = output.ToActionResult(statusMap: map);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(409, objectResult.StatusCode);
        Assert.Same(output, objectResult.Value);
    }

    // --- Per-envelope wiring: PaginatedOutput ---

    [Fact]
    public void GivenFailedPaginatedOutputWhoseFirstErrorIsMapped_WhenConvertingToAnActionResult_ThenMappedStatusUsed()
    {
        var output = PaginatedOutput<string>.New.WithError("forbidden");
        var map = new Dictionary<string, int> { ["forbidden"] = 403 };

        var result = output.ToActionResult(statusMap: map);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, objectResult.StatusCode);
        Assert.Same(output, objectResult.Value);
    }
}
