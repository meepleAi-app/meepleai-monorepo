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

    public void UpdatePreferences(GroupPreferences preferences)
    {
        Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
    }

    public void UpdateStats(GroupStats stats)
    {
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
    }
}
