using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Issue #2036 — Aggregates finalized session participants per game and surfaces
/// the top N registered users (guest participants without a UserId are excluded).
///
/// Pipeline: SessionTrackingSessions (Finalized, non-deleted) → flatten
/// Participants → drop guests (UserId == null) → GROUP BY UserId → COUNT →
/// ORDER BY count desc → take limit → join Users.DisplayName.
///
/// The SQL stays a single round-trip thanks to the open-generic <c>SessionTrackingSessions</c>
/// → <c>Participants</c> navigation already configured by the SessionTracking
/// EF configuration. The post-join Users lookup is a second batched query
/// (1 round-trip with <c>IN (...)</c>) — kept separate from the main GroupBy
/// projection because EF 9 still emits sub-optimal SQL for "group + join entity
/// in one shot" against nullable FKs.
/// </summary>
internal sealed class GetGameSessionContributorsQueryHandler
    : IQueryHandler<GetGameSessionContributorsQuery, IReadOnlyList<SessionContributorDto>>
{
    // CA1861: avoid allocating a new array on every initials computation.
    private static readonly char[] InitialsSeparators = { ' ', '\t' };

    private readonly MeepleAiDbContext _db;

    public GetGameSessionContributorsQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<SessionContributorDto>> Handle(
        GetGameSessionContributorsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.GameId == Guid.Empty)
        {
            return Array.Empty<SessionContributorDto>();
        }

        var limit = Math.Clamp(query.Limit, 1, 50);
        const string finalizedStatus = "Finalized";

        var grouped = await _db.SessionTrackingSessions
            .Where(s => s.GameId == query.GameId
                        && s.Status == finalizedStatus
                        && !s.IsDeleted)
            .SelectMany(s => s.Participants)
            .Where(p => p.UserId != null)
            .GroupBy(p => p.UserId!.Value)
            .Select(g => new { UserId = g.Key, SessionCount = g.Count() })
            .OrderByDescending(x => x.SessionCount)
            .ThenBy(x => x.UserId)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (grouped.Count == 0)
        {
            return Array.Empty<SessionContributorDto>();
        }

        var userIds = grouped.Select(g => g.UserId).ToList();
        var userNames = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var nameLookup = userNames.ToDictionary(u => u.Id, u => u.DisplayName);

        return grouped
            .Select(g =>
            {
                var displayName = nameLookup.GetValueOrDefault(g.UserId) ?? "Unknown";
                return new SessionContributorDto(
                    UserId: g.UserId,
                    DisplayName: displayName,
                    Initials: ComputeInitials(displayName),
                    SessionCount: g.SessionCount);
            })
            .ToList();
    }

    /// <summary>
    /// Returns 1–2 uppercase letters from the display name (first letter of the
    /// first two whitespace-separated parts; falls back to the first letter when
    /// the name is a single word). Empty/whitespace input returns "??" so the
    /// caller can render a deterministic placeholder.
    /// </summary>
    private static string ComputeInitials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "??";
        }

        var parts = displayName
            .Split(InitialsSeparators, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return "??";
        }

        if (parts.Length == 1)
        {
            return parts[0][..1].ToUpperInvariant();
        }

        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }
}
