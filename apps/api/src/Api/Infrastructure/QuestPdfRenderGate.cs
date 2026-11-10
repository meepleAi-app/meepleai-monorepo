using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Api.Infrastructure;

/// <summary>
/// #4950 — the only place the product turns a QuestPDF <see cref="IDocument"/> into bytes, one render at a time.
/// </summary>
/// <remarks>
/// <para>
/// Two QuestPDF renders overlapping in the same process can each come out with a different, broken <c>ToUnicode</c>
/// map for the embedded font: the PDF opens and looks right, but a reader gets a fraction of its words, or none
/// (#4933 measured it on test fixtures). The three product renderers — <c>PdfExportFormatter</c>,
/// <c>PdfReportFormatter</c> and <c>ExportLedgerQueryHandler</c> — render on request threads, so two exports at the
/// same time are enough. Measured in #4950 (Windows, Release, <c>Arial</c> as they use): 1 thread gave full text every
/// time and 4 threads lost it in about one document out of ten, for each of the three; a render done before the
/// overlap lowered that rate but did not remove it, so warming up at startup is not a cure.
/// </para>
/// <para>
/// The lock covers <c>GeneratePdf</c> because the document tree is composed lazily inside it: the content lambdas, the
/// font lookups and the layout all run there. An export waits for the one in front of it; they are short and rare
/// (admin reports, a ledger, a chat export), so the wait is not what to optimise.
/// </para>
/// <para>
/// <c>QuestPdfRenderGateArchitectureTests</c> forbids a direct <c>GeneratePdf</c> (or any other QuestPDF generator)
/// outside this file: a new renderer that skipped the gate would bring the defect back with nothing in its code to say so.
/// </para>
/// </remarks>
internal static class QuestPdfRenderGate
{
    private static readonly Lock Sync = new();

    /// <summary>Renders <paramref name="document"/> to PDF bytes, alone in the process.</summary>
    public static byte[] GeneratePdf(IDocument document) =>
        Run(() => document.GeneratePdf());

    /// <summary>Renders <paramref name="document"/> to <paramref name="stream"/>, alone in the process.</summary>
    public static void GeneratePdf(IDocument document, Stream stream) =>
        Run(() =>
        {
            document.GeneratePdf(stream);
            return true;
        });

    /// <summary>
    /// Runs <paramref name="render"/> while holding the gate. Exposed so the exclusion can be tested without a PDF;
    /// the lock is released when <paramref name="render"/> throws, as with any <c>lock</c>.
    /// </summary>
    internal static T Run<T>(Func<T> render)
    {
        lock (Sync)
        {
            return render();
        }
    }
}
