using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Command to revoke the public share token of a play record. Creator-only (#2437-2).
/// </summary>
internal record RevokePlayRecordShareTokenCommand(Guid PlayRecordId, Guid UserId) : ICommand;
