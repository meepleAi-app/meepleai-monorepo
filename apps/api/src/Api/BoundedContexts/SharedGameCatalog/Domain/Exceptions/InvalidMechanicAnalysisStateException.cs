using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;

/// <summary>
/// Thrown when a domain operation is invoked on a <see cref="Aggregates.MechanicAnalysis"/>
/// whose current status does not permit that transition.
/// </summary>
public sealed class InvalidMechanicAnalysisStateException : DomainException
{
    public Guid AnalysisId { get; }

    public MechanicAnalysisStatus CurrentStatus { get; }

    public string AttemptedOperation { get; }

    public MechanicAnalysisStatus[] AllowedStatuses { get; }

    public InvalidMechanicAnalysisStateException(
        Guid analysisId,
        MechanicAnalysisStatus currentStatus,
        string attemptedOperation,
        params MechanicAnalysisStatus[] allowedStatuses)
        : base(BuildMessage(analysisId, currentStatus, attemptedOperation, allowedStatuses))
    {
        AnalysisId = analysisId;
        CurrentStatus = currentStatus;
        AttemptedOperation = attemptedOperation;
        AllowedStatuses = allowedStatuses;
    }

    private static string BuildMessage(
        Guid analysisId,
        MechanicAnalysisStatus currentStatus,
        string operation,
        MechanicAnalysisStatus[] allowedStatuses)
    {
        var allowed = allowedStatuses.Length == 0
            ? "no status"
            : string.Join(" or ", allowedStatuses);

        return $"Cannot {operation} for MechanicAnalysis {analysisId} in {currentStatus} status. " +
               $"Allowed: {allowed}.";
    }
}
