// Spec azure-local §4.3: esiti dell'integration event PdfUploaded (API → Service Bus → Function → API).
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>Esiti bounded del ciclo PdfUploaded; tag <c>outcome</c>, mai id o host.</summary>
    public static class PdfUploadedEventOutcomes
    {
        public const string Published = "published";
        public const string PublishFailed = "publish_failed";
        public const string NotFound = "not_found";
        public const string Received = "received";
        public const string Indexed = "indexed";
        public const string Noop = "noop";
        public const string Rejected = "rejected";
        public const string Failed = "failed";
    }

    /// <summary>Senza unit: l'exporter Prometheus mette l'unit nel nome, e la spec fissa `meepleai_pdf_uploaded_events_total`.</summary>
    public static readonly Counter<long> PdfUploadedEventsTotal = Meter.CreateCounter<long>(
        name: "meepleai.pdf.uploaded_events.total",
        description: "PdfUploaded integration events by outcome (publish side and internal consumer side)");

    public static void RecordPdfUploadedEvent(string outcome) =>
        PdfUploadedEventsTotal.Add(1, new TagList { { "outcome", outcome } });
}
