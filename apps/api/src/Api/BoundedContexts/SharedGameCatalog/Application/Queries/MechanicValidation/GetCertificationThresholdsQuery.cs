using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

/// <summary>
/// Parameterless query that returns the current operator-configurable
/// <see cref="CertificationThresholds"/> value object from the singleton
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.CertificationThresholdsConfig"/>
/// aggregate. Used by the admin validation UI to populate the threshold-tuning
/// form and by read-only consumers that need to know the active certification
/// gates.
/// </summary>
/// <remarks>
/// ADR-051 Sprint 1, Phase 6 / Task 31. Cached for 30 minutes via
/// <c>IHybridCacheService</c> under key
/// <c>meepleai:mechanic-validation:thresholds</c> with tag
/// <c>mechanic-validation-thresholds</c> for targeted invalidation from the
/// <c>UpdateCertificationThresholdsCommand</c> handler. The 30-minute TTL is
/// substantially longer than the dashboard/trend queries (5 minutes) because
/// the only mutation path is an explicit admin save event, not background
/// projection writes.
/// </remarks>
internal sealed record GetCertificationThresholdsQuery() : IQuery<CertificationThresholds>;
