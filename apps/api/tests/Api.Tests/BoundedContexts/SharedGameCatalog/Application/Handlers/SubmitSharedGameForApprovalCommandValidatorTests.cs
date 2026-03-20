using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Validator tests for SubmitSharedGameForApprovalCommand.
/// Issue #2514: Approval workflow implementation
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class SubmitSharedGameForApprovalCommandValidatorTests
{
    private readonly SubmitSharedGameForApprovalCommandValidator _validator;

    public SubmitSharedGameForApprovalCommandValidatorTests()
    {
        _validator = new SubmitSharedGameForApprovalCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        var command = new SubmitSharedGameForApprovalCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyGameId_FailsValidation()
    {
        var command = new SubmitSharedGameForApprovalCommand(Guid.Empty, Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.GameId);
    }

    [Fact]
    public void Validate_WithEmptySubmittedBy_FailsValidation()
    {
        var command = new SubmitSharedGameForApprovalCommand(Guid.NewGuid(), Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SubmittedBy);
    }
}