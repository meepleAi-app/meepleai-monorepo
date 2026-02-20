using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Unit tests for GetGameWizardPreviewQueryValidator.
/// Issue #4823: Backend Game Preview API
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GetGameWizardPreviewQueryValidatorTests
{
    private readonly GetGameWizardPreviewQueryValidator _validator;

    public GetGameWizardPreviewQueryValidatorTests()
    {
        _validator = new GetGameWizardPreviewQueryValidator();
    }

    [Fact]
    public void Validate_WithValidQuery_ShouldPass()
    {
        // Arrange
        var query = new GetGameWizardPreviewQuery(Guid.NewGuid(), "catalog", Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyGameId_ShouldFail()
    {
        // Arrange
        var query = new GetGameWizardPreviewQuery(Guid.Empty, "catalog", Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GameId);
    }

    [Fact]
    public void Validate_WithEmptySource_ShouldFail()
    {
        // Arrange
        var query = new GetGameWizardPreviewQuery(Guid.NewGuid(), "", Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Source);
    }

    [Fact]
    public void Validate_WithInvalidSource_ShouldFail()
    {
        // Arrange
        var query = new GetGameWizardPreviewQuery(Guid.NewGuid(), "invalid", Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Source)
            .WithErrorMessage("Source must be one of: catalog");
    }

    [Fact]
    public void Validate_WithEmptyUserId_ShouldFail()
    {
        // Arrange
        var query = new GetGameWizardPreviewQuery(Guid.NewGuid(), "catalog", Guid.Empty);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Theory]
    [InlineData("catalog")]
    [InlineData("Catalog")]
    [InlineData("CATALOG")]
    public void Validate_WithCatalogSourceCaseInsensitive_ShouldPass(string source)
    {
        // Arrange
        var query = new GetGameWizardPreviewQuery(Guid.NewGuid(), source, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
