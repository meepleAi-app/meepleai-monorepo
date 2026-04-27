using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetTopContributors;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetTopContributors;

/// <summary>
/// Unit tests for <see cref="GetTopContributorsQueryValidator"/>.
/// Issue #593 (Wave A.3a) — spec §5.4: <c>limit</c> bounded 1..20.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetTopContributorsQueryValidatorTests
{
    private readonly GetTopContributorsQueryValidator _validator = new();

    [Fact]
    public void Should_Pass_When_Limit_Is_Default_5()
    {
        var query = new GetTopContributorsQuery(); // Limit = 5
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void Should_Pass_When_Limit_Is_Within_Bounds(int limit)
    {
        var query = new GetTopContributorsQuery(limit);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    [InlineData(100)]
    public void Should_Fail_When_Limit_Is_Out_Of_Bounds(int limit)
    {
        var query = new GetTopContributorsQuery(limit);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Limit)
            .WithErrorMessage("Limit must be between 1 and 20.");
    }
}
