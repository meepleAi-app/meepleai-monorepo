using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.Dashboard;

/// <summary>
/// Handler for <see cref="GetFriendsActivityQuery"/>.
/// Asse C (#1898) WP1 T1 DEC-2: computes the dashboard "Cosa fanno i tuoi"
/// activity feed.
///
/// <para>Algorithm:
/// <list type="number">
///   <item>Find GameNights where the current user is organizer OR has an RSVP,
///         scheduled in the last <see cref="FriendsWindowDays"/> days. These are
///         the "shared game nights".</item>
///   <item>Collect distinct other User-linked participants from those nights:
///         organizers + RSVP user ids, excluding the current user. These are
///         qualified "friends" (MAJ-5).</item>
///   <item>Project recent activities by those friends across all their GameNights
///         (not only the shared ones) into three verbs:
///         <c>created</c> (friend is organizer, status != Completed),
///         <c>completed</c> (friend is organizer, status == Completed),
///         <c>joined</c> (friend has an Accepted RSVP).</item>
///   <item>Order by timestamp DESC and take <see cref="GetFriendsActivityQuery.Limit"/>.</item>
/// </list></para>
///
/// <para>RSVP/Status comparisons use string literals because the persistence
/// entities store these as strings (see <c>GameNightEventEntity.Status</c> and
/// <c>GameNightRsvpEntity.Status</c>). Enum-based comparisons would not translate
/// to SQL.</para>
/// </summary>
internal sealed class GetFriendsActivityQueryHandler
    : IQueryHandler<GetFriendsActivityQuery, IReadOnlyList<FriendActivityDto>>
{
    /// <summary>
    /// Spec MAJ-5: friendship qualification window. A User-linked player counts
    /// as a "friend" if they share at least one GameNight with the current user
    /// scheduled within this window.
    /// </summary>
    private const int FriendsWindowDays = 90;

    private const string GameNightEntityType = "gameNight";
    private const string CompletedStatus = "Completed";
    private const string AcceptedRsvp = "Accepted";

    private readonly MeepleAiDbContext _context;

    public GetFriendsActivityQueryHandler(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<FriendActivityDto>> Handle(
        GetFriendsActivityQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.UserId == Guid.Empty)
        {
            return Array.Empty<FriendActivityDto>();
        }

        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddDays(-FriendsWindowDays);

        // Step 1: discover friend user ids from shared game nights in the window.
        var friendUserIds = await DiscoverFriendUserIdsAsync(
            request.UserId,
            windowStart,
            cancellationToken).ConfigureAwait(false);

        if (friendUserIds.Count == 0)
        {
            return Array.Empty<FriendActivityDto>();
        }

        // Step 2: load recent GameNights touched by any of those friends.
        // Overfetch (Limit * 3) so the per-night fan-out (1 organizer activity +
        // N accepted-RSVP activities) still has enough material after the final
        // timestamp-desc sort.
        var overfetchCount = Math.Max(request.Limit * 3, request.Limit);
        var recentNights = await _context.GameNightEvents
            .AsNoTracking()
            .Where(gn => friendUserIds.Contains(gn.OrganizerId)
                || gn.Rsvps.Any(r => friendUserIds.Contains(r.UserId)))
            .OrderByDescending(gn => gn.UpdatedAt ?? gn.CreatedAt)
            .Take(overfetchCount)
            .Select(gn => new GameNightProjection(
                gn.Id,
                gn.Title,
                gn.OrganizerId,
                gn.Status,
                gn.CreatedAt,
                gn.UpdatedAt,
                gn.Rsvps
                    .Where(r => friendUserIds.Contains(r.UserId) && r.Status == AcceptedRsvp)
                    .Select(r => new RsvpProjection(r.UserId, r.RespondedAt))
                    .ToList()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Step 3: hydrate display info for all friend user ids that actually
        // appear in the projections (organizer or accepted-RSVP author).
        var friendInfoMap = await LoadFriendDisplayInfoAsync(
            friendUserIds,
            cancellationToken).ConfigureAwait(false);

        // Step 4: fan out into FriendActivityDto and sort/limit.
        var activities = new List<FriendActivityDto>(capacity: overfetchCount);

        foreach (var night in recentNights)
        {
            // Organizer activity: "created" or "completed".
            if (friendUserIds.Contains(night.OrganizerId)
                && friendInfoMap.TryGetValue(night.OrganizerId, out var organizer))
            {
                var verb = string.Equals(night.Status, CompletedStatus, StringComparison.Ordinal)
                    ? "completed"
                    : "created";

                activities.Add(new FriendActivityDto(
                    FriendUserId: night.OrganizerId,
                    Avatar: organizer.AvatarUrl ?? string.Empty,
                    Name: organizer.DisplayName,
                    Verb: verb,
                    GameOrEventId: night.Id,
                    GameOrEventType: GameNightEntityType,
                    GameOrEventName: night.Title,
                    Timestamp: night.UpdatedAt ?? night.CreatedAt));
            }

            // Joined activities (accepted RSVPs by friends, excluding self-organized rows).
            foreach (var rsvp in night.FriendAcceptedRsvps)
            {
                if (rsvp.UserId == night.OrganizerId)
                {
                    // Avoid double-counting when an organizer also has an RSVP row
                    // (defensive — RSVPs typically don't include the organizer).
                    continue;
                }

                if (!friendInfoMap.TryGetValue(rsvp.UserId, out var friend))
                {
                    continue;
                }

                activities.Add(new FriendActivityDto(
                    FriendUserId: rsvp.UserId,
                    Avatar: friend.AvatarUrl ?? string.Empty,
                    Name: friend.DisplayName,
                    Verb: "joined",
                    GameOrEventId: night.Id,
                    GameOrEventType: GameNightEntityType,
                    GameOrEventName: night.Title,
                    Timestamp: rsvp.RespondedAt ?? night.CreatedAt));
            }
        }

        return activities
            .OrderByDescending(a => a.Timestamp)
            .Take(request.Limit)
            .ToList();
    }

    /// <summary>
    /// Discovers User-linked friends of <paramref name="userId"/> by looking at
    /// GameNights scheduled within the friends window where the current user is
    /// either the organizer or an invitee.
    /// </summary>
    private async Task<HashSet<Guid>> DiscoverFriendUserIdsAsync(
        Guid userId,
        DateTimeOffset windowStart,
        CancellationToken cancellationToken)
    {
        // Composite query: pull (OrganizerId, RsvpUserIds) for shared nights only.
        var sharedNights = await _context.GameNightEvents
            .AsNoTracking()
            .Where(gn => gn.ScheduledAt >= windowStart)
            .Where(gn => gn.OrganizerId == userId
                || gn.Rsvps.Any(r => r.UserId == userId))
            .Select(gn => new SharedNightProjection(
                gn.OrganizerId,
                gn.Rsvps.Select(r => r.UserId).ToList()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var friendIds = new HashSet<Guid>();
        foreach (var night in sharedNights)
        {
            if (night.OrganizerId != userId)
            {
                friendIds.Add(night.OrganizerId);
            }

            foreach (var participantId in night.ParticipantUserIds)
            {
                if (participantId != userId)
                {
                    friendIds.Add(participantId);
                }
            }
        }

        return friendIds;
    }

    /// <summary>
    /// Loads display info (DisplayName/Email/AvatarUrl) for the given user ids.
    /// Email-local-part is used as fallback when DisplayName is null/blank.
    /// </summary>
    private async Task<Dictionary<Guid, FriendDisplayInfo>> LoadFriendDisplayInfoAsync(
        HashSet<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, FriendDisplayInfo>();
        }

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.DisplayName,
                u.Email,
                u.AvatarUrl,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return users.ToDictionary(
            u => u.Id,
            u => new FriendDisplayInfo(
                DisplayName: ResolveDisplayName(u.DisplayName, u.Email),
                AvatarUrl: u.AvatarUrl));
    }

    private static string ResolveDisplayName(string? displayName, string email)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return "Unknown";
        }

        // Email-local-part fallback: "alice@example.com" → "alice"
        var atIndex = email.IndexOf('@', StringComparison.Ordinal);
        return atIndex > 0 ? email[..atIndex] : email;
    }

    // ── Projection records (private nested) ─────────────────────────────────

    private sealed record SharedNightProjection(
        Guid OrganizerId,
        List<Guid> ParticipantUserIds);

    private sealed record GameNightProjection(
        Guid Id,
        string Title,
        Guid OrganizerId,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        List<RsvpProjection> FriendAcceptedRsvps);

    private sealed record RsvpProjection(
        Guid UserId,
        DateTimeOffset? RespondedAt);

    private sealed record FriendDisplayInfo(
        string DisplayName,
        string? AvatarUrl);
}
