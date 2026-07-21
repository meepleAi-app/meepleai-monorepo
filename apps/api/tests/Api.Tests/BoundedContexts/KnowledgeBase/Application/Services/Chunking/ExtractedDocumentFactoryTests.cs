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
