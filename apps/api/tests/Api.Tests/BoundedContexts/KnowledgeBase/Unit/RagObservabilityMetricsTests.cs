using Api.Observability;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagObservabilityMetricsTests
{
    [Fact]
    public void RagMetrics_RetrievalQualityMetrics_AreDefinedAndNotNull()
    {
        Assert.NotNull(MeepleAiMetrics.RagRetrievalChunkCount);
        Assert.NotNull(MeepleAiMetrics.RagRetrievalAvgScore);
        Assert.NotNull(MeepleAiMetrics.RagEnhancementActivations);
        Assert.NotNull(MeepleAiMetrics.RagRetrievalFallbacks);
        Assert.NotNull(MeepleAiMetrics.RagCragVerdicts);
        Assert.NotNull(MeepleAiMetrics.RagAdaptiveRoutingDecisions);
        Assert.NotNull(MeepleAiMetrics.RagFusionQueryCount);
    }
}
