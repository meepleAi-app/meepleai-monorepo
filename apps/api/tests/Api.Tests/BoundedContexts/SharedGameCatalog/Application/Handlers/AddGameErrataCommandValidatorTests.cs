using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class AddGameErrataCommandValidatorTests
{
    private readonly AddGameErrataCommandValidator _validator;

    public AddGameErrataCommandValidatorTests()
    {
        _validator = new AddGameErrataCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        var command = new AddGameErrataCommand(Guid.NewGuid(), "Typo in rule 3.2", "Page 15", DateTime.UtcNow.AddDays(-1));
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyDescription_FailsValidation(string description)
    {
        var command = new AddGameErrataCommand(Guid.NewGuid(), description, "Page 1", DateTime.UtcNow);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithEmptySharedGameId_FailsValidation()
    {
        var command = new AddGameErrataCommand(Guid.Empty, "Description", "Page 1", DateTime.UtcNow);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SharedGameId);
    }
}