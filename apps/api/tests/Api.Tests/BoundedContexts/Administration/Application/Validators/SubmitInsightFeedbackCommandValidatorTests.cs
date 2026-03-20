using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Unit tests for SubmitInsightFeedbackCommandValidator.
/// Issue #4124: AI Insights Runtime Validation (Performance + Accuracy).
/// </summary>
public class SubmitInsightFeedbackCommandValidatorTests
{
    private readonly SubmitInsightFeedbackCommandValidator _validator = new();

    private static SubmitInsightFeedbackCommand CreateValidCommand() => new()
    {
        UserId = Guid.NewGuid(),
        InsightId = "backlog-user-20260216",
        InsightType = "Backlog",
        IsRelevant = true,
        Comment = null
    };

    [Fact]
    public async Task Validate_ValidCommand_ShouldNotHaveErrors()
    {
        var command = CreateValidCommand();
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_EmptyUserId_ShouldHaveError()
    {
        var command = CreateValidCommand() with { UserId = Guid.Empty };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public async Task Validate_EmptyInsightId_ShouldHaveError()
    {
        var command = CreateValidCommand() with { InsightId = string.Empty };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.InsightId);
    }

    [Fact]
    public async Task Validate_InsightIdTooLong_ShouldHaveError()
    {
        var command = CreateValidCommand() with { InsightId = new string('a', 201) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.InsightId);
    }

    [Fact]
    public async Task Validate_InsightIdMaxLength_ShouldNotHaveError()
    {
        var command = CreateValidCommand() with { InsightId = new string('a', 200) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveValidationErrorFor(x => x.InsightId);
    }

    [Theory]
    [InlineData("Backlog")]
    [InlineData("RulesReminder")]
    [InlineData("Recommendation")]
    [InlineData("Streak")]
    [InlineData("Achievement")]
    public async Task Validate_ValidInsightType_ShouldNotHaveError(string insightType)
    {
        var command = CreateValidCommand() with { InsightType = insightType };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveValidationErrorFor(x => x.InsightType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Invalid")]
    [InlineData("backlog_wrong")]
    [InlineData("UNKNOWN")]
    public async Task Validate_InvalidInsightType_ShouldHaveError(string insightType)
    {
        var command = CreateValidCommand() with { InsightType = insightType };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.InsightType);
    }

    [Fact]
    public async Task Validate_CommentTooLong_ShouldHaveError()
    {
        var command = CreateValidCommand() with { Comment = new string('c', 501) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Comment);
    }

    [Fact]
    public async Task Validate_CommentMaxLength_ShouldNotHaveError()
    {
        var command = CreateValidCommand() with { Comment = new string('c', 500) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Comment);
    }

    [Fact]
    public async Task Validate_NullComment_ShouldNotHaveError()
    {
        var command = CreateValidCommand() with { Comment = null };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Comment);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Validate_AnyIsRelevant_ShouldNotHaveError(bool isRelevant)
    {
        var command = CreateValidCommand() with { IsRelevant = isRelevant };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}