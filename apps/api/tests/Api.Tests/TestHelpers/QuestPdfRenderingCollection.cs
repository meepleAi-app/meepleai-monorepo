using Xunit;

namespace Api.Tests.TestHelpers;

/// <summary>
/// #4933 — serializes the test classes that render a PDF with QuestPDF. Two renders in the same process, overlapping
/// in time, can each come out with a different, broken <c>ToUnicode</c> map for the embedded font: the PDF opens and
/// looks right, but pdfium reads back a fraction of its words (nothing at all, in the worst case). The extraction bench
/// (<c>ExtractionBench</c>) turned red at random on that: the first render of a process, racing the render of the same
/// fixture in <c>ExtractionFixtureTests</c>, is the likeliest to break. Same bytes, extracted twice, gave the same short
/// text; the same document rendered again, alone, gave all of it. See the issue for the measurements.
/// </summary>
/// <remarks>
/// Every class that calls QuestPDF, directly or through a product formatter, belongs here:
/// <c>[Collection(QuestPdfRenderingCollection.Name)]</c>. <c>DisableParallelization</c> also keeps them from overlapping
/// with every other collection, not only with each other.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class QuestPdfRenderingCollection
{
    public const string Name = "QuestPdfRendering";
}
