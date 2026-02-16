using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase.Evaluation;

/// <summary>
/// Unit tests for EvaluationDataset domain model and JSON serialization (Issue #4278).
/// Validates dataset creation, validation rules, filtering, merging, and round-trip serialization.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4278")]
public sealed class EvaluationDatasetTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidParameters_ShouldCreateEmptyDataset()
    {
        // Act
        var dataset = EvaluationDataset.Create("Test Dataset", "A test dataset", "mozilla");

        // Assert
        dataset.Name.Should().Be("Test Dataset");
        dataset.Description.Should().Be("A test dataset");
        dataset.SourceType.Should().Be("mozilla");
        dataset.Version.Should().Be("1.0.0");
        dataset.Count.Should().Be(0);
        dataset.Samples.Should().BeEmpty();
    }

    [Fact]
    public void Create_DefaultSourceType_ShouldBeMeepleAiCustom()
    {
        // Act
        var dataset = EvaluationDataset.Create("Test", "desc");

        // Assert
        dataset.SourceType.Should().Be("meepleai_custom");
    }

    #endregion

    #region AddSample Tests

    [Fact]
    public void AddSample_WithValidSample_ShouldIncrementCount()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "desc");
        var sample = CreateSample("s-001");

        // Act
        dataset.AddSample(sample);

        // Assert
        dataset.Count.Should().Be(1);
        dataset.Samples.Should().ContainSingle().Which.Id.Should().Be("s-001");
    }

    [Fact]
    public void AddSample_DuplicateId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "desc");
        dataset.AddSample(CreateSample("s-001"));

        // Act
        var act = () => dataset.AddSample(CreateSample("s-001"));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public void AddSample_NullSample_ShouldThrowArgumentNullException()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "desc");

        // Act
        var act = () => dataset.AddSample(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddSamples_MultipleSamples_ShouldAddAll()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "desc");
        var samples = Enumerable.Range(1, 5)
            .Select(i => CreateSample($"s-{i:D3}"))
            .ToList();

        // Act
        dataset.AddSamples(samples);

        // Assert
        dataset.Count.Should().Be(5);
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WithLessThan30Samples_ShouldReturnError()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "desc");
        for (var i = 0; i < 29; i++)
        {
            dataset.AddSample(CreateSample($"s-{i:D3}"));
        }

        // Act
        var (isValid, errors) = dataset.Validate();

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("at least 30 samples"));
    }

    [Fact]
    public void Validate_With30OrMoreSamples_ShouldBeValid()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "desc");
        for (var i = 0; i < 30; i++)
        {
            dataset.AddSample(CreateSample($"s-{i:D3}"));
        }

        // Act
        var (isValid, errors) = dataset.Validate();

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldReturnError()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("", "desc");
        for (var i = 0; i < 30; i++)
        {
            dataset.AddSample(CreateSample($"s-{i:D3}"));
        }

        // Act
        var (isValid, errors) = dataset.Validate();

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("name is required"));
    }

    [Fact]
    public void Validate_WithEmptyQuestions_ShouldReturnError()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "desc");
        for (var i = 0; i < 30; i++)
        {
            dataset.AddSample(new EvaluationSample
            {
                Id = $"s-{i:D3}",
                Question = i == 0 ? "" : $"Question {i}?",
                ExpectedAnswer = $"Answer {i}"
            });
        }

        // Act
        var (isValid, errors) = dataset.Validate();

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("empty questions"));
    }

    [Fact]
    public void Validate_WithEmptyAnswers_ShouldReturnError()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "desc");
        for (var i = 0; i < 30; i++)
        {
            dataset.AddSample(new EvaluationSample
            {
                Id = $"s-{i:D3}",
                Question = $"Question {i}?",
                ExpectedAnswer = i == 0 ? "" : $"Answer {i}"
            });
        }

        // Act
        var (isValid, errors) = dataset.Validate();

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("empty expected answers"));
    }

    #endregion

    #region Filter Tests

    [Fact]
    public void GetByDifficulty_ShouldReturnMatchingSamples()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "desc");
        dataset.AddSample(CreateSample("s-001", difficulty: "easy"));
        dataset.AddSample(CreateSample("s-002", difficulty: "medium"));
        dataset.AddSample(CreateSample("s-003", difficulty: "hard"));
        dataset.AddSample(CreateSample("s-004", difficulty: "easy"));

        // Act
        var easyOnes = dataset.GetByDifficulty("easy");

        // Assert
        easyOnes.Should().HaveCount(2);
        easyOnes.Should().OnlyContain(s => s.Difficulty == "easy");
    }

    [Fact]
    public void GetByCategory_ShouldReturnMatchingSamples()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "desc");
        dataset.AddSample(CreateSample("s-001", category: "setup"));
        dataset.AddSample(CreateSample("s-002", category: "gameplay"));
        dataset.AddSample(CreateSample("s-003", category: "scoring"));

        // Act
        var gameplaySamples = dataset.GetByCategory("gameplay");

        // Assert
        gameplaySamples.Should().ContainSingle().Which.Category.Should().Be("gameplay");
    }

    [Fact]
    public void GetByGameId_ShouldReturnMatchingSamples()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "desc");
        dataset.AddSample(CreateSample("s-001", gameId: "catan"));
        dataset.AddSample(CreateSample("s-002", gameId: "catan"));
        dataset.AddSample(CreateSample("s-003", gameId: "pandemic"));

        // Act
        var catanSamples = dataset.GetByGameId("catan");

        // Assert
        catanSamples.Should().HaveCount(2);
    }

    [Fact]
    public void GetBySource_ShouldReturnMatchingSamples()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "desc");
        dataset.AddSample(CreateSample("s-001", datasetSource: "mozilla"));
        dataset.AddSample(CreateSample("s-002", datasetSource: "meepleai_custom"));

        // Act
        var mozillaSamples = dataset.GetBySource("mozilla");

        // Assert
        mozillaSamples.Should().ContainSingle().Which.DatasetSource.Should().Be("mozilla");
    }

    #endregion

    #region Merge Tests

    [Fact]
    public void Merge_CombinesTwoDatasets_ShouldSetSourceTypeToCombined()
    {
        // Arrange
        var mozilla = EvaluationDataset.Create("Mozilla", "desc", "mozilla");
        mozilla.AddSample(CreateSample("moz-001"));
        mozilla.AddSample(CreateSample("moz-002"));

        var custom = EvaluationDataset.Create("Custom", "desc", "meepleai_custom");
        custom.AddSample(CreateSample("ma-001"));

        // Act
        mozilla.Merge(custom);

        // Assert
        mozilla.Count.Should().Be(3);
        mozilla.SourceType.Should().Be("combined");
    }

    [Fact]
    public void Merge_WithDuplicateIds_ShouldSkipDuplicates()
    {
        // Arrange
        var ds1 = EvaluationDataset.Create("DS1", "desc");
        ds1.AddSample(CreateSample("shared-001"));
        ds1.AddSample(CreateSample("ds1-only"));

        var ds2 = EvaluationDataset.Create("DS2", "desc");
        ds2.AddSample(CreateSample("shared-001"));
        ds2.AddSample(CreateSample("ds2-only"));

        // Act
        ds1.Merge(ds2);

        // Assert
        ds1.Count.Should().Be(3); // shared-001, ds1-only, ds2-only
    }

    #endregion

    #region JSON Serialization Tests

    [Fact]
    public void ToJson_FromJson_ShouldRoundTrip()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Round Trip Test", "Testing JSON round-trip", "mozilla");
        dataset.AddSample(EvaluationSample.FromMozilla(
            "rt-001",
            "How many settlements in Catan?",
            "Each player starts with 5 settlements.",
            "catan-rulebook",
            "Components"));
        dataset.AddSample(EvaluationSample.FromMeepleAI(
            "rt-002",
            "What resources for a road?",
            "One brick and one lumber.",
            "catan",
            7,
            "easy",
            "gameplay",
            ["brick", "lumber"]));

        // Act
        var json = dataset.ToJson();
        var restored = EvaluationDataset.FromJson(json);

        // Assert
        restored.Name.Should().Be("Round Trip Test");
        restored.Description.Should().Be("Testing JSON round-trip");
        restored.SourceType.Should().Be("mozilla");
        restored.Count.Should().Be(2);

        var sample1 = restored.Samples[0];
        sample1.Id.Should().Be("rt-001");
        sample1.Question.Should().Be("How many settlements in Catan?");
        sample1.DatasetSource.Should().Be("mozilla");

        var sample2 = restored.Samples[1];
        sample2.Id.Should().Be("rt-002");
        sample2.GameId.Should().Be("catan");
        sample2.ExpectedKeywords.Should().Contain("brick");
        sample2.ExpectedKeywords.Should().Contain("lumber");
    }

    [Fact]
    public void FromJson_InvalidJson_ShouldThrowInvalidOperationException()
    {
        // Act
        var act = () => EvaluationDataset.FromJson("not valid json");

        // Assert
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void FromJson_EmptyDataset_ShouldCreateEmptyDataset()
    {
        // Arrange
        var json = """
        {
            "name": "Empty",
            "description": "No samples",
            "version": "1.0.0",
            "source_type": "test",
            "created_at": "2026-02-16T00:00:00Z",
            "samples": []
        }
        """;

        // Act
        var dataset = EvaluationDataset.FromJson(json);

        // Assert
        dataset.Name.Should().Be("Empty");
        dataset.Count.Should().Be(0);
    }

    #endregion

    #region Dataset File Loading Tests

    [Fact]
    public void LoadMozillaDataset_ShouldDeserializeCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(GetEvaluationDatasetsPath(), "mozilla-boardgames.json");
        if (!File.Exists(filePath))
        {
            // Skip if running from a different working directory
            return;
        }

        var json = File.ReadAllText(filePath);

        // Act
        var dataset = EvaluationDataset.FromJson(json);

        // Assert
        dataset.Name.Should().Be("Mozilla Board Games Structured QA");
        dataset.SourceType.Should().Be("mozilla");
        dataset.Count.Should().Be(25);
        dataset.Samples.Should().OnlyContain(s => s.DatasetSource == "mozilla");
    }

    [Fact]
    public void LoadMeepleAiDataset_ShouldDeserializeCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(GetEvaluationDatasetsPath(), "meepleai-custom.json");
        if (!File.Exists(filePath))
        {
            return;
        }

        var json = File.ReadAllText(filePath);

        // Act
        var dataset = EvaluationDataset.FromJson(json);

        // Assert
        dataset.Name.Should().Be("MeepleAI Custom Board Game QA");
        dataset.SourceType.Should().Be("meepleai_custom");
        dataset.Count.Should().Be(35);
    }

    [Fact]
    public void LoadAndMergeBothDatasets_ShouldPassValidation()
    {
        // Arrange
        var mozillaPath = Path.Combine(GetEvaluationDatasetsPath(), "mozilla-boardgames.json");
        var customPath = Path.Combine(GetEvaluationDatasetsPath(), "meepleai-custom.json");
        if (!File.Exists(mozillaPath) || !File.Exists(customPath))
        {
            return;
        }

        var mozilla = EvaluationDataset.FromJson(File.ReadAllText(mozillaPath));
        var custom = EvaluationDataset.FromJson(File.ReadAllText(customPath));

        // Act
        mozilla.Merge(custom);
        var (isValid, errors) = mozilla.Validate();

        // Assert
        isValid.Should().BeTrue(string.Join("; ", errors));
        mozilla.Count.Should().Be(60);
        mozilla.SourceType.Should().Be("combined");
    }

    [Fact]
    public void LoadMeepleAiDataset_ShouldHaveThreeComplexityTiers()
    {
        // Arrange
        var filePath = Path.Combine(GetEvaluationDatasetsPath(), "meepleai-custom.json");
        if (!File.Exists(filePath))
        {
            return;
        }

        var dataset = EvaluationDataset.FromJson(File.ReadAllText(filePath));

        // Act
        var catanSamples = dataset.GetByGameId("catan");
        var tmSamples = dataset.GetByGameId("terraforming-mars");
        var siSamples = dataset.GetByGameId("spirit-island");

        // Assert - three complexity tiers present
        catanSamples.Should().NotBeEmpty("Catan (simple tier) should have samples");
        tmSamples.Should().NotBeEmpty("Terraforming Mars (medium tier) should have samples");
        siSamples.Should().NotBeEmpty("Spirit Island (complex tier) should have samples");
    }

    [Fact]
    public void LoadMozillaDataset_ShouldHaveDifficultyDistribution()
    {
        // Arrange
        var filePath = Path.Combine(GetEvaluationDatasetsPath(), "mozilla-boardgames.json");
        if (!File.Exists(filePath))
        {
            return;
        }

        var dataset = EvaluationDataset.FromJson(File.ReadAllText(filePath));

        // Act & Assert - should have multiple difficulty levels
        dataset.GetByDifficulty("easy").Should().NotBeEmpty();
        dataset.GetByDifficulty("medium").Should().NotBeEmpty();
        dataset.GetByDifficulty("hard").Should().NotBeEmpty();
    }

    #endregion

    #region EvaluationSample Factory Tests

    [Fact]
    public void FromMozilla_ShouldSetCorrectDefaults()
    {
        // Act
        var sample = EvaluationSample.FromMozilla(
            "moz-test",
            "Test question?",
            "Test answer.",
            "test-rulebook",
            "Test Section");

        // Assert
        sample.DatasetSource.Should().Be("mozilla");
        sample.Source.Should().Be("test-rulebook");
        sample.Section.Should().Be("Test Section");
        sample.Difficulty.Should().Be("medium");
        sample.Category.Should().Be("gameplay");
        sample.GameId.Should().BeNull();
    }

    [Fact]
    public void FromMeepleAI_ShouldSetCorrectFields()
    {
        // Act
        var sample = EvaluationSample.FromMeepleAI(
            "ma-test",
            "Test question?",
            "Test answer.",
            "catan",
            5,
            "hard",
            "scoring",
            ["victory", "points"],
            ["chunk-1", "chunk-2"]);

        // Assert
        sample.DatasetSource.Should().Be("meepleai_custom");
        sample.GameId.Should().Be("catan");
        sample.SourcePage.Should().Be(5);
        sample.Difficulty.Should().Be("hard");
        sample.Category.Should().Be("scoring");
        sample.ExpectedKeywords.Should().HaveCount(2);
        sample.RelevantChunkIds.Should().HaveCount(2);
    }

    #endregion

    #region EvaluationMetrics Tests

    [Fact]
    public void EvaluationMetrics_Empty_ShouldHaveZeroValues()
    {
        // Act
        var metrics = EvaluationMetrics.Empty;

        // Assert
        metrics.RecallAt5.Should().Be(0.0);
        metrics.RecallAt10.Should().Be(0.0);
        metrics.NdcgAt10.Should().Be(0.0);
        metrics.Mrr.Should().Be(0.0);
        metrics.P95LatencyMs.Should().Be(0.0);
        metrics.AnswerCorrectness.Should().Be(0.0);
        metrics.SampleCount.Should().Be(0);
    }

    [Fact]
    public void EvaluationMetrics_Create_ShouldClampValues()
    {
        // Act
        var metrics = EvaluationMetrics.Create(
            recallAt5: 1.5,  // Should clamp to 1.0
            recallAt10: -0.1, // Should clamp to 0.0
            ndcgAt10: 0.85,
            mrr: 0.72,
            p95LatencyMs: -10.0, // Should clamp to 0.0
            answerCorrectness: 0.9,
            sampleCount: -5); // Should clamp to 0

        // Assert
        metrics.RecallAt5.Should().Be(1.0);
        metrics.RecallAt10.Should().Be(0.0);
        metrics.NdcgAt10.Should().Be(0.85);
        metrics.Mrr.Should().Be(0.72);
        metrics.P95LatencyMs.Should().Be(0.0);
        metrics.SampleCount.Should().Be(0);
    }

    [Fact]
    public void MeetsBaselineRequirements_With30Samples_ShouldReturnTrue()
    {
        // Arrange
        var metrics = EvaluationMetrics.Create(0.5, 0.6, 0.4, 0.5, 200.0, 0.7, 30);

        // Act & Assert
        metrics.MeetsBaselineRequirements().Should().BeTrue();
    }

    [Fact]
    public void MeetsBaselineRequirements_WithLessThan30Samples_ShouldReturnFalse()
    {
        // Arrange
        var metrics = EvaluationMetrics.Create(0.9, 0.95, 0.9, 0.9, 100.0, 0.95, 29);

        // Act & Assert
        metrics.MeetsBaselineRequirements().Should().BeFalse();
    }

    [Fact]
    public void MeetsPhase4Target_WithRecall60Percent_ShouldReturnTrue()
    {
        // Arrange
        var metrics = EvaluationMetrics.Create(0.5, 0.60, 0.5, 0.5, 500.0, 0.7, 30);

        // Act & Assert
        metrics.MeetsPhase4Target().Should().BeTrue();
    }

    [Fact]
    public void MeetsPhase5Target_WithRecall70AndLowLatency_ShouldReturnTrue()
    {
        // Arrange
        var metrics = EvaluationMetrics.Create(0.6, 0.70, 0.6, 0.6, 1000.0, 0.8, 30);

        // Act & Assert
        metrics.MeetsPhase5Target().Should().BeTrue();
    }

    [Fact]
    public void MeetsPhase5Target_WithHighLatency_ShouldReturnFalse()
    {
        // Arrange
        var metrics = EvaluationMetrics.Create(0.8, 0.85, 0.8, 0.8, 2000.0, 0.9, 30);

        // Act & Assert
        metrics.MeetsPhase5Target().Should().BeFalse();
    }

    #endregion

    #region EvaluationSampleResult Tests

    [Fact]
    public void EvaluationSampleResult_IsSuccess_ShouldBeTrueWhenNoError()
    {
        // Arrange
        var result = new EvaluationSampleResult
        {
            SampleId = "s-001",
            Question = "Test?",
            ExpectedAnswer = "Answer",
            GeneratedAnswer = "Answer",
            ReciprocalRank = 1.0,
            HitAt5 = true,
            HitAt10 = true,
            AnswerCorrectness = 0.9
        };

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void EvaluationSampleResult_IsSuccess_ShouldBeFalseWhenError()
    {
        // Arrange
        var result = new EvaluationSampleResult
        {
            SampleId = "s-001",
            Question = "Test?",
            ExpectedAnswer = "Answer",
            ErrorMessage = "RAG service unavailable"
        };

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void EvaluationSampleResult_NdcgAt10_ShouldCalculateCorrectly()
    {
        // Arrange
        var result = new EvaluationSampleResult
        {
            SampleId = "s-001",
            Question = "Test?",
            ExpectedAnswer = "Answer",
            DcgAt10 = 0.6,
            IdealDcgAt10 = 1.0
        };

        // Assert
        result.NdcgAt10.Should().BeApproximately(0.6, 0.001);
    }

    [Fact]
    public void EvaluationSampleResult_NdcgAt10_ShouldReturnZeroWhenIdealIsZero()
    {
        // Arrange
        var result = new EvaluationSampleResult
        {
            SampleId = "s-001",
            Question = "Test?",
            ExpectedAnswer = "Answer",
            DcgAt10 = 0.0,
            IdealDcgAt10 = 0.0
        };

        // Assert
        result.NdcgAt10.Should().Be(0.0);
    }

    #endregion

    #region EvaluationResult Create Tests

    [Fact]
    public void EvaluationResult_Create_ShouldComputeMetrics()
    {
        // Arrange
        var sampleResults = new List<EvaluationSampleResult>
        {
            new()
            {
                SampleId = "s-001",
                Question = "Q1?",
                ExpectedAnswer = "A1",
                HitAt5 = true,
                HitAt10 = true,
                ReciprocalRank = 1.0,
                DcgAt10 = 1.0,
                IdealDcgAt10 = 1.0,
                AnswerCorrectness = 0.9,
                LatencyMs = 100.0
            },
            new()
            {
                SampleId = "s-002",
                Question = "Q2?",
                ExpectedAnswer = "A2",
                HitAt5 = false,
                HitAt10 = true,
                ReciprocalRank = 0.5,
                DcgAt10 = 0.5,
                IdealDcgAt10 = 1.0,
                AnswerCorrectness = 0.8,
                LatencyMs = 200.0
            }
        };

        var startedAt = DateTime.UtcNow.AddMinutes(-1);
        var completedAt = DateTime.UtcNow;

        // Act
        var result = EvaluationResult.Create("TestDataset", "baseline", startedAt, completedAt, sampleResults);

        // Assert
        result.DatasetName.Should().Be("TestDataset");
        result.Configuration.Should().Be("baseline");
        result.SuccessCount.Should().Be(2);
        result.FailureCount.Should().Be(0);
        result.Metrics.RecallAt5.Should().Be(0.5); // 1 of 2 hit at 5
        result.Metrics.RecallAt10.Should().Be(1.0); // 2 of 2 hit at 10
        result.Metrics.Mrr.Should().Be(0.75); // (1.0 + 0.5) / 2
    }

    [Fact]
    public void EvaluationResult_Create_WithFailedSamples_ShouldCountCorrectly()
    {
        // Arrange
        var sampleResults = new List<EvaluationSampleResult>
        {
            new()
            {
                SampleId = "s-001",
                Question = "Q1?",
                ExpectedAnswer = "A1",
                AnswerCorrectness = 0.9,
                LatencyMs = 100.0
            },
            new()
            {
                SampleId = "s-002",
                Question = "Q2?",
                ExpectedAnswer = "A2",
                ErrorMessage = "Service unavailable",
                LatencyMs = 50.0
            }
        };

        // Act
        var result = EvaluationResult.Create("Test", "baseline", DateTime.UtcNow, DateTime.UtcNow, sampleResults);

        // Assert
        result.SuccessCount.Should().Be(1);
        result.FailureCount.Should().Be(1);
    }

    #endregion

    #region Helpers

    private static EvaluationSample CreateSample(
        string id,
        string difficulty = "medium",
        string category = "gameplay",
        string? gameId = null,
        string datasetSource = "meepleai_custom")
    {
        return new EvaluationSample
        {
            Id = id,
            Question = $"Test question for {id}?",
            ExpectedAnswer = $"Expected answer for {id}.",
            Difficulty = difficulty,
            Category = category,
            GameId = gameId,
            DatasetSource = datasetSource,
            ExpectedKeywords = ["test", "keyword"]
        };
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

    #endregion
}
