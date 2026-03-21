using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class AddGameFaqCommandValidatorTests
{
    private readonly AddGameFaqCommandValidator _validator;

    public AddGameFaqCommandValidatorTests()
    {
        _validator = new AddGameFaqCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        var command = new AddGameFaqCommand(Guid.NewGuid(), "How to play?", "Read the rules", 1);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyQuestion_FailsValidation(string question)
    {
        var command = new AddGameFaqCommand(Guid.NewGuid(), question, "Answer", 1);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Question);
    }

    [Fact]
    public void Validate_WithEmptyAnswer_FailsValidation()
    {
        var command = new AddGameFaqCommand(Guid.NewGuid(), "Question?", "", 1);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Answer);
    }
}
