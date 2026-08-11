using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Tests for <see cref="CreateRuleCommentCommandValidator"/> line-number handling.
/// A non-positive line number previously reached the handler, which threw
/// <see cref="InvalidOperationException"/> (→ HTTP 500). The validator must reject it → 422.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class CreateRuleCommentCommandValidatorTests
{
    private readonly CreateRuleCommentCommandValidator _validator = new();

    private static CreateRuleCommentCommand CommandWithLineNumber(int? lineNumber) =>
        new(
            GameId: "game-1",
            Version: "1.0.0",
            LineNumber: lineNumber,
            CommentText: "A rule comment",
            UserId: Guid.NewGuid());

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(42)]
    public void Validate_WithNullOrPositiveLineNumber_HasNoLineNumberError(int? lineNumber)
    {
        var result = _validator.TestValidate(CommandWithLineNumber(lineNumber));

        result.ShouldNotHaveValidationErrorFor(x => x.LineNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WithNonPositiveLineNumber_HasLineNumberError(int? lineNumber)
    {
        var result = _validator.TestValidate(CommandWithLineNumber(lineNumber));

        result.ShouldHaveValidationErrorFor(x => x.LineNumber);
    }
}
