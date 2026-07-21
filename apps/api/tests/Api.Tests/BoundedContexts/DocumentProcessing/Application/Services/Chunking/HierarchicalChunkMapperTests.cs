using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

[Trait("Category", TestCategories.Unit)]
public class HierarchicalChunkMapperTests
{
    private static HierarchicalChunk Parent(string heading, string content, int page = 1) =>
        HierarchicalChunk.CreateParent(content, new ChunkMetadata { Page = page, Heading = heading, ElementType = "heading", CharStart = 0, CharEnd = content.Length, DocumentId = System.Guid.NewGuid() });

    private static HierarchicalChunk Child(string parentId, string content, string? heading, int page, int charStart) =>
        HierarchicalChunk.CreateChild(content, level: 2, new ChunkMetadata { Page = page, Heading = heading, ElementType = "text", CharStart = charStart, CharEnd = charStart + content.Length, DocumentId = System.Guid.NewGuid() }, parentId);

    [Fact]
    public void ToChildDocumentChunks_KeepsOnlyChildren_InheritsHeading()
    {
        var parent = Parent("Preparazione", "Preparazione body");
        var c1 = Child(parent.Id, "Disponi le tessere.", "Preparazione", 1, 0);
        var c2 = Child(parent.Id, "Mescola il mazzo.", "Preparazione", 1, 20);

        var result = HierarchicalChunkMapper.ToChildDocumentChunks(new[] { parent, c1, c2 });

        result.Should().HaveCount(2); // parent excluded
        result[0].Text.Should().Be("Disponi le tessere.");
        result[0].Heading.Should().Be("Preparazione");
        result[0].Level.Should().Be(2);
        result[0].ElementType.Should().Be("text");
        result[0].ParentChunkId.Should().BeNull();
    }

    [Fact]
    public void ToChildDocumentChunks_NullHeadingPreamble_Preserved()
    {
        var parent = Parent(null!, "intro");
        var c = Child(parent.Id, "intro text", null, 1, 0);
        var result = HierarchicalChunkMapper.ToChildDocumentChunks(new[] { parent, c });
        result.Should().ContainSingle();
        result[0].Heading.Should().BeNull();
    }

    [Fact]
    public void ToChildDocumentChunks_OnlyParent_NoChildren_ReturnsEmpty()
    {
        // HierarchicalChunk forbids empty content, so the parent must carry text; the mapper still
        // returns empty because the sole chunk IsRoot and roots are skipped.
        var parent = Parent("Empty", "section body");
        HierarchicalChunkMapper.ToChildDocumentChunks(new[] { parent }).Should().BeEmpty();
    }

    [Fact]
    public void ToChildDocumentChunks_MultiPageSection_RecomputesPagePerChild()
    {
        var parent = Parent("Long", "long section body");
        // two children of the same section at very different char offsets → different pages
        var c1 = Child(parent.Id, "early text", "Long", page: 1, charStart: 10);
        var c2 = Child(parent.Id, "late text", "Long", page: 1, charStart: 5000);

        var result = HierarchicalChunkMapper.ToChildDocumentChunks(new[] { parent, c1, c2 });

        result[0].Page.Should().Be(1);           // 10 / 2000 + 1
        result[1].Page.Should().Be(3);           // 5000 / 2000 + 1  (not collapsed to the section's page 1)
    }
}
