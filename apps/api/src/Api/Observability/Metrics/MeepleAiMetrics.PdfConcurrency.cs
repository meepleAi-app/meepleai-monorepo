// #1802: Optimistic concurrency conflict metrics for PdfDocumentEntity
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Counter for DbUpdateConcurrencyException occurrences on PdfDocumentEntity (#1802).
    /// Cardinality bounded: ~17 handler labels × 3 category labels = 51 series max.
    /// Tags: handler (handler class name), category (A | B | C).
    /// </summary>
    public static readonly Counter<long> PdfConcurrencyConflictsTotal = Meter.CreateCounter<long>(
        name: "meepleai.pdf.concurrency.conflicts.total",
        unit: "conflicts",
        description: "Total number of DbUpdateConcurrencyException occurrences on PdfDocumentEntity, by handler and category.");

    /// <summary>
    /// Category labels for PDF concurrency conflicts.
    /// A = user-facing handlers (synchronous request path).
    /// B = background pipeline handlers (Hangfire / hosted service).
    /// C = maintenance handlers (admin, reindex, bulk ops).
    /// </summary>
    public static class PdfConcurrencyCategories
    {
        /// <summary>User-facing handlers — conflict surfaces to the caller as a retryable error.</summary>
        public const string A = "A";

        /// <summary>Background pipeline handlers — conflict is retried silently or job is rescheduled.</summary>
        public const string B = "B";

        /// <summary>Maintenance / admin handlers — conflict during bulk or reindex operations.</summary>
        public const string C = "C";
    }

    /// <summary>
    /// Records a PdfDocumentEntity optimistic concurrency conflict event.
    /// </summary>
    /// <param name="handlerName">
    /// Use <c>nameof(YourHandlerClass)</c> for rename-safety.
    /// Kept as the class short name (no namespace) to stay within bounded cardinality.
    /// </param>
    /// <param name="category">
    /// One of <see cref="PdfConcurrencyCategories.A"/>, <see cref="PdfConcurrencyCategories.B"/>,
    /// or <see cref="PdfConcurrencyCategories.C"/>.
    /// </param>
    public static void RecordPdfConcurrencyConflict(string handlerName, string category)
    {
        var tags = new TagList
        {
            { "handler", handlerName },
            { "category", category },
        };
        PdfConcurrencyConflictsTotal.Add(1, tags);
    }
}
