// Issue #3435 (SP1 slice 2): observability for the automatic image-region hi_res seed batch.
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>Wire value for the <c>outcome</c> tag: regions seeded (>=1 image region persisted).</summary>
    public const string ImageRegionSeedOutcomeSeeded = "seeded";

    /// <summary>Wire value for the <c>outcome</c> tag: hi_res succeeded but the PDF has no image regions
    /// (still marked so it isn't re-processed).</summary>
    public const string ImageRegionSeedOutcomeEmpty = "empty";

    /// <summary>Wire value for the <c>outcome</c> tag: a transient failure (hi_res timeout/HTTP error,
    /// missing blob) that stays retry-eligible (attempts still below MaxSeedAttempts).</summary>
    public const string ImageRegionSeedOutcomeFailed = "failed";

    /// <summary>Wire value for the <c>outcome</c> tag: the PDF hit MaxSeedAttempts and was dead-lettered
    /// (excluded from the selector). Kept distinct from <c>failed</c> so an alert on dead-letters tracks
    /// only give-ups, not recoverable blips.</summary>
    public const string ImageRegionSeedOutcomeDeadLetter = "dead_letter";

    /// <summary>
    /// Counter of automatic image-region seed outcomes, emitted per PDF by
    /// <c>RunImageRegionSeedBatchCommandHandler</c> (#3435 SP1). Tag <c>outcome</c>:
    /// <c>seeded</c>|<c>empty</c>|<c>failed</c>|<c>dead_letter</c>.
    /// NB (naming): unlike <c>meepleai_cover_generation_total</c> — where <c>failed</c> is TERMINAL and
    /// <c>retrying</c> is the transient tag — here <c>failed</c> is the TRANSIENT/retry-eligible outcome
    /// and <c>dead_letter</c> is terminal. Alert on <c>dead_letter</c> for give-ups, not <c>failed</c>.
    ///
    /// Suggested alerting:
    /// <list type="bullet">
    ///   <item><c>increase(meepleai_image_region_seed_total{outcome="dead_letter"}[1h]) &gt; 0</c> →
    ///     a PDF exhausted its hi_res retry budget; inspect the unstructured service / the document.</item>
    ///   <item><c>rate(...{outcome="failed"}[15m]) / rate(...[15m]) &gt; 0.5</c> sustained → the hi_res
    ///     pass is broadly failing (service down/overloaded).</item>
    /// </list>
    /// </summary>
    public static readonly Counter<long> ImageRegionSeed = Meter.CreateCounter<long>(
        name: "meepleai.image_region_seed.total",
        unit: "pdfs",
        description: "Automatic image-region hi_res seed outcomes grouped by outcome (#3435)");

    /// <summary>Records one image-region seed outcome (one of the <c>ImageRegionSeedOutcome*</c> constants).</summary>
    public static void RecordImageRegionSeed(string outcome) =>
        ImageRegionSeed.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
}
