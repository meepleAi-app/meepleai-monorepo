using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Command to add a player to a play record.
/// Supports both registered users and external guests.
/// Issue #3889: CQRS commands for play records.
/// Issue #2349: CallerUserId authorizes the caller; UserId identifies the added player.
/// </summary>
internal record AddPlayerToRecordCommand(
    Guid RecordId,
    Guid CallerUserId,
    Guid? UserId,
    string DisplayName
) : ICommand;
