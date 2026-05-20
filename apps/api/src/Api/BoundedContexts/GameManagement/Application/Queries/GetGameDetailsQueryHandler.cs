using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Handles get game details query with extended metadata and statistics.
/// Issue #1320 (P2c): Migrated from IGameRepository to IGameCoreDataProvider.
/// </summary>
internal class GetGameDetailsQueryHandler : IQueryHandler<GetGameDetailsQuery, GameDetailsDto?>
{
    private readonly IGameCoreDataProvider _gameCoreData;
    private readonly IGameSessionRepository _sessionRepository;

    public GetGameDetailsQueryHandler(
        IGameCoreDataProvider gameCoreData,
        IGameSessionRepository sessionRepository)
    {
        _gameCoreData = gameCoreData ?? throw new ArgumentNullException(nameof(gameCoreData));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<GameDetailsDto?> Handle(GetGameDetailsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var coreData = await _gameCoreData
            .GetCoreDataAsync(GameRef.Shared(query.GameId), cancellationToken)
            .ConfigureAwait(false);

        if (coreData == null)
            return null;

        // Get play statistics from sessions
        var sessions = await _sessionRepository.FindByGameIdAsync(query.GameId, cancellationToken).ConfigureAwait(false);
        var completedSessions = sessions
            .Where(s => s.Status == SessionStatus.Completed)
            .ToList();

        var totalSessionsPlayed = completedSessions.Count;
        var lastPlayedAt = completedSessions
            .OrderByDescending(s => s.CompletedAt)
            .Select(s => s.CompletedAt)
            .FirstOrDefault();

        return MapToDetailsDto(query.GameId, coreData, totalSessionsPlayed > 0 ? totalSessionsPlayed : null, lastPlayedAt);
    }

    private static GameDetailsDto MapToDetailsDto(Guid id, GameCoreData coreData, int? totalSessions, DateTime? lastPlayed)
    {
        return new GameDetailsDto(
            Id: id,
            Title: coreData.Title,
            Publisher: null,
            YearPublished: coreData.YearPublished,
            MinPlayers: coreData.MinPlayers,
            MaxPlayers: coreData.MaxPlayers,
            MinPlayTimeMinutes: coreData.PlayingTimeMinutes,
            MaxPlayTimeMinutes: coreData.PlayingTimeMinutes,
            BggId: coreData.BggId,
            BggMetadata: null,
            CreatedAt: DateTime.UtcNow,
            SupportsSolo: coreData.MinPlayers == 1,
            TotalSessionsPlayed: totalSessions,
            LastPlayedAt: lastPlayed,
            ImageUrl: coreData.ImageUrl
        );
    }
}
