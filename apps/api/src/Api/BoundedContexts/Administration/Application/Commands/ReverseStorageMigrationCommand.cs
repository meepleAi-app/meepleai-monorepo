using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Issue #1333 Phase B backout: reverses an in-flight or completed storage
/// layout migration identified by <see cref="MigrationId"/>. For each Sent row
/// the new-layout object is moved back to the legacy key; for each Pending row
/// the drainer attempt is short-circuited via a Reverted state transition.
/// </summary>
internal sealed record ReverseStorageMigrationCommand(
    Guid MigrationId,
    bool DryRun = false) : IRequest<ReverseStorageMigrationResult>;

/// <summary>
/// Result of a reverse operation.
/// </summary>
internal sealed record ReverseStorageMigrationResult(
    Guid MigrationId,
    int TotalRows,
    int ReversedFromSent,
    int CancelledFromPending,
    int Skipped,
    int Failed,
    bool IsDryRun,
    List<string> Errors);
