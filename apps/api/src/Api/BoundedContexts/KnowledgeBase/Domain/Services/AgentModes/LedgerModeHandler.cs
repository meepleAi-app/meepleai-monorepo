#pragma warning disable MA0002 // Dictionary without StringComparer
#pragma warning disable MA0011 // Use IFormatProvider
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.AgentModes;

/// <summary>
/// Ledger Mode handler that maintains complete game state tracking.
/// Parses natural language messages to extract and track state changes automatically.
/// Issue #2405 - Ledger Mode full state tracking
/// </summary>
internal sealed class LedgerModeHandler : IAgentModeHandler
{
    private readonly IGameSessionStateRepository _sessionStateRepository;
    private readonly IStateParser _stateParser;
    private readonly ILogger<LedgerModeHandler> _logger;

    public LedgerModeHandler(
        IGameSessionStateRepository sessionStateRepository,
        IStateParser stateParser,
        ILogger<LedgerModeHandler> logger)
    {
        _sessionStateRepository = sessionStateRepository;
        _stateParser = stateParser;
        _logger = logger;
    }

    public AgentMode SupportedMode => AgentMode.Ledger;

    public async Task<AgentModeResult> HandleAsync(
        AgentModeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Validate Ledger mode requirements
        if (context.Configuration.SelectedDocumentIds.Count == 0)
        {
            throw new InvalidOperationException(
                "Ledger mode requires at least one rulebook document selected");
        }

        _logger.LogInformation(
            "LedgerModeHandler invoked for Agent {AgentId} with message: {Query}",
            context.Agent.Id,
            context.Query);

        // Get current game state if GameId provided
        JsonDocument? currentState = null;
        if (context.GameId.HasValue)
        {
            var sessionState = await _sessionStateRepository
                .GetBySessionIdAsync(context.GameId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (sessionState != null)
            {
                currentState = sessionState.CurrentState;
                _logger.LogDebug(
                    "Retrieved current state for session {SessionId}",
                    context.GameId.Value);
            }
        }

        // Parse message to extract state changes
        var extraction = await _stateParser.ParseAsync(
            context.Query,
            currentState,
            cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug(
            "State extraction completed: Type={ChangeType}, Confidence={Confidence}, Changes={ChangeCount}",
            extraction.ChangeType,
            extraction.Confidence,
            extraction.ExtractedState.Count);

        // Detect conflicts if current state exists
        var conflicts = new List<StateConflict>();
        if (currentState != null && extraction.HasStateChanges)
        {
            conflicts = (await _stateParser.DetectConflictsAsync(
                extraction,
                currentState,
                cancellationToken)
                .ConfigureAwait(false))
                .ToList();

            _logger.LogDebug("Detected {ConflictCount} conflicts", conflicts.Count);
        }

        // Calculate confidence from extraction and search results
        var confidence = CalculateOverallConfidence(extraction, context.SearchResults);

        // Format response content
        var content = FormatLedgerResponse(extraction, conflicts);

        // Build metadata
        var metadata = new Dictionary<string, object>
        {
            { "changeType", extraction.ChangeType.ToString() },
            { "hasStateChanges", extraction.HasStateChanges },
            { "extractionConfidence", extraction.Confidence },
            { "requiresConfirmation", extraction.RequiresConfirmation },
            { "conflictCount", conflicts.Count },
            { "warningCount", extraction.Warnings.Count }
        };

        // Include extracted state in metadata for confirmation workflow
        if (extraction.HasStateChanges)
        {
            metadata["extractedState"] = extraction.ExtractedState;
            metadata["playerName"] = extraction.PlayerName ?? string.Empty;
        }

        // Include conflicts in metadata if any
        if (conflicts.Count > 0)
        {
            metadata["conflicts"] = conflicts
                .Select(c => new
                {
                    c.PropertyName,
                    c.PlayerName,
                    c.ExistingValue,
                    c.NewValue,
                    Severity = c.Severity.ToString(),
                    Resolution = c.SuggestedResolution.ToString()
                })
                .ToList();
        }

        return new AgentModeResult
        {
            Mode = AgentMode.Ledger,
            Content = content,
            Confidence = confidence,
            Metadata = metadata
        };
    }

    /// <summary>
    /// Calculates overall confidence from extraction and search context
    /// </summary>
    private static double CalculateOverallConfidence(
        StateExtractionResult extraction,
        IReadOnlyList<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult> searchResults)
    {
        // Base confidence from extraction
        var extractionConfidence = extraction.Confidence;

        // Average confidence from search results (rules context)
        var searchConfidence = searchResults.Any()
            ? searchResults.Average(r => r.RelevanceScore.Value)
            : 0.5;

        // Weighted average: extraction 70%, search 30%
        return (extractionConfidence * 0.7) + (searchConfidence * 0.3);
    }

    /// <summary>
    /// Formats the ledger response for chat display
    /// </summary>
    private static string FormatLedgerResponse(
        StateExtractionResult extraction,
        List<StateConflict> conflicts)
    {
        // No state changes detected
        if (!extraction.HasStateChanges)
        {
            return "📋 **Nessuna modifica di stato rilevata**\n\nIl messaggio non contiene cambiamenti di stato tracciabili.";
        }

        // Build state change summary
        var stateChanges = string.Join("\n", extraction.ExtractedState
            .Select(kvp => $"- **{FormatPropertyName(kvp.Key)}**: {FormatValue(kvp.Value)}"));

        var response = $"""
            📊 **Stato aggiornato rilevato:**
            {(extraction.PlayerName != null ? $"🎮 Giocatore: **{extraction.PlayerName}**\n" : "")}
            {stateChanges}
            """;

        // Add confidence indicator
        var confidenceEmoji = extraction.Confidence switch
        {
            >= 0.8f => "✅",
            >= 0.6f => "⚠️",
            _ => "❓"
        };

        response += $"\n\n{confidenceEmoji} Confidenza: {extraction.Confidence:P0}";

        // Add warnings if any
        if (extraction.Warnings.Count > 0)
        {
            response += "\n\n⚠️ **Avvisi:**\n" +
                string.Join("\n", extraction.Warnings.Select(w => $"- {w}"));
        }

        // Add conflicts if any
        if (conflicts.Count > 0)
        {
            response += "\n\n" + FormatConflicts(conflicts);
        }
        else if (extraction.RequiresConfirmation)
        {
            // No conflicts, but still requires confirmation
            response += "\n\n🔄 **Vuoi confermare questa modifica?**";
        }

        return response;
    }

    /// <summary>
    /// Formats conflicts for display
    /// </summary>
    private static string FormatConflicts(List<StateConflict> conflicts)
    {
        var formattedConflicts = conflicts
            .Select(c => c.FormatForDisplay())
            .ToList();

        return string.Join("\n\n", formattedConflicts);
    }

    /// <summary>
    /// Formats property name for display (converts camelCase to readable format)
    /// </summary>
    private static string FormatPropertyName(string propertyName)
    {
        return propertyName switch
        {
            "score" => "Punti",
            "roads" => "Strade",
            "cities" => "Città",
            "settlements" => "Insediamenti",
            "resources" => "Risorse",
            "cards" => "Carte",
            "turn" => "Turno",
            "phase" => "Fase",
            _ => propertyName
        };
    }

    /// <summary>
    /// Formats value for display
    /// </summary>
    private static string FormatValue(object value)
    {
        return value switch
        {
            int i => i.ToString(),
            double d => d.ToString("F2"),
            string s => s,
            bool b => b ? "Sì" : "No",
            _ => value.ToString() ?? "sconosciuto"
        };
    }
}
