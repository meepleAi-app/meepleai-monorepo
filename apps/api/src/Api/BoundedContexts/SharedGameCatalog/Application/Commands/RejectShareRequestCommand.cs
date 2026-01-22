using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to reject a share request after admin review.
/// </summary>
/// <param name="ShareRequestId">The ID of the share request to reject.</param>
/// <param name="AdminId">The ID of the admin performing the rejection.</param>
/// <param name="Reason">The reason for rejection (required).</param>
public record RejectShareRequestCommand(
    Guid ShareRequestId,
    Guid AdminId,
    string Reason
) : ICommand<RejectShareRequestResponse>;

/// <summary>
/// Response from rejecting a share request.
/// </summary>
/// <param name="ShareRequestId">The ID of the rejected share request.</param>
/// <param name="Status">The new status (Rejected).</param>
/// <param name="ResolvedAt">When the request was resolved.</param>
public record RejectShareRequestResponse(
    Guid ShareRequestId,
    ShareRequestStatus Status,
    DateTime ResolvedAt);
