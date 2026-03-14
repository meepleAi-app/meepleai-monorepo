using Api.BoundedContexts.GameToolbox.Domain.ValueObjects;

namespace Api.BoundedContexts.GameToolbox.Domain.Entities;

/// <summary>
/// Source of a Toolbox template.
/// </summary>
public enum TemplateSource
{
    Manual = 0,
    Community = 1,
    AI = 2
}

/// <summary>
/// A reusable blueprint for creating Toolboxes with preconfigured tools and phases.
/// </summary>
public class ToolboxTemplate
{
    public Guid Id { get; private set; }
    public Guid? GameId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ToolboxMode Mode { get; private set; }
    public TemplateSource Source { get; private set; }
    public string ToolsJson { get; private set; } = "[]";
    public string PhasesJson { get; private set; } = "[]";
    public string SharedContextDefaultsJson { get; private set; } = "{}";
    public DateTime CreatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private ToolboxTemplate() { }

    /// <summary>
    /// Creates a new Toolbox template.
    /// </summary>
    public static ToolboxTemplate Create(
        string name,
        ToolboxMode mode,
        TemplateSource source,
        string toolsJson,
        string phasesJson,
        string sharedContextDefaultsJson,
        Guid? gameId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name is required.", nameof(name));

        return new ToolboxTemplate
        {
            Id = Guid.NewGuid(),
            Name = name,
            GameId = gameId,
            Mode = mode,
            Source = source,
            ToolsJson = toolsJson,
            PhasesJson = phasesJson,
            SharedContextDefaultsJson = sharedContextDefaultsJson,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}
