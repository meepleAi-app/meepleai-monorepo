using System.Text.RegularExpressions;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Il rischio più insidioso (spec §6.3): il modello dice di aver fatto un'azione che ha solo proposto, o
/// che non ha nemmeno proposto. <see cref="ClaimsAction"/> riconosce le affermazioni al passato prossimo di
/// registrazione, sempre false perché l'assistente non scrive. <see cref="ClaimsProposal"/> riconosce chi
/// dice che una proposta esiste: è vero solo se il turno ne ha creata una, e lo sa chi chiama.
/// </summary>
internal static partial class ClaimedActionDetector
{
    public static bool ClaimsAction(string text) => !string.IsNullOrWhiteSpace(text) && ClaimPattern().IsMatch(text);

    /// <summary>
    /// «Ho preparato una proposta», «the proposal is ready». Nello spike (#4240) il modello lo ha detto dopo
    /// aver solo letto lo stato: l'host avrebbe cercato una card che non c'era.
    /// </summary>
    public static bool ClaimsProposal(string text) => !string.IsNullOrWhiteSpace(text) && ProposalPattern().IsMatch(text);

    [GeneratedRegex(
        @"\b(ho|abbiamo)\s+(segnato|registrato|aggiunto|tolto|passato|aggiornato)\b|\b(I've|I have|I)\s+(recorded|added|removed|passed|updated|logged)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex ClaimPattern();

    [GeneratedRegex(
        @"(?<!\bnon\s)\b(ho|abbiamo)\s+(appena\s+|già\s+)?(preparato|creato|predisposto|generato|inviato)\s+(\w+\s+){0,2}propost[ae]\b"
        + @"|\bpropost[ae]\s+(è\s+|sono\s+)?pront[ae]\b"
        + @"|\b(I've|I have|I)\s+(just\s+|already\s+)?(prepared|created|drafted|made|submitted|generated)\s+(\w+\s+){0,2}proposals?\b"
        + @"|\bproposals?\s+(is|are)\s+ready\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex ProposalPattern();
}
