using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class UpdateGameFaqCommandValidatorTests
{
    private readonly UpdateGameFaqCommandValidator _validator;

    public UpdateGameFaqCommandValidatorTests()
    {
        _validator = new UpdateGameFaqCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        var command = new UpdateGameFaqCommand(Guid.NewGuid(), "How to play?", "Follow the rulebook", 1);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyQuestion_FailsValidation(string question)
    {
        var command = new UpdateGameFaqCommand(Guid.NewGuid(), question, "Answer", 0);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Question);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyAnswer_FailsValidation(string answer)
    {
        var command = new UpdateGameFaqCommand(Guid.NewGuid(), "Question?", answer, 0);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Answer);
    }

    [Fact]
    public void Validate_WithEmptyFaqId_FailsValidation()
    {
        var command = new UpdateGameFaqCommand(Guid.Empty, "Question?", "Answer", 0);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.FaqId);
    }

    [Fact]
    public void Validate_WithNegativeOrder_FailsValidation()
    {
        var command = new UpdateGameFaqCommand(Guid.NewGuid(), "Question?", "Answer", -1);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Order);
    }

    [Fact]
    public void Validate_WithQuestionTooLong_FailsValidation()
    {
        var longQuestion = new string('a', 501);
        var command = new UpdateGameFaqCommand(Guid.NewGuid(), longQuestion, "Answer", 0);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Question);
    }
}
