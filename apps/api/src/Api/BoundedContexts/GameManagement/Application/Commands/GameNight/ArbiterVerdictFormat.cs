using System.Text;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using CitationDto = Api.Models.CitationDto;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNight;

/// <summary>
/// What a verdict of the Arbitro rests on. It decides the references, and the notice that opens the verdict
/// when it does not rest on the rulebook (#4751).
/// </summary>
internal enum VerdictBasis
{
    /// <summary>Passages of the game's rulebook were retrieved, and the model declared they cover the case.</summary>
    Rulebook,

    /// <summary>The session has no catalog game: there was never a rulebook to look in.</summary>
    NoGame,

    /// <summary>
    /// The caller may not query the knowledge base of this game (ADR-096: ownership not declared, or the
    /// private game of someone else). Nothing of the rulebook entered the prompt.
    /// </summary>
    OwnershipNotDeclared,

    /// <summary>The rulebook could not be consulted: the time budget expired, or the retrieval failed.</summary>
    RetrievalUnavailable,

    /// <summary>The retrieval ran and found no passage.</summary>
    NoPassages,

    /// <summary>
    /// Passages were retrieved, but the answer does not say they cover the case: the model said they do not,
    /// refused, or did not say (fail-honest).
    /// </summary>
    NotCovered,

    /// <summary>
    /// The answer that rested on the rulebook repeated a run of a Protected passage, and the copyright guard
    /// replaced it. The verdict is given once more without the rulebook, so that a player whose dispute makes
    /// the model repeat the text does not get an error on every attempt.
    /// </summary>
    CopyrightWithheld,
}

/// <summary>A parsed answer of the Arbitro.</summary>
/// <param name="Verdict">The VERDETTO section; empty when the answer holds no verdict.</param>
/// <param name="Note">The NOTA section, if any.</param>
/// <param name="RulebookCoversTheCase">
/// True only when the answer has exactly one COPERTURA line and its value is SI (or SÌ), nothing else. A missing
/// line, two lines, NO, or any other value is false.
/// </param>
internal readonly record struct ParsedVerdict(string Verdict, string? Note, bool RulebookCoversTheCase);

/// <summary>
/// The text side of the Arbitro (#4751): the instructions it is given, the parsing of what it answers, and the
/// rule references built from what was retrieved. Kept apart from the handler so that the format has one place.
/// </summary>
/// <remarks>
/// <para>
/// The model is asked for a COPERTURA line (does the rulebook cover the case), VERDETTO and NOTA. It is not
/// asked for the rule: a quote is the one thing it cannot have.
/// </para>
/// <para>
/// <b>What counts as a section label.</b> Only the exact labels of the format — VERDETTO, NOTA, COPERTURA and
/// REGOLA — and only as a whole line: the label, then the colon, after the markdown around it (bold, heading,
/// list item, number) and whatever the case. «Regola del ladro:», «Regole di movimento:», «Nota che…:» and
/// «Regola applicabile al nostro caso:» are text of the verdict, not labels. The rule is chosen so that it never
/// cuts a legitimate verdict: a line the model invented and left in the verdict is a smaller fault than a
/// verdict truncated at it. Free text in the verdict cannot be verified anyway, and no reference comes from it.
/// </para>
/// <para>
/// The parsing is a linear walk over the text, with no pattern that can backtrack: the model's answer is not
/// something to hand to a regular expression on faith.
/// </para>
/// </remarks>
internal static class ArbiterVerdictFormat
{
    private const int MaxReferences = 3;

    /// <summary>Longest document id the copyright marker is read for (ids are GUIDs).</summary>
    private const int MaxMarkerDocumentIdLength = 128;

    /// <summary>Label of a reference. Neutral on purpose: the retrieval does not say whether a passage is from
    /// the rulebook, an expansion or an errata, and calling every one «Regolamento» would be false for some.</summary>
    private const string ReferenceLabel = "Fonte";

    /// <summary>Wire value of a Full-tier citation (<c>GroundedAnswerService</c> maps the tier name, lower-cased).</summary>
    private const string FullTier = "full";

    private const string Persona =
        "Sei l'ARBITRO ufficiale di una partita da tavolo. " +
        "Il tuo compito è emettere verdetti chiari, precisi e imparziali sulle dispute di regole. " +
        "Rispondi SEMPRE in italiano nel formato strutturato richiesto.";

    /// <remarks>
    /// The parser takes «yes» only from a COPERTURA line whose value is exactly SI (or SÌ), so the prompt says to
    /// write only the word, and the line it shows is one the parser reads: no explanation on it, which would turn
    /// «SI, i passaggi coprono il caso» into a «no». The example is the NO form on purpose: a model that copies it
    /// word for word lands on the honest side (notice, no references), not on the one that cites.
    /// </remarks>
    private const string RulebookFormat =
        "Rispondi in formato strutturato, una sezione per riga.\n" +
        "Prima valuta se i passaggi del regolamento forniti sopra coprono il caso. " +
        "Se non lo coprono, dillo nel VERDETTO e proponi comunque l'interpretazione più ragionevole.\n" +
        "Sulla riga COPERTURA scrivi SOLO la parola SI oppure la parola NO, senza altro testo: " +
        "la spiegazione va nel VERDETTO.\n" +
        "COPERTURA: NO\n" +
        "VERDETTO: [Chi ha ragione e perché — 1-2 frasi]\n" +
        "NOTA: [Se ambigua, spiega perché e suggerisci come gestirla]";

    private const string DirectFormat =
        "Rispondi in formato strutturato:\n" +
        "VERDETTO: [Chi ha ragione e perché — 1-2 frasi]\n" +
        "NOTA: [Se ambigua, spiega perché e suggerisci come gestirla]";

    private const string NoRulebookClause =
        " Il verdetto non si basa sul regolamento (è buon senso e regole generali del gioco) " +
        "e nessun riferimento è citato.";

    // ── Notices ────────────────────────────────────────────────────────────────

    internal const string NoGameNotice =
        "Attenzione: per questa partita non c'è un regolamento di riferimento." + NoRulebookClause;

    internal const string OwnershipNotDeclaredNotice =
        "Attenzione: possesso del gioco non dichiarato, quindi il regolamento non è stato consultato." + NoRulebookClause;

    internal const string RetrievalUnavailableNotice =
        "Attenzione: non è stato possibile consultare il regolamento (tempo scaduto o servizio non raggiungibile)." +
        NoRulebookClause;

    internal const string NoPassagesNotice =
        "Attenzione: nel regolamento non ho trovato passaggi pertinenti." + NoRulebookClause;

    internal const string NotCoveredNotice =
        "Attenzione: i passaggi del regolamento trovati non coprono questo caso." + NoRulebookClause;

    internal const string CopyrightWithheldNotice =
        "Attenzione: il verdetto non può riportare il testo del regolamento." + NoRulebookClause;

    /// <summary>
    /// The notice that opens a verdict that does not rest on the rulebook; null for one that does. It is part of
    /// the verdict itself, because the verdict and its references are what the whole table receives: a note kept
    /// in the HTTP response would reach only who asked.
    /// </summary>
    public static string? NoticeFor(VerdictBasis basis) => basis switch
    {
        VerdictBasis.Rulebook => null,
        VerdictBasis.NoGame => NoGameNotice,
        VerdictBasis.OwnershipNotDeclared => OwnershipNotDeclaredNotice,
        VerdictBasis.RetrievalUnavailable => RetrievalUnavailableNotice,
        VerdictBasis.NoPassages => NoPassagesNotice,
        VerdictBasis.NotCovered => NotCoveredNotice,
        VerdictBasis.CopyrightWithheld => CopyrightWithheldNotice,
        _ => throw new ArgumentOutOfRangeException(nameof(basis), basis, "Unknown verdict basis"),
    };

    // ── Prompts ────────────────────────────────────────────────────────────────

    /// <summary>
    /// For the grounded path: appended to the system prompt, after the rulebook passages. Not a request for a
    /// quote, and no REGOLA section: the references are added from the passages found. The model declares on a
    /// line of its own whether those passages cover the case.
    /// </summary>
    public static string ResponseInstructions(LiveGameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var builder = new StringBuilder();
        builder.AppendLine("## Modalità Arbitro");
        builder.AppendLine(Persona);
        builder.AppendLine(
            "Basa il verdetto sui passaggi del regolamento forniti sopra. " +
            "Non scrivere citazioni tra virgolette né riferimenti a pagina o sezione: " +
            "li aggiunge il sistema, solo da passaggi davvero trovati.");
        builder.AppendLine(Context(session));
        builder.Append(RulebookFormat);
        return builder.ToString();
    }

    /// <summary>For the direct path: there is no rulebook in the prompt, and the model is told so.</summary>
    public static (string SystemPrompt, string UserPrompt) DirectPrompt(LiveGameSession session, string description)
    {
        ArgumentNullException.ThrowIfNull(session);

        var systemPrompt =
            Persona + " " +
            "Non hai accesso al regolamento di questo gioco: basa il verdetto sul buon senso e sulle regole " +
            "generali del gioco, dichiaralo con chiarezza e non citare pagine, sezioni o testo del regolamento.";

        var userPrompt = new StringBuilder();
        userPrompt.AppendLine("MODALITÀ ARBITRO. Emetti un VERDETTO chiaro su questa disputa.");
        userPrompt.AppendLine();
        userPrompt.Append("Disputa: \"").Append(description).AppendLine("\"");
        userPrompt.AppendLine(Context(session));
        userPrompt.AppendLine();
        userPrompt.Append(DirectFormat);

        return (systemPrompt, userPrompt.ToString());
    }

    /// <summary>The session context both prompts carry: game, players, turn and the latest verdicts.</summary>
    private static string Context(LiveGameSession session)
    {
        var playerNames = session.Players.Count > 0
            ? string.Join(", ", session.Players.Where(p => p.IsActive).Select(p => p.DisplayName))
            : "N/D";

        var context = new StringBuilder();
        context
            .Append("Contesto: Sessione \"").Append(session.GameName)
            .Append("\" con giocatori: ").Append(playerNames)
            .Append(". Turno: ").Append(session.CurrentTurnIndex).Append('.');

        var previous = PreviousDisputes(session.Disputes);
        if (!string.IsNullOrEmpty(previous))
        {
            context.AppendLine().Append("Verdetti precedenti: ").Append(previous);
        }

        return context.ToString();
    }

    private static string PreviousDisputes(IReadOnlyList<RuleDisputeEntry> disputes)
    {
        if (disputes.Count == 0)
            return string.Empty;

        // Only include the last 3 disputes to keep the prompt concise
        var recent = disputes.TakeLast(3).ToList();
        var parts = recent.Select((d, i) => $"{i + 1}. \"{d.Description}\" → {d.Verdict}");

        return string.Join("; ", parts);
    }

    // ── Copyright markers ──────────────────────────────────────────────────────

    /// <summary>
    /// Takes the machine syntax of the copyright paraphrase (<c>[ref:documentId:pageNum]</c>, which the prompt
    /// of a Protected source tells the model to put before each paraphrase) out of the text, with the spaces
    /// around it on the same line replaced by one — or by none at the start or the end of a line.
    /// </summary>
    public static string StripCopyrightMarkers(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        const string opening = "[ref:";
        if (!text.Contains(opening, StringComparison.Ordinal))
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        var position = 0;
        while (position < text.Length)
        {
            var start = text.IndexOf(opening, position, StringComparison.Ordinal);
            if (start < 0)
            {
                result.Append(text, position, text.Length - position);
                break;
            }

            var end = MarkerEnd(text, start + opening.Length);
            if (end < 0)
            {
                // «[ref:» that is not a marker: keep it as text and go on after it
                result.Append(text, position, start + opening.Length - position);
                position = start + opening.Length;
                continue;
            }

            result.Append(text, position, start - position);
            while (result.Length > 0 && result[^1] is ' ' or '\t')
            {
                result.Length--;
            }

            var next = end;
            while (next < text.Length && text[next] is ' ' or '\t')
            {
                next++;
            }

            var atLineStart = result.Length == 0 || result[^1] == '\n';
            var atLineEnd = next >= text.Length || text[next] is '\n' or '\r';
            if (!atLineStart && !atLineEnd)
            {
                result.Append(' ');
            }

            position = next;
        }

        return result.ToString();
    }

    /// <summary>The offset just past the <c>]</c> of a marker whose document id starts at <paramref name="from"/>; -1 if there is none.</summary>
    private static int MarkerEnd(string text, int from)
    {
        var i = from;
        while (i < text.Length && text[i] is not ':' and not ']' && i - from <= MaxMarkerDocumentIdLength)
        {
            i++;
        }

        if (i == from || i >= text.Length || text[i] != ':' || i - from > MaxMarkerDocumentIdLength)
        {
            return -1;
        }

        var digits = ++i;
        while (i < text.Length && char.IsAsciiDigit(text[i]))
        {
            i++;
        }

        return i > digits && i < text.Length && text[i] == ']' ? i + 1 : -1;
    }

    // ── Parsing ────────────────────────────────────────────────────────────────

    private enum Label { Verdict, Note, Coverage, Rule }

    private enum Section { Preamble, Verdict, Note }

    /// <summary>
    /// The COPERTURA, VERDETTO and NOTA sections.
    /// <list type="bullet">
    /// <item>VERDETTO and NOTA start their section; what follows, up to the next of them, belongs to it.</item>
    /// <item>COPERTURA is one line and changes no section: a stray one in the middle of the verdict is read and
    /// leaves the verdict whole.</item>
    /// <item>A REGOLA line is dropped, and so are the lines right after it that start with a quotation mark or
    /// are indented (the continuation of a quote). The next line that is neither is back in the section it was
    /// in: nothing is cut for a REGOLA.</item>
    /// <item>An answer with no label at all is the verdict itself.</item>
    /// </list>
    /// </summary>
    public static ParsedVerdict Parse(string response)
    {
        // #4751: the handler turns an empty answer into a failure before it gets here. This used to
        // invent «Nessun verdetto disponibile.», which the handler then saved as a real verdict; reaching
        // this point empty is a bug in the caller, so it fails loudly instead of producing one.
        ArgumentException.ThrowIfNullOrWhiteSpace(response);

        var preamble = new List<string>();
        var verdict = new List<string>();
        var note = new List<string>();
        var coverageLines = new List<string>();
        var section = Section.Preamble;
        var anyLabel = false;
        var droppingQuoteContinuation = false;

        foreach (var raw in response.Split('\n'))
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (TryReadLabel(trimmed, out var label, out var rest))
            {
                anyLabel = true;
                droppingQuoteContinuation = label == Label.Rule;

                switch (label)
                {
                    case Label.Verdict:
                        section = Section.Verdict;
                        AddIfAny(verdict, rest);
                        break;
                    case Label.Note:
                        section = Section.Note;
                        AddIfAny(note, rest);
                        break;
                    case Label.Coverage:
                        coverageLines.Add(rest);
                        break;
                }

                continue;
            }

            if (droppingQuoteContinuation && (StartsIndented(line) || StartsWithQuotationMark(trimmed)))
            {
                continue;
            }

            droppingQuoteContinuation = false;
            var target = section switch
            {
                Section.Verdict => verdict,
                Section.Note => note,
                _ => preamble,
            };
            target.Add(trimmed);
        }

        var verdictText = string.Join(" ", verdict).Trim();
        if (verdictText.Length == 0)
        {
            // No VERDETTO text. An unstructured answer is the verdict itself; the bare skeleton
            // («VERDETTO:\nREGOLA:\nNOTA:», or an answer cut right after a header) is not an answer at all,
            // and stays empty so that the handler can refuse it. Text before the first label counts as the
            // verdict only when nothing else gave one.
            verdictText = anyLabel ? string.Join(" ", preamble).Trim() : response.Trim();
        }

        var noteText = string.Join(" ", note).Trim();
        var covers = coverageLines.Count == 1 && IsAPlainYes(coverageLines[0]);

        return new ParsedVerdict(verdictText, noteText.Length == 0 ? null : noteText, covers);
    }

    private static void AddIfAny(List<string> lines, string text)
    {
        if (text.Length > 0)
        {
            lines.Add(text);
        }
    }

    private static bool StartsIndented(string line) => line.Length > 0 && line[0] is ' ' or '\t';

    private static bool StartsWithQuotationMark(string trimmed) => trimmed[0] is '"' or '«' or '“' or '„' or '”' or '\'';

    /// <summary>
    /// Reads a section label at the start of a line: markdown around it (quote, heading, list item, number,
    /// bold, backticks) is ignored, the case is ignored, and the label must be one of the four exact names, then
    /// the colon. Linear: the line is walked once.
    /// </summary>
    private static bool TryReadLabel(string line, out Label label, out string rest)
    {
        label = default;
        rest = string.Empty;

        var i = SkipDecoration(line, 0);

        // a numbered item: «1.» or «2)»
        var digitsEnd = i;
        while (digitsEnd < line.Length && char.IsAsciiDigit(line[digitsEnd]))
        {
            digitsEnd++;
        }

        if (digitsEnd > i && digitsEnd < line.Length && line[digitsEnd] is '.' or ')')
        {
            i = SkipDecoration(line, digitsEnd + 1);
        }

        if (!TryMatchName(line, i, out label, out var afterName))
        {
            return false;
        }

        // the colon, with only emphasis and spaces between it and the name: «**VERDETTO**:», «VERDETTO :»
        var colon = SkipEmphasisAndSpaces(line, afterName);
        if (colon >= line.Length || line[colon] != ':')
        {
            return false;
        }

        var start = SkipEmphasisAndSpaces(line, colon + 1);
        rest = TrimClosingEmphasis(line[start..]);
        return true;
    }

    private static readonly (string Name, Label Label)[] LabelNames =
    [
        ("VERDETTO", Label.Verdict), ("NOTA", Label.Note), ("COPERTURA", Label.Coverage), ("REGOLA", Label.Rule),
    ];

    private static bool TryMatchName(string line, int index, out Label label, out int end)
    {
        foreach (var (name, candidate) in LabelNames)
        {
            if (string.Compare(line, index, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) == 0
                && index + name.Length <= line.Length)
            {
                label = candidate;
                end = index + name.Length;
                return true;
            }
        }

        label = default;
        end = index;
        return false;
    }

    /// <summary>Past the markdown that can precede a label: spaces, quote, heading, list marker, emphasis, backticks.</summary>
    private static int SkipDecoration(string line, int from)
    {
        var i = from;
        while (i < line.Length && (char.IsWhiteSpace(line[i]) || line[i] is '>' or '#' or '*' or '_' or '`' or '-' or '•'))
        {
            i++;
        }

        return i;
    }

    private static int SkipEmphasisAndSpaces(string line, int from)
    {
        var i = from;
        while (i < line.Length && (char.IsWhiteSpace(line[i]) || line[i] is '*' or '_' or '`'))
        {
            i++;
        }

        return i;
    }

    /// <summary>Takes off the emphasis that closes a headed line («**VERDETTO: testo**») — a run of two or more.</summary>
    private static string TrimClosingEmphasis(string text)
    {
        var end = text.Length;
        while (end > 0 && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        var run = end;
        while (run > 0 && text[run - 1] is '*' or '_' or '`')
        {
            run--;
        }

        return end - run >= 2 ? text[..run].TrimEnd() : text[..end];
    }

    /// <summary>
    /// A COPERTURA value that is a clear yes: «SI» or «SÌ» — after the case, the emphasis around it and the
    /// punctuation at its end — and nothing else. «SÍ», «Y», «YES», «SI/NO», «SI se… NO se…», «Sì, ma solo in
    /// parte», «SI (parziale)» and the template echoed back in square brackets are all «no».
    /// </summary>
    private static bool IsAPlainYes(string value)
    {
        var normalized = value.Trim().Trim('*', '_', '`').Trim().TrimEnd('.', '!', ';').Trim('*', '_', '`').Trim();
        return normalized.Equals("SI", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("SÌ", StringComparison.OrdinalIgnoreCase);
    }

    // ── Rule references ────────────────────────────────────────────────────────

    /// <summary>
    /// The references of a verdict that rests on the rulebook: the first passages in the order they are handed
    /// over, each as a page and — only for a Full-tier passage — its text. That order is KnowledgeBase's, and it
    /// is <b>not</b> the reranker's: after the rerank, the sentence-window expansion sorts the passages again by
    /// their score from before it, so what arrives here is a score order (the reranker's only if the expansion
    /// failed). This method keeps it as it is. A Protected passage is cited by page alone: the verdict is the
    /// paraphrase, and the tier, not the presence of a snippet, decides what may be quoted (the shared finalize
    /// step nulls a Protected preview; this does not rely on it). Two passages that give the same reference text
    /// appear once.
    /// </summary>
    public static List<string> BuildReferences(IReadOnlyList<CitationDto> citations)
    {
        ArgumentNullException.ThrowIfNull(citations);

        return citations
            .Select(ToReference)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxReferences)
            .ToList();
    }

    private static string ToReference(CitationDto citation)
    {
        var label = citation.PageNumber > 0 ? $"{ReferenceLabel}, p. {citation.PageNumber}" : ReferenceLabel;

        // SnippetPreview is the start of the chunk (KnowledgeBase caps its length), not the sentence the
        // verdict used. It comes from a PDF: line breaks and runs of spaces are collapsed, since the reference
        // is stored in disputes_json and sent to every player as a line of text.
        if (string.Equals(citation.CopyrightTier, FullTier, StringComparison.OrdinalIgnoreCase))
        {
            var text = CollapseWhitespace(citation.SnippetPreview);
            if (text.Length > 0)
            {
                return $"{label}: «{text}»";
            }
        }

        return label;
    }

    /// <summary>Every run of whitespace (line breaks included) becomes one space; the ends are trimmed. Nothing is cut.</summary>
    private static string CollapseWhitespace(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var result = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                result.Append(' ');
                pendingSpace = false;
            }

            result.Append(c);
        }

        return result.ToString();
    }
}
