using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.ApproveGameProposal;

/// <summary>
/// Command to approve a NewGameProposal ShareRequest with enhanced admin actions.
/// Issue #3667: Phase 6 - Admin Review Enhancements.
/// </summary>
/// <param name="ShareRequestId">ID of the ShareRequest to approve</param>
/// <param name="AdminId">ID of the admin approving</param>
/// <param name="ApprovalAction">The approval action to take</param>
/// <param name="TargetSharedGameId">ID of existing SharedGame (required for MergeKnowledgeBase and ApproveAsVariant)</param>
/// <param name="AdminNotes">Optional admin notes</param>
internal record ApproveGameProposalCommand(
    Guid ShareRequestId,
    Guid AdminId,
    ProposalApprovalAction ApprovalAction,
    Guid? TargetSharedGameId,
    string? AdminNotes
) : ICommand<ApproveShareRequestResponse>;
