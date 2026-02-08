using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Command to complete a play record.
/// Supports manual duration override or automatic calculation.
/// Issue #3889: CQRS commands for play records.
/// </summary>
internal record CompletePlayRecordCommand(
    Guid RecordId,
    TimeSpan? ManualDuration = null
) : ICommand;
