using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;

/// <summary>
/// Unit tests for EvaluationReportFormatter.
/// ADR-016 / #3433: Validates markdown + json report projections, coverage counts, and
/// per-language metric breakdown.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class EvaluationReportFormatterTests
{
    private static EvaluationSampleResult CreateSampleResult(
        string sampleId,
        bool hit = true,
        double reciprocalRank = 1.0,
        double ndcgAt10 = 1.0,
        double answerCorrectness = 0.9,
        double latencyMs = 100.0,
        IReadOnlyList<string>? relevantChunkIds = null)
    {
        return new EvaluationSampleResult
        {
            SampleId = sampleId,
            Question = $"Question for {sampleId}?",
            ExpectedAnswer = "Expected answer",
            RelevantChunkIds = relevantChunkIds ?? ["c1"],
            HitAt5 = hit,
            HitAt10 = hit,
            ReciprocalRank = reciprocalRank,
            DcgAt10 = ndcgAt10,
            IdealDcgAt10 = 1.0,
            AnswerCorrectness = answerCorrectness,
            LatencyMs = latencyMs
        };
    }

    private static EvaluationResult CreateResult(IReadOnlyList<EvaluationSampleResult> sampleResults)
    {
        return EvaluationResult.Create(
            datasetName: "test-dataset",
            configuration: "test-config",
            startedAt: DateTime.UtcNow.AddSeconds(-1),
            completedAt: DateTime.UtcNow,
            sampleResults: sampleResults);
    }

    [Fact]
    public void ToMarkdown_IncludesCoverageAndPerLanguageBreakdown()
    {
        // Arrange
        var sampleResults = new List<EvaluationSampleResult>
        {
            CreateSampleResult("s-en-001"),
            CreateSampleResult("s-it-001")
        };
        var result = CreateResult(sampleResults);

        var metricsEn = EvaluationMetrics.Compute([sampleResults[0]]);
        var metricsIt = EvaluationMetrics.Compute([sampleResults[1]]);
        var byLanguage = new Dictionary<string, EvaluationMetrics>(StringComparer.Ordinal)
        {
            ["en"] = metricsEn,
            ["it"] = metricsIt
        };

        // Act
        var markdown = EvaluationReportFormatter.ToMarkdown(result, byLanguage);

        // Assert
        markdown.Should().Contain("Recall@10");
        markdown.Should().Contain("Labeled");
        markdown.Should().Contain("Unlabeled");
        markdown.Should().Contain("en");
        markdown.Should().Contain("it");
    }

    [Fact]
    public void ToJson_IsValidJsonWithMetrics()
    {
        // Arrange
        var sampleResults = new List<EvaluationSampleResult>
        {
            CreateSampleResult("s-001"),
            CreateSampleResult("s-002", relevantChunkIds: [])
        };
        var result = CreateResult(sampleResults);
        var byLanguage = new Dictionary<string, EvaluationMetrics>(StringComparer.Ordinal)
        {
            ["en"] = EvaluationMetrics.Compute(sampleResults)
        };

        // Act
        var json = EvaluationReportFormatter.ToJson(result, byLanguage);

        // Assert
        var act = () => JsonDocument.Parse(json);
        act.Should().NotThrow();

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.TryGetProperty("metrics", out var metricsElement).Should().BeTrue();
        metricsElement.TryGetProperty("recall_at_10", out _).Should().BeTrue();

        root.TryGetProperty("by_language", out var byLanguageElement).Should().BeTrue();
        byLanguageElement.TryGetProperty("en", out _).Should().BeTrue();
    }

    [Fact]
    public void MetricsByLanguage_GroupsSampleResultsBySampleLanguage()
    {
        // Arrange
        var sampleResults = new List<EvaluationSampleResult>
        {
            CreateSampleResult("s-en-001"),
            CreateSampleResult("s-it-001"),
            CreateSampleResult("s-unk-001")
        };
        var result = CreateResult(sampleResults);

        var dataset = EvaluationDataset.Create("test-dataset", "desc");
        dataset.AddSample(new EvaluationSample
        {
            Id = "s-en-001",
            Question = "Q?",
            ExpectedAnswer = "A",
            Language = "en"
        });
        dataset.AddSample(new EvaluationSample
        {
            Id = "s-it-001",
            Question = "Q?",
            ExpectedAnswer = "A",
            Language = "it"
        });
        dataset.AddSample(new EvaluationSample
        {
            Id = "s-unk-001",
            Question = "Q?",
            ExpectedAnswer = "A",
            Language = null
        });

        // Act
        var byLanguage = EvaluationReportFormatter.MetricsByLanguage(result, dataset);

        // Assert
        byLanguage.Keys.Should().BeEquivalentTo(new[] { "en", "it", "unknown" });
        byLanguage["en"].SampleCount.Should().Be(1);
        byLanguage["it"].SampleCount.Should().Be(1);
        byLanguage["unknown"].SampleCount.Should().Be(1);
    }

    [Fact]
    public void MetricsByLanguage_WithDuplicateSampleIds_DoesNotThrow()
    {
        // Arrange: EvaluationDataset.FromJson does not dedup by id (only Dataset.AddSample
        // enforces uniqueness), so a dataset loaded from a hand-crafted or externally-produced
        // JSON file can contain duplicate ids. MetricsByLanguage must tolerate that (dedup-safe
        // GroupBy + First) instead of crashing via ToDictionary's duplicate-key ArgumentException.
        const string json = """
        {
          "name": "dup-dataset",
          "description": "duplicate id regression",
          "version": "1.0.0",
          "source_type": "meepleai_custom",
          "created_at": "2026-01-01T00:00:00Z",
          "samples": [
            { "id": "dup-1", "question": "Q1?", "expected_answer": "A1", "language": "en" },
            { "id": "dup-1", "question": "Q1 dup?", "expected_answer": "A1 dup", "language": "it" }
          ]
        }
        """;

        var dataset = EvaluationDataset.FromJson(json);
        var sampleResults = new List<EvaluationSampleResult>
        {
            CreateSampleResult("dup-1")
        };
        var result = CreateResult(sampleResults);

        // Act
        var act = () => EvaluationReportFormatter.MetricsByLanguage(result, dataset);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ToMarkdown_IncludesCitationMetrics()
    {
        // Arrange
        var sampleResults = new List<EvaluationSampleResult>
        {
            CreateSampleResult("s-001") with { CitationMatched = true, CitationStructuralValidity = 1.0 }
        };
        var result = CreateResult(sampleResults);
        var byLanguage = new Dictionary<string, EvaluationMetrics>(StringComparer.Ordinal)
        {
            ["en"] = EvaluationMetrics.Compute(sampleResults)
        };

        // Act
        var markdown = EvaluationReportFormatter.ToMarkdown(result, byLanguage);

        // Assert
        markdown.Should().Contain("Citation Accuracy");
        markdown.Should().Contain("Citation Structural Validity");
        markdown.Should().Contain("Cited");
    }

    [Fact]
    public void ToJson_IncludesCitationMetrics()
    {
        // Arrange
        var sampleResults = new List<EvaluationSampleResult>
        {
            CreateSampleResult("s-001") with { CitationMatched = true, CitationStructuralValidity = 1.0 }
        };
        var result = CreateResult(sampleResults);
        var byLanguage = new Dictionary<string, EvaluationMetrics>(StringComparer.Ordinal)
        {
            ["en"] = EvaluationMetrics.Compute(sampleResults)
        };

        // Act
        var json = EvaluationReportFormatter.ToJson(result, byLanguage);

        // Assert
        using var document = JsonDocument.Parse(json);
        var metrics = document.RootElement.GetProperty("metrics");
        metrics.TryGetProperty("citation_accuracy", out _).Should().BeTrue();
        metrics.TryGetProperty("citation_structural_validity", out _).Should().BeTrue();
        metrics.TryGetProperty("cited_sample_count", out _).Should().BeTrue();
    }
}
