using System.Text.RegularExpressions;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Il rischio più insidioso (spec §6.3): il modello dice di aver fatto un'azione che ha solo proposto, o
/// che non ha nemmeno proposto. Riconosce le affermazioni al passato prossimo di registrazione.
/// </summary>
internal static partial class ClaimedActionDetector
{
    public static bool ClaimsAction(string text) => !string.IsNullOrWhiteSpace(text) && ClaimPattern().IsMatch(text);

    [GeneratedRegex(
        @"\b(ho|abbiamo)\s+(segnato|registrato|aggiunto|tolto|passato|aggiornato)\b|\b(I've|I have|I)\s+(recorded|added|removed|passed|updated|logged)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex ClaimPattern();
}
