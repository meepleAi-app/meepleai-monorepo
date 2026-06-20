using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Command to soft-delete a play record. Creator-only (issue #2439).
/// </summary>
internal record DeletePlayRecordCommand(Guid RecordId, Guid UserId) : ICommand;
