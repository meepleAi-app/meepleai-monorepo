using System.Text;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Builds system prompt enrichment from a GameSessionContext for session-aware RAG chat.
/// Prepends game name, expansions, current phase, key mechanics, and missing analysis warnings
/// to the system prompt so the LLM has full session context.
/// Issue #5580: Session-aware RAG chat.
/// </summary>
internal static class SessionContextPromptBuilder
{
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
