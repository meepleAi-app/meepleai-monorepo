using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators.ChatSession;

/// <summary>
/// Tests for CreateChatSessionCommandValidator.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateChatSessionCommandValidatorTests
{
    private readonly CreateChatSessionCommandValidator _validator;

    public CreateChatSessionCommandValidatorTests()
    {
        _validator = new CreateChatSessionCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var command = new CreateChatSessionCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            Title: "Valid Title");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyUserId_ShouldHaveValidationError()
    {
        // Arrange
        var command = new CreateChatSessionCommand(
            UserId: Guid.Empty,
            GameId: Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    [Fact]
    public void Validate_WithEmptyGameId_ShouldHaveValidationError()
    {
        // Arrange
        var command = new CreateChatSessionCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GameId)
            .WithErrorMessage("GameId is required");
    }

    [Fact]
    public void Validate_WithNullTitle_ShouldNotHaveValidationError()
    {
        // Arrange
        var command = new CreateChatSessionCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            Title: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithEmptyTitle_ShouldNotHaveValidationError()
    {
        // Arrange
        var command = new CreateChatSessionCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            Title: string.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithTitleAt200Characters_ShouldNotHaveValidationError()
    {
        // Arrange
        var command = new CreateChatSessionCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            Title: new string('a', 200));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithTitleExceeding200Characters_ShouldHaveValidationError()
    {
        // Arrange
        var command = new CreateChatSessionCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            Title: new string('a', 201));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title cannot exceed 200 characters");
    }

    [Fact]
    public void Validate_WithBothUserIdAndGameIdEmpty_ShouldHaveMultipleErrors()
    {
        // Arrange
        var command = new CreateChatSessionCommand(
            UserId: Guid.Empty,
            GameId: Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
        result.ShouldHaveValidationErrorFor(x => x.GameId);
        result.Errors.Count.Should().Be(2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(199)]
    [InlineData(200)]
    public void Validate_WithValidTitleLengths_ShouldNotHaveValidationError(int length)
    {
        // Arrange
        var command = new CreateChatSessionCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            Title: new string('a', length));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }
}
