using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Unit tests for GetGameFaqsQueryValidator.
/// Issue #2681: Public FAQs endpoints
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class GetGameFaqsQueryValidatorTests
{
    private readonly GetGameFaqsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithValidParameters_ShouldNotHaveErrors()
    {
        // Arrange
        var query = new GetGameFaqsQuery(Guid.NewGuid(), Limit: 10, Offset: 0);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyGameId_ShouldHaveError()
    {
        // Arrange
        var query = new GetGameFaqsQuery(Guid.Empty, Limit: 10, Offset: 0);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GameId)
            .WithErrorMessage("GameId is required");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_WithInvalidLimit_ShouldHaveError(int limit)
    {
        // Arrange
        var query = new GetGameFaqsQuery(Guid.NewGuid(), Limit: limit, Offset: 0);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Limit)
            .WithErrorMessage("Limit must be between 1 and 100");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_WithValidLimit_ShouldNotHaveErrors(int limit)
    {
        // Arrange
        var query = new GetGameFaqsQuery(Guid.NewGuid(), Limit: limit, Offset: 0);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Limit);
    }

    [Fact]
    public void Validate_WithNegativeOffset_ShouldHaveError()
    {
        // Arrange
        var query = new GetGameFaqsQuery(Guid.NewGuid(), Limit: 10, Offset: -1);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Offset)
            .WithErrorMessage("Offset cannot be negative");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(100)]
    public void Validate_WithValidOffset_ShouldNotHaveErrors(int offset)
    {
        // Arrange
        var query = new GetGameFaqsQuery(Guid.NewGuid(), Limit: 10, Offset: offset);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Offset);
    }
}
