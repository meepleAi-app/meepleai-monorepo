using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Unit tests for UpvoteFaqCommandValidator.
/// Issue #2681: Public FAQs endpoints
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class UpvoteFaqCommandValidatorTests
{
    private readonly UpvoteFaqCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidFaqId_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new UpvoteFaqCommand(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyFaqId_ShouldHaveError()
    {
        // Arrange
        var command = new UpvoteFaqCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FaqId)
            .WithErrorMessage("FaqId is required");
    }
}