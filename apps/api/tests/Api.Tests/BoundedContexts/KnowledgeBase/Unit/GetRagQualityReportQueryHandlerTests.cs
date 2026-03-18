using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Tests.Constants;
using MediatR;
using Xunit;

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

        Assert.Equal(100, dto.TotalIndexedDocuments);
        Assert.Equal(50, dto.TotalRaptorSummaries);
        Assert.Equal(200, dto.TotalEntityRelations);
        Assert.Equal(1500, dto.TotalEmbeddedChunks);
        Assert.Single(dto.TopGamesByChunkCount);
        Assert.Single(dto.EnhancementStatuses);
        Assert.Equal("Catan", dto.TopGamesByChunkCount[0].GameTitle);
        Assert.Equal("AdaptiveRouting", dto.EnhancementStatuses[0].Name);
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
        Assert.Equal(a, b);
    }
}
