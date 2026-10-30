using System.Text;
using System.Text.RegularExpressions;
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
}

/// <summary>A parsed answer of the Arbitro.</summary>
/// <param name="Verdict">The VERDETTO section; empty when the answer holds no verdict.</param>
/// <param name="Note">The NOTA section, if any.</param>
/// <param name="RulebookCoversTheCase">
/// True only when the answer has a COPERTURA line that says SI. A missing line, NO, or anything else is false.
/// </param>
internal readonly record struct ParsedVerdict(string Verdict, string? Note, bool RulebookCoversTheCase);

/// <summary>
/// The text side of the Arbitro (#4751): the instructions it is given, the parsing of what it answers, and the
/// rule references built from what was retrieved. Kept apart from the handler so that the format has one place.
/// </summary>
/// <remarks>
/// The model is asked for a COPERTURA line (does the rulebook cover the case), VERDETTO and NOTA. It is not
/// asked for the rule: a quote is the one thing it cannot have, and what it writes under a REGOLA header, in
/// any form, is dropped. The references come from the passages that were retrieved.
/// </remarks>
internal static class ArbiterVerdictFormat
{
    private const int MaxReferences = 3;

    /// <summary>Label of a reference. Neutral on purpose: the retrieval does not say whether a passage is from
    /// the rulebook, an expansion or an errata, and calling every one «Regolamento» would be false for some.</summary>
    private const string ReferenceLabel = "Fonte";

    /// <summary>Wire value of a Full-tier citation (<c>GroundedAnswerService</c> maps the tier name, lower-cased).</summary>
    private const string FullTier = "full";

    private const string Persona =
        "Sei l'ARBITRO ufficiale di una partita da tavolo. " +
        "Il tuo compito è emettere verdetti chiari, precisi e imparziali sulle dispute di regole. " +
        "Rispondi SEMPRE in italiano nel formato strutturato richiesto.";

    private const string RulebookFormat =
        "Rispondi in formato strutturato, una sezione per riga:\n" +
        "COPERTURA: [SI se i passaggi del regolamento forniti sopra coprono il caso; NO se non lo coprono " +
        "(in quel caso proponi comunque l'interpretazione più ragionevole)]\n" +
        "VERDETTO: [Chi ha ragione e perché — 1-2 frasi]\n" +
        "NOTA: [Se ambigua, spiega perché e suggerisci come gestirla]";

    private const string DirectFormat =
        "Rispondi in formato strutturato:\n" +
        "VERDETTO: [Chi ha ragione e perché — 1-2 frasi]\n" +
        "NOTA: [Se ambigua, spiega perché e suggerisci come gestirla]";

    private const string NoRulebookClause =
        " Il verdetto non si basa sul regolamento (è buon senso e regole generali del gioco) " +
        "e nessun riferimento è citato.";

    // For Protected sources the model is told to put [ref:documentId:pageNum] before each paraphrase
    // (RagPromptAssemblyService): machine syntax, not something to show the table.
    private static readonly Regex CopyrightMarker = new(
        @"[ \t]*\[ref:[^:\]]+:\d+\][ \t]*",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// A section header at the start of a line, in any of the forms a model writes: plain, bold
    /// (<c>**VERDETTO:**</c>, <c>**VERDETTO**:</c>), a markdown heading, a list item, a numbered item. The
    /// REGOLA header may carry up to three more words («Regola applicabile:»): whatever starts like it is dropped.
    /// </summary>
    private static readonly Regex Header = new(
        @"^[\s>#*_`\-•]*(?:\d+[.)]\s*)?[*_`]*\s*(?:(?<name>VERDETTO|NOTA|COPERTURA)|(?<rule>REGOL[AE](?:[ \t]+[\p{L}']+){0,3}))[*_`]*\s*:\s*[*_`]*\s*(?<rest>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex TrailingEmphasis = new(
        @"[\s*_`]*[*_`]{2,}\s*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex CoverageYes = new(
        @"^\s*[*_`]*\s*(?:s[iì]|yes)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

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

    // ── Parsing ────────────────────────────────────────────────────────────────

    /// <summary>Takes the machine syntax of the copyright paraphrase (<c>[ref:documentId:pageNum]</c>) out of the text.</summary>
    public static string StripCopyrightMarkers(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return CopyrightMarker.Replace(text, " ");
    }

    private enum Section { Preamble, Verdict, Note, Rule }

    /// <summary>
    /// The COPERTURA, VERDETTO and NOTA sections. A header is recognised at the start of a line whatever the
    /// markdown around it. A REGOLA section is dropped, header and the lines up to the next header, in any form:
    /// the model has no quote to give. An occurrence inside a line (<c>«vale la regola: …»</c>) is not a header
    /// and truncates nothing.
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
        var coverage = new List<string>();
        var section = Section.Preamble;
        var anyHeader = false;

        foreach (var raw in response.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var header = Header.Match(line);
            if (header.Success)
            {
                anyHeader = true;
                var rest = TrailingEmphasis.Replace(header.Groups["rest"].Value, string.Empty).Trim();

                if (header.Groups["rule"].Success)
                {
                    section = Section.Rule;
                    continue;
                }

                var name = header.Groups["name"].Value.ToUpperInvariant();
                if (string.Equals(name, "COPERTURA", StringComparison.Ordinal))
                {
                    // One line, no continuation: what follows is not part of it.
                    if (rest.Length > 0)
                    {
                        coverage.Add(rest);
                    }

                    section = Section.Preamble;
                    continue;
                }

                section = string.Equals(name, "VERDETTO", StringComparison.Ordinal) ? Section.Verdict : Section.Note;
                if (rest.Length > 0)
                {
                    (section == Section.Verdict ? verdict : note).Add(rest);
                }

                continue;
            }

            Target(section, preamble, verdict, note)?.Add(line);
        }

        var verdictText = string.Join(" ", verdict).Trim();
        if (verdictText.Length == 0)
        {
            // No VERDETTO text. An unstructured answer is the verdict itself; the bare skeleton
            // («VERDETTO:\nREGOLA:\nNOTA:», or an answer cut right after a header) is not an answer at all,
            // and stays empty so that the handler can refuse it. Text before the first header counts as the
            // verdict only when nothing else gave one; a REGOLA section never does.
            verdictText = anyHeader ? string.Join(" ", preamble).Trim() : response.Trim();
        }

        var noteText = string.Join(" ", note).Trim();
        var covers = coverage.Count > 0 && CoverageYes.IsMatch(coverage[0]);

        return new ParsedVerdict(verdictText, noteText.Length == 0 ? null : noteText, covers);
    }

    /// <summary>The list a line of this section goes to; null for the section that is dropped.</summary>
    private static List<string>? Target(
        Section section, List<string> preamble, List<string> verdict, List<string> note)
        => section switch
        {
            Section.Preamble => preamble,
            Section.Verdict => verdict,
            Section.Note => note,
            _ => null,
        };

    // ── Rule references ────────────────────────────────────────────────────────

    /// <summary>
    /// The references of a verdict that rests on the rulebook: the first passages the retrieval returned, in the
    /// order it returned them (the reranker's: <c>RelevanceScore</c> is the score before the rerank, so sorting
    /// by it would undo it), each as a page and — only for a Full-tier passage — its text. A Protected passage is
    /// cited by page alone: the verdict is the paraphrase, and the tier, not the presence of a snippet, decides
    /// what may be quoted (the shared finalize step nulls a Protected preview; this does not rely on it). Two
    /// passages that give the same reference text appear once.
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

        // SnippetPreview is the start of the chunk (KnowledgeBase caps it at 120 characters), not the
        // sentence the verdict used.
        if (string.Equals(citation.CopyrightTier, FullTier, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(citation.SnippetPreview))
        {
            return $"{label}: «{citation.SnippetPreview.Trim()}»";
        }

        return label;
    }
}
