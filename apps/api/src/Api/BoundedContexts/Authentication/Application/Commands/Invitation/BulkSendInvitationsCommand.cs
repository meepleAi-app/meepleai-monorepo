using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Response records
namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Command to bulk-send invitations from CSV content.
/// Issue #124: User invitation system.
/// </summary>
internal record BulkSendInvitationsCommand(
    string CsvContent,
    Guid InvitedByUserId
) : ICommand<BulkInviteResponse>;

/// <summary>
/// Response for bulk invitation operation.
/// </summary>
internal sealed record BulkInviteResponse(
    IReadOnlyList<InvitationDto> Successful,
    IReadOnlyList<BulkInviteFailure> Failed);

/// <summary>
/// Represents a failed invitation in a bulk operation.
/// </summary>
internal sealed record BulkInviteFailure(string Email, string Error);
