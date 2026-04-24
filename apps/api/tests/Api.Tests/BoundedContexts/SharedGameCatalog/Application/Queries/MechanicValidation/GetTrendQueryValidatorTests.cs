using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class GetTrendQueryValidatorTests
{
    private readonly GetTrendQueryValidator _validator = new();

    [Fact]
    public void Validate_WithEmptySharedGameId_HasError()
    {
        var query = new GetTrendQuery(Guid.Empty);

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.SharedGameId)
            .WithErrorMessage("SharedGameId is required");
    }

    [Fact]
    public void Validate_WithTakeZero_HasError()
    {
        var query = new GetTrendQuery(Guid.NewGuid(), Take: 0);

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.Take)
            .WithErrorMessage("Take must be between 1 and 100");
    }

    [Fact]
    public void Validate_WithTakeAbove100_HasError()
    {
        var query = new GetTrendQuery(Guid.NewGuid(), Take: 101);

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.Take)
            .WithErrorMessage("Take must be between 1 and 100");
    }

    [Fact]
    public void Validate_WithTakeOne_NoError()
    {
        var query = new GetTrendQuery(Guid.NewGuid(), Take: 1);

        var result = _validator.TestValidate(query);

        result.ShouldNotHaveValidationErrorFor(x => x.Take);
        result.ShouldNotHaveValidationErrorFor(x => x.SharedGameId);
    }

    [Fact]
    public void Validate_WithTakeOneHundred_NoError()
    {
        var query = new GetTrendQuery(Guid.NewGuid(), Take: 100);

        var result = _validator.TestValidate(query);

        result.ShouldNotHaveValidationErrorFor(x => x.Take);
        result.ShouldNotHaveValidationErrorFor(x => x.SharedGameId);
    }

    [Fact]
    public void Validate_WithDefaultTake_NoError()
    {
        var query = new GetTrendQuery(Guid.NewGuid());

        var result = _validator.TestValidate(query);

        result.ShouldNotHaveValidationErrorFor(x => x.Take);
        result.ShouldNotHaveValidationErrorFor(x => x.SharedGameId);
    }
}
