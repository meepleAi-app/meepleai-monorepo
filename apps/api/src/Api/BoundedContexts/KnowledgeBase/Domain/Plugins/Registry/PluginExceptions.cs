// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3417 - Plugin Registry Service
// =============================================================================

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Registry;

/// <summary>
/// Exception thrown when a requested plugin is not found in the registry.
/// </summary>
public sealed class PluginNotFoundException : Exception
{
    /// <summary>
    /// The ID of the plugin that was not found.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// The requested version, if any.
    /// </summary>
    public string? RequestedVersion { get; }

    /// <summary>
    /// Creates a new PluginNotFoundException.
    /// </summary>
    public PluginNotFoundException(string pluginId, string? version = null)
        : base(CreateMessage(pluginId, version))
    {
        PluginId = pluginId;
        RequestedVersion = version;
    }

    private static string CreateMessage(string pluginId, string? version)
    {
        return version != null
            ? $"Plugin '{pluginId}' version '{version}' was not found in the registry"
            : $"Plugin '{pluginId}' was not found in the registry";
    }
}

/// <summary>
/// Exception thrown when attempting to load a disabled plugin.
/// </summary>
public sealed class PluginDisabledException : Exception
{
    /// <summary>
    /// The ID of the disabled plugin.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Creates a new PluginDisabledException.
    /// </summary>
    public PluginDisabledException(string pluginId)
        : base($"Plugin '{pluginId}' is disabled and cannot be loaded")
    {
        PluginId = pluginId;
    }
}

/// <summary>
/// Exception thrown when plugin registration fails.
/// </summary>
public sealed class PluginRegistrationException : Exception
{
    /// <summary>
    /// The ID of the plugin that failed to register.
    /// </summary>
    public string? PluginId { get; }

    /// <summary>
    /// The type that failed to register.
    /// </summary>
    public Type? PluginType { get; }

    /// <summary>
    /// Creates a new PluginRegistrationException.
    /// </summary>
    public PluginRegistrationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new PluginRegistrationException with plugin ID.
    /// </summary>
    public PluginRegistrationException(string pluginId, string message)
        : base($"Failed to register plugin '{pluginId}': {message}")
    {
        PluginId = pluginId;
    }

    /// <summary>
    /// Creates a new PluginRegistrationException with type.
    /// </summary>
    public PluginRegistrationException(Type pluginType, string message)
        : base($"Failed to register plugin type '{pluginType.FullName}': {message}")
    {
        PluginType = pluginType;
    }

    /// <summary>
    /// Creates a new PluginRegistrationException with inner exception.
    /// </summary>
    public PluginRegistrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
