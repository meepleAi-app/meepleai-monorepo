namespace Api.BoundedContexts.KnowledgeBase.Domain.Interfaces;

/// <summary>
/// Interface for agent mode-specific behavior handlers.
/// Each agent mode (Chat, Player, Ledger) implements this interface.
/// Issue #2405: Ledger Mode - Full State Tracking.
/// </summary>
internal interface IAgentModeHandler
{
    /// <summary>
    /// Gets the agent mode this handler supports.
    /// </summary>
    ValueObjects.AgentMode SupportedMode { get; }

    /// <summary>
    /// Processes a user message according to the mode's behavior.
    /// </summary>
    /// <param name="message">The user message text.</param>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="sessionId">The game session ID (if applicable).</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processing result with response and any state changes.</returns>
    Task<AgentModeResult> ProcessMessageAsync(
        string message,
        Guid agentId,
        Guid? sessionId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if the handler can process the given context.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="sessionId">The game session ID (if applicable).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if handler can process, false otherwise.</returns>
    Task<bool> CanProcessAsync(
        Guid agentId,
        Guid? sessionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of agent mode message processing.
/// </summary>
internal sealed record AgentModeResult
{
    /// <summary>
    /// The response message from the agent.
    /// </summary>
    public string Response { get; init; } = string.Empty;

    /// <summary>
    /// Whether processing was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// State changes detected (for Ledger Mode).
    /// </summary>
    public List<StateChangeInfo>? StateChanges { get; init; }

    /// <summary>
    /// Whether state changes require user confirmation.
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    public static AgentModeResult Success

WithResponse(string response)
    {
        return new AgentModeResult
        {
            Success = true,
            Response = response
        };
    }

    public static AgentModeResult SuccessWithStateChanges(
        string response,
        List<StateChangeInfo> stateChanges,
        bool requiresConfirmation = true)
    {
        return new AgentModeResult
        {
            Success = true,
            Response = response,
            StateChanges = stateChanges,
            RequiresConfirmation = requiresConfirmation
        };
    }

    public static AgentModeResult Failure(string errorMessage)
    {
        return new AgentModeResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Information about a detected state change.
/// </summary>
internal sealed record StateChangeInfo
{
    public string? PlayerName { get; init; }
    public string ChangeType { get; init; } = string.Empty;
    public string FieldName { get; init; } = string.Empty;
    public string? OldValue { get; init; }
    public string NewValue { get; init; } = string.Empty;
    public double ConfidenceScore { get; init; } = 1.0;
}
