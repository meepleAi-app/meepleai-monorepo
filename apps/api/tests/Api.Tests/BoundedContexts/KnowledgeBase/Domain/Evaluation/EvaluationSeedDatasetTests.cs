using System.IO;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Evaluation;

/// <summary>
/// Validates the EN seed evaluation dataset (Issue #3433, Task 7).
/// The seed dataset is intentionally unlabeled (RelevantChunkIds empty) — labels are meant
/// to be filled in later via the labeling-assist workflow (Task 5).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class EvaluationSeedDatasetTests
{
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
        dataset.Samples.Count.Should().BeGreaterThanOrEqualTo(12);
        dataset.Samples.Should().OnlyContain(s => s.Language == "en");
        dataset.Samples.Should().OnlyContain(s => s.RelevantChunkIds.Count == 0);
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
