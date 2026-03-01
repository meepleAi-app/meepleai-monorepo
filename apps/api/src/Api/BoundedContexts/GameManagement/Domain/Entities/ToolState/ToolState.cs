using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.ToolState;

/// <summary>
/// Represents the persistent runtime state of a tool in a live game session.
/// Each ToolState corresponds to one tool configuration from the session's toolkit.
/// Issue #4754: ToolState entity for Toolkit ↔ Session integration.
/// </summary>
#pragma warning disable MA0049 // Type name matches containing namespace - intentional DDD folder structure
internal sealed class ToolState : Entity<Guid>
#pragma warning restore MA0049
{
    public Guid SessionId { get; private set; }
    public Guid ToolkitId { get; private set; }
    public string ToolName { get; private set; }
    public ToolType ToolType { get; private set; }
    public string StateDataJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastUpdatedAt { get; private set; }

    // Private constructor for EF Core
#pragma warning disable CS8618
    private ToolState() : base() { }
#pragma warning restore CS8618

    /// <summary>
    /// Creates a new ToolState for a session tool.
    /// </summary>
    public ToolState(
        Guid id,
        Guid sessionId,
        Guid toolkitId,
        string toolName,
        ToolType toolType,
        string initialStateJson) : base(id)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("SessionId cannot be empty", nameof(sessionId));
        if (toolkitId == Guid.Empty)
            throw new ArgumentException("ToolkitId cannot be empty", nameof(toolkitId));
        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException("ToolName cannot be empty", nameof(toolName));

        SessionId = sessionId;
        ToolkitId = toolkitId;
        ToolName = toolName.Trim();
        ToolType = toolType;
        StateDataJson = initialStateJson ?? "{}";
        CreatedAt = DateTime.UtcNow;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the state data JSON.
    /// </summary>
    public void UpdateState(string stateDataJson)
    {
        ArgumentNullException.ThrowIfNull(stateDataJson);
        StateDataJson = stateDataJson;
        LastUpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Types of tools that can have persistent state in a session.
/// </summary>
internal enum ToolType
{
    Dice = 0,
    Counter = 1,
    Card = 2,
    Timer = 3
}
