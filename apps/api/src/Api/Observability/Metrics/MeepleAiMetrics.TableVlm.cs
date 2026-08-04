// Issue #3435 (SP4): observability for the async VLM table-extraction pass.
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>Wire value for the <c>outcome</c> tag: an OTSL table was extracted and a retrievable
    /// table chunk persisted.</summary>
    public const string TableVlmOutcomeExtracted = "extracted";

    /// <summary>Wire value for the <c>outcome</c> tag: the VLM ran but the crop is not a table (no
    /// <c>&lt;otsl&gt;</c>, or rejected by the colorfulness pre-filter). Terminal, nothing persisted.</summary>
    public const string TableVlmOutcomeNotTable = "not_table";

    /// <summary>Wire value for the <c>outcome</c> tag: a transient failure (crop render, VLM call, or
    /// index) that stays retry-eligible (attempts still below the budget).</summary>
    public const string TableVlmOutcomeFailed = "failed";

    /// <summary>Wire value for the <c>outcome</c> tag: the region hit the attempt budget and was
    /// dead-lettered (excluded from the selector). Kept distinct from <c>failed</c> so an alert on
    /// dead-letters tracks only give-ups, not recoverable blips.</summary>
    public const string TableVlmOutcomeDeadLetter = "dead_letter";

    /// <summary>
    /// Counter of async VLM table-extraction outcomes, emitted per image region by
    /// <c>RunTableExtractionBatchCommandHandler</c> (#3435 SP4). Tag <c>outcome</c>:
    /// <c>extracted</c>|<c>not_table</c>|<c>failed</c>|<c>dead_letter</c>.
    /// NB (naming, mirrors <c>meepleai_image_region_seed_total</c>): here <c>failed</c> is the
    /// TRANSIENT/retry-eligible outcome and <c>dead_letter</c> is terminal. Alert on
    /// <c>dead_letter</c> for give-ups, and on a sustained high <c>failed</c> ratio for a broadly
    /// failing VLM service (§8 NFR4).
    /// </summary>
    public static readonly Counter<long> TableVlm = Meter.CreateCounter<long>(
        name: "meepleai.table_vlm.total",
        unit: "regions",
        description: "Async VLM table-extraction outcomes grouped by outcome (#3435 SP4)");

    /// <summary>Records one table-extraction outcome (one of the <c>TableVlmOutcome*</c> constants).</summary>
    public static void RecordTableVlm(string outcome) =>
        TableVlm.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
}
