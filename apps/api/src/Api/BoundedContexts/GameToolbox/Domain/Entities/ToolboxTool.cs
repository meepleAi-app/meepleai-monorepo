namespace Api.BoundedContexts.GameToolbox.Domain.Entities;

/// <summary>
/// A tool within a Toolbox (e.g., DiceRoller, ScoreTracker, CardDeck).
/// </summary>
public class ToolboxTool
{
    public Guid Id { get; private set; }
    public Guid ToolboxId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Config { get; private set; } = "{}";
    public string State { get; private set; } = "{}";
    public bool IsEnabled { get; private set; } = true;
    public int Order { get; private set; }

    private ToolboxTool() { }

    /// <summary>
    /// Creates a new tool with the specified type and configuration.
    /// </summary>
    public static ToolboxTool Create(string type, string config, int order)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Tool type is required.", nameof(type));

        return new ToolboxTool
        {
            Id = Guid.NewGuid(),
            Type = type,
            Config = config,
            Order = order,
            IsEnabled = true,
            State = "{}"
        };
    }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;
    public void UpdateConfig(string config) => Config = config;
    public void UpdateState(string state) => State = state;
    public void SetOrder(int order) => Order = order;

    internal void SetToolboxId(Guid toolboxId) => ToolboxId = toolboxId;
}
