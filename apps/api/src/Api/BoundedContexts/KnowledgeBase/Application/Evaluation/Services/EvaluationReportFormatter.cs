using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;

/// <summary>
/// Formats <see cref="EvaluationResult"/> instances into human-readable (Markdown) and
/// machine-readable (JSON, snake_case) reports, including coverage counts (labeled vs
/// unlabeled samples) and a per-language metrics breakdown.
/// </summary>
internal static class EvaluationReportFormatter
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Groups <paramref name="result"/>'s sample results by the language of the owning sample
    /// in <paramref name="dataset"/> (joined by <see cref="EvaluationSampleResult.SampleId"/> ==
    /// <see cref="EvaluationSample.Id"/>), using <c>"unknown"</c> when the sample's
    /// <see cref="EvaluationSample.Language"/> is null. Metrics per language are computed via
    /// <see cref="EvaluationMetrics.Compute"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, EvaluationMetrics> MetricsByLanguage(
        EvaluationResult result,
        EvaluationDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(dataset);

        // GroupBy + First (rather than ToDictionary directly) tolerates dataset files with
        // duplicate sample ids (EvaluationDataset.FromJson does not dedup on load) instead of
        // throwing ArgumentException on the second occurrence.
        var languageBySampleId = dataset.Samples
            .GroupBy(s => s.Id, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => string.IsNullOrEmpty(g.First().Language) ? "unknown" : g.First().Language,
                StringComparer.Ordinal);

        var byLanguage = new Dictionary<string, EvaluationMetrics>(StringComparer.Ordinal);

        var grouped = result.SampleResults
            .GroupBy(r => languageBySampleId.TryGetValue(r.SampleId, out var language) ? language : "unknown", StringComparer.Ordinal);

        foreach (var group in grouped)
        {
            byLanguage[group.Key] = EvaluationMetrics.Compute(group.ToList());
        }

        return byLanguage;
    }

    /// <summary>
    /// Renders a Markdown report: a header table with recall@5/@10, nDCG@10, MRR, P95 latency,
    /// answer-correctness, and labeled/unlabeled coverage counts, followed by a per-language
    /// metrics section.
    /// </summary>
    public static string ToMarkdown(EvaluationResult result, IReadOnlyDictionary<string, EvaluationMetrics> byLanguage)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(byLanguage);

        var metrics = result.Metrics;
        var sb = new StringBuilder();

        sb.Append("# Evaluation Report: ").AppendLine(result.DatasetName);
        sb.AppendLine();
        sb.Append("Configuration: `").Append(result.Configuration).AppendLine("`");
        sb.AppendLine();

        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("| --- | --- |");
        AppendMetricRow(sb, "Recall@5", metrics.RecallAt5);
        AppendMetricRow(sb, "Recall@10", metrics.RecallAt10);
        AppendMetricRow(sb, "nDCG@10", metrics.NdcgAt10);
        AppendMetricRow(sb, "MRR", metrics.Mrr);
        AppendMetricRow(sb, "P95 Latency (ms)", metrics.P95LatencyMs);
        AppendMetricRow(sb, "Answer Correctness", metrics.AnswerCorrectness);
        sb.Append("| Sample Count | ").Append(metrics.SampleCount.ToString(CultureInfo.InvariantCulture)).AppendLine(" |");
        sb.Append("| Labeled | ").Append(metrics.LabeledSampleCount.ToString(CultureInfo.InvariantCulture)).AppendLine(" |");
        sb.Append("| Unlabeled | ").Append(metrics.UnlabeledSampleCount.ToString(CultureInfo.InvariantCulture)).AppendLine(" |");
        sb.AppendLine();

        sb.AppendLine("## Metrics by Language");
        sb.AppendLine();
        sb.AppendLine("| Language | Recall@5 | Recall@10 | nDCG@10 | MRR | Answer Correctness | Labeled | Unlabeled |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");

        foreach (var (language, languageMetrics) in byLanguage.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
        {
            sb.Append("| ").Append(language)
              .Append(" | ").Append(FormatDouble(languageMetrics.RecallAt5))
              .Append(" | ").Append(FormatDouble(languageMetrics.RecallAt10))
              .Append(" | ").Append(FormatDouble(languageMetrics.NdcgAt10))
              .Append(" | ").Append(FormatDouble(languageMetrics.Mrr))
              .Append(" | ").Append(FormatDouble(languageMetrics.AnswerCorrectness))
              .Append(" | ").Append(languageMetrics.LabeledSampleCount.ToString(CultureInfo.InvariantCulture))
              .Append(" | ").Append(languageMetrics.UnlabeledSampleCount.ToString(CultureInfo.InvariantCulture))
              .AppendLine(" |");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Renders a snake_case JSON projection:
    /// <c>{ dataset, metrics{...}, coverage{ labeled, unlabeled }, by_language{ &lt;lang&gt;: {...} } }</c>.
    /// </summary>
    public static string ToJson(EvaluationResult result, IReadOnlyDictionary<string, EvaluationMetrics> byLanguage)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(byLanguage);

        var projection = new ReportProjection
        {
            Dataset = result.DatasetName,
            Metrics = ToMetricsProjection(result.Metrics),
            Coverage = new CoverageProjection
            {
                Labeled = result.Metrics.LabeledSampleCount,
                Unlabeled = result.Metrics.UnlabeledSampleCount
            },
            ByLanguage = byLanguage.ToDictionary(
                kvp => kvp.Key,
                kvp => ToMetricsProjection(kvp.Value),
                StringComparer.Ordinal)
        };

        return JsonSerializer.Serialize(projection, s_jsonOptions);
    }

    private static void AppendMetricRow(StringBuilder sb, string label, double value)
    {
        sb.Append("| ").Append(label).Append(" | ").Append(FormatDouble(value)).AppendLine(" |");
    }

    private static string FormatDouble(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static MetricsProjection ToMetricsProjection(EvaluationMetrics metrics) => new()
    {
        RecallAt5 = metrics.RecallAt5,
        RecallAt10 = metrics.RecallAt10,
        NdcgAt10 = metrics.NdcgAt10,
        Mrr = metrics.Mrr,
        P95LatencyMs = metrics.P95LatencyMs,
        AnswerCorrectness = metrics.AnswerCorrectness,
        SampleCount = metrics.SampleCount,
        LabeledSampleCount = metrics.LabeledSampleCount,
        UnlabeledSampleCount = metrics.UnlabeledSampleCount
    };

    private sealed class ReportProjection
    {
        public string Dataset { get; init; } = string.Empty;
        public MetricsProjection Metrics { get; init; } = new();
        public CoverageProjection Coverage { get; init; } = new();
        public Dictionary<string, MetricsProjection> ByLanguage { get; init; } = new(StringComparer.Ordinal);
    }

    private sealed class MetricsProjection
    {
        // JsonPropertyName overrides are required because JsonNamingPolicy.SnakeCaseLower does not
        // insert a separator before a trailing digit (e.g. "RecallAt10" -> "recall_at10", not
        // "recall_at_10"), but the documented/consumed report key is "recall_at_10".
        [JsonPropertyName("recall_at_5")]
        public double RecallAt5 { get; init; }

        [JsonPropertyName("recall_at_10")]
        public double RecallAt10 { get; init; }

        [JsonPropertyName("ndcg_at_10")]
        public double NdcgAt10 { get; init; }

        public double Mrr { get; init; }

        [JsonPropertyName("p95_latency_ms")]
        public double P95LatencyMs { get; init; }

        public double AnswerCorrectness { get; init; }
        public int SampleCount { get; init; }
        public int LabeledSampleCount { get; init; }
        public int UnlabeledSampleCount { get; init; }
    }

    private sealed class CoverageProjection
    {
        public int Labeled { get; init; }
        public int Unlabeled { get; init; }
    }
}
