using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get games pending approval with optional filtering and urgency indicators.
/// For admin dashboard to review and approve/reject shared games.
/// Issue #3533: Admin API Endpoints - Approval Queue Management
/// </summary>
internal record GetApprovalQueueQuery(
    bool? Urgency = null,
    Guid? Submitter = null,
    bool? HasPdfs = null
) : IQuery<IReadOnlyList<ApprovalQueueItemDto>>;
