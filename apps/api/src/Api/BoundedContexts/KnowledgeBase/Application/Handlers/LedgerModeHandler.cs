using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Interfaces;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for Ledger Mode agent behavior.
/// Maintains complete game state tracking by listening to chat messages and extracting state changes.
/// Issue #2405: Ledger Mode - Full State Tracking.
/// </summary>
internal sealed class LedgerModeHandler : IAgentModeHandler
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly StateParsingService _parsingService;
    private readonly ILogger<LedgerModeHandler> _logger;

    public AgentMode SupportedMode => AgentMode.Ledger;

    public LedgerModeHandler(
        MeepleAiDbContext dbContext,
        StateParsingService parsingService,
        ILogger<LedgerModeHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _parsingService = parsingService ?? throw new ArgumentNullException(nameof(parsingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentModeResult> ProcessMessageAsync(
        string message,
        Guid agentId,
        Guid? sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return AgentModeResult.Failure("Message cannot be empty");

        if (!sessionId.HasValue)
            return AgentModeResult.Failure("Ledger Mode requires an active game session");

        _logger.LogInformation(
            "Processing Ledger Mode message for agent {AgentId}, session {SessionId}",
            agentId,
            sessionId.Value);

        // Parse message for state changes
        var stateChanges = _parsingService.ParseMessage(message);

        if (stateChanges.Count == 0)
        {
            // No state changes detected, just acknowledge
            return AgentModeResult.SuccessWithResponse(
                "📊 Messaggio registrato. Nessuna modifica di stato rilevata.");
        }

        // Get or create session state
        var sessionState = await GetOrCreateSessionStateAsync(
            sessionId.Value,
            userId,
            cancellationToken).ConfigureAwait(false);

        if (sessionState == null)
        {
            return AgentModeResult.Failure("Failed to load or create session state");
        }

        // Load current state schema
        var ledgerState = LedgerStateSchema.FromJsonDocument(sessionState.CurrentState);

        // Build confirmation message
        var response = BuildConfirmationMessage(stateChanges, ledgerState);

        return AgentModeResult.SuccessWithStateChanges(
            response,
            stateChanges,
            requiresConfirmation: true);
    }

    public async Task<bool> CanProcessAsync(
        Guid agentId,
        Guid? sessionId,
        CancellationToken cancellationToken = default)
    {
        // Ledger Mode requires an active session
        if (!sessionId.HasValue)
            return false;

        // Verify session exists
        var sessionExists = await _dbContext.GameSessions
            .AnyAsync(s => s.Id == sessionId.Value, cancellationToken)
            .ConfigureAwait(false);

        return sessionExists;
    }

    private async Task<GameSessionState?> GetOrCreateSessionStateAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Try to find existing state
        var state = await _dbContext.GameSessionStates
            .FirstOrDefaultAsync(s => s.GameSessionId == sessionId, cancellationToken)
            .ConfigureAwait(false);

        if (state != null)
            return state;

        // Create new state if not exists
        var session = await _dbContext.GameSessions
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session == null)
            return null;

        // Initialize ledger state
        var playerNames = session.Players.Select(p => p.PlayerName).ToList();
        var initialState = LedgerStateSchema.CreateEmpty(playerNames);

        state = GameSessionState.Create(
            id: Guid.NewGuid(),
            gameSessionId: sessionId,
            templateId: Guid.Empty, // No template for ledger mode
            initialState: initialState.ToJsonDocument(),
            createdBy: userId.ToString());

        _dbContext.GameSessionStates.Add(state);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created new session state {StateId} for session {SessionId}",
            state.Id,
            sessionId);

        return state;
    }

    private static string BuildConfirmationMessage(
        List<StateChangeInfo> changes,
        LedgerStateSchema currentState)
    {
        var message = "📊 **Stato aggiornato:**\n";

        foreach (var change in changes)
        {
            var playerPrefix = !string.IsNullOrWhiteSpace(change.PlayerName)
                ? $"{change.PlayerName}: "
                : "";

            var icon = change.ChangeType switch
            {
                "score" => "🏆",
                "resource" => "🏗️",
                "turn" => "🎲",
                "phase" => "📋",
                _ => "✏️"
            };

            var oldValueDisplay = change.OldValue != null ? $"{change.OldValue} → " : "";

            message += $"- {icon} {playerPrefix}{change.FieldName}: {oldValueDisplay}{change.NewValue}\n";
        }

        message += "\nVuoi confermare questa modifica?";

        return message;
    }
}
