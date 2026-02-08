using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Command to update play record details.
/// Allowed even after completion for corrections.
/// Issue #3889: CQRS commands for play records.
/// </summary>
internal record UpdatePlayRecordCommand(
    Guid RecordId,
    DateTime? SessionDate = null,
    string? Notes = null,
    string? Location = null
) : ICommand;
