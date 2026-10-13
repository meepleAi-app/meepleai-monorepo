using System.Text;
using Microsoft.Extensions.AI;

#pragma warning disable MA0048 // File name must match type name - conversation builder and history turn
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>Un turno passato della conversazione: solo il testo (spec §6.2).</summary>
internal sealed record AssistantHistoryTurn(string Role, string Text);

/// <summary>
/// I messaggi di un turno, dal più stabile al più volatile (spec §4.1): prompt di sistema, contesto
/// statico della sessione, storia, domanda. Punteggi, turno e fase non entrano mai qui: arrivano solo
/// dai risultati di stato_partita, così l'inizio del prompt resta uguale fra i giri e i turni.
/// </summary>
internal static class AssistantConversation
{
    public static List<ChatMessage> Build(
        AssistantPromptSet prompts, string language, AssistantSessionState state, IReadOnlyList<AssistantHistoryTurn> history, string question)
    {
        ArgumentNullException.ThrowIfNull(prompts);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, prompts.For(language)),
            new(ChatRole.System, StaticContext(state, AssistantLanguage.IsEnglish(language))),
        };
        messages.AddRange(history.Select(h => new ChatMessage(
            string.Equals(h.Role, "assistant", StringComparison.Ordinal) ? ChatRole.Assistant : ChatRole.User, h.Text)));
        messages.Add(new ChatMessage(ChatRole.User, question));
        return messages;
    }

    /// <summary>Nella lingua della sessione, come il prompt: in una sessione inglese era in italiano (#4256).</summary>
    private static string StaticContext(AssistantSessionState state, bool english)
    {
        var text = new StringBuilder();
        text.AppendLine(english
            ? $"Game: {state.GameName ?? "(no game linked)"}"
            : $"Gioco: {state.GameName ?? "(nessun gioco collegato)"}");
        text.AppendLine($"{(english ? "Players" : "Giocatori")}: {string.Join(", ", state.Players.Select(p => p.Name))}");
        text.AppendLine($"{(english ? "Scoring dimensions" : "Dimensioni del punteggio")}: {string.Join(", ", state.Dimensions)}");
        if (state.HouseRules.Count > 0)
        {
            text.AppendLine(english ? "House rules (they override the rulebook):" : "House rules (valgono sul regolamento):");
            foreach (var rule in state.HouseRules)
            {
                text.AppendLine($"- {rule}");
            }
        }

        return text.ToString().TrimEnd();
    }
}
