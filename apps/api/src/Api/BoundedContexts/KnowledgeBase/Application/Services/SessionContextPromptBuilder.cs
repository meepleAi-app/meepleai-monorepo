using System.Globalization;
using System.Text;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Builds system prompt enrichment from a GameSessionContext for session-aware RAG chat.
/// Prepends game name, expansions, current phase, key mechanics, and missing analysis warnings
/// to the system prompt so the LLM has full session context.
/// Also builds live-session prompts that include player names, scores, and dispute history.
/// Issue #5580: Session-aware RAG chat.
/// Task 8b: Session context injection for general chat during live game sessions.
/// </summary>
internal class SessionContextPromptBuilder
{
    /// <summary>
    /// Builds a live-session system prompt for general chat (not Arbitro mode).
    /// Includes game name, player roster, current turn/phase, score summary,
    /// rulebook citation reminder, and any previous dispute verdicts.
    /// Italian-language output to match the product's target locale.
    /// Called from the chat query handler when a GameSessionId is provided alongside live session metadata.
    /// </summary>
    /// <param name="gameName">Name of the active board game.</param>
    /// <param name="playerNames">Ordered list of player names in the session.</param>
    /// <param name="currentTurn">Current turn number (1-based).</param>
    /// <param name="currentPhase">Active game phase, or null if not applicable.</param>
    /// <param name="scores">Map of player name to their current score.</param>
    /// <param name="previousDisputes">Rule dispute verdicts issued earlier in this session.</param>
    /// <returns>Italian system prompt string ready to be prepended to the LLM system prompt.</returns>
    public string BuildSessionPrompt(
        string gameName,
        List<string> playerNames,
        int currentTurn,
        string? currentPhase,
        Dictionary<string, decimal> scores,
        List<RuleDisputeEntry> previousDisputes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
        ArgumentNullException.ThrowIfNull(playerNames);
        ArgumentNullException.ThrowIfNull(scores);
        ArgumentNullException.ThrowIfNull(previousDisputes);

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"Sei l'assistente per \"{gameName}\". Sessione attiva con {playerNames.Count} giocatori: {string.Join(", ", playerNames)}.").AppendLine();
        sb.Append(CultureInfo.InvariantCulture,
            $"Turno corrente: {currentTurn}. Fase: {currentPhase ?? "N/A"}.").AppendLine();
        sb.AppendLine($"Punteggi: {string.Join(", ", scores.Select(s => $"{s.Key} {s.Value}"))}.");
        sb.AppendLine("Quando rispondi su regole, cita SEMPRE la pagina del regolamento.");
        sb.AppendLine("Se c'è ambiguità nella regola, spiega le possibili interpretazioni.");

        if (previousDisputes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Verdetti precedenti in questa sessione:");
            foreach (var d in previousDisputes)
                sb.AppendLine($"- Disputa: \"{d.Description}\" → Verdetto: \"{d.Verdict}\"");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds a session context preamble to prepend to the system prompt.
    /// Returns empty string if context provides no useful information.
    /// </summary>
    public static string BuildSessionPreamble(GameSessionContextDto context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sb = new StringBuilder();
        sb.AppendLine("=== CONTESTO SESSIONE DI GIOCO ===");

        // Game name + expansions
        if (context.PrimaryRules != null)
        {
            sb.AppendLine($"Gioco attivo: {context.PrimaryRules.GameTitle}");
        }

        if (context.ExpansionRules.Count > 0)
        {
            var expansionNames = context.ExpansionRules.Select(r => r.GameTitle);
            sb.AppendLine($"Espansioni attive: {string.Join(", ", expansionNames)}");
        }

        // Current phase
        if (!string.IsNullOrWhiteSpace(context.CurrentPhase))
        {
            sb.AppendLine($"Fase corrente: {context.CurrentPhase}");
        }

        // Key mechanics from primary game
        if (context.PrimaryRules?.KeyMechanics is { Count: > 0 })
        {
            sb.AppendLine($"Meccaniche principali: {string.Join(", ", context.PrimaryRules.KeyMechanics)}");
        }

        // Summary from primary rules
        if (!string.IsNullOrWhiteSpace(context.PrimaryRules?.Summary))
        {
            sb.AppendLine($"Riassunto regole: {context.PrimaryRules.Summary}");
        }

        // Missing analysis warning
        if (context.MissingAnalysisGameNames is { Count: > 0 })
        {
            var names = string.Join(", ", context.MissingAnalysisGameNames);
            sb.AppendLine($"ATTENZIONE: Non hai dati analizzati per: {names}. Se ti chiedono regole di queste espansioni, dichiaralo.");
        }

        sb.AppendLine("=== FINE CONTESTO SESSIONE ===");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Returns a user-friendly message for when the session degradation level is NoAI.
    /// </summary>
    public static string GetNoAiDegradationMessage()
    {
        return "Non sono disponibili documenti analizzati per questa sessione di gioco. " +
               "Per utilizzare l'assistente AI, carica e indicizza i PDF delle regole del gioco dalla sezione Knowledge Base.";
    }
}
