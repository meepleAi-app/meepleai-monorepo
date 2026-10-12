using System.Text;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4168 (ADR-095 D3, passo 5): il prompt di una risposta, dalla versione pubblicata del profilo.
/// Il prompt di sistema è quello della lingua di chi chiede, o quello della lingua di ripiego; lo
/// stile delle citazioni e la lingua della risposta sono istruzioni a parte, così che ogni campo del
/// profilo abbia un effetto che si vede.
/// </summary>
internal static class AnswerPromptBuilder
{
    /// <summary>Autorizza il blocco delle regole verificate come contesto valido (R1, ADR-088).</summary>
    internal const string VerifiedRulesInstruction =
        "A section titled '[Verified Rules — human-approved]' may precede the Context; it is human-approved "
        + "rulebook content and counts as provided context — you MAY answer from it and cite its [Page N].";

    /// <summary>
    /// #3855: autorizza la regola del gruppo e ne vieta la parafrasi. Senza, il modello la riassumeva
    /// cambiandone il senso, e le attaccava una pagina inventata del regolamento.
    /// </summary>
    internal const string HouseRuleInstruction =
        "A section titled '[House Rule for this group]' may precede the Context; it is a rule this group agreed on "
        + "and counts as provided context. Reproduce its wording exactly — do not paraphrase, summarise, or reword it. "
        + "It comes from the group's own notes, NOT from the rulebook: never attach a [Page N] citation to it, and say "
        + "explicitly that it is the group's house rule.";

    /// <summary>L'istruzione che corrisponde allo stile delle citazioni del profilo.</summary>
    internal static string CitationInstruction(CitationStyle style) => style switch
    {
        CitationStyle.PageReferencesInText =>
            "Cite the page of every rule you use in brackets, for example [Page 3].",
        CitationStyle.StructuredEvents =>
            "Do not cite pages in the text: the sources are attached to the answer separately.",
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown citation style."),
    };

    /// <summary>La lingua della risposta: quella di chi chiede, anche quando il prompt è di ripiego.</summary>
    internal static string AnswerLanguageInstruction(string language) =>
        $"Write the answer in {LanguageName(language)}.";

    /// <summary>La lunghezza e la forma chieste da chi usa la chat; non toccano le citazioni.</summary>
    internal static string ResponseStyleInstruction(string? responseStyle) => responseStyle switch
    {
        "detailed" =>
            "Answer thoroughly: explain every relevant aspect with examples from the rulebook, using subsections if needed.",
        "continuation" =>
            "Continue the previous answer from where it stopped. Do not repeat what was already said.",
        _ =>
            "Answer concisely and in a structured way, in at most 200 words. Focus on the question instead of "
            + "rewriting the rulebook, and use bullet points.",
    };

    /// <summary>
    /// Compone il prompt. Il contesto arriva già formattato dal chiamante, con le regole verificate
    /// davanti se ci sono.
    /// </summary>
    /// <param name="profile">Il contenuto della versione pubblicata.</param>
    /// <param name="answerLanguage">La lingua di chi chiede (codice ISO 639-1).</param>
    /// <param name="context">I passaggi recuperati, «[Page N] testo», separati da una riga vuota.</param>
    /// <param name="question">La domanda.</param>
    /// <param name="responseStyle">«concise», «detailed» o «continuation»; <c>null</c> per nessuna istruzione di stile.</param>
    /// <param name="continuationContext">La risposta parziale da continuare, se c'è.</param>
    /// <param name="hasVerifiedRules">Se il contesto comincia con le regole verificate.</param>
    /// <param name="houseRule">La regola del gruppo che corrisponde alla domanda, se c'è.</param>
    internal static AnswerPrompt Build(
        AgentProfileContent profile,
        string answerLanguage,
        string context,
        string question,
        string? responseStyle,
        string? continuationContext,
        bool hasVerifiedRules,
        string? houseRule = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var promptLanguage = profile.SystemPrompts.ContainsKey(answerLanguage)
            ? answerLanguage
            : profile.FallbackLanguage;

        var system = new StringBuilder(profile.SystemPrompts[promptLanguage].Trim())
            .Append("\n\n")
            .Append(CitationInstruction(profile.CitationStyle));
        if (responseStyle is not null)
        {
            system.Append('\n').Append(ResponseStyleInstruction(responseStyle));
        }

        if (hasVerifiedRules)
        {
            system.Append('\n').Append(VerifiedRulesInstruction);
        }

        if (!string.IsNullOrWhiteSpace(houseRule))
        {
            system.Append('\n').Append(HouseRuleInstruction);
        }

        system.Append('\n').Append(AnswerLanguageInstruction(answerLanguage));

        var houseRuleBlock = string.IsNullOrWhiteSpace(houseRule)
            ? string.Empty
            : $"[House Rule for this group]\n{houseRule}\n\n";
        var user = $"{houseRuleBlock}CONTEXT FROM RULEBOOK:\n{context}\n\nQUESTION:\n{question}\n\nANSWER:";
        if (!string.IsNullOrWhiteSpace(continuationContext))
        {
            user = $"PREVIOUS PARTIAL ANSWER (continue from here):\n{continuationContext}\n\n{user}";
        }

        return new AnswerPrompt(system.ToString(), user, promptLanguage);
    }

    /// <summary>
    /// #4168: le fonti di un'altra persona, nella libreria, sono regolamenti coperti da copyright.
    /// La ricerca cross-game le tratta tutte come protette: si riformula, non si copia.
    /// </summary>
    internal const string ProtectedSourcesInstruction =
        "The sources marked PROTECTED are copyrighted rulebooks: paraphrase them in your own words and never copy "
        + "their text verbatim.";

    /// <summary>
    /// Lo stile delle citazioni quando le fonti sono numerate, come nella ricerca fra più giochi: un
    /// numero di pagina da solo non dice a quale manuale appartiene, il marcatore [N] sì, e la pagina
    /// della libreria lo trasforma in un collegamento al passaggio.
    /// </summary>
    internal static string NumberedCitationInstruction(CitationStyle style) => style switch
    {
        // Verificato nel browser: il prompt del seed chiede «[Page 3]», e senza un divieto esplicito il
        // modello mescolava le due forme («[2, Pagina 3]»), che la pagina non sa collegare.
        CitationStyle.PageReferencesInText =>
            "Each source in the context is numbered [N]. Cite sources ONLY with their marker, right after the statement "
            + "drawn from them: for example [2], or [2,5] when several sources support it. Never put page numbers inside "
            + "the brackets and never write [Page N], even if asked elsewhere: the marker already links to the page.",
        CitationStyle.StructuredEvents =>
            "Do not add source markers in the text: the sources are attached to the answer separately.",
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown citation style."),
    };

    /// <summary>
    /// #4168: il prompt di una risposta fra i giochi della libreria. Il prompt di sistema è quello
    /// del profilo, come per un gioco solo; cambiano le citazioni (fonti numerate) e il contesto,
    /// che porta per ogni fonte documento, pagina e tier di copyright.
    /// </summary>
    /// <param name="profile">Il contenuto della versione pubblicata.</param>
    /// <param name="answerLanguage">La lingua di chi chiede (codice ISO 639-1).</param>
    /// <param name="numberedContext">Le fonti, ciascuna preceduta da [N] e dalla sua etichetta.</param>
    /// <param name="question">La domanda.</param>
    /// <param name="hasProtectedSources">Se fra le fonti ce ne sono di protette da copyright.</param>
    internal static AnswerPrompt BuildAcrossGames(
        AgentProfileContent profile,
        string answerLanguage,
        string numberedContext,
        string question,
        bool hasProtectedSources)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var promptLanguage = profile.SystemPrompts.ContainsKey(answerLanguage)
            ? answerLanguage
            : profile.FallbackLanguage;

        var system = new StringBuilder(profile.SystemPrompts[promptLanguage].Trim())
            .Append("\n\n")
            .Append(NumberedCitationInstruction(profile.CitationStyle));
        if (hasProtectedSources)
        {
            system.Append('\n').Append(ProtectedSourcesInstruction);
        }

        system.Append('\n').Append(AnswerLanguageInstruction(answerLanguage));

        var user = $"CONTEXT FROM YOUR GAME LIBRARY:\n{numberedContext}\n\nQUESTION:\n{question}\n\nANSWER:";
        return new AnswerPrompt(system.ToString(), user, promptLanguage);
    }

    private static string LanguageName(string language) => language switch
    {
        "en" => "English",
        "it" => "Italian",
        "de" => "German",
        "fr" => "French",
        "es" => "Spanish",
        _ => language,
    };
}

/// <summary>Il prompt di una risposta, con la lingua del prompt di sistema scelto.</summary>
internal sealed record AnswerPrompt(string SystemPrompt, string UserPrompt, string PromptLanguage);
