using System.Text.Json;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Runtime state of the Toolkit widgets during a specific game session.
/// Stores per-widget JSON state (turn count, scores, resources, etc.).
/// One record per (SessionId, ToolkitId) pair.
/// Issue #5148 — Epic B5.
/// </summary>
public sealed class ToolkitSessionState
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Backing field for JSONB storage
    private string _widgetStatesJson = "{}";

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid ToolkitId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Per-widget runtime state. Key = WidgetType name (e.g. "TurnManager"), Value = JSON blob.
    /// </summary>
    public IReadOnlyDictionary<string, string> WidgetStates =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(_widgetStatesJson, JsonOptions)
        ?? new Dictionary<string, string>(StringComparer.Ordinal);

    private ToolkitSessionState() { } // EF Core

    /// <summary>
    /// Creates a new ToolkitSessionState for a session.
    /// </summary>
    public static ToolkitSessionState Create(Guid sessionId, Guid toolkitId)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));

        var now = DateTime.UtcNow;
        return new ToolkitSessionState
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ToolkitId = toolkitId,
            _widgetStatesJson = "{}",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Updates the state JSON for a single widget. Creates entry if it doesn't exist.
    /// </summary>
    /// <summary>Maximum size in characters for a single widget state blob (500 KB).</summary>
    private const int MaxWidgetStateLength = 512_000;

    public void UpdateWidgetState(string widgetType, string stateJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(widgetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateJson);

        if (stateJson.Length > MaxWidgetStateLength)
            throw new ArgumentException(
                $"Widget state exceeds maximum size (500KB). Current: {stateJson.Length / 1024}KB",
                nameof(stateJson));

        var states = JsonSerializer.Deserialize<Dictionary<string, string>>(_widgetStatesJson, JsonOptions)
                     ?? new Dictionary<string, string>(StringComparer.Ordinal);

        states[widgetType] = stateJson;
        _widgetStatesJson = JsonSerializer.Serialize(states, JsonOptions);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Removes state for a widget (reset).
    /// </summary>
    public void ClearWidgetState(string widgetType)
    {
        var states = JsonSerializer.Deserialize<Dictionary<string, string>>(_widgetStatesJson, JsonOptions)
                     ?? new Dictionary<string, string>(StringComparer.Ordinal);

        states.Remove(widgetType);
        _widgetStatesJson = JsonSerializer.Serialize(states, JsonOptions);
        UpdatedAt = DateTime.UtcNow;
    }
}
