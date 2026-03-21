using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Projections;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Integration;

[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public class CopyrightTierPipelineTests
{
    [Fact]
    public async Task FullPipeline_MixedTiers_ProducesCorrectCitationsJson()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var projection = new Mock<ICopyrightDataProjection>();

        var ccInfo = new PdfCopyrightInfo("doc-cc", LicenseType.CreativeCommons,
            DocumentCategory.Rulebook, Guid.NewGuid(), gameId, null, true);
        var protectedInfo = new PdfCopyrightInfo("doc-protected", LicenseType.Copyrighted,
            DocumentCategory.Rulebook, Guid.NewGuid(), gameId, null, false);

        projection.Setup(p => p.GetPdfCopyrightInfoAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, PdfCopyrightInfo>
            {
                ["doc-cc"] = ccInfo,
                ["doc-protected"] = protectedInfo
            });
        projection.Setup(p => p.CheckOwnershipAsync(userId, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, bool> { [gameId] = false });

        var resolver = new CopyrightTierResolver(projection.Object);
        var citations = new List<ChunkCitation>
        {
            new("doc-cc", 5, 0.95f, "CC licensed text"),
            new("doc-protected", 14, 0.88f, "Copyrighted rulebook text")
        };

        // Act
        var resolved = await resolver.ResolveAsync(citations, userId, CancellationToken.None);

        // Assert tiers
        Assert.Equal(CopyrightTier.Full, resolved[0].CopyrightTier);
        Assert.Equal(CopyrightTier.Protected, resolved[1].CopyrightTier);
        Assert.True(resolved[0].IsPublic);
        Assert.False(resolved[1].IsPublic);

        // Assert CitationsJson serialization roundtrip
        var json = JsonSerializer.Serialize(resolved);
        Assert.Contains("\"CopyrightTier\":1", json); // Full = 1
        Assert.Contains("\"CopyrightTier\":0", json); // Protected = 0
    }

    [Fact]
    public void ParaphraseExtraction_WithMarker_PopulatesSnippet()
    {
        var responseText = "The game works like this. [ref:doc-1:14] Players take turns placing tokens on the board. [ref:doc-1:22] Next phase begins.";
        var originalSnippet = "During the construction phase each player may place settlements";

        var result = ParaphraseExtractor.Extract(responseText, "doc-1", 14, originalSnippet);

        Assert.NotNull(result);
        Assert.Contains("Players take turns", result);
    }

    [Fact]
    public void ParaphraseExtraction_FallbackNoMarker_ReturnsNull_NotOriginalText()
    {
        var responseText = "A general answer with no markers.";
        var originalSnippet = "The original copyrighted text that must not leak";

        var result = ParaphraseExtractor.Extract(responseText, "doc-1", 14, originalSnippet);

        Assert.Null(result);
    }

    [Fact]
    public void FormatChunkForPrompt_IncludesCopyrightAnnotation()
    {
        var fullCitation = new ChunkCitation("doc-1", 14, 0.92f, "text", CopyrightTier.Full);
        var protectedCitation = new ChunkCitation("doc-2", 22, 0.88f, "text", CopyrightTier.Protected);

        var fullFormatted = RagPromptAssemblyService.FormatChunkForPrompt(fullCitation, "chunk text");
        var protectedFormatted = RagPromptAssemblyService.FormatChunkForPrompt(protectedCitation, "chunk text");

        Assert.Contains("Copyright: FULL", fullFormatted);
        Assert.Contains("Copyright: PROTECTED", protectedFormatted);
    }
}
