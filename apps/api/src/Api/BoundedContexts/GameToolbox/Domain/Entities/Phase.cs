namespace Api.BoundedContexts.GameToolbox.Domain.Entities;

/// <summary>
/// A phase in a Phased-mode Toolbox that controls which tools are active.
/// </summary>
public class Phase
{
    public Guid Id { get; private set; }
    public Guid ToolboxId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public List<Guid> ActiveToolIds { get; private set; } = [];

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
            ActiveToolIds = activeToolIds ?? []
        };
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Phase name is required.", nameof(name));
        Name = name;
    }

    public void SetOrder(int order) => Order = order;
    public void SetActiveTools(List<Guid> toolIds) => ActiveToolIds = toolIds;
    public bool IsToolActive(Guid toolId) => ActiveToolIds.Contains(toolId);

    internal void SetToolboxId(Guid toolboxId) => ToolboxId = toolboxId;
}
