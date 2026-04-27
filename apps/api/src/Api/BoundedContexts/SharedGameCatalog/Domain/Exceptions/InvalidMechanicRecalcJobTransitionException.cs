using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;

/// <summary>
/// Thrown when a domain operation is invoked on a <see cref="Aggregates.MechanicRecalcJob"/>
/// whose current status does not permit that transition.
/// </summary>
public sealed class InvalidMechanicRecalcJobTransitionException : DomainException
{
    public Guid JobId { get; }

    public RecalcJobStatus CurrentStatus { get; }

    public string AttemptedOperation { get; }

    public RecalcJobStatus[] AllowedStatuses { get; }

    public InvalidMechanicRecalcJobTransitionException(
        Guid jobId,
        RecalcJobStatus currentStatus,
        string attemptedOperation,
        params RecalcJobStatus[] allowedStatuses)
        : base(BuildMessage(jobId, currentStatus, attemptedOperation, allowedStatuses))
    {
        JobId = jobId;
        CurrentStatus = currentStatus;
        AttemptedOperation = attemptedOperation;
        AllowedStatuses = allowedStatuses;
    }

    private static string BuildMessage(
        Guid jobId,
        RecalcJobStatus currentStatus,
        string operation,
        RecalcJobStatus[] allowedStatuses)
    {
        var allowed = allowedStatuses.Length == 0
            ? "no status"
            : string.Join(" or ", allowedStatuses);

        return $"Cannot {operation} for MechanicRecalcJob {jobId} in {currentStatus} status. " +
               $"Allowed: {allowed}.";
    }
}
