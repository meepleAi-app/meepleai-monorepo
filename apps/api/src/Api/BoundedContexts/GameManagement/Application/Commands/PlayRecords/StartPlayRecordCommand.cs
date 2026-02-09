using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Command to start a play record (moves from Planned to InProgress).
/// Issue #3889: CQRS commands for play records.
/// </summary>
internal record StartPlayRecordCommand(
    Guid RecordId
) : ICommand;
