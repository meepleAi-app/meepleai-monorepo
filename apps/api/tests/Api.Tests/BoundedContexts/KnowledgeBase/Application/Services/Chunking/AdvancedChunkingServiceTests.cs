using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

/// <summary>
/// Unit tests for AdvancedChunkingService.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AdvancedChunkingServiceTests
{
    private readonly Mock<ITextChunkingService> _textChunkingServiceMock;
    private readonly ChunkingStrategySelector _strategySelector;
    private readonly Mock<ILogger<AdvancedChunkingService>> _loggerMock;
    private readonly AdvancedChunkingService _service;

    public AdvancedChunkingServiceTests()
    {
        _textChunkingServiceMock = new Mock<ITextChunkingService>();
        _strategySelector = new ChunkingStrategySelector();
        _loggerMock = new Mock<ILogger<AdvancedChunkingService>>();

        _service = new AdvancedChunkingService(
            _textChunkingServiceMock.Object,
            _strategySelector,
            _loggerMock.Object);
    }

    // ── #4782: the page of a chunk comes from the page map, when there is one ────────────────

    // "page one text" (0..13) \n\n "page two text" (15..28) \n\n "page four text" (30..44)
    private const string PagedContent = "page one text\n\npage two text\n\npage four text";
    private static readonly PageSpan[] PagedSpans = [new(1, 0), new(2, 15), new(4, 30)];

    private static ExtractedDocument SingleSectionDocument(IReadOnlyList<PageSpan>? spans) => new()
    {
        Id = Guid.NewGuid(),
        Content = PagedContent,
        PageSpans = spans,
        Sections =
        [
            new() { Content = PagedContent, Page = 1, CharStart = 0, CharEnd = PagedContent.Length },
        ],
    };

    private void SetupTextChunks(params (int Start, string Text)[] pieces)
    {
        _textChunkingServiceMock
            .Setup(x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(pieces.Select((p, i) => new TextChunk
            {
                Text = p.Text, Index = i, CharStart = p.Start, CharEnd = p.Start + p.Text.Length, Page = 1,
            }).ToList());
    }

    [Fact]
    public async Task ChunkDocumentAsync_WithAPageMap_PutsEachChildOnThePageHoldingItsFirstCharacter()
    {
        SetupTextChunks((0, "page one text"), (15, "page two text"), (30, "page four text"));

        var chunks = await _service.ChunkDocumentAsync(SingleSectionDocument(PagedSpans));

        chunks.Where(c => !c.IsRoot).Select(c => c.Metadata.Page).Should().Equal(1, 2, 4);
        chunks.Single(c => c.IsRoot).Metadata.Page.Should().Be(1, "the parent starts on page 1");
    }

    [Fact]
    public async Task ChunkDocumentAsync_WithAPageMap_AChildStartingInTheBlankLineBetweenPages_IsOnThePageOfItsWords()
    {
        // Offset 13 is the first "\n" after page 1: the text of the chunk is on page 2.
        SetupTextChunks((13, "\n\npage two text"));

        var chunks = await _service.ChunkDocumentAsync(SingleSectionDocument(PagedSpans));

        chunks.Single(c => !c.IsRoot).Metadata.Page.Should().Be(2);
    }

    [Fact]
    public async Task ChunkDocumentAsync_WithAPageMap_TheParentTakesThePageWhereItsTextStarts()
    {
        // The first pages had no text: the section starts on page 3, not on the 1 the section carries.
        var document = new ExtractedDocument
        {
            Id = Guid.NewGuid(),
            Content = "first words\n\nsecond words",
            PageSpans = [new(3, 0), new(5, 13)],
            Sections = [new() { Content = "first words\n\nsecond words", Page = 1, CharStart = 0, CharEnd = 25 }],
        };
        SetupTextChunks((0, "first words"), (13, "second words"));

        var chunks = await _service.ChunkDocumentAsync(document);

        chunks.Single(c => c.IsRoot).Metadata.Page.Should().Be(3);
        chunks.Where(c => !c.IsRoot).Select(c => c.Metadata.Page).Should().Equal(3, 5);
    }

    [Fact]
    public async Task ChunkDocumentAsync_WithoutAPageMap_EveryChunkKeepsThePageOfItsSection()
    {
        SetupTextChunks((0, "page one text"), (15, "page two text"), (30, "page four text"));

        var chunks = await _service.ChunkDocumentAsync(SingleSectionDocument(null));

        chunks.Select(c => c.Metadata.Page).Distinct().Should().Equal(1);
    }

    [Fact]
    public async Task ChunkDocumentAsync_WithNullDocument_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _service.ChunkDocumentAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ChunkDocumentAsync_WithSections_CreatesParentChildHierarchy()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = new ExtractedDocument
        {
            Id = documentId,
            Content = "Section 1 content. Section 2 content.",
            Sections = new List<DocumentSection>
            {
                new()
                {
                    Heading = "Introduction",
                    Content = "This is the introduction section with multiple sentences. It contains important information.",
                    Page = 1,
                    ElementType = "text",
                    CharStart = 0,
                    CharEnd = 89
                },
                new()
                {
                    Heading = "Rules",
                    Content = "These are the rules. Follow them carefully.",
                    Page = 2,
                    ElementType = "text",
                    CharStart = 90,
                    CharEnd = 133
                }
            }
        };

        // Setup mock to return child chunks
        _textChunkingServiceMock
            .Setup(x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((string text, int size, int overlap) => new List<TextChunk>
            {
                new() { Text = text, Index = 0, CharStart = 0, CharEnd = text.Length, Page = 1 }
            });

        // Act
        var chunks = await _service.ChunkDocumentAsync(document);

        // Assert
        chunks.Should().NotBeEmpty();

        // Should have parent chunks for each section
        var parentChunks = chunks.Where(c => c.IsRoot).ToList();
        parentChunks.Should().HaveCount(2);

        // Parents should have correct headings
        parentChunks.Should().Contain(c => c.Metadata.Heading == "Introduction");
        parentChunks.Should().Contain(c => c.Metadata.Heading == "Rules");

        // All chunks should reference the document
        chunks.Should().OnlyContain(c => c.Metadata.DocumentId == documentId);
    }

    [Fact]
    public async Task ChunkDocumentAsync_WithoutSections_CreatesParagraphBasedHierarchy()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = new ExtractedDocument
        {
            Id = documentId,
            Content = "First paragraph content.\n\nSecond paragraph content.\n\nThird paragraph content.",
            Sections = new List<DocumentSection>() // No sections
        };

        _textChunkingServiceMock
            .Setup(x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((string text, int size, int overlap) => new List<TextChunk>
            {
                new() { Text = text, Index = 0, CharStart = 0, CharEnd = text.Length, Page = 1 }
            });

        // Act
        var chunks = await _service.ChunkDocumentAsync(document);

        // Assert
        chunks.Should().NotBeEmpty();

        // Should have parent chunks for each paragraph
        var parentChunks = chunks.Where(c => c.IsRoot).ToList();
        parentChunks.Should().HaveCount(3); // 3 paragraphs

        // All parent chunks should be at level 0
        parentChunks.Should().OnlyContain(c => c.Level == 0);
    }

    [Fact]
    public async Task ChunkDocumentAsync_WithCustomConfig_UsesProvidedConfig()
    {
        // Arrange
        var document = new ExtractedDocument
        {
            Id = Guid.NewGuid(),
            Content = "Test content",
            Sections = new List<DocumentSection>
            {
                new() { Content = "Section content", Page = 1, CharStart = 0, CharEnd = 15 }
            }
        };

        var customConfig = new ChunkingConfiguration
        {
            Name = "custom",
            ChunkSizeTokens = 100,
            OverlapPercentage = 0.25
        };

        _textChunkingServiceMock
            .Setup(x => x.ChunkText(
                It.IsAny<string>(),
                customConfig.ChunkSizeChars,
                customConfig.OverlapChars))
            .Returns(new List<TextChunk>());

        // Act
        await _service.ChunkDocumentAsync(document, customConfig);

        // Assert
        _textChunkingServiceMock.Verify(
            x => x.ChunkText(It.IsAny<string>(), customConfig.ChunkSizeChars, customConfig.OverlapChars),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ChunkTextAsync_WithEmptyText_ReturnsEmptyList()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        // Act
        var chunks = await _service.ChunkTextAsync("", documentId);

        // Assert
        chunks.Should().BeEmpty();
    }

    [Fact]
    public async Task ChunkTextAsync_WithWhitespaceText_ReturnsEmptyList()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        // Act
        var chunks = await _service.ChunkTextAsync("   ", documentId);

        // Assert
        chunks.Should().BeEmpty();
    }

    [Fact]
    public async Task ChunkTextAsync_WithValidText_CreatesHierarchy()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var text = "First paragraph.\n\nSecond paragraph.";

        _textChunkingServiceMock
            .Setup(x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((string t, int size, int overlap) => new List<TextChunk>
            {
                new() { Text = t, Index = 0, CharStart = 0, CharEnd = t.Length, Page = 1 }
            });

        // Act
        var chunks = await _service.ChunkTextAsync(text, documentId);

        // Assert
        chunks.Should().NotBeEmpty();
        chunks.Should().OnlyContain(c => c.Metadata.DocumentId == documentId);
    }

    [Fact]
    public async Task ChunkDocumentAsync_ParentChildRelationships_AreCorrect()
    {
        // Arrange
        var document = new ExtractedDocument
        {
            Id = Guid.NewGuid(),
            Content = "Long section content that will be split into multiple child chunks.",
            Sections = new List<DocumentSection>
            {
                new()
                {
                    Heading = "Test Section",
                    Content = "Long section content that will be split into multiple child chunks.",
                    Page = 1,
                    CharStart = 0,
                    CharEnd = 68
                }
            }
        };

        // Mock returns multiple child chunks
        _textChunkingServiceMock
            .Setup(x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new List<TextChunk>
            {
                new() { Text = "Long section content", Index = 0, CharStart = 0, CharEnd = 20, Page = 1 },
                new() { Text = "that will be split", Index = 1, CharStart = 21, CharEnd = 39, Page = 1 },
                new() { Text = "into multiple child chunks", Index = 2, CharStart = 40, CharEnd = 68, Page = 1 }
            });

        // Act
        var chunks = await _service.ChunkDocumentAsync(document);

        // Assert
        var parent = chunks.First(c => c.IsRoot);
        var children = chunks.Where(c => !c.IsRoot).ToList();

        // Parent should have correct number of children
        parent.ChildIds.Should().HaveCount(3);
        parent.HasChildren.Should().BeTrue();

        // All children should reference the parent
        children.Should().OnlyContain(c => c.ParentId == parent.Id);

        // Children should be at correct level
        children.Should().OnlyContain(c => c.Level == 2);
    }

    [Fact]
    public async Task ChunkDocumentAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var document = new ExtractedDocument { Id = Guid.NewGuid(), Content = "Test" };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => _service.ChunkDocumentAsync(document, cancellationToken: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ChunkDocumentAsync_AutoSelectsStrategy_WhenConfigIsNull()
    {
        // Arrange
        var document = new ExtractedDocument
        {
            Id = Guid.NewGuid(),
            Content = "| Col1 | Col2 |\n|------|------|\n| A | B |", // Table content
            Sections = new List<DocumentSection>
            {
                new()
                {
                    Content = "| Col1 | Col2 |\n|------|------|\n| A | B |",
                    ElementType = "table",
                    Page = 1,
                    CharStart = 0,
                    CharEnd = 42
                }
            }
        };

        _textChunkingServiceMock
            .Setup(x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new List<TextChunk>());

        // Act
        await _service.ChunkDocumentAsync(document); // No config provided

        // Assert - Should use Dense strategy for table content
        // Dense config has ChunkSizeChars = 200 * 4 = 800
        _textChunkingServiceMock.Verify(
            x => x.ChunkText(
                It.IsAny<string>(),
                ChunkingConfiguration.Dense.ChunkSizeChars,
                ChunkingConfiguration.Dense.OverlapChars),
            Times.AtLeastOnce);
    }
}
