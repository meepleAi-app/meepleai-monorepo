using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// ISSUE-3499: Unit tests for TutorQueryCommandValidator.
/// Tests validation rules for tutor query requests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3499")]
public class TutorQueryCommandValidatorTests
{
    private readonly TutorQueryCommandValidator _validator;

    public TutorQueryCommandValidatorTests()
    {
        _validator = new TutorQueryCommandValidator();
    }

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = new TutorQueryCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Query: "How do I set up the game?"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyGameId_ShouldFail()
    {
        // Arrange
        var command = new TutorQueryCommand(
            GameId: Guid.Empty,
            SessionId: Guid.NewGuid(),
            Query: "Test query"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "GameId");
    }

    [Fact]
    public void Validate_EmptyQuery_ShouldFail()
    {
        // Arrange
        var command = new TutorQueryCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Query: ""
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Query");
    }

    [Fact]
    public void Validate_QueryTooLong_ShouldFail()
    {
        // Arrange
        var command = new TutorQueryCommand(
            GameId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            Query: new string('a', 2001) // 2001 characters
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Query" && e.ErrorMessage.Contains("2000"));
    }
}
