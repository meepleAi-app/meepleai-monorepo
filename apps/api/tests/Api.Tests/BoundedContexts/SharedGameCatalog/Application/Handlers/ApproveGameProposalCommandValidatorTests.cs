using Api.BoundedContexts.SharedGameCatalog.Application.Commands.ApproveGameProposal;
using Api.BoundedContexts.SharedGameCatalog.Application.Validators.ApproveGameProposal;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ApproveGameProposalCommandValidatorTests
{
    private readonly ApproveGameProposalCommandValidator _validator;

    public ApproveGameProposalCommandValidatorTests()
    {
        _validator = new ApproveGameProposalCommandValidator();
    }

    [Fact]
    public void Validate_WithValidApproveAsNewCommand_Passes()
    {
        // Arrange
        var command = new ApproveGameProposalCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: Guid.NewGuid(),
            ApprovalAction: ProposalApprovalAction.ApproveAsNew,
            TargetSharedGameId: null,
            AdminNotes: "Approved");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyShareRequestId_Fails()
    {
        // Arrange
        var command = new ApproveGameProposalCommand(
            ShareRequestId: Guid.Empty,
            AdminId: Guid.NewGuid(),
            ApprovalAction: ProposalApprovalAction.ApproveAsNew,
            TargetSharedGameId: null,
            AdminNotes: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.ShareRequestId)
            .WithErrorMessage("ShareRequestId is required");
    }

    [Fact]
    public void Validate_WithEmptyAdminId_Fails()
    {
        // Arrange
        var command = new ApproveGameProposalCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: Guid.Empty,
            ApprovalAction: ProposalApprovalAction.ApproveAsNew,
            TargetSharedGameId: null,
            AdminNotes: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.AdminId)
            .WithErrorMessage("AdminId is required");
    }

    [Fact]
    public void Validate_WithInvalidApprovalAction_Fails()
    {
        // Arrange
        var command = new ApproveGameProposalCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: Guid.NewGuid(),
            ApprovalAction: (ProposalApprovalAction)999, // Invalid enum value
            TargetSharedGameId: null,
            AdminNotes: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.ApprovalAction)
            .WithErrorMessage("Invalid approval action");
    }

    [Fact]
    public void Validate_WithMergeKnowledgeBase_MissingTargetSharedGameId_Fails()
    {
        // Arrange
        var command = new ApproveGameProposalCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: Guid.NewGuid(),
            ApprovalAction: ProposalApprovalAction.MergeKnowledgeBase,
            TargetSharedGameId: null, // Required for MergeKnowledgeBase
            AdminNotes: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.TargetSharedGameId)
            .WithErrorMessage("TargetSharedGameId is required for MergeKnowledgeBase and ApproveAsVariant actions");
    }

    [Fact]
    public void Validate_WithMergeKnowledgeBase_EmptyTargetSharedGameId_Fails()
    {
        // Arrange
        var command = new ApproveGameProposalCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: Guid.NewGuid(),
            ApprovalAction: ProposalApprovalAction.MergeKnowledgeBase,
            TargetSharedGameId: Guid.Empty, // Required for MergeKnowledgeBase
            AdminNotes: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.TargetSharedGameId)
            .WithErrorMessage("TargetSharedGameId is required for MergeKnowledgeBase and ApproveAsVariant actions");
    }

    [Fact]
    public void Validate_WithMergeKnowledgeBase_ValidTargetSharedGameId_Passes()
    {
        // Arrange
        var command = new ApproveGameProposalCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: Guid.NewGuid(),
            ApprovalAction: ProposalApprovalAction.MergeKnowledgeBase,
            TargetSharedGameId: Guid.NewGuid(),
            AdminNotes: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithApproveAsVariant_MissingTargetSharedGameId_Fails()
    {
        // Arrange
        var command = new ApproveGameProposalCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: Guid.NewGuid(),
            ApprovalAction: ProposalApprovalAction.ApproveAsVariant,
            TargetSharedGameId: null, // Required for ApproveAsVariant
            AdminNotes: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.TargetSharedGameId)
            .WithErrorMessage("TargetSharedGameId is required for MergeKnowledgeBase and ApproveAsVariant actions");
    }

    [Fact]
    public void Validate_WithApproveAsVariant_ValidTargetSharedGameId_Passes()
    {
        // Arrange
        var command = new ApproveGameProposalCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: Guid.NewGuid(),
            ApprovalAction: ProposalApprovalAction.ApproveAsVariant,
            TargetSharedGameId: Guid.NewGuid(),
            AdminNotes: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithApproveAsNew_WithTargetSharedGameId_Fails()
    {
        // Arrange
        var command = new ApproveGameProposalCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: Guid.NewGuid(),
            ApprovalAction: ProposalApprovalAction.ApproveAsNew,
            TargetSharedGameId: Guid.NewGuid(), // Should NOT be provided for ApproveAsNew
            AdminNotes: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.TargetSharedGameId)
            .WithErrorMessage("TargetSharedGameId should not be provided for ApproveAsNew action");
    }

    [Fact]
    public void Validate_WithAdminNotes_ExceedingMaxLength_Fails()
    {
        // Arrange
        var longNotes = new string('A', 1001); // 1001 characters
        var command = new ApproveGameProposalCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: Guid.NewGuid(),
            ApprovalAction: ProposalApprovalAction.ApproveAsNew,
            TargetSharedGameId: null,
            AdminNotes: longNotes);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.AdminNotes)
            .WithErrorMessage("AdminNotes cannot exceed 1000 characters");
    }

    [Fact]
    public void Validate_WithAdminNotes_AtMaxLength_Passes()
    {
        // Arrange
        var maxNotes = new string('A', 1000); // Exactly 1000 characters
        var command = new ApproveGameProposalCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: Guid.NewGuid(),
            ApprovalAction: ProposalApprovalAction.ApproveAsNew,
            TargetSharedGameId: null,
            AdminNotes: maxNotes);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
