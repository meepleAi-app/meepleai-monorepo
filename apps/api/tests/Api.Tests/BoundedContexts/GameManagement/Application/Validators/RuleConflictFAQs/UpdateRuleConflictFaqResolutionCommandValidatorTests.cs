using Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Application.Validators.RuleConflictFAQs;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Validators.RuleConflictFAQs;

/// <summary>
/// Unit tests for UpdateRuleConflictFaqResolutionCommandValidator.
/// Issue #3966: Validator tests for FAQ resolution updates.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class UpdateRuleConflictFaqResolutionCommandValidatorTests
{
    private readonly UpdateRuleConflictFaqResolutionCommandValidator _validator = new();

    [Fact]
    public void Should_Pass_When_Valid_Command()
    {
        // Arrange
        var command = new UpdateRuleConflictFaqResolutionCommand(
            Id: Guid.NewGuid(),
            Resolution: "Updated resolution text"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_Id_Is_Empty()
    {
        // Arrange
        var command = new UpdateRuleConflictFaqResolutionCommand(
            Id: Guid.Empty,
            Resolution: "Test resolution"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id)
            .WithErrorMessage("FAQ ID is required");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_When_Resolution_Is_Empty(string? resolution)
    {
        // Arrange
        var command = new UpdateRuleConflictFaqResolutionCommand(
            Id: Guid.NewGuid(),
            Resolution: resolution!
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Resolution)
            .WithErrorMessage("Resolution is required");
    }

    [Fact]
    public void Should_Fail_When_Resolution_Exceeds_MaxLength()
    {
        // Arrange
        var longResolution = new string('a', 2001); // 2001 chars > 2000 max
        var command = new UpdateRuleConflictFaqResolutionCommand(
            Id: Guid.NewGuid(),
            Resolution: longResolution
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Resolution)
            .WithErrorMessage("Resolution cannot exceed 2000 characters");
    }

    [Fact]
    public void Should_Pass_When_Resolution_At_MaxLength()
    {
        // Arrange
        var maxResolution = new string('a', 2000); // Exactly 2000 chars
        var command = new UpdateRuleConflictFaqResolutionCommand(
            Id: Guid.NewGuid(),
            Resolution: maxResolution
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
