using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Tests.Constants;
using MediatR;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class GetRagQualityReportQueryHandlerTests
{
    [Fact]
    public void Query_ShouldImplementIRequest_WithCorrectResponseType()
    {
        var query = new GetRagQualityReportQuery();
        Assert.IsAssignableFrom<IRequest<RagQualityReportDto>>(query);
    }

    [Fact]
    public void RagQualityReportDto_ShouldHoldAllProperties()
    {
        var breakdown = new RagQualityGameBreakdown(
            GameId: Guid.NewGuid(),
            GameTitle: "Catan",
            ChunkCount: 42,
            RaptorNodeCount: 5,
            EntityRelationCount: 12);

        var status = new RagEnhancementStatusDto(
            Name: "AdaptiveRouting",
            FeatureFlagKey: "rag.enhancement.adaptive-routing",
            FreeEnabled: false,
            NormalEnabled: true,
            PremiumEnabled: true);

        var dto = new RagQualityReportDto(
            TotalIndexedDocuments: 100,
            TotalRaptorSummaries: 50,
            TotalEntityRelations: 200,
            TotalEmbeddedChunks: 1500,
            TopGamesByChunkCount: [breakdown],
            EnhancementStatuses: [status]);

        dto.TotalIndexedDocuments.Should().Be(100);
        dto.TotalRaptorSummaries.Should().Be(50);
        dto.TotalEntityRelations.Should().Be(200);
        dto.TotalEmbeddedChunks.Should().Be(1500);
        dto.TopGamesByChunkCount.Should().ContainSingle();
        dto.EnhancementStatuses.Should().ContainSingle();
        dto.TopGamesByChunkCount[0].GameTitle.Should().Be("Catan");
        dto.EnhancementStatuses[0].Name.Should().Be("AdaptiveRouting");
    }

    [Fact]
    public void RagQualityGameBreakdown_ShouldBeImmutableRecord()
    {
        var a = new RagQualityGameBreakdown(Guid.Empty, "Test", 10, 2, 3);
        var b = new RagQualityGameBreakdown(Guid.Empty, "Test", 10, 2, 3);
        Assert.Equal(a, b); // Records have value equality
    }

    [Fact]
    public void RagEnhancementStatusDto_ShouldBeImmutableRecord()
    {
        var a = new RagEnhancementStatusDto("X", "key", true, false, true);
        var b = new RagEnhancementStatusDto("X", "key", true, false, true);
        b.Should().Be(a);
    }
}
