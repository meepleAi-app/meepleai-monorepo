using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #3620 (code review follow-up) — serializes the test classes that assert on the
/// shared <c>meepleai.cover.resolution.total</c> counter via <c>MeterListener</c>
/// (mirrors the established pattern in <c>AgentGroundingMetricsCollection</c> /
/// <c>GamebookMeterCollection</c>). A <c>MeterListener</c> is global to the process-wide
/// <c>MeepleAiMetrics</c> Meter, so two such classes running in parallel collections
/// (the <c>xunit.runner.json</c> default: <c>parallelizeTestCollections: true</c>,
/// <c>maxParallelThreads: 4</c>) capture each other's measurements — a
/// <c>ContainSingle()</c>/<c>HaveCount(1)</c> assertion then intermittently sees
/// extra entries from a concurrently-running class. <c>CoverUrlResolverPresignExpiryTests</c>
/// exercises <c>CoverUrlResolver.Resolve*</c> (and therefore emits the same metric) without
/// itself asserting on it, so it still needs to join this collection to stop polluting
/// <c>CoverUrlResolverTests</c>'s captures.
/// </summary>
[CollectionDefinition("CoverResolutionMetrics", DisableParallelization = true)]
public sealed class CoverResolutionMetricsCollection
{
}
