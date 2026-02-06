using Api.BoundedContexts.SharedGameCatalog.Application.Commands.ApproveGameProposal;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Validators.ApproveGameProposal;

/// <summary>
/// Validator for ApproveGameProposalCommand.
/// Issue #3667: Phase 6 - Admin Review Enhancements.
/// </summary>
internal class ApproveGameProposalCommandValidator : AbstractValidator<ApproveGameProposalCommand>
{
    public ApproveGameProposalCommandValidator()
    {
        RuleFor(x => x.ShareRequestId)
            .NotEmpty()
            .WithMessage("ShareRequestId is required");

        RuleFor(x => x.AdminId)
            .NotEmpty()
            .WithMessage("AdminId is required");

        RuleFor(x => x.ApprovalAction)
            .IsInEnum()
            .WithMessage("Invalid approval action");

        // MergeKnowledgeBase and ApproveAsVariant require TargetSharedGameId
        When(x => x.ApprovalAction == ProposalApprovalAction.MergeKnowledgeBase ||
                  x.ApprovalAction == ProposalApprovalAction.ApproveAsVariant, () =>
        {
            RuleFor(x => x.TargetSharedGameId)
                .NotEmpty()
                .WithMessage("TargetSharedGameId is required for MergeKnowledgeBase and ApproveAsVariant actions");
        });

        // ApproveAsNew should NOT have TargetSharedGameId
        When(x => x.ApprovalAction == ProposalApprovalAction.ApproveAsNew, () =>
        {
            RuleFor(x => x.TargetSharedGameId)
                .Empty()
                .WithMessage("TargetSharedGameId should not be provided for ApproveAsNew action");
        });

        RuleFor(x => x.AdminNotes)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrEmpty(x.AdminNotes))
            .WithMessage("AdminNotes cannot exceed 1000 characters");
    }
}
