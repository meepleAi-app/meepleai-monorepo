using Xunit;

namespace Api.Tests;

/// <summary>
/// #3390 — serializes the test classes that assert on the shared <c>MeepleAI.Api</c> Meter via
/// <c>MeterListener</c> for the <c>meepleai.agent.response.grounding</c> counter. A MeterListener
/// is global to the Meter, so two such classes running in parallel capture each other's
/// measurements (captures.ContainSingle() then sees 0 or 2). Grouping them into one
/// non-parallel collection removes that cross-class race (previously a known flake on
/// ChatWithSessionAgentMetricsTests).
/// </summary>
[CollectionDefinition("AgentGroundingMetrics", DisableParallelization = true)]
public sealed class AgentGroundingMetricsCollection
{
}
