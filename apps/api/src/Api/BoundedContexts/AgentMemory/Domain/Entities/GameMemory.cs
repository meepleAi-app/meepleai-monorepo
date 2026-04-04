using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.AgentMemory.Domain.Models;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.AgentMemory.Domain.Entities;

/// <summary>
/// Aggregate root storing per-game, per-owner memory: house rules, setup checklists, and notes.
/// </summary>
internal sealed class GameMemory : AggregateRoot<Guid>
{
    public Guid GameId { get; private set; }
    public Guid OwnerId { get; private set; }

    private readonly List<HouseRule> _houseRules = new();
    public IReadOnlyList<HouseRule> HouseRules => _houseRules.AsReadOnly();

    public SetupChecklistData? CustomSetup { get; private set; }

    private readonly List<MemoryNote> _notes = new();
    public IReadOnlyList<MemoryNote> Notes => _notes.AsReadOnly();

    public DateTime CreatedAt { get; private set; }

    /// <summary>EF Core constructor.</summary>
    private GameMemory() { }

    public static GameMemory Create(Guid gameId, Guid ownerId)
    {
        if (gameId == Guid.Empty) throw new ArgumentException("GameId cannot be empty.", nameof(gameId));
        if (ownerId == Guid.Empty) throw new ArgumentException("OwnerId cannot be empty.", nameof(ownerId));

        return new GameMemory
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AddHouseRule(string description, HouseRuleSource source)
    {
        _houseRules.Add(HouseRule.Create(description, source));
    }

    public void SetCustomSetup(SetupChecklistData setup)
    {
        CustomSetup = setup ?? throw new ArgumentNullException(nameof(setup));
    }

    public void AddNote(string content, Guid? addedByUserId)
    {
        _notes.Add(MemoryNote.Create(content, addedByUserId));
    }
}
