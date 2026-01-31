using System.Globalization;
using System.Text;
using Api.BoundedContexts.KnowledgeBase.Domain.GridSearch;
using Api.BoundedContexts.KnowledgeBase.Domain.Reports;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.KnowledgeBase.Application.Reports.Services;

/// <summary>
/// ADR-016 Phase 5: Service for generating benchmark reports in markdown format.
/// </summary>
internal interface IReportGeneratorService
{
    /// <summary>
    /// Generates a markdown report from benchmark results.
    /// </summary>
    string GenerateMarkdownReport(BenchmarkReport report);

    /// <summary>
    /// Generates a configuration comparison table.
    /// </summary>
    string GenerateComparisonTable(GridSearchResult result);
}

/// <summary>
/// Implementation of report generator service.
/// </summary>
internal sealed class ReportGeneratorService : IReportGeneratorService
{
    /// <inheritdoc />
    public string GenerateMarkdownReport(BenchmarkReport report)
    {
        var sb = new StringBuilder();

        AppendHeader(sb, report);
        AppendSummary(sb, report);
        AppendTargetStatus(sb, report);
        AppendComparisonTable(sb, report.GridSearchResult);
        AppendBestConfigurations(sb, report.GridSearchResult);
        AppendRecommendations(sb, report);
        AppendFooter(sb, report);

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateComparisonTable(GridSearchResult result)
    {
        var sb = new StringBuilder();
        AppendComparisonTable(sb, result);
        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb, BenchmarkReport report)
    {
        // FIX MA0011: Use IFormatProvider for culture-aware formatting
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {report.Title}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Generated**: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Dataset**: {report.DatasetName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Samples**: {report.SampleCount}");
        sb.AppendLine();
    }

    private static void AppendSummary(StringBuilder sb, BenchmarkReport report)
    {
        var summary = report.Summary;

        sb.AppendLine("## Executive Summary");
        sb.AppendLine();
        // FIX MA0011: Use IFormatProvider for culture-aware formatting
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Configurations Evaluated**: {summary.TotalConfigurations}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Successful Evaluations**: {summary.SuccessfulConfigurations}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Meeting Phase 5 Target**: {summary.ConfigurationsMeetingTarget}");
        sb.AppendLine();
        sb.AppendLine("### Best Metrics Achieved");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Best Recall@10 | {summary.BestRecallAt10:P1} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Best nDCG@10 | {summary.BestNdcgAt10:P1} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Best P95 Latency | {summary.BestP95LatencyMs:F0}ms |");
        sb.AppendLine();
    }

    private static void AppendTargetStatus(StringBuilder sb, BenchmarkReport report)
    {
        var targets = report.Targets;
        var summary = report.Summary;

        sb.AppendLine("## Phase 5 Target Status");
        sb.AppendLine();

        var recallStatus = summary.BestRecallAt10 >= targets.MinRecallAt10 ? "✅" : "❌";
        var latencyStatus = summary.BestP95LatencyMs <= targets.MaxP95LatencyMs ? "✅" : "❌";
        var overallStatus = summary.MeetsPhase5Target ? "✅ **PASSED**" : "❌ **NOT MET**";

        sb.AppendLine("| Target | Required | Achieved | Status |");
        sb.AppendLine("|--------|----------|----------|--------|");
        // FIX MA0011: Use IFormatProvider for culture-aware formatting
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Recall@10 | ≥{targets.MinRecallAt10:P0} | {summary.BestRecallAt10:P1} | {recallStatus} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| P95 Latency | <{targets.MaxP95LatencyMs:F0}ms | {summary.BestP95LatencyMs:F0}ms | {latencyStatus} |");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Overall Status**: {overallStatus}");
        sb.AppendLine();
    }

    private static void AppendComparisonTable(StringBuilder sb, GridSearchResult result)
    {
        sb.AppendLine("## Configuration Comparison");
        sb.AppendLine();
        sb.AppendLine("| Configuration | Chunking | Quantization | Reranking | Recall@5 | Recall@10 | nDCG@10 | MRR | P95 (ms) | Status |");
        sb.AppendLine("|--------------|----------|--------------|-----------|----------|-----------|---------|-----|----------|--------|");

        foreach (var config in result.ConfigurationResults.OrderByDescending(r => r.Metrics.RecallAt10))
        {
            var chunking = config.Configuration.Chunking.DisplayName;
            var quantization = config.Configuration.Quantization.DisplayName;
            var reranking = config.Configuration.Reranking.DisplayName;

            // FIX MA0011: Use IFormatProvider for culture-aware formatting
            if (config.IsSuccess)
            {
                var meetsTarget = config.Metrics.MeetsPhase5Target() ? "✅" : "⚠️";
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {config.Configuration.ConfigurationId} | {chunking} | {quantization} | {reranking} | {config.Metrics.RecallAt5:P1} | {config.Metrics.RecallAt10:P1} | {config.Metrics.NdcgAt10:P1} | {config.Metrics.Mrr:F3} | {config.Metrics.P95LatencyMs:F0} | {meetsTarget} |");
            }
            else
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {config.Configuration.ConfigurationId} | {chunking} | {quantization} | {reranking} | - | - | - | - | - | ❌ Failed |");
            }
        }

        sb.AppendLine();
    }

    private static void AppendBestConfigurations(StringBuilder sb, GridSearchResult result)
    {
        sb.AppendLine("## Best Configurations");
        sb.AppendLine();

        if (result.BestByRecallAt10 != null)
        {
            sb.AppendLine("### Best by Recall@10");
            AppendConfigurationDetails(sb, result.BestByRecallAt10);
        }

        if (result.BestByNdcg != null)
        {
            sb.AppendLine("### Best by nDCG@10");
            AppendConfigurationDetails(sb, result.BestByNdcg);
        }

        if (result.BestByLatency != null)
        {
            sb.AppendLine("### Best by P95 Latency");
            AppendConfigurationDetails(sb, result.BestByLatency);
        }
    }

    private static void AppendConfigurationDetails(StringBuilder sb, ConfigurationResult config)
    {
        var c = config.Configuration;
        sb.AppendLine();
        // FIX MA0011: Use IFormatProvider for culture-aware formatting
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Configuration**: `{c.ConfigurationId}`");
        sb.AppendLine();
        sb.AppendLine("| Setting | Value |");
        sb.AppendLine("|---------|-------|");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Chunking | {c.Chunking.DisplayName} ({c.Chunking.SizeTokens} tokens, {c.Chunking.OverlapPercent:P0} overlap) |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Quantization | {c.Quantization.DisplayName} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Reranking | {c.Reranking.DisplayName} |");
        sb.AppendLine();
        sb.AppendLine("**Metrics**:");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Recall@5: {config.Metrics.RecallAt5:P1}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Recall@10: {config.Metrics.RecallAt10:P1}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- nDCG@10: {config.Metrics.NdcgAt10:P1}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- MRR: {config.Metrics.Mrr:F3}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- P95 Latency: {config.Metrics.P95LatencyMs:F0}ms");
        sb.AppendLine();
    }

    private static void AppendRecommendations(StringBuilder sb, BenchmarkReport report)
    {
        sb.AppendLine("## Recommendations");
        sb.AppendLine();

        var summary = report.Summary;

        // FIX MA0011: Use IFormatProvider for culture-aware formatting
        if (summary.RecommendedConfiguration != null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Recommended Configuration**: `{summary.RecommendedConfiguration}`");
            sb.AppendLine();
        }

        if (summary.MeetsPhase5Target)
        {
            sb.AppendLine("### Phase 5 Target Met ✅");
            sb.AppendLine();
            sb.AppendLine("The evaluation results meet the Phase 5 targets defined in ADR-016:");
            sb.AppendLine();
            sb.AppendLine("- Recall@10 ≥ 70%");
            sb.AppendLine("- P95 Latency < 1.5s");
            sb.AppendLine();
            sb.AppendLine("**Next Steps**:");
            sb.AppendLine();
            sb.AppendLine("1. Deploy recommended configuration to staging");
            sb.AppendLine("2. Monitor metrics via Grafana dashboard");
            sb.AppendLine("3. Run A/B testing with production traffic");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("### Phase 5 Target Not Met ⚠️");
            sb.AppendLine();
            sb.AppendLine("The evaluation results do not yet meet all Phase 5 targets.");
            sb.AppendLine();
            sb.AppendLine("**Improvement Suggestions**:");
            sb.AppendLine();

            if (summary.BestRecallAt10 < report.Targets.MinRecallAt10)
            {
                sb.AppendLine("- **Recall**: Consider enabling reranking, adjusting chunk size, or improving embedding quality");
            }

            if (summary.BestP95LatencyMs > report.Targets.MaxP95LatencyMs)
            {
                sb.AppendLine("- **Latency**: Consider enabling quantization, reducing top-K, or optimizing infrastructure");
            }

            sb.AppendLine();
        }
    }

    private static void AppendFooter(StringBuilder sb, BenchmarkReport report)
    {
        sb.AppendLine("---");
        sb.AppendLine();
        // FIX MA0011: Use IFormatProvider for culture-aware formatting
        sb.AppendLine("*Report generated by ADR-016 Phase 5 Evaluation Framework*");
        sb.AppendLine(CultureInfo.InvariantCulture, $"*Grid Search ID: {report.GridSearchResult.GridSearchId}*");
        sb.AppendLine(CultureInfo.InvariantCulture, $"*Total Duration: {report.GridSearchResult.TotalDurationMs:F0}ms*");
    }
}
