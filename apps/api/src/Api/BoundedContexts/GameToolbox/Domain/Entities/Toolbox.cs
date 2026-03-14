using Api.BoundedContexts.GameToolbox.Domain.ValueObjects;

namespace Api.BoundedContexts.GameToolbox.Domain.Entities;

/// <summary>
/// Toolbox aggregate root — a per-game configurable container of tools.
/// </summary>
public class Toolbox
{
    private List<ToolboxTool> _tools = [];
    private List<Phase> _phases = [];

    public Guid Id { get; private set; }
    public Guid? GameId { get; private set; }
    public Guid? TemplateId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ToolboxMode Mode { get; private set; } = ToolboxMode.Freeform;
    public SharedContext SharedContext { get; private set; } = new();
    public Guid? CurrentPhaseId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public IReadOnlyList<ToolboxTool> Tools => _tools.AsReadOnly();
    public IReadOnlyList<Phase> Phases => _phases.AsReadOnly();

    private Toolbox() { }

    /// <summary>
    /// Creates a new Toolbox with the specified name and optional game association.
    /// </summary>
    public static Toolbox Create(string name, Guid? gameId = null, ToolboxMode mode = ToolboxMode.Freeform)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Toolbox name is required.", nameof(name));

        return new Toolbox
        {
            Id = Guid.NewGuid(),
            Name = name,
            GameId = gameId,
            Mode = mode,
            SharedContext = new SharedContext(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Changes the Toolbox mode. Switching to Freeform clears the current phase.
    /// Switching to Phased sets the first phase as current (if any exist).
    /// </summary>
    public void UpdateMode(ToolboxMode mode)
    {
        Mode = mode;
        if (mode == ToolboxMode.Freeform)
            CurrentPhaseId = null;
        else if (_phases.Count > 0)
            CurrentPhaseId = _phases.OrderBy(p => p.Order).First().Id;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a tool to this Toolbox with auto-incrementing order.
    /// </summary>
    public ToolboxTool AddTool(string type, string config)
    {
        var order = _tools.Count > 0 ? _tools.Max(t => t.Order) + 1 : 0;
        var tool = ToolboxTool.Create(type, config, order);
        tool.SetToolboxId(Id);
        _tools.Add(tool);
        UpdatedAt = DateTime.UtcNow;
        return tool;
    }

    /// <summary>
    /// Removes a tool by ID. Throws if not found.
    /// </summary>
    public void RemoveTool(Guid toolId)
    {
        var tool = _tools.FirstOrDefault(t => t.Id == toolId)
            ?? throw new InvalidOperationException($"Tool {toolId} not found in Toolbox {Id}.");
        _tools.Remove(tool);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reorders tools based on the provided ordered list of tool IDs.
    /// </summary>
    public void ReorderTools(List<Guid> orderedToolIds)
    {
        for (var i = 0; i < orderedToolIds.Count; i++)
        {
            var tool = _tools.FirstOrDefault(t => t.Id == orderedToolIds[i]);
            tool?.SetOrder(i);
        }
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the shared context (players, turn, round).
    /// </summary>
    public void UpdateSharedContext(SharedContext context)
    {
        SharedContext = context ?? throw new ArgumentNullException(nameof(context));
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a phase to this Toolbox. In Phased mode, sets as current if it's the first.
    /// </summary>
    public Phase AddPhase(string name, List<Guid>? activeToolIds = null)
    {
        var order = _phases.Count > 0 ? _phases.Max(p => p.Order) + 1 : 0;
        var phase = Phase.Create(name, order, activeToolIds);
        phase.SetToolboxId(Id);
        _phases.Add(phase);
        if (CurrentPhaseId == null && Mode == ToolboxMode.Phased)
            CurrentPhaseId = phase.Id;
        UpdatedAt = DateTime.UtcNow;
        return phase;
    }

    /// <summary>
    /// Removes a phase. If it was the current phase, advances to the next.
    /// </summary>
    public void RemovePhase(Guid phaseId)
    {
        var phase = _phases.FirstOrDefault(p => p.Id == phaseId)
            ?? throw new InvalidOperationException($"Phase {phaseId} not found in Toolbox {Id}.");
        _phases.Remove(phase);
        if (CurrentPhaseId == phaseId)
            CurrentPhaseId = _phases.OrderBy(p => p.Order).FirstOrDefault()?.Id;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reorders phases based on the provided ordered list of phase IDs.
    /// </summary>
    public void ReorderPhases(List<Guid> orderedPhaseIds)
    {
        for (var i = 0; i < orderedPhaseIds.Count; i++)
        {
            var phase = _phases.FirstOrDefault(p => p.Id == orderedPhaseIds[i]);
            phase?.SetOrder(i);
        }
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Advances to the next phase. Wrapping around advances the round.
    /// Only valid in Phased mode.
    /// </summary>
    public Phase AdvancePhase()
    {
        if (Mode != ToolboxMode.Phased)
            throw new InvalidOperationException("Cannot advance phase in Freeform mode.");
        if (_phases.Count == 0)
            throw new InvalidOperationException("No phases defined.");

        var ordered = _phases.OrderBy(p => p.Order).ToList();
        var currentIndex = ordered.FindIndex(p => p.Id == CurrentPhaseId);
        var nextIndex = (currentIndex + 1) % ordered.Count;

        if (nextIndex == 0)
            SharedContext = SharedContext.AdvanceRound();

        CurrentPhaseId = ordered[nextIndex].Id;
        UpdatedAt = DateTime.UtcNow;
        return ordered[nextIndex];
    }

    /// <summary>
    /// Marks this Toolbox as soft-deleted.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    // EF Core hydration
    internal void SetTools(List<ToolboxTool> tools) => _tools = tools;
    internal void SetPhases(List<Phase> phases) => _phases = phases;
    internal void SetTemplateId(Guid templateId) => TemplateId = templateId;
}
