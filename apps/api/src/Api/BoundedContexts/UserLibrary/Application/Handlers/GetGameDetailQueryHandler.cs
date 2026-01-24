using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for retrieving complete game detail with statistics, sessions, and checklist.
/// Uses HybridCache with 5-minute TTL for performance optimization.
/// </summary>
internal class GetGameDetailQueryHandler : IQueryHandler<GetGameDetailQuery, GameDetailDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
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
        HybridCache cache,
        ILogger<GetGameDetailQueryHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
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
                        Order: c.Order,
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
                    GameImageUrl: sharedGame.ImageUrl,
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
                    CustomPdf: customPdf
                );
            },
            options: CacheOptions,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }
}
