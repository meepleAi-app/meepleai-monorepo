using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Command to update play record details.
/// Allowed even after completion for corrections.
/// Issue #3889: CQRS commands for play records.
/// Issue #2437-1: nullable Xmin for stale-form optimistic concurrency. When provided,
/// the handler pushes it onto the domain object so EF raises DbUpdateConcurrencyException
/// on a stale-form write → 409. When absent, fresh-load behaviour (no concurrency check).
/// </summary>
internal record UpdatePlayRecordCommand(
    Guid RecordId,
    Guid UserId,
    DateTime? SessionDate = null,
    string? Notes = null,
    string? Location = null,
    uint? Xmin = null
) : ICommand;
