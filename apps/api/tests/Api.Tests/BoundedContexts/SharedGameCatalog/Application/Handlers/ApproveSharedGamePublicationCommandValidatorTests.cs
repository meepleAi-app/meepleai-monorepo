using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Validator tests for ApproveSharedGamePublicationCommand.
/// Issue #2514: Approval workflow implementation
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ApproveSharedGamePublicationCommandValidatorTests
{
    private readonly ApproveSharedGamePublicationCommandValidator _validator;

    public ApproveSharedGamePublicationCommandValidatorTests()
    {
        _validator = new ApproveSharedGamePublicationCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        var command = new ApproveSharedGamePublicationCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyGameId_FailsValidation()
    {
        var command = new ApproveSharedGamePublicationCommand(Guid.Empty, Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.GameId);
    }

    [Fact]
    public void Validate_WithEmptyApprovedBy_FailsValidation()
    {
        var command = new ApproveSharedGamePublicationCommand(Guid.NewGuid(), Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ApprovedBy);
    }
}