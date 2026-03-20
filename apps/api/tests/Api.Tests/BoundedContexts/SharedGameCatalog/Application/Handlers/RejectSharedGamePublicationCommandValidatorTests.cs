using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Validator tests for RejectSharedGamePublicationCommand.
/// Issue #2514: Approval workflow implementation
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class RejectSharedGamePublicationCommandValidatorTests
{
    private readonly RejectSharedGamePublicationCommandValidator _validator;

    public RejectSharedGamePublicationCommandValidatorTests()
    {
        _validator = new RejectSharedGamePublicationCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        var command = new RejectSharedGamePublicationCommand(Guid.NewGuid(), Guid.NewGuid(), "Valid reason");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyGameId_FailsValidation()
    {
        var command = new RejectSharedGamePublicationCommand(Guid.Empty, Guid.NewGuid(), "Reason");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.GameId);
    }

    [Fact]
    public void Validate_WithEmptyRejectedBy_FailsValidation()
    {
        var command = new RejectSharedGamePublicationCommand(Guid.NewGuid(), Guid.Empty, "Reason");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RejectedBy);
    }

    [Fact]
    public void Validate_WithEmptyReason_FailsValidation()
    {
        var command = new RejectSharedGamePublicationCommand(Guid.NewGuid(), Guid.NewGuid(), "");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Validate_WithTooLongReason_FailsValidation()
    {
        var longReason = new string('a', 1001);
        var command = new RejectSharedGamePublicationCommand(Guid.NewGuid(), Guid.NewGuid(), longReason);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }
}