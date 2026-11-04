namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// The shape of the answer a chat thread asks for (ADR-098 §5, #4404). The selector of the chat stops choosing
/// an «agent» — there is one agent (ADR-094) — and chooses how that agent answers:
/// <list type="bullet">
///   <item><c>auto</c> — the answer of today; it adds nothing to the prompt.</item>
///   <item><c>tutor</c> — the rule step by step, with an example from play.</item>
///   <item><c>secca</c> — one line and the citation.</item>
/// </list>
/// The thread stores it in <c>AgentType</c>; the web offers the same three ids.
/// </summary>
/// <remarks>
/// <para>
/// The third id is <c>secca</c> and not <c>decisore</c>: «Decisore» already means something in the repo, and it is
/// the opposite — the strategic move suggestions (<c>DecisoreMoveFeedback</c>) that ADR-098 §5 takes out of the
/// chat, which are the answers without sources. A <c>decisore</c> already stored on a thread must not start
/// answering in one line, so the old id is left outside the list and reads as <c>auto</c>.
/// </para>
/// <para>
/// <b>Expand, then contract.</b> What is READ is strict from the start: anything outside <see cref="All"/>
/// answers as <c>auto</c>, so values stored under the old list (and by other features that use the same column)
/// keep loading and answering as before. What is WRITTEN accepts, for now, the three intents plus the two retired
/// ids that older web clients still send (<see cref="IsAcceptedWhenSwitching"/>); they stop being accepted in the
/// last step, once the web no longer offers them.
/// </para>
/// <para>
/// This is not the <c>IntentClassifierService</c> of the pipeline: that one classifies the QUESTION; this is the
/// shape of the ANSWER the user chose.
/// </para>
/// </remarks>
public static class AnswerIntent
{
    public const string Auto = "auto";
    public const string Tutor = "tutor";
    public const string Secca = "secca";

    /// <summary>The intents of the chat, in the order the selector shows them.</summary>
    public static IReadOnlyList<string> All { get; } = [Auto, Tutor, Secca];

    // Ids the old selector sent and a thread can still be switched to until the contract step (#4404).
    private static readonly string[] RetiredButStillAcceptedWhenSwitching = ["arbitro", "decisore"];

    // Both end by leaving two things to the rest of the prompt: what to answer when the context does not hold the
    // answer (the refusal sentence, which only counts as a refusal when it is the WHOLE answer), and how to cite
    // (a profile can ask for no page numbers in the text). An instruction that said «then give the citation» would
    // contradict the second and, on a refusal, turn the first into a grounded-looking answer.
    private const string TutorInstruction =
        "Explain the rule step by step, the way a patient tutor teaches a new player: say what the rule is for, "
        + "walk through how it works in order, and finish with a short worked example from play. Everything you say "
        + "must still come from the context and be cited as instructed above. If the context does not contain the "
        + "answer, do not teach: reply as instructed above for that case, and nothing else.";

    private const string SeccaInstruction =
        "Answer in a single sentence. No introduction, no explanation, no example; cite only as instructed above. "
        + "This overrides any length or format instruction above, but not the case where the context does not "
        + "contain the answer: then reply as instructed above for that case, and nothing else.";

    /// <summary>The intent stored on a thread, as the pipeline reads it: anything outside the list is <c>auto</c>.</summary>
    public static string FromStored(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return Auto;
        }

        var normalized = stored.Trim().ToLowerInvariant();
        return All.Contains(normalized, StringComparer.Ordinal) ? normalized : Auto;
    }

    /// <summary>
    /// Whether a thread may be switched to <paramref name="value"/>: the three intents, and for now the two
    /// retired ids. See the remarks on the type.
    /// </summary>
    public static bool IsAcceptedWhenSwitching(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return All.Contains(normalized, StringComparer.Ordinal)
            || RetiredButStillAcceptedWhenSwitching.Contains(normalized, StringComparer.Ordinal);
    }

    /// <summary>
    /// The built-in instruction an intent adds to the prompt, or <c>null</c> when it adds nothing (<c>auto</c>, or
    /// an id outside the list). A profile version can replace it for an intent with a section of its own.
    /// </summary>
    public static string? DefaultInstructionFor(string intent) => FromStored(intent) switch
    {
        Tutor => TutorInstruction,
        Secca => SeccaInstruction,
        _ => null,
    };
}
