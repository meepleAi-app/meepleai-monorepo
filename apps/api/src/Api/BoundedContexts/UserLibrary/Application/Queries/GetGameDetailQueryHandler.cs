using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Covers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Handler for retrieving complete game detail with statistics, sessions, and checklist.
/// Uses HybridCache with 5-minute TTL for performance optimization.
/// </summary>
internal class GetGameDetailQueryHandler : IQueryHandler<GetGameDetailQuery, GameDetailDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IGameLabelRepository _labelRepository;
    private readonly IAgentDefinitionRepository _agentDefinitionRepository;
    private readonly IChatThreadRepository _chatThreadRepository;
    // #4085: injected alongside the repository abstractions, not instead of them — same
    // mixed pattern already used by GetUserLibraryQueryHandler in this bounded context.
    // CoverUrlResolver needs the EF SharedGameEntity (raw cover-key columns +
    // CoverAssignments nav) and IBlobStorageService for the presign; neither is reachable
    // through ISharedGameRepository, which returns the domain SharedGame aggregate.
    private readonly MeepleAiDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly HybridCache _cache;
    private readonly ILogger<GetGameDetailQueryHandler> _logger;

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };

    public GetGameDetailQueryHandler(
        IUserLibraryRepository libraryRepository,
        ISharedGameRepository sharedGameRepository,
        IGameLabelRepository labelRepository,
        IAgentDefinitionRepository agentDefinitionRepository,
        IChatThreadRepository chatThreadRepository,
        MeepleAiDbContext db,
        IBlobStorageService blobStorage,
        HybridCache cache,
        ILogger<GetGameDetailQueryHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _labelRepository = labelRepository ?? throw new ArgumentNullException(nameof(labelRepository));
        _agentDefinitionRepository = agentDefinitionRepository ?? throw new ArgumentNullException(nameof(agentDefinitionRepository));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameDetailDto> Handle(GetGameDetailQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Try cache first
        var cacheKey = $"game-detail:{query.UserId}:{query.GameId}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                _logger.LogDebug("Cache miss for game detail {GameId} for user {UserId}", query.GameId, query.UserId);

                // Get library entry with sessions and checklist
                var entry = await _libraryRepository.GetUserGameWithStatsAsync(
                    query.UserId,
                    query.GameId,
                    cancel).ConfigureAwait(false);

                if (entry is null)
                {
                    _logger.LogWarning("Game {GameId} not found in library for user {UserId}", query.GameId, query.UserId);
                    throw new NotFoundException($"Game {query.GameId} not found in your library");
                }

                // Get game metadata from SharedGameCatalog
                var sharedGame = await _sharedGameRepository.GetByIdAsync(query.GameId, cancel).ConfigureAwait(false);

                if (sharedGame is null)
                {
                    _logger.LogError("SharedGame {GameId} not found in catalog (data integrity issue)", query.GameId);
                    throw new NotFoundException($"Game {query.GameId} not found in catalog");
                }

                // Map recent sessions (last 5)
                var recentSessions = entry.Sessions
                    .OrderByDescending(s => s.PlayedAt)
                    .Take(5)
                    .Select(s => new GameSessionDto(
                        Id: s.Id,
                        PlayedAt: s.PlayedAt,
                        DurationMinutes: s.DurationMinutes,
                        DurationFormatted: s.GetDurationFormatted(),
                        DidWin: s.DidWin,
                        Players: s.Players,
                        Notes: s.Notes
                    ))
                    .ToArray();

                // Map checklist (ordered)
                var checklist = entry.GetOrderedChecklist()
                    .Select(c => new GameChecklistItemDto(
                        Id: c.Id,
                        Description: c.Description,
                        Order: c.DisplayOrder,
                        IsCompleted: c.IsCompleted,
                        AdditionalInfo: c.AdditionalInfo
                    ))
                    .ToArray();

                // Map custom agent config if present
                AgentConfigDto? customAgentConfig = null;
                if (entry.CustomAgentConfig is not null)
                {
                    customAgentConfig = new AgentConfigDto(
                        LlmModel: entry.CustomAgentConfig.LlmModel,
                        Temperature: entry.CustomAgentConfig.Temperature,
                        MaxTokens: entry.CustomAgentConfig.MaxTokens,
                        Personality: entry.CustomAgentConfig.Personality,
                        DetailLevel: entry.CustomAgentConfig.DetailLevel,
                        PersonalNotes: entry.CustomAgentConfig.PersonalNotes
                    );
                }

                // Map custom PDF if present
                CustomPdfDto? customPdf = null;
                if (entry.CustomPdfMetadata is not null)
                {
                    customPdf = new CustomPdfDto(
                        Url: entry.CustomPdfMetadata.Url,
                        UploadedAt: entry.CustomPdfMetadata.UploadedAt,
                        FileSizeBytes: entry.CustomPdfMetadata.FileSizeBytes,
                        OriginalFileName: entry.CustomPdfMetadata.OriginalFileName
                    );
                }

                // Get labels for this entry
                var labels = await _labelRepository.GetLabelsForEntryAsync(entry.Id, cancel).ConfigureAwait(false);
                var labelsDto = labels.Select(l => new LabelDto(
                    Id: l.Id,
                    Name: l.Name,
                    Color: l.Color,
                    IsPredefined: l.IsPredefined,
                    CreatedAt: l.CreatedAt
                )).ToArray();

                // Issue #2034 — ConnectionBar pill counts. AgentCount is cross-user
                // (AgentDefinition.GameId points to the shared catalog game), while
                // ChatThreadCount is scoped to the requesting user.
                var agentCount = await _agentDefinitionRepository
                    .CountActiveByGameIdsAsync(new[] { query.GameId }, cancel)
                    .ConfigureAwait(false);

                var userThreads = await _chatThreadRepository
                    .FindByUserIdAndGameIdAsync(query.UserId, query.GameId, cancel)
                    .ConfigureAwait(false);
                var chatThreadCount = userThreads.Count;

                // #4085: GameImageUrl used to be sharedGame.ImageUrl — the #2123 tombstone
                // column, always empty since the BGG user-side asset ban. Resolve the real
                // cover (admin override → PDF → BGG → Wikidata → null) the same way the
                // catalog list and detail pages already do. Cache TTL here is 5 minutes
                // (CacheOptions above), well under the resolver's 4h presign lifetime
                // (CoverUrlResolver.CoverPresignExpirySeconds) — safe to resolve inside this
                // cached factory, unlike GetCatalogTrendingQueryHandler's 12h cache (#4085).
                // L3 user-custom-cover is intentionally NOT wired here (would need an
                // additional UserLibraryEntryEntity fetch); ResolveForContextAsync still
                // honors the admin per-context override and the implicit L4→L2 chain.
                var sharedGameEntity = await _db.Set<SharedGameEntity>()
                    .AsNoTracking()
                    .Include(g => g.CoverAssignments)
                    .FirstOrDefaultAsync(g => g.Id == query.GameId, cancel)
                    .ConfigureAwait(false);
                var coverUrl = sharedGameEntity is not null
                    ? await CoverUrlResolver.ResolveForContextAsync(sharedGameEntity, CoverContext.Hero, _blobStorage).ConfigureAwait(false)
                    : null;

                _logger.LogInformation("Retrieved game detail for {GameId} for user {UserId}", query.GameId, query.UserId);

                return new GameDetailDto(
                    Id: entry.Id,
                    UserId: entry.UserId,
                    GameId: entry.GameId,

                    // Game metadata from SharedGameCatalog
                    GameTitle: sharedGame.Title,
                    GamePublisher: string.Join(", ", sharedGame.Publishers.Select(p => p.Name)),
                    GameYearPublished: sharedGame.YearPublished,
                    GameDescription: sharedGame.Description,
                    GameIconUrl: sharedGame.ThumbnailUrl,
                    GameImageUrl: coverUrl,
                    MinPlayers: sharedGame.MinPlayers,
                    MaxPlayers: sharedGame.MaxPlayers,
                    PlayTimeMinutes: sharedGame.PlayingTimeMinutes,
                    ComplexityRating: sharedGame.ComplexityRating,
                    AverageRating: sharedGame.AverageRating,

                    // Library metadata
                    AddedAt: entry.AddedAt,
                    Notes: entry.Notes?.Value,
                    IsFavorite: entry.IsFavorite,

                    // Game state
                    CurrentState: entry.CurrentState.Value.ToString(),
                    StateChangedAt: entry.CurrentState.ChangedAt,
                    StateNotes: entry.CurrentState.StateNotes,
                    IsAvailableForPlay: entry.IsAvailableForPlay(),

                    // Game statistics
                    TimesPlayed: entry.Stats.TimesPlayed,
                    LastPlayed: entry.Stats.LastPlayed,
                    WinRate: entry.Stats.GetWinRateFormatted(),
                    AvgDuration: entry.Stats.GetAvgDurationFormatted(),

                    // Collections
                    RecentSessions: recentSessions,
                    Checklist: checklist,
                    CustomAgentConfig: customAgentConfig,
                    CustomPdf: customPdf,
                    Labels: labelsDto,

                    // Issue #1824 L3: user-custom cover key (null if no custom cover uploaded)
                    CustomCoverR2Key: entry.CustomCoverR2Key,

                    // Issue #2035: Designer names from the SharedGame catalog M:N relation.
                    // Always materialised to a list (possibly empty) so the FE consumer
                    // (GameDetailDesktop.tsx) can safely read `game.designers?.[0]?.name`.
                    Designers: sharedGame.Designers
                        .Select(d => d.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .ToList(),

                    // Issue #2034: ConnectionBar pill counts.
                    AgentCount: agentCount,
                    ChatThreadCount: chatThreadCount
                );
            },
            options: CacheOptions,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }
}
