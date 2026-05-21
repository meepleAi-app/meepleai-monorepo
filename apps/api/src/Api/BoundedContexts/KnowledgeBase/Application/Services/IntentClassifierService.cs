using System.Text.RegularExpressions;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Rule-based intent classifier (regex over Italian + English query patterns).
/// No LLM, no DI dependencies — purely lexical. Consumed by D6 retrieval boost.
/// MA0009/MA0023: ExplicitCapture + Compiled + 100ms timeout to prevent ReDoS.
/// </summary>
internal sealed class IntentClassifierService : IIntentClassifierService
{
    private static readonly (Regex Pattern, GameBookRole Role)[] IntentRules = new[]
    {
        (new Regex(@"\b(?:setup|set\s*up|setupa|preparare|come\s+si\s+(?:comincia|inizia)|prima\s+partita|primo\s+turno)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            TimeSpan.FromMilliseconds(100)),
            GameBookRole.Tutorial | GameBookRole.Setup),

        (new Regex(@"§\s*\d+|\bparagrap?h\s*\d+|\bparagrafo\s*\d+|\btraduci\b|\bnext\s*paragraph\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            TimeSpan.FromMilliseconds(100)),
            GameBookRole.Narrative),

        (new Regex(@"\b(?:incontro|scontro|combatti(?:mento)?|encounter)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            TimeSpan.FromMilliseconds(100)),
            GameBookRole.Encounter),

        (new Regex(@"\b(?:qual\s+(?:è|e)\s+la\s+regola|come\s+funziona|posso\s+fare|when\s+can\s+i|rule\s+about|regola\s+(?:del|della))\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            TimeSpan.FromMilliseconds(100)),
            GameBookRole.RulesReference),
    };

    public GameBookRole ClassifyIntent(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return GameBookRole.RulesReference;
        }

        var result = GameBookRole.None;
        foreach (var (pattern, role) in IntentRules)
        {
            if (pattern.IsMatch(query))
            {
                result |= role;
            }
        }

        return result == GameBookRole.None ? GameBookRole.RulesReference : result;
    }
}
