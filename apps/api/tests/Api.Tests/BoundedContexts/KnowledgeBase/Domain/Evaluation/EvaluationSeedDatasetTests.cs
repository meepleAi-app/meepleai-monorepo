using System.IO;
using System.Linq;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Evaluation;

/// <summary>
/// Validates the EN golden evaluation dataset (Issue #3433 Task 7, extended by #3467).
/// The dataset is intentionally recall-unlabeled (RelevantChunkIds empty) — chunk-id labels are
/// filled in via the labeling-assist workflow after the #3427 re-index. Page-level expected_citations
/// (#3467) are present now because pages are stable across re-index.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class EvaluationSeedDatasetTests
{
    private static readonly string[] GoldenGames = ["catan", "wingspan", "dominion", "ark-nova", "7-wonders"];

    [Fact]
    public void EnSeed_LoadsAsValidLanguageTaggedDataset()
    {
        // Arrange
        var filePath = Path.Combine(GetEvaluationDatasetsPath(), "meepleai-en-seed.json");
        File.Exists(filePath).Should().BeTrue(
            $"the EN seed dataset should exist at '{filePath}'");

        var json = File.ReadAllText(filePath);

        // Act
        var dataset = EvaluationDataset.FromJson(json);

        // Assert
        dataset.Samples.Count.Should().BeGreaterThanOrEqualTo(30,
            "the golden set must meet the >=30 baseline sample requirement (#3467)");
        dataset.Samples.Should().OnlyContain(s => s.Language == "en");
        dataset.Samples.Should().OnlyContain(s => s.RelevantChunkIds.Count == 0,
            "recall labeling is deferred to the labeling-assist workflow after the #3427 re-index");
    }

    [Fact]
    public void EnSeed_CoversAllFiveGoldenGames()
    {
        var filePath = Path.Combine(GetEvaluationDatasetsPath(), "meepleai-en-seed.json");
        var dataset = EvaluationDataset.FromJson(File.ReadAllText(filePath));

        var games = dataset.Samples.Select(s => s.GameId).Distinct().ToList();
        games.Should().Contain(GoldenGames);
    }

    [Fact]
    public void EnSeed_HasNonDegenerateCitationGradedCoverage()
    {
        var filePath = Path.Combine(GetEvaluationDatasetsPath(), "meepleai-en-seed.json");
        var dataset = EvaluationDataset.FromJson(File.ReadAllText(filePath));

        var citationGraded = dataset.Samples.Where(s => s.ExpectedCitations is not null).ToList();

        citationGraded.Should().HaveCountGreaterThanOrEqualTo(20,
            "the golden set must expose a non-degenerate citation-accuracy signal (#3467)");
        citationGraded.Should().OnlyContain(s => s.ExpectedCitations!.PrimaryPages.Count > 0,
            "a citation-graded sample must name at least one expected page");
    }

    private static string GetEvaluationDatasetsPath()
    {
        // Navigate from test output directory to repo root/tests/evaluation-datasets
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "evaluation-datasets")))
        {
            dir = dir.Parent;
        }

        return dir != null
            ? Path.Combine(dir.FullName, "tests", "evaluation-datasets")
            : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "tests", "evaluation-datasets");
    }
}
