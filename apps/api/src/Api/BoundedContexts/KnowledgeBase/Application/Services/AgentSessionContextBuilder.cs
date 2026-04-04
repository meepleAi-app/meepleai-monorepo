using System.Globalization;
using System.Text;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Builds a context string describing the current live session state
/// to be injected into the agent system prompt.
/// </summary>
internal static class AgentSessionContextBuilder
{
    private static readonly string NL = Environment.NewLine;

    /// <summary>
    /// Builds a concise context block for the current session state.
    /// Returns empty string if context is null or has no meaningful data.
    /// </summary>
    public static string Build(LiveSessionContext? context)
    {
        if (context == null)
            return string.Empty;

        var sb = new StringBuilder();
        sb.Append("## Current Game Session").Append(NL);
        sb.Append(string.Format(CultureInfo.InvariantCulture, "- Game: {0}", context.GameName)).Append(NL);
        sb.Append(string.Format(CultureInfo.InvariantCulture, "- Turn: {0}", context.CurrentTurnIndex)).Append(NL);

        if (context.PhaseNames.Length > 0)
        {
            var phaseName = context.CurrentPhaseName ?? string.Format(CultureInfo.InvariantCulture, "Phase {0}", context.CurrentPhaseIndex + 1);
            sb.Append(string.Format(CultureInfo.InvariantCulture, "- Current Phase: {0} ({1}/{2})", phaseName, context.CurrentPhaseIndex + 1, context.PhaseNames.Length)).Append(NL);
        }

        if (!string.IsNullOrWhiteSpace(context.CurrentPlayerDisplayName))
            sb.Append(string.Format(CultureInfo.InvariantCulture, "- Active Player: {0}", context.CurrentPlayerDisplayName)).Append(NL);

        if (context.ActivePlayerNames.Count > 0)
            sb.Append(string.Format(CultureInfo.InvariantCulture, "- Players: {0}", string.Join(", ", context.ActivePlayerNames))).Append(NL);

        sb.Append(string.Format(CultureInfo.InvariantCulture, "- Turn Advance: {0}", context.TurnAdvancePolicy)).Append(NL);

        return sb.ToString();
    }
}
