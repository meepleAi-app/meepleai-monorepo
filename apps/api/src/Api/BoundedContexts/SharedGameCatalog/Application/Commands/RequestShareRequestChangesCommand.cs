using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to request changes from the user on a share request.
/// </summary>
/// <param name="ShareRequestId">The ID of the share request.</param>
/// <param name="AdminId">The ID of the admin requesting changes.</param>
/// <param name="Feedback">Detailed feedback about required changes (required).</param>
public record RequestShareRequestChangesCommand(
    Guid ShareRequestId,
    Guid AdminId,
    string Feedback
) : ICommand<RequestShareRequestChangesResponse>;

/// <summary>
/// Response from requesting changes on a share request.
/// </summary>
/// <param name="ShareRequestId">The ID of the share request.</param>
/// <param name="Status">The new status (ChangesRequested).</param>
/// <param name="ModifiedAt">When the request was modified.</param>
public record RequestShareRequestChangesResponse(
    Guid ShareRequestId,
    ShareRequestStatus Status,
    DateTime ModifiedAt);
