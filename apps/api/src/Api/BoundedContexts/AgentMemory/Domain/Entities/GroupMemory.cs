using Api.BoundedContexts.AgentMemory.Domain.Models;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.AgentMemory.Domain.Entities;

/// <summary>
/// Aggregate root representing a play group's collective memory: members, preferences, and stats.
/// </summary>
internal sealed class GroupMemory : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public Guid CreatorId { get; private set; }

    private readonly List<GroupMember> _members = new();
    public IReadOnlyList<GroupMember> Members => _members.AsReadOnly();

    public GroupPreferences Preferences { get; private set; } = new();
    public GroupStats Stats { get; private set; } = new();
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Last domain event id processed against this aggregate (issue #1939 / CF-3).
    /// Handlers MUST early-exit when this equals the incoming event id to avoid
    /// double-incrementing GroupStats (TotalSessions, GamePlayCounts) on retry.
    /// </summary>
    public Guid? LastProcessedEventId { get; private set; }

    private readonly ProcessedSessions _processedSessions = new();

    /// <summary>The sessions already counted in this group's stats (#4839, option C), oldest first.</summary>
    public IReadOnlyList<Guid> ProcessedSessionIds => _processedSessions.Ids;

    /// <summary>EF Core constructor.</summary>
    private GroupMemory() { }

    public static GroupMemory Create(Guid creatorId, string name)
    {
        if (creatorId == Guid.Empty) throw new ArgumentException("CreatorId cannot be empty.", nameof(creatorId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be empty.", nameof(name));

        return new GroupMemory
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            Name = name,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AddMember(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        _members.Add(new GroupMember
        {
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        });
    }

    public void AddGuestMember(string guestName)
    {
        if (string.IsNullOrWhiteSpace(guestName))
            throw new ArgumentException("Guest name cannot be empty.", nameof(guestName));

        _members.Add(new GroupMember
        {
            GuestName = guestName,
            JoinedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Returns true when <paramref name="userId"/> is the group creator or a
    /// registered member. Used to authorize read/mutate access to the group
    /// (#2655 IDOR guard). The creator is normally also added as a member, but
    /// the explicit CreatorId check keeps this robust for legacy rows.
    /// </summary>
    public bool IsMemberOrCreator(Guid userId) =>
        userId != Guid.Empty
        && (CreatorId == userId || _members.Any(m => m.UserId == userId));

    public void UpdatePreferences(GroupPreferences preferences)
    {
        Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
    }

    public void UpdateStats(GroupStats stats)
    {
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
    }

    /// <summary>
    /// <c>true</c> when the result of this session has already been counted for this group, whatever event or
    /// delivery counted it (#4839, option C).
    /// </summary>
    public bool HasProcessedSession(Guid sessionId) => _processedSessions.Contains(sessionId);

    /// <summary>
    /// Records which domain event, and which session, have just been processed against this aggregate
    /// (issue #1939 / CF-3, #4839). Used by handlers to short-circuit on re-dispatch.
    /// </summary>
    public void MarkProcessed(Guid eventId, Guid sessionId)
    {
        LastProcessedEventId = eventId;
        _processedSessions.Add(sessionId);
    }

    /// <summary>Repository reconstitution: puts back the sessions already counted.</summary>
    internal void RestoreProcessedSessions(IEnumerable<Guid> sessionIds)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);
        foreach (var sessionId in new ProcessedSessions(sessionIds).Ids)
            _processedSessions.Add(sessionId);
    }
}
