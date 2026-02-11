using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;

[Trait("Category", TestCategories.Unit)]
public class GetCatalogTrendingQueryValidatorTests
{
    private readonly GetCatalogTrendingQueryValidator _validator;

    public GetCatalogTrendingQueryValidatorTests()
    {
        _validator = new GetCatalogTrendingQueryValidator();
    }

    [Fact]
    public void Validate_WithDefaultLimit_PassesValidation()
    {
        // Arrange
        var query = new GetCatalogTrendingQuery();

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    public void Validate_WithValidLimit_PassesValidation(int limit)
    {
        // Arrange
        var query = new GetCatalogTrendingQuery(limit);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithZeroLimit_FailsValidation()
    {
        // Arrange
        var query = new GetCatalogTrendingQuery(0);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Limit)
            .WithErrorMessage("Limit must be between 1 and 50.");
    }

    [Fact]
    public void Validate_WithNegativeLimit_FailsValidation()
    {
        // Arrange
        var query = new GetCatalogTrendingQuery(-1);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Limit);
    }

    [Fact]
    public void Validate_WithExcessiveLimit_FailsValidation()
    {
        // Arrange
        var query = new GetCatalogTrendingQuery(51);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Limit)
            .WithErrorMessage("Limit must be between 1 and 50.");
    }
}
