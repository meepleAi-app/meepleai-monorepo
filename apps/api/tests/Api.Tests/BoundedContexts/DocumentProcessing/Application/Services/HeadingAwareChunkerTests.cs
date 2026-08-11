using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Unit tests for <see cref="HeadingAwareChunker"/> — the shared helper chaining
/// <see cref="ExtractedDocumentFactory.FromExtraction"/> → <see cref="IAdvancedChunkingService.ChunkDocumentAsync"/>
/// → <see cref="HierarchicalChunkMapper.ToDocumentChunks"/> for all ingest paths (Slice D).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class HeadingAwareChunkerTests
{
    private static readonly Guid DocumentId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();

    private static ChunkMetadata Metadata(
        int page = 1,
        string? heading = "Setup",
        string elementType = "text",
        int charStart = 0,
        int charEnd = 10) => new()
        {
            Page = page,
            Heading = heading,
            ElementType = elementType,
            DocumentId = DocumentId,
            CharStart = charStart,
            CharEnd = charEnd
        };

    [Fact]
    public async Task BuildAsync_MockReturnsParentAndChild_ReturnsMappedChunksWithHierarchy()
    {
        // Arrange
        var parent = HierarchicalChunk.CreateParent("Section content", Metadata(heading: "Setup"));
        var child = HierarchicalChunk.CreateChild(
            "Child sentence",
            level: 2,
            Metadata(heading: "Setup"),
            parent.Id);

        var advancedChunkingMock = new Mock<IAdvancedChunkingService>();
        advancedChunkingMock
            .Setup(s => s.ChunkDocumentAsync(
                It.IsAny<ExtractedDocument>(),
                It.IsAny<ChunkingConfiguration?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HierarchicalChunk> { parent, child });

        var structuredElements = new List<ExtractedElement>
        {
            new("Setup", 1, "Title"),
            new("Disponi le tessere.", 1, "NarrativeText")
        };

        // Act
        var result = await HeadingAwareChunker.BuildAsync(
            structuredElements,
            "ignored flat text",
            DocumentId,
            GameId,
            advancedChunkingMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);

        var mappedParent = result.Single(c => c.Text == "Section content");
        var mappedChild = result.Single(c => c.Text == "Child sentence");

        mappedParent.Level.Should().Be((short)0);
        mappedParent.Heading.Should().Be("Setup");
        mappedParent.ParentChunkId.Should().BeNull();

        mappedChild.Level.Should().Be((short)2);
        mappedChild.ParentChunkId.Should().Be(mappedParent.Id);
    }

    [Fact]
    public async Task BuildAsync_NullStructuredElements_StillReturnsAtLeastOneChunk()
    {
        // Arrange — null structuredElements forces ExtractedDocumentFactory's NullPathDocument
        // fallback (single null-heading preamble section carrying the flat text).
        var fallbackChunk = HierarchicalChunk.CreateParent(
            "flat text fallback",
            Metadata(heading: null, elementType: "text"));

        var advancedChunkingMock = new Mock<IAdvancedChunkingService>();
        advancedChunkingMock
            .Setup(s => s.ChunkDocumentAsync(
                It.IsAny<ExtractedDocument>(),
                It.IsAny<ChunkingConfiguration?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HierarchicalChunk> { fallbackChunk });

        // Act
        var result = await HeadingAwareChunker.BuildAsync(
            null,
            "flat text fallback",
            DocumentId,
            GameId,
            advancedChunkingMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().HaveCountGreaterThanOrEqualTo(1);
        result[0].Heading.Should().BeNull();
    }

    [Fact]
    public async Task BuildAsync_PassesFactoryBuiltExtractedDocument_WithSectionsFromStructuredElements()
    {
        // Arrange — capture the ExtractedDocument the helper hands to ChunkDocumentAsync and
        // assert its Sections were built by ExtractedDocumentFactory (a Title element becomes
        // a section Heading), proving BuildAsync wires the factory output through, not raw text.
        ExtractedDocument? capturedDocument = null;

        var advancedChunkingMock = new Mock<IAdvancedChunkingService>();
        advancedChunkingMock
            .Setup(s => s.ChunkDocumentAsync(
                It.IsAny<ExtractedDocument>(),
                It.IsAny<ChunkingConfiguration?>(),
                It.IsAny<CancellationToken>()))
            .Callback<ExtractedDocument, ChunkingConfiguration?, CancellationToken>(
                (doc, _, _) => capturedDocument = doc)
            .ReturnsAsync(new List<HierarchicalChunk>
            {
                HierarchicalChunk.CreateParent("Preparazione content", Metadata(heading: "Preparazione"))
            });

        var structuredElements = new List<ExtractedElement>
        {
            new("Preparazione", 1, "Title"),
            new("Disponi le tessere.", 1, "NarrativeText")
        };

        // Act
        await HeadingAwareChunker.BuildAsync(
            structuredElements,
            "ignored flat text",
            DocumentId,
            GameId,
            advancedChunkingMock.Object,
            CancellationToken.None);

        // Assert
        capturedDocument.Should().NotBeNull();
        capturedDocument!.Id.Should().Be(DocumentId);
        capturedDocument.GameId.Should().Be(GameId);
        capturedDocument.Sections.Should().ContainSingle();
        capturedDocument.Sections[0].Heading.Should().Be("Preparazione");
    }

    [Fact]
    public async Task BuildAsync_DropsFragmentChunks_KeepsSubstantiveOnes()
    {
        // #3269 fragment filter: decorative/vertical-text artefacts unstructured emits as bogus Title
        // elements ("A N", "N", "I L E X Y R F", digit-only) must be dropped — a query token like "N"
        // matches such micro-chunks with a high ts_rank and buries the real section chunk.
        var substantive = HierarchicalChunk.CreateParent(
            "6 PREPARAZIONE Di seguito viene descritta la preparazione per il gioco da 2 a 5 giocatori.",
            Metadata(heading: "Preparazione"));
        var fragAN = HierarchicalChunk.CreateParent("A N", Metadata(heading: "A N"));
        var fragN = HierarchicalChunk.CreateParent("N", Metadata(heading: "N"));
        var fragVertical = HierarchicalChunk.CreateParent("I L E X Y R F", Metadata(heading: null));
        var fragDigits = HierarchicalChunk.CreateParent("12 34 56 78", Metadata(heading: null));

        var advancedChunkingMock = new Mock<IAdvancedChunkingService>();
        advancedChunkingMock
            .Setup(s => s.ChunkDocumentAsync(
                It.IsAny<ExtractedDocument>(),
                It.IsAny<ChunkingConfiguration?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HierarchicalChunk> { substantive, fragAN, fragN, fragVertical, fragDigits });

        // Act
        var result = await HeadingAwareChunker.BuildAsync(
            new List<ExtractedElement> { new("Preparazione", 1, "Title") },
            "flat text",
            DocumentId,
            GameId,
            advancedChunkingMock.Object,
            CancellationToken.None);

        // Assert — only the substantive chunk survives.
        result.Should().ContainSingle();
        result[0].Text.Should().Contain("PREPARAZIONE");
    }

    [Theory]
    [InlineData("A N", false)]
    [InlineData("N", false)]
    [InlineData("I L E X Y R F", false)]
    [InlineData("12 34 56 78", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("A. B. C.", false)]   // single letters + punctuation, no 3-letter run
    [InlineData("AZIONI", true)]
    [InlineData("Preparazione", true)]
    [InlineData("Da 2 a 5 giocatori", true)]
    [InlineData("6 PREPARAZIONE Di seguito", true)]
    public void IsSubstantial_ClassifiesFragmentsVsRealWords(string text, bool expected)
    {
        var chunk = new DocumentChunk { Id = Guid.NewGuid(), Text = text };
        HeadingAwareChunker.IsSubstantial(chunk).Should().Be(expected);
    }
}
