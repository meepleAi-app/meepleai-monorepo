namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #3850, #4481: la rinuncia, cioè la risposta che dice che il regolamento non contiene
/// l'informazione. È l'unico posto che sa com'è fatta: il confronto della cache di
/// <c>AskQuestion</c>, lo stato di grounding (<see cref="AnswerGrounding"/>) e la sostituzione con il
/// testo nella lingua della risposta passano tutti da qui.
/// </summary>
/// <remarks>
/// <para>
/// Il prompt di sistema del profilo pubblicato chiede al modello di rispondere «EXACTLY» con
/// <see cref="ModelSentence"/>, in inglese, qualunque sia la lingua della domanda (migrazione
/// <c>AddAgentProfile</c> e le versioni pubblicate dopo). Il modello lo fa: nella misura del
/// 2026-10-18 la frase è uscita in inglese in ogni rinuncia, da sola, in coda a una risposta in
/// italiano, dopo due punti o seguita da una parentesi, e mai tradotta.
/// </para>
/// <para>
/// Il comportamento del modello resta quello: la frase fissa è ciò che rende la rinuncia
/// riconoscibile. Il codice la sostituisce con il testo nella lingua della risposta prima che
/// l'utente la legga (<see cref="Localize"/>, e <see cref="RefusalStreamLocalizer"/> sullo stream).
/// </para>
/// <para>
/// Il confronto tollera maiuscole, spazi e a capo, virgolette, grassetto e la punteggiatura finale.
/// Riconosce come rinuncia anche i testi localizzati, perché sono quelli che finiscono nella cache e
/// nella chat salvata.
/// </para>
/// </remarks>
internal static class RulebookRefusal
{
    /// <summary>La frase che il prompt del profilo impone al modello, alla lettera.</summary>
    internal const string ModelSentence = "This information is not available in the provided rulebook.";

    /// <summary>
    /// Il corpo della rinuncia per lingua, senza punteggiatura finale: dentro un testo si sostituisce
    /// il corpo, e la punteggiatura resta quella che ha scritto il modello. Le lingue sono quelle che
    /// la pipeline sa rilevare (<c>LanguageDetectionService</c>).
    /// </summary>
    private static readonly Dictionary<string, string> Sentences = new(StringComparer.Ordinal)
    {
        ["en"] = "This information is not available in the provided rulebook",
        ["it"] = "Questa informazione non è disponibile nel regolamento fornito",
        ["de"] = "Diese Information ist im bereitgestellten Regelheft nicht enthalten",
        ["fr"] = "Cette information n'est pas disponible dans le livret de règles fourni",
        ["es"] = "Esta información no está disponible en el reglamento proporcionado",
    };

    /// <summary>
    /// Il messaggio gemello della chat in streaming, quando il recupero non trova nessun passaggio e
    /// il modello non viene chiamato (errore <c>NO_RESULTS</c>).
    /// </summary>
    private static readonly Dictionary<string, string> NoResultsTexts = new(StringComparer.Ordinal)
    {
        ["en"] = "No relevant information found in the rulebook.",
        ["it"] = "Nel regolamento non ho trovato informazioni pertinenti.",
        ["de"] = "Im Regelheft wurden keine passenden Informationen gefunden.",
        ["fr"] = "Aucune information pertinente n'a été trouvée dans le livret de règles.",
        ["es"] = "No se encontró información relevante en el reglamento.",
    };

    /// <summary>Ciò che si toglie ai bordi di una risposta prima di confrontarla con la frase.</summary>
    private static readonly char[] Wrapping =
        [' ', '\t', '\r', '\n', '\'', '"', '`', '«', '»', '“', '”', '‘', '’', '*', '_', '.', '!', '…'];

    internal static string EnglishSentence => Sentences["en"];

    /// <summary>La rinuncia nella lingua della risposta; l'inglese per una lingua senza testo.</summary>
    internal static string TextFor(string? language) => SentenceFor(language) + ".";

    /// <summary>Il messaggio di <c>NO_RESULTS</c> nella lingua della risposta; l'inglese per una lingua senza testo.</summary>
    internal static string NoResultsTextFor(string? language) =>
        NoResultsTexts.TryGetValue(LanguageKey(language), out var text) ? text : NoResultsTexts["en"];

    /// <summary>
    /// Se tutta la risposta è la rinuncia, in una delle lingue note. Una risposta che dice qualcosa
    /// e poi aggiunge la rinuncia in coda non lo è: è una risposta parziale.
    /// </summary>
    internal static bool IsRefusal(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return false;
        }

        var core = answer.Trim(Wrapping);
        foreach (var sentence in Sentences.Values)
        {
            var (kind, end) = MatchAt(core, 0, sentence, textIsComplete: true);
            if (kind == SentenceMatch.Full && end == core.Length)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// La risposta con ogni frase inglese della rinuncia sostituita da quella nella lingua della
    /// risposta. Il resto del testo, virgolette e punteggiatura comprese, resta com'è. In inglese, o
    /// in una lingua senza testo, la risposta non cambia.
    /// </summary>
    internal static string Localize(string answer, string? language)
    {
        ArgumentNullException.ThrowIfNull(answer);

        var localizer = new RefusalStreamLocalizer(language);
        return localizer.Push(answer) + localizer.Complete();
    }

    /// <summary>La frase nella lingua chiesta, senza punteggiatura finale; l'inglese se non c'è.</summary>
    internal static string SentenceFor(string? language) =>
        Sentences.TryGetValue(LanguageKey(language), out var sentence) ? sentence : Sentences["en"];

    /// <summary>
    /// La prima posizione, da <paramref name="from"/> in poi, dove comincia la frase inglese: tutta
    /// (<see cref="SentenceMatch.Full"/>, con la fine) o solo il suo inizio, perché il testo finisce
    /// prima (<see cref="SentenceMatch.Partial"/>). Solo a inizio parola.
    /// </summary>
    internal static (int Position, SentenceMatch Kind, int End) FindEnglish(string text, int from, bool textIsComplete)
    {
        for (var position = from; position < text.Length; position++)
        {
            if (!IsWordStart(text, position))
            {
                continue;
            }

            var (kind, end) = MatchAt(text, position, EnglishSentence, textIsComplete);
            if (kind != SentenceMatch.None)
            {
                return (position, kind, end);
            }
        }

        return (text.Length, SentenceMatch.None, text.Length);
    }

    /// <summary>
    /// Se a <paramref name="start"/> comincia <paramref name="sentence"/>. Maiuscole indifferenti;
    /// uno spazio della frase vale qualunque sequenza di spazi e a capo; gli apostrofi tipografici
    /// valgono quello dritto. Dopo la frase non deve continuare una parola («rulebooks» non è
    /// «rulebook»). Se il testo finisce prima di poterlo dire e può ancora crescere, è
    /// <see cref="SentenceMatch.Partial"/>.
    /// </summary>
    private static (SentenceMatch Kind, int End) MatchAt(string text, int start, string sentence, bool textIsComplete)
    {
        var i = start;
        var j = 0;
        while (j < sentence.Length)
        {
            if (i >= text.Length)
            {
                return (textIsComplete ? SentenceMatch.None : SentenceMatch.Partial, i);
            }

            if (sentence[j] == ' ')
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    return (SentenceMatch.None, i);
                }

                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                j++;
                continue;
            }

            if (!SameLetter(text[i], sentence[j]))
            {
                return (SentenceMatch.None, i);
            }

            i++;
            j++;
        }

        if (i < text.Length)
        {
            return (char.IsLetterOrDigit(text[i]) ? SentenceMatch.None : SentenceMatch.Full, i);
        }

        return (textIsComplete ? SentenceMatch.Full : SentenceMatch.Partial, i);
    }

    private static bool SameLetter(char a, char b) =>
        char.ToLowerInvariant(a) == char.ToLowerInvariant(b) || (IsApostrophe(a) && IsApostrophe(b));

    private static bool IsApostrophe(char c) => c is '\'' or '’' or 'ʼ';

    private static bool IsWordStart(string text, int index) => index == 0 || !char.IsLetterOrDigit(text[index - 1]);

    /// <summary>«it-IT», «IT» e « it » valgono «it».</summary>
    private static string LanguageKey(string? language) =>
        string.IsNullOrWhiteSpace(language)
            ? string.Empty
            : language.Trim().Split('-', 2)[0].ToLowerInvariant();
}

/// <summary>Quanto della frase della rinuncia comincia in un punto del testo.</summary>
#pragma warning disable MA0048 // File name must match type name - the match kind belongs to the recognizer
internal enum SentenceMatch
#pragma warning restore MA0048
{
    /// <summary>Lì non comincia la frase.</summary>
    None,

    /// <summary>Il testo finisce con l'inizio della frase: potrebbe ancora diventarla.</summary>
    Partial,

    /// <summary>Lì c'è la frase intera.</summary>
    Full,
}
