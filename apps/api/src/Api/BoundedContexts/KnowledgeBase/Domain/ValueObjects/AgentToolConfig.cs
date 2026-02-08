namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing a tool configuration for agent definitions.
/// Issue #3808: Stores tool name and settings dictionary.
/// </summary>
public sealed record AgentToolConfig
{
    /// <summary>
    /// Gets the tool name (e.g., "web_search", "calculator", "code_interpreter").
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the tool-specific configuration settings as JSON string.
    /// </summary>
    /// <remarks>
    /// Stored as JSON to support EF Core serialization.
    /// Use Create() to initialize from dictionary.
    /// </remarks>
    public string SettingsJson { get; init; }

    /// <summary>
    /// Parameterless constructor for EF Core deserialization.
    /// </summary>
#pragma warning disable CS8618
    private AgentToolConfig()
#pragma warning restore CS8618
    {
    }

    private AgentToolConfig(string name, string settingsJson)
    {
        Name = name;
        SettingsJson = settingsJson;
    }

    /// <summary>
    /// Creates a new tool configuration with validation.
    /// </summary>
    public static AgentToolConfig Create(string name, IDictionary<string, object>? settings = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tool name cannot be empty", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Tool name cannot exceed 100 characters", nameof(name));

        var settingsDict = settings ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var settingsJson = System.Text.Json.JsonSerializer.Serialize(settingsDict);

        return new AgentToolConfig(name.Trim(), settingsJson);
    }

    /// <summary>
    /// Gets the settings as a dictionary (deserialized from JSON).
    /// </summary>
    public IDictionary<string, object> GetSettings()
    {
        if (string.IsNullOrWhiteSpace(SettingsJson))
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(SettingsJson)
            ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }
}
