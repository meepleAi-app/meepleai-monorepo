using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Application.DTOs.Leaderboard;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.Leaderboard;

/// <summary>
/// Handles the game social leaderboard query (#1467): ranks the registered players across the
/// play records the caller is allowed to see (own records + records of the caller's groups).
/// </summary>
internal sealed class GetGameLeaderboardQueryHandler : IQueryHandler<GetGameLeaderboardQuery, GameLeaderboardResponse>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly MeepleAiDbContext _context;
    private readonly IGroupMemoryRepository _groupRepository;
    private readonly IHybridCacheService _cache;

    public GetGameLeaderboardQueryHandler(
        MeepleAiDbContext context,
        IGroupMemoryRepository groupRepository,
        IHybridCacheService cache)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _groupRepository = groupRepository ?? throw new ArgumentNullException(nameof(groupRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<GameLeaderboardResponse> Handle(GetGameLeaderboardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Normalize the optional filter to UTC so the cache key is timezone-stable and the echoed
        // value serializes with a 'Z' offset (the FE Zod schema requires an offset).
        if (query.Since.HasValue)
        {
            query = query with { Since = DateTime.SpecifyKind(query.Since.Value, DateTimeKind.Utc) };
        }

        // Response varies per caller (visibility scope) and per filter, so the cache key includes both.
        var cacheKey = $"game-leaderboard:{query.GameId}:user:{query.CurrentUserId}:since:{query.Since?.Ticks ?? 0}:limit:{query.Limit}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            ct => ComputeLeaderboardAsync(query, ct),
            tags: [$"game:{query.GameId}", "leaderboard"],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);
    }

    private async Task<GameLeaderboardResponse> ComputeLeaderboardAsync(GetGameLeaderboardQuery query, CancellationToken ct)
    {
        var groupIds = (await _groupRepository.GetGroupIdsForUserAsync(query.CurrentUserId, ct).ConfigureAwait(false)).ToList();

        var completed = (int)PlayRecordStatus.Completed;
        var groupVisibility = (int)PlayRecordVisibility.Group;

        var records = await _context.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players)
                .ThenInclude(p => p.Scores)
            .Where(r => r.GameId == query.GameId
                        && r.Status == completed
                        && (query.Since == null || r.SessionDate >= query.Since.Value)
                        && (r.CreatedByUserId == query.CurrentUserId
                            || (r.Visibility == groupVisibility
                                && r.GroupId.HasValue
                                && groupIds.Contains(r.GroupId.Value))))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var entries = records
            .SelectMany(r => r.Players
                .Where(p => p.UserId.HasValue)
                .Select(p => new PlayerParticipation(p.UserId!.Value, p.DisplayName, r.SessionDate, p)))
            .GroupBy(x => x.UserId)
            .Select(ToEntry)
            .OrderByDescending(e => e.Wins)
            .ThenByDescending(e => e.AvgScore ?? double.MinValue)
            .ThenByDescending(e => e.Plays)
            .ThenBy(e => e.PlayerId)
            .Take(query.Limit)
            .ToList();

        return new GameLeaderboardResponse
        {
            GameId = query.GameId,
            Entries = entries,
            ReturnedCount = entries.Count,
            Since = query.Since,
        };
    }

    private static GameLeaderboardEntryDto ToEntry(IGrouping<Guid, PlayerParticipation> group)
    {
        var wins = group
            .SelectMany(x => x.Player.Scores
                .Where(s => string.Equals(s.Dimension, ScoringDimensions.Wins, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Value))
            .Sum();

        var pointValues = group
            .SelectMany(x => x.Player.Scores
                .Where(s => string.Equals(s.Dimension, ScoringDimensions.Points, StringComparison.OrdinalIgnoreCase))
                .Select(s => (double)s.Value))
            .ToList();

        var mostRecent = group.OrderByDescending(x => x.SessionDate).First();

        return new GameLeaderboardEntryDto
        {
            PlayerId = group.Key,
            DisplayName = mostRecent.DisplayName,
            Initials = ComputeInitials(mostRecent.DisplayName),
            Wins = wins,
            Plays = group.Count(),
            AvgScore = pointValues.Count > 0 ? pointValues.Average() : null,
            // Force UTC kind so System.Text.Json emits a 'Z' offset (the FE Zod schema requires it).
            LastPlayedAt = DateTime.SpecifyKind(group.Max(x => x.SessionDate), DateTimeKind.Utc),
        };
    }

    private static string ComputeInitials(string displayName)
    {
        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "?";
        }

        if (parts.Length == 1)
        {
            var single = parts[0];
            return (single.Length >= 2 ? single[..2] : single[..1]).ToUpperInvariant();
        }

        return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
    }

    private readonly record struct PlayerParticipation(Guid UserId, string DisplayName, DateTime SessionDate, RecordPlayerEntity Player);
}
