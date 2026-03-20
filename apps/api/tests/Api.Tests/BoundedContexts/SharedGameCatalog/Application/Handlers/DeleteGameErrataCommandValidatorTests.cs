using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class DeleteGameErrataCommandValidatorTests
{
    private readonly DeleteGameErrataCommandValidator _validator;

    public DeleteGameErrataCommandValidatorTests()
    {
        _validator = new DeleteGameErrataCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        var command = new DeleteGameErrataCommand(Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyErrataId_FailsValidation()
    {
        var command = new DeleteGameErrataCommand(Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ErrataId);
    }
}