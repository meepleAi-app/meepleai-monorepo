using System.Text.RegularExpressions;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Il rischio più insidioso (spec §6.3): il modello dice di aver fatto un'azione che ha solo proposto, o
/// che non ha nemmeno proposto. <see cref="ClaimsAction"/> riconosce le affermazioni al passato prossimo di
/// registrazione, sempre false perché l'assistente non scrive. <see cref="ClaimsProposal"/> riconosce chi
/// dice che una proposta esiste: è vero solo se il turno ne ha creata una, e lo sa chi chiama.
/// </summary>
/// <remarks>
/// È una rete a maglie larghe, non una garanzia: riconosce le forme viste, non ogni parafrasi. La garanzia
/// è strutturale (nessuna scrittura senza conferma). Una negazione («non ho registrato nulla») è il rifiuto
/// corretto di una trappola e non deve scattare: per questo i due schemi hanno la stessa guardia.
/// </remarks>
internal static partial class ClaimedActionDetector
{
    // «non ho», «non ti ho», «non le abbiamo»: la negazione, con al più un pronome in mezzo.
    private const string NotNegatedIt = @"(?<!\bnon\s+(\w+\s+)?)";
    private const string SubjectIt = @"\b(ho|abbiamo)\s+(appena\s+|già\s+)?";

    // L'apostrofo può essere tipografico. «I have not» e «I haven't» restano fuori da soli: fra il soggetto e
    // il participio sono ammessi solo «just» e «already».
    private const string SubjectEn = @"\b(I['’]ve|I\s+have|I)\s+(just\s+|already\s+)?";

    public static bool ClaimsAction(string text) => !string.IsNullOrWhiteSpace(text) && ClaimPattern().IsMatch(text);

    /// <summary>
    /// «Ho preparato una proposta», «proposta creata», «conferma la proposta». Nello spike (#4240) il modello
    /// lo ha detto dopo aver solo letto lo stato: l'host avrebbe cercato una card che non c'era.
    /// </summary>
    public static bool ClaimsProposal(string text) => !string.IsNullOrWhiteSpace(text) && ProposalPattern().IsMatch(text);

    [GeneratedRegex(
        NotNegatedIt + SubjectIt + @"(segnato|registrato|aggiunto|tolto|passato|aggiornato)\b"
        + "|" + SubjectEn + @"(recorded|added|removed|passed|updated|logged)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex ClaimPattern();

    [GeneratedRegex(
        // «ho preparato una proposta», «abbiamo creato le due proposte»
        NotNegatedIt + SubjectIt + @"(preparato|creato|predisposto|generato|inviato)\s+(\w+\s+){0,2}propost[ae]\b"
        // «ho proposto +5 a Marco»
        + "|" + NotNegatedIt + SubjectIt + @"proposto\b"
        // «proposta creata», «la proposta è pronta»; non «nessuna proposta è pronta»
        + "|" + @"(?<!\b(nessuna|alcuna)\s+)\bpropost[ae]\s+(è\s+|sono\s+)?(pront[ae]|creat[ae]|preparat[ae])\b"
        // «conferma la proposta», «confermate le proposte»
        + "|" + @"\bconferm(a|i|ate)\s+(la|le|questa|queste)\s+propost[ae]\b"
        + "|" + SubjectEn + @"(prepared|created|drafted|made|submitted|generated)\s+(\w+\s+){0,2}proposals?\b"
        + "|" + SubjectEn + @"proposed\b"
        // «the proposal is ready», «proposal created»; non «no proposal is ready»
        + "|" + @"(?<!\bno\s+)\bproposals?\s+((is|are)\s+)?(ready|created|prepared)\b"
        + "|" + @"\bconfirm\s+(the|this|these|both)\s+proposals?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex ProposalPattern();
}
