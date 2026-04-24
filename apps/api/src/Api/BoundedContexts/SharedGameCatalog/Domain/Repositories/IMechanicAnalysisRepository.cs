using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for the <see cref="MechanicAnalysis"/> aggregate (ADR-051).
/// Returns the aggregate root with its child claims and citations eagerly loaded
/// via <c>AsSplitQuery</c> to avoid cartesian explosion across the 1:N:N hierarchy.
/// </summary>
public interface IMechanicAnalysisRepository
{
    /// <summary>
    /// Stages a new <see cref="MechanicAnalysis"/> for insertion. Persistence occurs
    /// at <c>SaveChangesAsync</c>, which also dispatches any pending domain events.
    /// </summary>
    Task AddAsync(MechanicAnalysis analysis, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a <see cref="MechanicAnalysis"/> by its primary key without eagerly loading
    /// claims or citations. Intended for status-only operations (e.g. suppression toggle,
    /// cost-cap override) where the child graph is not needed.
    /// Honors the <see cref="MechanicAnalysis.IsSuppressed"/> query filter.
    /// </summary>
    Task<MechanicAnalysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a <see cref="MechanicAnalysis"/> by its primary key with the full claim and
    /// citation graph attached. Uses <c>AsSplitQuery</c> to avoid cartesian explosion.
    /// Honors the <see cref="MechanicAnalysis.IsSuppressed"/> query filter.
    /// </summary>
    Task<MechanicAnalysis?> GetByIdWithClaimsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a <see cref="MechanicAnalysis"/> by its primary key without its claim graph,
    /// bypassing the <see cref="MechanicAnalysis.IsSuppressed"/> global query filter. Used by
    /// lifecycle operations where suppression is orthogonal to the transition (e.g. the
    /// Suppress command itself must be able to detect already-suppressed rows and yield 409
    /// rather than 404; ADR-051 T5).
    /// </summary>
    Task<MechanicAnalysis?> GetByIdIgnoringFiltersAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a <see cref="MechanicAnalysis"/> by its primary key with the full claim and
    /// citation graph, bypassing the <see cref="MechanicAnalysis.IsSuppressed"/> global query
    /// filter. Used by lifecycle operations (SubmitForReview, Approve) that must inspect claims
    /// on rows that may be simultaneously suppressed — suppression is orthogonal to the
    /// lifecycle state machine (ADR-051 T5).
    /// </summary>
    Task<MechanicAnalysis?> GetByIdWithClaimsIgnoringFiltersAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the currently published (non-suppressed) analysis for a shared game,
    /// or <c>null</c> if none exists. At most one <see cref="Enums.MechanicAnalysisStatus.Published"/>
    /// analysis may exist per shared game at a time (uniqueness enforced by partial index
    /// in the EF migration per plan §2.2.1).
    /// </summary>
    Task<MechanicAnalysis?> GetPublishedForSharedGameAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all analyses currently in the admin review queue
    /// (<see cref="Enums.MechanicAnalysisStatus.InReview"/>), including suppressed rows so
    /// they stay visible to moderators. Ignores the global suppression filter.
    /// </summary>
    Task<IReadOnlyList<MechanicAnalysis>> GetReviewQueueAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the primary keys of every <see cref="MechanicAnalysis"/> in the given
    /// <see cref="Enums.MechanicAnalysisStatus"/>, including suppressed rows (suppression is
    /// orthogonal to status; ignores the global query filter). Used by the Sprint 1 mass-recalc
    /// dispatcher (ADR-051 Task 25) to enumerate candidates without hydrating the full claim
    /// graph; per-id handlers reload the aggregate fresh as they need it.
    /// </summary>
    /// <remarks>
    /// Id-only projection — no claim or citation join — to keep the candidate enumeration cheap
    /// even on large catalogs. Caller iterates and dispatches one
    /// <see cref="Application.Commands.Validation.CalculateMechanicAnalysisMetricsCommand"/> per id.
    /// </remarks>
    Task<IReadOnlyList<Guid>> GetIdsByStatusAsync(
        Enums.MechanicAnalysisStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the existing non-rejected analysis for the (SharedGame, PdfDocument, PromptVersion)
    /// tuple, or <c>null</c> if none exists. Supports T7 idempotency (ADR-051 §3.5): the generation
    /// pipeline uses this lookup to short-circuit duplicate runs with the same prompt version.
    /// </summary>
    /// <remarks>
    /// Rejected analyses (<see cref="Enums.MechanicAnalysisStatus.Rejected"/>) are excluded from
    /// this lookup so operators can retry the same prompt version after a failure. Suppressed rows
    /// are included to prevent silently creating a duplicate while a takedown is in effect.
    /// </remarks>
    Task<MechanicAnalysis?> FindByPromptVersionAsync(
        Guid sharedGameId,
        Guid pdfDocumentId,
        string promptVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a previously loaded <see cref="MechanicAnalysis"/> as modified.
    /// Intended for entities loaded as <c>AsNoTracking</c> or detached.
    /// Tracked entities do not require an explicit call.
    /// </summary>
    void Update(MechanicAnalysis analysis);
}
