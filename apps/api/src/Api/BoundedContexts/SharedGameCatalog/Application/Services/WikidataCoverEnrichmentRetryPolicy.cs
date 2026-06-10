using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Classification decision produced by <see cref="IWikidataCoverEnrichmentRetryPolicy"/>.
/// Discriminated union encoded as a sealed-record hierarchy.
/// Issue #1823 Wave 3 M9.
/// </summary>
internal abstract record WikidataCoverEnrichmentRetryDecision
{
    private protected WikidataCoverEnrichmentRetryDecision() { }

    /// <summary>Pipeline result is terminal — record and STOP. The scheduler will not pick this game up again until the underlying state changes (e.g. <c>WikidataQid</c> reassigned, freshness window expires).</summary>
    public sealed record Terminal : WikidataCoverEnrichmentRetryDecision;

    /// <summary>Pipeline failure is retry-eligible — schedule the next attempt at <paramref name="NextRetryAt"/> (UTC).</summary>
    public sealed record ScheduleRetry(DateTime NextRetryAt) : WikidataCoverEnrichmentRetryDecision;

    /// <summary>Pipeline failure is unrecoverable OR retry budget exhausted — dead-letter the entry (surfaced on M13 admin page).</summary>
    public sealed record DeadLetter : WikidataCoverEnrichmentRetryDecision;
}

/// <summary>
/// Classifies a <see cref="EnrichCatalogCoverResult"/> into one of three
/// scheduler-relevant decisions: terminal, schedule-retry, or dead-letter.
/// Encodes ADR DEC-3j (retry + dead-letter policy).
/// Issue #1823 Wave 3 M9.
/// </summary>
internal interface IWikidataCoverEnrichmentRetryPolicy
{
    /// <summary>
    /// Decides what to record for the supplied pipeline result.
    /// </summary>
    /// <param name="result">The terminal outcome of the <see cref="EnrichCatalogCoverCommandHandler"/>.</param>
    /// <param name="currentRetryCount">Retry count of the PREVIOUS attempt — 0 if this is the first run. Used to lookup the appropriate backoff window.</param>
    /// <param name="nowUtc">The UTC clock used to anchor the <c>NextRetryAt</c> offset.</param>
    WikidataCoverEnrichmentRetryDecision Classify(
        EnrichCatalogCoverResult result,
        int currentRetryCount,
        DateTime nowUtc);
}

/// <summary>
/// Default implementation of <see cref="IWikidataCoverEnrichmentRetryPolicy"/>.
/// DEC-3j semantics:
/// <list type="bullet">
///   <item><see cref="EnrichCatalogCoverResult.Success"/> → <see cref="WikidataCoverEnrichmentRetryDecision.Terminal"/>.</item>
///   <item>Any <see cref="EnrichCatalogCoverResult.Skipped"/> → <see cref="WikidataCoverEnrichmentRetryDecision.Terminal"/> (business condition, retry won't change it within the freshness window).</item>
///   <item><see cref="EnrichCatalogCoverResult.Failed"/> with reason <c>image-processing-error</c> → <see cref="WikidataCoverEnrichmentRetryDecision.DeadLetter"/> (corrupted image bytes, retry won't help).</item>
///   <item><see cref="EnrichCatalogCoverResult.Failed"/> with reason <c>r2-upload-error</c> → <see cref="WikidataCoverEnrichmentRetryDecision.ScheduleRetry"/> with exponential backoff (1m / 5m / 15m) for attempts 0/1/2; <see cref="WikidataCoverEnrichmentRetryDecision.DeadLetter"/> on attempt 3+.</item>
///   <item>Unknown <see cref="EnrichCatalogCoverResult.Failed"/> reason → <see cref="WikidataCoverEnrichmentRetryDecision.DeadLetter"/> (fail-fast on unexpected outcomes).</item>
/// </list>
/// </summary>
internal sealed class WikidataCoverEnrichmentRetryPolicy : IWikidataCoverEnrichmentRetryPolicy
{
    /// <summary>Max retry budget per DEC-3j (3 retries → attempt 4 dead-letters).</summary>
    internal const int MaxRetryCount = 3;

    /// <summary>
    /// Backoff windows in minutes, indexed by previous retry count. Index 0 is the
    /// first retry after the original attempt; index 2 is the last retry before
    /// dead-letter. Hard-coded to mirror ADR DEC-3j.
    /// </summary>
    private static readonly TimeSpan[] BackoffSchedule =
    {
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
    };

    public WikidataCoverEnrichmentRetryDecision Classify(
        EnrichCatalogCoverResult result,
        int currentRetryCount,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result switch
        {
            EnrichCatalogCoverResult.Success => new WikidataCoverEnrichmentRetryDecision.Terminal(),
            EnrichCatalogCoverResult.Skipped => new WikidataCoverEnrichmentRetryDecision.Terminal(),
            EnrichCatalogCoverResult.Failed failed => ClassifyFailed(failed, currentRetryCount, nowUtc),
            _ => new WikidataCoverEnrichmentRetryDecision.DeadLetter(),
        };
    }

    private static WikidataCoverEnrichmentRetryDecision ClassifyFailed(
        EnrichCatalogCoverResult.Failed failed,
        int currentRetryCount,
        DateTime nowUtc)
    {
        // Corrupted image — retry won't change the upstream bytes. Dead-letter immediately.
        if (string.Equals(failed.Reason, EnrichCatalogCoverCommandHandler.FailReasonImageProcessing, StringComparison.Ordinal))
        {
            return new WikidataCoverEnrichmentRetryDecision.DeadLetter();
        }

        // R2 upload error — transient infra failure. Retry per DEC-3j schedule.
        if (string.Equals(failed.Reason, EnrichCatalogCoverCommandHandler.FailReasonR2Upload, StringComparison.Ordinal))
        {
            if (currentRetryCount >= MaxRetryCount)
            {
                return new WikidataCoverEnrichmentRetryDecision.DeadLetter();
            }

            // currentRetryCount = 0 → schedule the first retry (BackoffSchedule[0] = 1m)
            var backoff = BackoffSchedule[Math.Min(currentRetryCount, BackoffSchedule.Length - 1)];
            return new WikidataCoverEnrichmentRetryDecision.ScheduleRetry(nowUtc.Add(backoff));
        }

        // Unknown failure reason — fail-fast to surface it on the admin dead-letter page
        // for triage rather than silently retrying.
        return new WikidataCoverEnrichmentRetryDecision.DeadLetter();
    }
}
