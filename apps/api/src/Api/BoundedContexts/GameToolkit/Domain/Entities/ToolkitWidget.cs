using Api.BoundedContexts.GameToolkit.Domain.Enums;

namespace Api.BoundedContexts.GameToolkit.Domain.Entities;

/// <summary>
/// Child entity representing a single widget slot within a Toolkit.
/// Owned by the Toolkit aggregate (cannot be created standalone).
/// Issue #5144 — Epic B1.
/// </summary>
public sealed class ToolkitWidget
{
    public Guid Id { get; private set; }
    public Guid ToolkitId { get; private set; }
    public WidgetType Type { get; private set; }
    public bool IsEnabled { get; private set; }
    public int DisplayOrder { get; private set; }

    /// <summary>Widget-specific configuration stored as JSONB. Defaults to empty object.</summary>
    public string Config { get; private set; } = "{}";

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // EF Core parameterless constructor
    private ToolkitWidget() { }

    internal static ToolkitWidget Create(Guid toolkitId, WidgetType type, int displayOrder, bool isEnabled = true)
    {
        return new ToolkitWidget
        {
            Id = Guid.NewGuid(),
            ToolkitId = toolkitId,
            Type = type,
            IsEnabled = isEnabled,
            DisplayOrder = displayOrder,
            Config = "{}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    internal void Enable()
    {
        IsEnabled = true;
        UpdatedAt = DateTime.UtcNow;
    }

    internal void Disable()
    {
        IsEnabled = false;
        UpdatedAt = DateTime.UtcNow;
    }

    internal void UpdateConfig(string configJson)
    {
        Config = configJson;
        UpdatedAt = DateTime.UtcNow;
    }
}
