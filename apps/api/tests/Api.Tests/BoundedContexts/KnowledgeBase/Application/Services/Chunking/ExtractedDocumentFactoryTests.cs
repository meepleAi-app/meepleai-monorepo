using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

[Trait("Category", TestCategories.Unit)]
public class ExtractedDocumentFactoryTests
{
    private static ExtractedElement El(string text, string type, int page = 1) => new(text, page, type);
    private static ExtractedElement ElBox(string text, string type, int page, float x, float y, float w, float h) =>
        new(text, page, type, new ElementBoundingBox(x, y, w, h));
    private static readonly Guid Doc = Guid.NewGuid();

    [Fact]
    public void SingleTitle_CreatesOneSectionWithHeading()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("Preparazione", "Title"), El("Disponi le tessere.", "NarrativeText") }, "ignored");

        doc.Sections.Should().HaveCount(1);
        doc.Sections[0].Heading.Should().Be("Preparazione");
        doc.Sections[0].ElementType.Should().Be("heading");
        doc.Sections[0].Content.Should().Be("Preparazione\n\nDisponi le tessere.");
    }

    // ── SP-B #3406: section bounding-box union ─────────────────────────────────

    [Fact]
    public void SectionBBox_IsUnionOfElementBoxesOnStartPage()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null, new[]
        {
            ElBox("Setup", "Title", 1, 0.1f, 0.1f, 0.2f, 0.05f),
            ElBox("body", "NarrativeText", 1, 0.1f, 0.2f, 0.6f, 0.1f),
        }, "x");

        var bbox = doc.Sections[0].BBox;
        bbox.Should().NotBeNull();
        bbox!.X.Should().BeApproximately(0.1f, 1e-4f);
        bbox.Y.Should().BeApproximately(0.1f, 1e-4f);
        bbox.Width.Should().BeApproximately(0.6f, 1e-4f); // maxX=max(0.3,0.7)=0.7 → 0.7-0.1
        bbox.Height.Should().BeApproximately(0.2f, 1e-4f); // maxY=max(0.15,0.3)=0.3 → 0.3-0.1
    }

    [Fact]
    public void SectionBBox_IsNullWhenNoElementHasCoordinates()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("Setup", "Title"), El("body", "NarrativeText") }, "x");

        doc.Sections[0].BBox.Should().BeNull();
    }

    [Fact]
    public void SectionBBox_SkipsElementsNotOnStartPage()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null, new[]
        {
            ElBox("Setup", "Title", 1, 0.1f, 0.1f, 0.2f, 0.05f),
            ElBox("later", "NarrativeText", 2, 0.9f, 0.9f, 0.1f, 0.1f), // page 2 → ignored
        }, "x");

        var bbox = doc.Sections[0].BBox;
        bbox.Should().NotBeNull();
        bbox!.X.Should().BeApproximately(0.1f, 1e-4f);
        bbox.Width.Should().BeApproximately(0.2f, 1e-4f); // only the start-page box counts
    }

    [Fact]
    public void MultipleTitles_CreateSeparateSections()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("Setup", "Title"), El("a", "NarrativeText"), El("Punteggio", "Title"), El("b", "NarrativeText") }, "x");

        doc.Sections.Select(s => s.Heading).Should().Equal("Setup", "Punteggio");
    }

    [Fact]
    public void ElementsBeforeFirstTitle_BecomePreambleWithNullHeading()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("intro text", "NarrativeText"), El("Setup", "Title"), El("body", "NarrativeText") }, "x");

        doc.Sections.Should().HaveCount(2);
        doc.Sections[0].Heading.Should().BeNull();
        doc.Sections[0].Content.Should().Be("intro text");
        doc.Sections[1].Heading.Should().Be("Setup");
    }

    [Fact]
    public void ConsecutiveTitles_EmitHeadingOnlySection()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("A", "Title"), El("B", "Title"), El("body", "NarrativeText") }, "x");

        doc.Sections.Select(s => s.Heading).Should().Equal("A", "B");
        doc.Sections[0].Content.Should().Be("A");
    }

    [Fact]
    public void TrailingTitle_EmitsHeadingOnlySection()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("body", "NarrativeText"), El("Appendice", "Title") }, "x");

        doc.Sections.Last().Heading.Should().Be("Appendice");
        doc.Sections.Last().Content.Should().Be("Appendice");
    }

    [Fact]
    public void NoTitle_SinglePreambleWithAllContent()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("a", "NarrativeText"), El("b", "NarrativeText") }, "x");

        doc.Sections.Should().HaveCount(1);
        doc.Sections[0].Heading.Should().BeNull();
        doc.Sections[0].Content.Should().Be("a\n\nb");
    }

    [Fact]
    public void CaseSensitiveTitle_LowercaseDoesNotOpenSection()
    {
        // "title" (lowercase) is NOT a section opener; only "Title" is. The lowercase element
        // therefore falls into the null-heading preamble (spec §6.1/§7 — text is never lost).
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("nope", "title"), El("Real", "Title") }, "x");

        doc.Sections.Should().HaveCount(2);
        doc.Sections[0].Heading.Should().BeNull();
        doc.Sections[0].Content.Should().Be("nope");
        doc.Sections[1].Heading.Should().Be("Real");
    }

    [Fact]
    public void DocContent_IsAllElementsJoinedBySeparator()
    {
        // §6.2: doc.Content is every element concatenated in order, separated by "\n\n"
        // (independently computed — NOT read back from the produced offsets).
        var input = new[] { El("pre", "NarrativeText"), El("S1", "Title"), El("x", "NarrativeText"), El("S2", "Title") };
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null, input, "ignored");

        doc.Content.Should().Be(string.Join("\n\n", input.Select(e => e.Text))); // "pre\n\nS1\n\nx\n\nS2"
    }

    [Fact]
    public void TableElement_MapsToTableElementTypeInBody()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("Costi", "Title"), El("r1 | r2", "Table") }, "x");

        doc.Sections[0].Content.Should().Contain("r1 | r2");
    }

    [Fact]
    public void SubstringInvariant_HoldsForEverySection()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("pre", "NarrativeText"), El("S1", "Title"), El("x", "NarrativeText"), El("S2", "Title") }, "x");

        foreach (var s in doc.Sections)
        {
            s.Content.Should().Be(doc.Content.Substring(s.CharStart, s.CharEnd - s.CharStart));
        }
    }

    [Fact]
    public void NullStructuredElements_ProducesOnePreambleFromFlatText()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null, null, "flat body text");

        doc.Sections.Should().HaveCount(1);
        doc.Sections[0].Heading.Should().BeNull();
        doc.Sections[0].Content.Should().Be("flat body text");
        doc.Sections[0].CharStart.Should().Be(0);
        doc.Sections[0].CharEnd.Should().Be("flat body text".Length);
        doc.Content.Should().Be("flat body text");
    }

    [Fact]
    public void EmptyStructuredElements_AlsoFallsBackToFlatText()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null, System.Array.Empty<ExtractedElement>(), "flat");

        doc.Sections.Should().HaveCount(1);
        doc.Sections[0].Content.Should().Be("flat");
    }
}
