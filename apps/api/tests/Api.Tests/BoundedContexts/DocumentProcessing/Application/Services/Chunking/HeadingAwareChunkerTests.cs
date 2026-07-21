using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Constants;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

[Trait("Category", TestCategories.Unit)]
public class HeadingAwareChunkerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Real collaborators (all pure/deterministic): TextChunkingService + strategy selector + AdvancedChunkingService.
    private static HeadingAwareChunker CreateChunker()
    {
        var textChunking = new TextChunkingService(NullLogger<TextChunkingService>.Instance);
        var selector = new ChunkingStrategySelector();
        var advanced = new AdvancedChunkingService(textChunking, selector, NullLogger<AdvancedChunkingService>.Instance);
        return new HeadingAwareChunker(advanced, textChunking, NullLogger<HeadingAwareChunker>.Instance);
    }

    [Fact]
    public async Task ChunkAsync_WithTitleElement_ProducesChildrenWithHeading()
    {
        var elements = new List<ExtractedElement>
        {
            new("Preparazione", 1, "Title"),
            new("Disponi le tessere sul tavolo e mescola il mazzo di carte.", 1, "NarrativeText"),
        };
        var chunker = CreateChunker();

        var result = await chunker.ChunkAsync(System.Guid.NewGuid(), null, elements, "flat", Ct);

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(c => c.Heading == "Preparazione");
    }

    [Fact]
    public async Task ChunkAsync_NullElements_ProducesNullHeadingChildren_ContentPreserved()
    {
        var chunker = CreateChunker();
        var flat = "Some flat body text without any structure.";
        var result = await chunker.ChunkAsync(System.Guid.NewGuid(), null, null, flat, Ct);

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(c => c.Heading == null);
        string.Join(" ", result.Select(c => c.Text)).Should().Contain("flat body text");
    }

    [Fact]
    public async Task ChunkAsync_NoChildExceedsMaxEmbeddingChars()
    {
        var longBody = string.Join(" ", Enumerable.Repeat("parola", 1200)); // > 1800 chars, narrative → Sparse (2000)
        var elements = new List<ExtractedElement> { new("Regole", 1, "Title"), new(longBody, 1, "NarrativeText") };
        var chunker = CreateChunker();

        var result = await chunker.ChunkAsync(System.Guid.NewGuid(), null, elements, "flat", Ct);

        result.Should().OnlyContain(c => c.Text.Length <= ChunkingConstants.MaxEmbeddingChars);
    }
}
