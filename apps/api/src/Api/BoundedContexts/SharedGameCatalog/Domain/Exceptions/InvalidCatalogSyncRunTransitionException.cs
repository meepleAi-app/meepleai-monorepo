using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;

/// <summary>
/// Thrown when a domain operation is invoked on a <see cref="Aggregates.CatalogSyncRun"/>
/// whose current status does not permit that transition (#1861).
/// </summary>
public sealed class InvalidCatalogSyncRunTransitionException : DomainException
{
    public Guid RunId { get; }

    public CatalogSyncStatus CurrentStatus { get; }

    public string AttemptedOperation { get; }

    public CatalogSyncStatus[] AllowedStatuses { get; }

    public InvalidCatalogSyncRunTransitionException(
        Guid runId,
        CatalogSyncStatus currentStatus,
        string attemptedOperation,
        params CatalogSyncStatus[] allowedStatuses)
        : base(BuildMessage(runId, currentStatus, attemptedOperation, allowedStatuses))
    {
        RunId = runId;
        CurrentStatus = currentStatus;
        AttemptedOperation = attemptedOperation;
        AllowedStatuses = allowedStatuses;
    }

    private static string BuildMessage(
        Guid runId,
        CatalogSyncStatus currentStatus,
        string operation,
        CatalogSyncStatus[] allowedStatuses)
    {
        var allowed = allowedStatuses.Length == 0
            ? "no status"
            : string.Join(" or ", allowedStatuses);

        return $"Cannot {operation} for CatalogSyncRun {runId} in {currentStatus} status. " +
               $"Allowed: {allowed}.";
    }
}
