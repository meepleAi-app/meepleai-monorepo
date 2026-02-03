using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators.ChatSession;

/// <summary>
/// Tests for DeleteChatSessionCommandValidator.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DeleteChatSessionCommandValidatorTests
{
    private readonly DeleteChatSessionCommandValidator _validator;

    public DeleteChatSessionCommandValidatorTests()
    {
        _validator = new DeleteChatSessionCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var command = new DeleteChatSessionCommand(
            SessionId: Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptySessionId_ShouldHaveValidationError()
    {
        // Arrange
        var command = new DeleteChatSessionCommand(
            SessionId: Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionId)
            .WithErrorMessage("SessionId is required");
    }

    [Theory]
    [MemberData(nameof(GetValidGuids))]
    public void Validate_WithVariousValidGuids_ShouldNotHaveValidationErrors(Guid sessionId)
    {
        // Arrange
        var command = new DeleteChatSessionCommand(SessionId: sessionId);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    public static IEnumerable<object[]> GetValidGuids()
    {
        yield return new object[] { Guid.NewGuid() };
        yield return new object[] { Guid.NewGuid() };
        yield return new object[] { Guid.NewGuid() };
        // Specific known GUIDs for edge case testing
        yield return new object[] { Guid.Parse("12345678-1234-1234-1234-123456789abc") };
        yield return new object[] { Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff") };
    }
}
