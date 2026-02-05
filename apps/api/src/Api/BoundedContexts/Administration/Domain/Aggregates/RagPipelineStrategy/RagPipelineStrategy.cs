using System.Globalization;

namespace Api.BoundedContexts.Administration.Domain.Aggregates.RagPipelineStrategy;

/// <summary>
/// Aggregate root for RAG pipeline strategies.
/// Stores custom pipeline configurations with nodes and edges as JSONB.
/// Issue #3464: Save/load/export for custom strategies.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "MA0049:Type name should not match containing namespace", Justification = "Conventional DDD aggregate naming")]
public sealed class RagPipelineStrategy
{
    private RagPipelineStrategy() { }

    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Display name for the strategy.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Description of the strategy purpose.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Version string (semantic versioning).
    /// </summary>
    public string Version { get; private set; } = "1.0.0";

    /// <summary>
    /// Pipeline nodes as JSON string (stored as JSONB in PostgreSQL).
    /// </summary>
    public string NodesJson { get; private set; } = "[]";

    /// <summary>
    /// Pipeline edges as JSON string (stored as JSONB in PostgreSQL).
    /// </summary>
    public string EdgesJson { get; private set; } = "[]";

    /// <summary>
    /// User who created the strategy.
    /// </summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>
    /// Whether this strategy is active/enabled.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Whether this is a template for quick start.
    /// </summary>
    public bool IsTemplate { get; private set; }

    /// <summary>
    /// Category for templates (e.g., "Basic", "Advanced", "Enterprise").
    /// </summary>
    public string? TemplateCategory { get; private set; }

    /// <summary>
    /// Tags for filtering and discovery (stored as JSONB array).
    /// </summary>
    public string TagsJson { get; private set; } = "[]";

    /// <summary>
    /// Soft delete flag.
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Soft delete timestamp.
    /// </summary>
    public DateTime? DeletedAt { get; private set; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Creates a new RAG pipeline strategy.
    /// </summary>
    public static RagPipelineStrategy Create(
        string name,
        string description,
        string nodesJson,
        string edgesJson,
        Guid createdByUserId,
        bool isTemplate = false,
        string? templateCategory = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodesJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(edgesJson);

        var tagsList = tags?.ToList() ?? [];
        var tagsJsonStr = System.Text.Json.JsonSerializer.Serialize(tagsList);
        var now = DateTime.UtcNow;

        return new RagPipelineStrategy
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description ?? string.Empty,
            Version = "1.0.0",
            NodesJson = nodesJson,
            EdgesJson = edgesJson,
            CreatedByUserId = createdByUserId,
            IsActive = true,
            IsTemplate = isTemplate,
            TemplateCategory = templateCategory,
            TagsJson = tagsJsonStr,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Updates the strategy configuration.
    /// </summary>
    public void Update(
        string name,
        string description,
        string nodesJson,
        string edgesJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodesJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(edgesJson);

        Name = name;
        Description = description ?? string.Empty;
        NodesJson = nodesJson;
        EdgesJson = edgesJson;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    /// <summary>
    /// Updates metadata without changing the pipeline.
    /// </summary>
    public void UpdateMetadata(
        string name,
        string description,
        bool isActive,
        IEnumerable<string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Description = description ?? string.Empty;
        IsActive = isActive;
        if (tags != null)
        {
            TagsJson = System.Text.Json.JsonSerializer.Serialize(tags.ToList());
        }
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the strategy as a template.
    /// </summary>
    public void SetAsTemplate(string category)
    {
        IsTemplate = true;
        TemplateCategory = category;
    }

    /// <summary>
    /// Removes template status.
    /// </summary>
    public void RemoveTemplateStatus()
    {
        IsTemplate = false;
        TemplateCategory = null;
    }

    /// <summary>
    /// Soft deletes the strategy.
    /// </summary>
    public void Delete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        IsActive = false;
    }

    /// <summary>
    /// Restores a soft-deleted strategy.
    /// </summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
    }

    /// <summary>
    /// Increments the version number.
    /// </summary>
    private void IncrementVersion()
    {
        var parts = Version.Split('.');
        if (parts.Length == 3 && int.TryParse(parts[2], CultureInfo.InvariantCulture, out var patch))
        {
            Version = string.Create(CultureInfo.InvariantCulture, $"{parts[0]}.{parts[1]}.{patch + 1}");
        }
    }

    /// <summary>
    /// Gets tags as a list.
    /// </summary>
    public List<string> GetTags() =>
        System.Text.Json.JsonSerializer.Deserialize<List<string>>(TagsJson) ?? [];
}
