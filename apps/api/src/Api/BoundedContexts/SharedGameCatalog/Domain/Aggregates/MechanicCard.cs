using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Aggregate root for a published mechanics card (ADR-051 #527). A card is an immutable, user-facing
/// snapshot of an approved <see cref="MechanicAnalysis"/>, surfaced on <c>/games/{slug}/card</c>
/// (#528, login-gated). Publishing is an explicit admin act, distinct from approving the analysis
/// lifecycle (AD-2). A revision produces a new card version rather than mutating an existing one
/// (AD-1 / AD-3); at most one non-suppressed card may exist per game (enforced by a partial unique
/// index).
/// </summary>
public sealed class MechanicCard : AggregateRoot<Guid>
{
    // === Identity / lineage ===
    public Guid SharedGameId { get; private set; }
    public Guid OriginAnalysisId { get; private set; }
    public MechanicCardOrigin Origin { get; private set; }

    // === Content ===
    public string Title { get; private set; } = string.Empty;

    /// <summary>Immutable JSONB snapshot of the approved claims (see <see cref="MechanicCardContent"/>).</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>Monotonic version per <see cref="SharedGameId"/> (AD-3), starts at 1.</summary>
    public int Version { get; private set; }

    // === Takedown (T5, #529) — orthogonal to lifecycle ===
    public bool IsSuppressed { get; private set; }
    public string? SuppressedReason { get; private set; }
    public DateTime? SuppressedAt { get; private set; }
    public Guid? SuppressedBy { get; private set; }

    // === Player feedback (preparatory for #531) ===
    public int ErrorReportsCount { get; private set; }
    public decimal? FeedbackScore { get; private set; }

    // === Publication audit ===
    public DateTime PublishedAt { get; private set; }
    public Guid PublishedBy { get; private set; }

    // === Housekeeping ===
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>, ADR-060).</summary>
    public uint XminVersion { get; private set; }

    private const int MinTitleLength = 5;
    private const int MaxTitleLength = 200;

    /// <summary>EF Core / reconstitution constructor.</summary>
    private MechanicCard() : base()
    {
    }

    private MechanicCard(Guid id) : base(id)
    {
    }

    /// <summary>
    /// Promotes an approved <see cref="MechanicAnalysis"/> into a new mechanics card, capturing an
    /// immutable snapshot of its claims (AD-1). The version is computed by the caller (monotonic per
    /// game, AD-3) and passed in.
    /// </summary>
    /// <param name="analysis">The source analysis, with its claim + citation graph materialized.
    /// Must be <see cref="MechanicAnalysisStatus.Published"/> and not already published.</param>
    /// <param name="overrideTitle">Optional admin-supplied title (validated 5..200). When null, a
    /// default is derived from the game name.</param>
    /// <param name="gameContext">Cross-aggregate game metadata resolved by the handler.</param>
    /// <param name="publisherId">The admin performing the publish (sourced from the session).</param>
    /// <param name="version">Monotonic version for this game (≥1).</param>
    /// <param name="utcNow">Publish timestamp; also the snapshot timestamp.</param>
    public static MechanicCard PublishFromAnalysis(
        MechanicAnalysis analysis,
        string? overrideTitle,
        MechanicCardGameContext gameContext,
        Guid publisherId,
        int version,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(gameContext);

        if (publisherId == Guid.Empty)
        {
            throw new ArgumentException("PublisherId cannot be empty.", nameof(publisherId));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Version must be >= 1.");
        }

        // Defense in depth: the handler already guards these, but the factory re-checks so the
        // aggregate can never be constructed from a non-publishable analysis.
        if (analysis.Status != MechanicAnalysisStatus.Published)
        {
            throw new InvalidOperationException(
                $"Cannot publish MechanicAnalysis {analysis.Id}: status is {analysis.Status}, expected Published.");
        }

        if (analysis.PublishedCardId is not null)
        {
            throw new InvalidOperationException(
                $"MechanicAnalysis {analysis.Id} is already published as card {analysis.PublishedCardId}.");
        }

        if (analysis.Claims.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot publish MechanicAnalysis {analysis.Id}: it has no claims.");
        }

        if (analysis.Claims.Any(c => c.Status != MechanicClaimStatus.Approved))
        {
            throw new InvalidOperationException(
                $"Cannot publish MechanicAnalysis {analysis.Id}: not all claims are Approved.");
        }

        var title = ResolveTitle(overrideTitle, gameContext.SharedGameName);
        var content = MechanicCardContent.FromAnalysis(analysis, gameContext, utcNow).ToJson();

        var card = new MechanicCard(Guid.NewGuid())
        {
            SharedGameId = analysis.SharedGameId,
            OriginAnalysisId = analysis.Id,
            Origin = MechanicCardOrigin.AiReviewed,
            Title = title,
            Content = content,
            Version = version,
            IsSuppressed = false,
            ErrorReportsCount = 0,
            FeedbackScore = null,
            PublishedAt = utcNow,
            PublishedBy = publisherId,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        card.AddDomainEvent(new MechanicCardPublishedEvent(
            card.Id,
            card.SharedGameId,
            card.OriginAnalysisId,
            publisherId,
            version));

        return card;
    }

    /// <summary>
    /// Applies a takedown (suppress) on the card (T5 / #529). Idempotency: throws if already
    /// suppressed so the caller can surface a 409 rather than silently no-op.
    /// </summary>
    public void Suppress(Guid actorId, string reason, DateTime utcNow)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("ActorId cannot be empty.", nameof(actorId));
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 20 or > 500)
        {
            throw new ArgumentException("Suppression reason must be 20..500 characters (legal evidence chain).", nameof(reason));
        }

        if (IsSuppressed)
        {
            throw new InvalidOperationException($"MechanicCard {Id} is already suppressed.");
        }

        IsSuppressed = true;
        SuppressedReason = reason.Trim();
        SuppressedAt = utcNow;
        SuppressedBy = actorId;
        UpdatedAt = utcNow;

        AddDomainEvent(new MechanicCardSuppressedEvent(Id, SharedGameId, actorId, SuppressedReason));
    }

    /// <summary>Lifts a takedown, clearing suppression tracking. Throws if not suppressed.</summary>
    public void Unsuppress(Guid actorId, DateTime utcNow)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("ActorId cannot be empty.", nameof(actorId));
        }

        if (!IsSuppressed)
        {
            throw new InvalidOperationException($"MechanicCard {Id} is not suppressed.");
        }

        IsSuppressed = false;
        SuppressedReason = null;
        SuppressedAt = null;
        SuppressedBy = null;
        UpdatedAt = utcNow;
    }

    /// <summary>Increments the player error-report counter (preparatory for #531 feedback).</summary>
    public void MarkErrorReport(DateTime utcNow)
    {
        ErrorReportsCount += 1;
        UpdatedAt = utcNow;
    }

    private static string ResolveTitle(string? overrideTitle, string sharedGameName)
    {
        if (!string.IsNullOrWhiteSpace(overrideTitle))
        {
            var trimmed = overrideTitle.Trim();
            if (trimmed.Length is < MinTitleLength or > MaxTitleLength)
            {
                throw new ArgumentException(
                    $"Title must be {MinTitleLength}..{MaxTitleLength} characters.", nameof(overrideTitle));
            }

            return trimmed;
        }

        var name = string.IsNullOrWhiteSpace(sharedGameName) ? "Untitled Game" : sharedGameName.Trim();
        return $"{name} — Mechanics Card";
    }

    /// <summary>Rehydrates a card from persistence (no validation, no events).</summary>
    public static MechanicCard Reconstitute(
        Guid id,
        Guid sharedGameId,
        Guid originAnalysisId,
        MechanicCardOrigin origin,
        string title,
        string content,
        int version,
        bool isSuppressed,
        string? suppressedReason,
        DateTime? suppressedAt,
        Guid? suppressedBy,
        int errorReportsCount,
        decimal? feedbackScore,
        DateTime publishedAt,
        Guid publishedBy,
        DateTime createdAt,
        DateTime updatedAt,
        uint xminVersion = 0) =>
        new(id)
        {
            SharedGameId = sharedGameId,
            OriginAnalysisId = originAnalysisId,
            Origin = origin,
            Title = title,
            Content = content,
            Version = version,
            IsSuppressed = isSuppressed,
            SuppressedReason = suppressedReason,
            SuppressedAt = suppressedAt,
            SuppressedBy = suppressedBy,
            ErrorReportsCount = errorReportsCount,
            FeedbackScore = feedbackScore,
            PublishedAt = publishedAt,
            PublishedBy = publishedBy,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            XminVersion = xminVersion
        };
}
