using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Command to record a score for a player in a play record.
/// Supports multi-dimensional scoring.
/// Issue #3889: CQRS commands for play records.
/// Issue #2349: CallerUserId added for ownership authorization.
/// </summary>
internal record RecordScoreCommand(
    Guid RecordId,
    Guid CallerUserId,
    Guid PlayerId,
    string Dimension,
    int Value,
    string? Unit = null
) : ICommand;
