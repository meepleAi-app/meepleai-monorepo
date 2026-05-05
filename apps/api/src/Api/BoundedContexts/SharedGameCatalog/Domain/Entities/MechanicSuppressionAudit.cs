using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Immutable audit row recording a suppression toggle (takedown) on a
/// <see cref="Aggregates.MechanicAnalysis"/>. Separate from <see cref="MechanicStatusAudit"/>
/// because suppression is orthogonal to status: a Published analysis can be Suppressed without
/// changing its status (ADR-051 T5 kill-switch).
/// </summary>
/// <remarks>
/// This table forms the legal evidence chain for DMCA / editor takedown requests:
/// - <see cref="Reason"/> is required and holds the takedown reference (email subject, ticket, etc.).
/// - <see cref="RequestedAt"/> + <see cref="OccurredAt"/> feed the SLA dashboard.
/// - <see cref="RequestSource"/> disambiguates editor email vs. legal department vs. other.
/// Append-only — no update surface.
/// </remarks>
public sealed class MechanicSuppressionAudit : Entity<Guid>
{
    /// <summary>FK to the analysis that was suppressed / unsuppressed.</summary>
    public Guid AnalysisId { get; private set; }

    /// <summary>Previous suppression state.</summary>
    public bool FromSuppressed { get; private set; }

    /// <summary>New suppression state. Must differ from <see cref="FromSuppressed"/>.</summary>
    public bool ToSuppressed { get; private set; }

    /// <summary>Admin who toggled suppression.</summary>
    public Guid ActorId { get; private set; }

    /// <summary>
    /// Takedown reference (required). For suppress events: editor email subject / legal ticket.
    /// For unsuppress events: justification for lifting the takedown.
    /// </summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>
    /// Origin of the takedown request. Nullable because unsuppress events don't originate from
    /// an external request.
    /// </summary>
    public SuppressionRequestSource? RequestSource { get; private set; }

    /// <summary>When the takedown request was notified to us (null on unsuppress).</summary>
    public DateTime? RequestedAt { get; private set; }

    /// <summary>UTC timestamp when the suppression actually took effect.</summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>EF Core constructor.</summary>
    private MechanicSuppressionAudit() : base()
    {
    }

    private MechanicSuppressionAudit(
        Guid id,
        Guid analysisId,
        bool fromSuppressed,
        bool toSuppressed,
        Guid actorId,
        string reason,
        SuppressionRequestSource? requestSource,
        DateTime? requestedAt,
        DateTime occurredAt)
        : base(id)
    {
        AnalysisId = analysisId;
        FromSuppressed = fromSuppressed;
        ToSuppressed = toSuppressed;
        ActorId = actorId;
        Reason = reason;
        RequestSource = requestSource;
        RequestedAt = requestedAt;
        OccurredAt = occurredAt;
    }

    /// <summary>
    /// Creates a suppression audit row (takedown applied).
    /// </summary>
    public static MechanicSuppressionAudit CreateSuppress(
        Guid analysisId,
        Guid actorId,
        string reason,
        SuppressionRequestSource requestSource,
        DateTime? requestedAt,
        DateTime occurredAt)
    {
        ValidateCommon(analysisId, actorId, reason);

        return new MechanicSuppressionAudit(
            id: Guid.NewGuid(),
            analysisId: analysisId,
            fromSuppressed: false,
            toSuppressed: true,
            actorId: actorId,
            reason: reason.Trim(),
            requestSource: requestSource,
            requestedAt: requestedAt,
            occurredAt: occurredAt);
    }

    /// <summary>
    /// Creates an unsuppression audit row (takedown lifted).
    /// </summary>
    public static MechanicSuppressionAudit CreateUnsuppress(
        Guid analysisId,
        Guid actorId,
        string reason,
        DateTime occurredAt)
    {
        ValidateCommon(analysisId, actorId, reason);

        return new MechanicSuppressionAudit(
            id: Guid.NewGuid(),
            analysisId: analysisId,
            fromSuppressed: true,
            toSuppressed: false,
            actorId: actorId,
            reason: reason.Trim(),
            requestSource: null,
            requestedAt: null,
            occurredAt: occurredAt);
    }

    private static void ValidateCommon(Guid analysisId, Guid actorId, string reason)
    {
        if (analysisId == Guid.Empty)
        {
            throw new ArgumentException("AnalysisId cannot be empty.", nameof(analysisId));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("ActorId cannot be empty.", nameof(actorId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Suppression reason is required (legal evidence chain).", nameof(reason));
        }
    }
}
