using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Command to add a player to a play record.
/// Supports both registered users and external guests.
/// Issue #3889: CQRS commands for play records.
/// </summary>
internal record AddPlayerToRecordCommand(
    Guid RecordId,
    Guid? UserId,
    string DisplayName
) : ICommand;
