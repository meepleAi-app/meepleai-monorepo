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
    /// <param name="responseStyle">«concise» (default), «detailed» o «continuation».</param>
    /// <param name="continuationContext">La risposta parziale da continuare, se c'è.</param>
    /// <param name="hasVerifiedRules">Se il contesto comincia con le regole verificate.</param>
    internal static AnswerPrompt Build(
        AgentProfileContent profile,
        string answerLanguage,
        string context,
        string question,
        string? responseStyle,
        string? continuationContext,
        bool hasVerifiedRules)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var promptLanguage = profile.SystemPrompts.ContainsKey(answerLanguage)
            ? answerLanguage
            : profile.FallbackLanguage;

        var system = new StringBuilder(profile.SystemPrompts[promptLanguage].Trim())
            .Append("\n\n")
            .Append(CitationInstruction(profile.CitationStyle))
            .Append('\n')
            .Append(ResponseStyleInstruction(responseStyle));
        if (hasVerifiedRules)
        {
            system.Append('\n').Append(VerifiedRulesInstruction);
        }

        system.Append('\n').Append(AnswerLanguageInstruction(answerLanguage));

        var user = $"CONTEXT FROM RULEBOOK:\n{context}\n\nQUESTION:\n{question}\n\nANSWER:";
        if (!string.IsNullOrWhiteSpace(continuationContext))
        {
            user = $"PREVIOUS PARTIAL ANSWER (continue from here):\n{continuationContext}\n\n{user}";
        }

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
