using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.AgentMemory.Domain.Models;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.AgentMemory.Domain.Entities;

/// <summary>
/// Aggregate root storing per-game, per-owner memory: house rules, setup checklists, notes, and glossary entries.
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

    private readonly List<GlossaryEntry> _glossaryEntries = new();
    public IReadOnlyList<GlossaryEntry> GlossaryEntries => _glossaryEntries.AsReadOnly();

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

    /// <summary>
    /// Updates the description of an existing house rule (#1464). Throws when the rule is unknown.
    /// </summary>
    public void UpdateHouseRule(Guid ruleId, string newDescription)
    {
        if (ruleId == Guid.Empty) throw new ArgumentException("RuleId cannot be empty.", nameof(ruleId));
        var rule = _houseRules.FirstOrDefault(r => r.Id == ruleId)
            ?? throw new InvalidOperationException($"House rule {ruleId} not found.");
        rule.UpdateDescription(newDescription);
    }

    /// <summary>
    /// Removes an existing house rule by id (#1464). No-op if the rule is unknown
    /// (idempotent delete); the caller decides whether to map that to a 404 at the endpoint.
    /// Returns true when a rule was actually removed.
    /// </summary>
    public bool RemoveHouseRule(Guid ruleId)
    {
        if (ruleId == Guid.Empty) throw new ArgumentException("RuleId cannot be empty.", nameof(ruleId));
        var rule = _houseRules.FirstOrDefault(r => r.Id == ruleId);
        if (rule is null) return false;
        return _houseRules.Remove(rule);
    }

    public void SetCustomSetup(SetupChecklistData setup)
    {
        CustomSetup = setup ?? throw new ArgumentNullException(nameof(setup));
    }

    public void AddNote(string content, Guid? addedByUserId)
    {
        _notes.Add(MemoryNote.Create(content, addedByUserId));
    }

    /// <summary>
    /// Adds a glossary entry for the game. Throws if a term with the same name already exists for the same language.
    /// </summary>
    public void AddGlossaryEntry(string term, string definition, string language, GlossaryEntrySource source)
    {
        if (_glossaryEntries.Any(e =>
            string.Equals(e.Term, term, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Language, language, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Glossary term '{term}' already exists for language '{language}'.");
        }

        _glossaryEntries.Add(GlossaryEntry.Create(term, definition, language, source));
    }
}
