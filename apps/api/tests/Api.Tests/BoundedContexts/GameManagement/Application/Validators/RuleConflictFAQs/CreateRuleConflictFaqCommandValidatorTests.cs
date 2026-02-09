using Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Application.Validators.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Validators.RuleConflictFAQs;

/// <summary>
/// Unit tests for CreateRuleConflictFaqCommandValidator.
/// Issue #3966: Validator tests for FAQ creation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class CreateRuleConflictFaqCommandValidatorTests
{
    private readonly Mock<IGameRepository> _gameRepositoryMock;
    private readonly CreateRuleConflictFaqCommandValidator _validator;

    public CreateRuleConflictFaqCommandValidatorTests()
    {
        _gameRepositoryMock = new Mock<IGameRepository>();
        _validator = new CreateRuleConflictFaqCommandValidator(_gameRepositoryMock.Object);
    }

    [Fact]
    public async Task Should_Pass_When_Valid_Command()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock.Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreateRuleConflictFaqCommand(
            GameId: gameId,
            ConflictType: ConflictType.Contradiction,
            Pattern: "setup_vs_turn_order",
            Resolution: "Setup actions always occur before turn order resolution",
            Priority: 5
        );

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Should_Fail_When_GameId_Is_Empty()
    {
        // Arrange
        var command = new CreateRuleConflictFaqCommand(
            GameId: Guid.Empty,
            ConflictType: ConflictType.Contradiction,
            Pattern: "test_pattern",
            Resolution: "Test resolution",
            Priority: 5
        );

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GameId)
            .WithErrorMessage("GameId is required");
    }

    [Fact]
    public async Task Should_Fail_When_Game_Does_Not_Exist()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock.Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateRuleConflictFaqCommand(
            GameId: gameId,
            ConflictType: ConflictType.Contradiction,
            Pattern: "test_pattern",
            Resolution: "Test resolution",
            Priority: 5
        );

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GameId)
            .WithErrorMessage("Game not found");
    }

    [Fact]
    public async Task Should_Fail_When_ConflictType_Is_Invalid()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock.Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreateRuleConflictFaqCommand(
            GameId: gameId,
            ConflictType: (ConflictType)999, // Invalid enum
            Pattern: "test_pattern",
            Resolution: "Test resolution",
            Priority: 5
        );

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ConflictType)
            .WithErrorMessage("Invalid conflict type");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Fail_When_Pattern_Is_Empty(string? pattern)
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock.Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreateRuleConflictFaqCommand(
            GameId: gameId,
            ConflictType: ConflictType.Contradiction,
            Pattern: pattern!,
            Resolution: "Test resolution",
            Priority: 5
        );

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Pattern)
            .WithErrorMessage("Pattern is required");
    }

    [Fact]
    public async Task Should_Fail_When_Pattern_Exceeds_MaxLength()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock.Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var longPattern = new string('a', 201); // 201 chars > 200 max
        var command = new CreateRuleConflictFaqCommand(
            GameId: gameId,
            ConflictType: ConflictType.Contradiction,
            Pattern: longPattern,
            Resolution: "Test resolution",
            Priority: 5
        );

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Pattern)
            .WithErrorMessage("Pattern cannot exceed 200 characters");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Fail_When_Resolution_Is_Empty(string? resolution)
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock.Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreateRuleConflictFaqCommand(
            GameId: gameId,
            ConflictType: ConflictType.Contradiction,
            Pattern: "test_pattern",
            Resolution: resolution!,
            Priority: 5
        );

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Resolution)
            .WithErrorMessage("Resolution is required");
    }

    [Fact]
    public async Task Should_Fail_When_Resolution_Exceeds_MaxLength()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock.Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var longResolution = new string('a', 2001); // 2001 chars > 2000 max
        var command = new CreateRuleConflictFaqCommand(
            GameId: gameId,
            ConflictType: ConflictType.Contradiction,
            Pattern: "test_pattern",
            Resolution: longResolution,
            Priority: 5
        );

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Resolution)
            .WithErrorMessage("Resolution cannot exceed 2000 characters");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(100)]
    public async Task Should_Fail_When_Priority_Is_Out_Of_Range(int priority)
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock.Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreateRuleConflictFaqCommand(
            GameId: gameId,
            ConflictType: ConflictType.Contradiction,
            Pattern: "test_pattern",
            Resolution: "Test resolution",
            Priority: priority
        );

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Priority)
            .WithErrorMessage("Priority must be between 1 and 10");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task Should_Pass_When_Priority_Is_Valid(int priority)
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _gameRepositoryMock.Setup(r => r.ExistsAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreateRuleConflictFaqCommand(
            GameId: gameId,
            ConflictType: ConflictType.Contradiction,
            Pattern: "test_pattern",
            Resolution: "Test resolution",
            Priority: priority
        );

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
