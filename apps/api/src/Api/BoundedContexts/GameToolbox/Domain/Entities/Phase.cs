namespace Api.BoundedContexts.GameToolbox.Domain.Entities;

/// <summary>
/// A phase in a Phased-mode Toolbox that controls which tools are active.
/// </summary>
public class Phase
{
    private List<Guid> _activeToolIds = [];

    public Guid Id { get; private set; }
    public Guid ToolboxId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public IReadOnlyList<Guid> ActiveToolIds => _activeToolIds.AsReadOnly();

    private Phase() { }

    /// <summary>
    /// Creates a new phase with the specified name and tool activations.
    /// </summary>
    public static Phase Create(string name, int order, List<Guid>? activeToolIds = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Phase name is required.", nameof(name));

        return new Phase
        {
            Id = Guid.NewGuid(),
            Name = name,
            Order = order,
            _activeToolIds = activeToolIds ?? []
        };
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Phase name is required.", nameof(name));
        Name = name;
    }

    public void SetOrder(int order) => Order = order;
    public void SetActiveTools(List<Guid> toolIds) => _activeToolIds = toolIds;
    public bool IsToolActive(Guid toolId) => _activeToolIds.Contains(toolId);

    internal void SetToolboxId(Guid toolboxId) => ToolboxId = toolboxId;
}
