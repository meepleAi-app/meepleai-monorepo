using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to approve a share request after admin review.
/// </summary>
/// <param name="ShareRequestId">The ID of the share request to approve.</param>
/// <param name="AdminId">The ID of the admin performing the approval.</param>
/// <param name="TargetSharedGameId">Optional: The target shared game ID (for AdditionalContent contributions).</param>
/// <param name="AdminNotes">Optional: Notes from the admin about the approval.</param>
public record ApproveShareRequestCommand(
    Guid ShareRequestId,
    Guid AdminId,
    Guid? TargetSharedGameId = null,
    string? AdminNotes = null
) : ICommand<ApproveShareRequestResponse>;

/// <summary>
/// Response from approving a share request.
/// </summary>
/// <param name="ShareRequestId">The ID of the approved share request.</param>
/// <param name="Status">The new status (Approved).</param>
/// <param name="TargetSharedGameId">The ID of the shared game that received the contribution.</param>
/// <param name="ResolvedAt">When the request was resolved.</param>
public record ApproveShareRequestResponse(
    Guid ShareRequestId,
    ShareRequestStatus Status,
    Guid? TargetSharedGameId,
    DateTime ResolvedAt);
