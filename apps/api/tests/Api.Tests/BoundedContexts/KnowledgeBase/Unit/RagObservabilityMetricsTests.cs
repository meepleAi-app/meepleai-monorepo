using Api.Observability;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagObservabilityMetricsTests
{
    [Fact]
    public void RagMetrics_RetrievalQualityMetrics_AreDefinedAndNotNull()
    {
        MeepleAiMetrics.RagRetrievalChunkCount.Should().NotBeNull();
        MeepleAiMetrics.RagRetrievalAvgScore.Should().NotBeNull();
        MeepleAiMetrics.RagEnhancementActivations.Should().NotBeNull();
        MeepleAiMetrics.RagRetrievalFallbacks.Should().NotBeNull();
        MeepleAiMetrics.RagCragVerdicts.Should().NotBeNull();
        MeepleAiMetrics.RagAdaptiveRoutingDecisions.Should().NotBeNull();
        MeepleAiMetrics.RagFusionQueryCount.Should().NotBeNull();
    }
}
