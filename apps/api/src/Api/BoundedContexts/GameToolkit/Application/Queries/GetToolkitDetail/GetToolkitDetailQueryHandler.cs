using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameToolkit;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitDetail;

/// <summary>
/// Handler for <see cref="GetToolkitDetailQuery"/> — surfaces the marketplace
/// detail envelope with per-viewer derived <see cref="ViewerContextDto"/>.
/// Wave 3 Phase 2, PR #732 §5.3.1 / Issue #805.
/// </summary>
/// <remarks>
/// Cache strategy (PR #732 §5.3.1): per-viewer 10min HybridCache, since
/// <c>ViewerContext.IsOwner</c> / <c>HasInstalled</c> / <c>CanRate</c> all
/// vary by caller. Cache key: <c>toolkits:{toolkitId}:detail:{viewerId}</c>.
/// Tagged with <c>"toolkit:{id}"</c> for downstream invalidation by the
/// install command (cf. <see cref="GameToolkit.Application.Commands.InstallToolkit"/>).
///
/// Visibility rule (PR #732 §5.2 Wiegers security boundary):
/// <list type="bullet">
///   <item>Owner: always sees toolkit (drafts, yanked, published).</item>
///   <item>Other viewers: only see published + non-yanked (proxied via
///         <c>IsPublished == true &amp;&amp; TemplateStatus == Approved</c> in v1).</item>
/// </list>
///
/// Schema reality v1 carryover (Gate B) — flagged in DTO XML doc:
/// installCount/ratingAverage/ratingCount stubbed (no entities); description
/// derived from name; coverImageUrl always null; publishedAt = UpdatedAt
/// when in published state; yankedAt always null; currentVersion =
/// "1.0.{intVersion}" until semver schema lands.
/// </remarks>
internal sealed class GetToolkitDetailQueryHandler
    : IRequestHandler<GetToolkitDetailQuery, ToolkitDetailResponse?>
{
    private const int MaxSystemPromptPreviewLength = 500;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly MeepleAiDbContext _context;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetToolkitDetailQueryHandler> _logger;

    public GetToolkitDetailQueryHandler(
        MeepleAiDbContext context,
        IHybridCacheService cache,
        ILogger<GetToolkitDetailQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ToolkitDetailResponse?> Handle(
        GetToolkitDetailQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cacheKey = $"toolkits:{request.ToolkitId:N}:detail:{request.ViewerId:N}";

        // Wrap in a class container so HybridCache's class constraint accepts
        // the nullable detail (record is reference type but we still need a
        // non-null sentinel for the cache value channel).
        var cached = await _cache.GetOrCreateAsync(
            cacheKey,
            async ct => await ComputeDetailAsync(request, ct).ConfigureAwait(false),
            tags:
            [
                $"toolkit:{request.ToolkitId:N}",
                "toolkitDetail",
            ],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);

        return cached.Response;
    }

    private async Task<ToolkitDetailContainer> ComputeDetailAsync(
        GetToolkitDetailQuery request,
        CancellationToken cancellationToken)
    {
        // Issue #1144 — Include(t => t.Game) drives a LEFT JOIN so the marketplace
        // surface can project Game.Name without a second round-trip. EF emits a
        // LEFT JOIN automatically because GameId is nullable.
        var entity = await _context.GameToolkits
            .AsNoTracking()
            .Include(t => t.Game)
            .FirstOrDefaultAsync(t => t.Id == request.ToolkitId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            _logger.LogInformation(
                "Toolkit {ToolkitId} not found for viewer {ViewerId}",
                request.ToolkitId,
                request.ViewerId);
            return new ToolkitDetailContainer((ToolkitDetailResponse?)null);
        }

        var isOwner = entity.CreatedByUserId == request.ViewerId;
        var isPublished = entity.IsPublished
            && (TemplateStatus)entity.TemplateStatus == TemplateStatus.Approved;
        // YankedAt is always null in v1 (Gate B carryover); the visibility check
        // is structured for the future yank flag without changing wire shape.
        const bool isYanked = false;

        // PR #732 §5.2 security boundary — non-authors must not see drafts/yanked.
        if (!isOwner && (!isPublished || isYanked))
        {
            _logger.LogInformation(
                "Toolkit {ToolkitId} hidden from viewer {ViewerId} (not published or yanked, not author)",
                request.ToolkitId,
                request.ViewerId);
            return new ToolkitDetailContainer((ToolkitDetailResponse?)null);
        }

        // Author lookup (DisplayName + AvatarUrl). Falls back gracefully when
        // missing (e.g. removed account) so the detail surface stays renderable.
        var author = await _context.Set<UserEntity>()
            .AsNoTracking()
            .Where(u => u.Id == entity.CreatedByUserId)
            .Select(u => new { u.DisplayName, u.AvatarUrl, u.Email })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var authorName = author?.DisplayName
            ?? (string.IsNullOrWhiteSpace(author?.Email)
                ? "Unknown author"
                : author!.Email);

        var (toolsCount, kbDocsCount) = ComputeToolAndKbCounts(entity);

        // Schema reality v1 carryover (Gate B):
        //   - InstallCount: no ToolkitInstallation entity yet → stub 0.
        //   - HasInstalled: derives from same missing entity → false.
        //   - RatingAverage/RatingCount: no ToolkitRating entity → null/0.
        //   - CanRate: HasInstalled && !alreadyRated && !isOwner. With no
        //     installation tracking, this collapses to false in v1.
        const int installCount = 0;
        const bool hasInstalled = false;
        decimal? ratingAverage = null;
        const int ratingCount = 0;
        var canRate = hasInstalled && !isOwner; // !alreadyRated true by stub

        var publishedAt = isPublished ? entity.UpdatedAt : (DateTime?)null;

        // Issue #1144 — VersionSemver is the source of truth for the marketplace
        // surface (spec D-5). Legacy int Version retained for the composite
        // uniqueness index until the cleanup PR scheduled in spec §13.
        var currentVersion = entity.VersionSemver;

        var agentSummary = BuildAgentSummary(entity);
        var sizeBytes = ComputeSizeBytes(entity);

        // Description: prefer the real column; fall back to a synthetic preview
        // only when the column is NULL. Empty-string ("") is treated as a
        // legitimate user choice (spec §9.1 cross-aggregate edge cases).
        var description = entity.Description
            ?? $"Toolkit \"{entity.Name}\" by {authorName}.";

        var detail = new ToolkitDetailDto(
            Id: entity.Id,
            Name: entity.Name,
            Description: description,
            AuthorId: entity.CreatedByUserId,
            AuthorName: authorName,
            AuthorAvatarUrl: author?.AvatarUrl,
            CoverImageUrl: null,
            Agent: agentSummary,
            KbDocsCount: kbDocsCount,
            ToolsCount: toolsCount,
            InstallCount: installCount,
            RatingAverage: ratingAverage,
            RatingCount: ratingCount,
            CreatedAt: entity.CreatedAt,
            PublishedAt: publishedAt,
            YankedAt: null,
            CurrentVersion: currentVersion,
            // Stage 3 marketplace fields (spec §5.3)
            License: entity.License,
            GameName: entity.Game?.Name,
            SizeBytes: sizeBytes);

        var viewerContext = new ViewerContextDto(
            IsOwner: isOwner,
            HasInstalled: hasInstalled,
            CanRate: canRate);

        var response = new ToolkitDetailResponse(detail, viewerContext);

        _logger.LogInformation(
            "Returning toolkit detail {ToolkitId} for viewer {ViewerId} (isOwner={IsOwner}, isPublished={IsPublished})",
            entity.Id,
            request.ViewerId,
            isOwner,
            isPublished);

        return new ToolkitDetailContainer(response);
    }

    /// <summary>
    /// Issue #1144 / spec §5.4 — single canonical SizeBytes formula. Sums UTF-8
    /// byte counts (NEVER char counts) of every JSON/text payload stored on the
    /// entity. Reads the persisted raw JSON columns directly (NEVER
    /// re-serialises from a typed object — re-serialisation is non-deterministic
    /// on key ordering and whitespace, which would make SizeBytes drift between
    /// sequential reads of the same row).
    /// </summary>
    private static long ComputeSizeBytes(GameToolkitEntity entity)
    {
        long total = 0;
        total += Utf8ByteCount(entity.AgentConfig);
        total += Utf8ByteCount(entity.DiceToolsJson);
        total += Utf8ByteCount(entity.CardToolsJson);
        total += Utf8ByteCount(entity.TimerToolsJson);
        total += Utf8ByteCount(entity.CounterToolsJson);
        total += Utf8ByteCount(entity.UserDicePresetsJson);
        total += Utf8ByteCount(entity.ScoringTemplateJson);
        total += Utf8ByteCount(entity.TurnTemplateJson);
        total += Utf8ByteCount(entity.StateTemplate);
        return total;
    }

    private static long Utf8ByteCount(string? value)
        => value is null ? 0 : System.Text.Encoding.UTF8.GetByteCount(value);

    private static (int ToolsCount, int KbDocsCount) ComputeToolAndKbCounts(GameToolkitEntity entity)
    {
        // ToolsCount: rough sum of dice/card/timer/counter tool entries embedded
        // in the JSONB columns. Counting at the JSON-array level avoids a full
        // aggregate hydration just for the surface metric.
        var toolsCount = CountJsonArrayItems(entity.DiceToolsJson)
            + CountJsonArrayItems(entity.CardToolsJson)
            + CountJsonArrayItems(entity.TimerToolsJson)
            + CountJsonArrayItems(entity.CounterToolsJson);

        // KbDocsCount: GameToolkitEntity does not currently track linked KB
        // documents (Gate B carryover). Stub 0 until Phase 4 wires the schema.
        const int kbDocsCount = 0;

        return (toolsCount, kbDocsCount);
    }

    private static int CountJsonArrayItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                ? doc.RootElement.GetArrayLength()
                : 0;
        }
        catch (System.Text.Json.JsonException)
        {
            return 0;
        }
    }

    private static ToolkitAgentSummaryDto BuildAgentSummary(GameToolkitEntity entity)
    {
        // AgentConfig is JSON; extract a name + system prompt preview when
        // present. Absent / malformed configs fall back to a deterministic stub
        // so the FE renderer can always show *something* (Doumont clarity).
        var agentName = entity.Name;
        var systemPromptPreview = string.Empty;

        if (!string.IsNullOrWhiteSpace(entity.AgentConfig))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(entity.AgentConfig);
                var root = doc.RootElement;
                if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (root.TryGetProperty("name", out var nameProp)
                        && nameProp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        agentName = nameProp.GetString() ?? entity.Name;
                    }

                    if (root.TryGetProperty("systemPrompt", out var promptProp)
                        && promptProp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var raw = promptProp.GetString() ?? string.Empty;
                        systemPromptPreview = raw.Length > MaxSystemPromptPreviewLength
                            ? raw[..MaxSystemPromptPreviewLength]
                            : raw;
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Malformed AgentConfig → leave preview empty.
            }
        }

        // AgentId: GameToolkitEntity has no FK to AgentDefinition in v1.
        // Use a deterministic GUID derived from toolkit Id so the FE can
        // dedupe across renders without colliding across toolkits.
        return new ToolkitAgentSummaryDto(
            Id: entity.Id,
            Name: agentName,
            SystemPromptPreview: systemPromptPreview);
    }

    /// <summary>
    /// HybridCache requires a class type for caching; this container lets us
    /// cache a nullable <see cref="ToolkitDetailResponse"/> uniformly.
    /// </summary>
    internal sealed record ToolkitDetailContainer(ToolkitDetailResponse? Response);
}
