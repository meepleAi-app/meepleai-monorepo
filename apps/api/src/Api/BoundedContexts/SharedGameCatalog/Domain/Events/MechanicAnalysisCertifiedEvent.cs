using Api.SharedKernel.Domain.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Raised when a <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicAnalysis"/>
/// transitions to <see cref="ValueObjects.CertificationStatus.Certified"/> (ADR-051 M2).
/// </summary>
/// <remarks>
/// <para>
/// Two provenance paths are encoded via <paramref name="WasOverride"/>:
/// <list type="bullet">
///   <item><description><c>false</c>: automatic certification — the computed metrics passed the
///     configured thresholds. <paramref name="CertifiedByUserId"/> is <see cref="Guid.Empty"/>
///     (system sentinel — no human actor) and <paramref name="OverrideReason"/> is <c>null</c>.
///     Raised by <c>CalculateMechanicAnalysisMetricsHandler</c>.</description></item>
///   <item><description><c>true</c>: admin escalation — an operator overrode failing metrics with a
///     justification. <paramref name="CertifiedByUserId"/> carries the admin user id and
///     <paramref name="OverrideReason"/> carries the 20..500-char justification.</description></item>
/// </list>
/// </para>
/// <para>
/// Implements <see cref="IDomainEvent"/> so the aggregate's <c>AddDomainEvent</c> accepts it and
/// the unit of work dispatches it through MediatR on commit.
/// </para>
/// </remarks>
public sealed record MechanicAnalysisCertifiedEvent(
    Guid AnalysisId,
    Guid SharedGameId,
    bool WasOverride,
    string? OverrideReason,
    Guid CertifiedByUserId,
    DateTimeOffset CertifiedAt) : IDomainEvent
{
    /// <inheritdoc />
    public DateTime OccurredAt { get; } = TimeProvider.System.GetUtcNow().UtcDateTime;

    /// <inheritdoc />
    public Guid EventId { get; } = Guid.NewGuid();
}
