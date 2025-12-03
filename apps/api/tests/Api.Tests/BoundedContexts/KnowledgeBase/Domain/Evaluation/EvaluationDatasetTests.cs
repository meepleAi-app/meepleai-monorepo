using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Evaluation;

/// <summary>
/// Unit tests for EvaluationDataset.
/// ADR-016 Phase 0: Validates dataset creation, validation, filtering, and JSON serialization.
/// </summary>
public class EvaluationDatasetTests
{
    private static EvaluationSample CreateSample(
        string id,
        string question = "Test question?",
        string answer = "Test answer",
        string difficulty = "medium",
        string category = "gameplay",
        string? gameId = null)
    {
        return new EvaluationSample
        {
            Id = id,
            Question = question,
            ExpectedAnswer = answer,
            Difficulty = difficulty,
            Category = category,
            GameId = gameId,
            DatasetSource = "meepleai_custom"
        };
    }

    [Fact]
    public void Create_WithNameAndDescription_CreatesEmptyDataset()
    {
        // Arrange & Act
        var dataset = EvaluationDataset.Create(
            name: "Test Dataset",
            description: "A test dataset");

        // Assert
        Assert.Equal("Test Dataset", dataset.Name);
        Assert.Equal("A test dataset", dataset.Description);
        Assert.Equal("meepleai_custom", dataset.SourceType);
        Assert.Equal("1.0.0", dataset.Version);
        Assert.Equal(0, dataset.Count);
        Assert.Empty(dataset.Samples);
    }

    [Fact]
    public void Create_WithSourceType_SetsSourceType()
    {
        // Arrange & Act
        var dataset = EvaluationDataset.Create(
            name: "Mozilla Dataset",
            description: "Mozilla samples",
            sourceType: "mozilla");

        // Assert
        Assert.Equal("mozilla", dataset.SourceType);
    }

    [Fact]
    public void AddSample_AddsSampleToDataset()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");
        var sample = CreateSample("sample-1");

        // Act
        dataset.AddSample(sample);

        // Assert
        Assert.Equal(1, dataset.Count);
        Assert.Contains(sample, dataset.Samples);
    }

    [Fact]
    public void AddSample_WithDuplicateId_ThrowsInvalidOperationException()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");
        dataset.AddSample(CreateSample("duplicate-id"));

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            dataset.AddSample(CreateSample("duplicate-id")));

        Assert.Contains("duplicate-id", ex.Message);
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public void AddSample_WithNullSample_ThrowsArgumentNullException()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => dataset.AddSample(null!));
    }

    [Fact]
    public void AddSamples_AddsMultipleSamples()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");
        var samples = new[]
        {
            CreateSample("sample-1"),
            CreateSample("sample-2"),
            CreateSample("sample-3")
        };

        // Act
        dataset.AddSamples(samples);

        // Assert
        Assert.Equal(3, dataset.Count);
    }

    [Fact]
    public void GetByDifficulty_ReturnsFilteredSamples()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");
        dataset.AddSample(CreateSample("easy-1", difficulty: "easy"));
        dataset.AddSample(CreateSample("easy-2", difficulty: "easy"));
        dataset.AddSample(CreateSample("hard-1", difficulty: "hard"));
        dataset.AddSample(CreateSample("medium-1", difficulty: "medium"));

        // Act
        var easySamples = dataset.GetByDifficulty("easy");

        // Assert
        Assert.Equal(2, easySamples.Count);
        Assert.All(easySamples, s => Assert.Equal("easy", s.Difficulty));
    }

    [Fact]
    public void GetByDifficulty_IsCaseInsensitive()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");
        dataset.AddSample(CreateSample("easy-1", difficulty: "Easy"));
        dataset.AddSample(CreateSample("easy-2", difficulty: "EASY"));

        // Act
        var easySamples = dataset.GetByDifficulty("easy");

        // Assert
        Assert.Equal(2, easySamples.Count);
    }

    [Fact]
    public void GetByCategory_ReturnsFilteredSamples()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");
        dataset.AddSample(CreateSample("setup-1", category: "setup"));
        dataset.AddSample(CreateSample("setup-2", category: "setup"));
        dataset.AddSample(CreateSample("gameplay-1", category: "gameplay"));

        // Act
        var setupSamples = dataset.GetByCategory("setup");

        // Assert
        Assert.Equal(2, setupSamples.Count);
        Assert.All(setupSamples, s => Assert.Equal("setup", s.Category));
    }

    [Fact]
    public void GetByGameId_ReturnsFilteredSamples()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");
        dataset.AddSample(CreateSample("azul-1", gameId: "azul"));
        dataset.AddSample(CreateSample("azul-2", gameId: "azul"));
        dataset.AddSample(CreateSample("catan-1", gameId: "catan"));

        // Act
        var azulSamples = dataset.GetByGameId("azul");

        // Assert
        Assert.Equal(2, azulSamples.Count);
        Assert.All(azulSamples, s => Assert.Equal("azul", s.GameId));
    }

    [Fact]
    public void GetBySource_ReturnsFilteredSamples()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");
        dataset.AddSample(new EvaluationSample
        {
            Id = "mozilla-1",
            Question = "Q1",
            ExpectedAnswer = "A1",
            DatasetSource = "mozilla"
        });
        dataset.AddSample(new EvaluationSample
        {
            Id = "custom-1",
            Question = "Q2",
            ExpectedAnswer = "A2",
            DatasetSource = "meepleai_custom"
        });

        // Act
        var mozillaSamples = dataset.GetBySource("mozilla");

        // Assert
        Assert.Single(mozillaSamples);
        Assert.Equal("mozilla", mozillaSamples[0].DatasetSource);
    }

    [Fact]
    public void Validate_WithAtLeast30Samples_ReturnsValid()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Valid Dataset", "Test dataset");
        for (var i = 0; i < 30; i++)
        {
            dataset.AddSample(CreateSample($"sample-{i}"));
        }

        // Act
        var (isValid, errors) = dataset.Validate();

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithLessThan30Samples_ReturnsInvalid()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Small Dataset", "Test dataset");
        for (var i = 0; i < 29; i++)
        {
            dataset.AddSample(CreateSample($"sample-{i}"));
        }

        // Act
        var (isValid, errors) = dataset.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Single(errors);
        Assert.Contains("at least 30 samples", errors[0]);
        Assert.Contains("29", errors[0]);
    }

    [Fact]
    public void Validate_WithEmptyName_ReturnsInvalid()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("", "Test dataset");
        for (var i = 0; i < 30; i++)
        {
            dataset.AddSample(CreateSample($"sample-{i}"));
        }

        // Act
        var (isValid, errors) = dataset.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("name is required"));
    }

    [Fact]
    public void Validate_WithEmptyQuestion_ReportsError()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test Dataset", "Test");
        for (var i = 0; i < 30; i++)
        {
            dataset.AddSample(new EvaluationSample
            {
                Id = $"sample-{i}",
                Question = i == 0 ? "" : "Valid question?",
                ExpectedAnswer = "Valid answer"
            });
        }

        // Act
        var (isValid, errors) = dataset.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("empty questions"));
    }

    [Fact]
    public void Validate_WithEmptyAnswer_ReportsError()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test Dataset", "Test");
        for (var i = 0; i < 30; i++)
        {
            dataset.AddSample(new EvaluationSample
            {
                Id = $"sample-{i}",
                Question = "Valid question?",
                ExpectedAnswer = i == 0 ? "" : "Valid answer"
            });
        }

        // Act
        var (isValid, errors) = dataset.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("empty expected answers"));
    }

    [Fact]
    public void Merge_CombinesDatasets()
    {
        // Arrange
        var dataset1 = EvaluationDataset.Create("Dataset 1", "First dataset");
        dataset1.AddSample(CreateSample("sample-1"));
        dataset1.AddSample(CreateSample("sample-2"));

        var dataset2 = EvaluationDataset.Create("Dataset 2", "Second dataset");
        dataset2.AddSample(CreateSample("sample-3"));
        dataset2.AddSample(CreateSample("sample-4"));

        // Act
        dataset1.Merge(dataset2);

        // Assert
        Assert.Equal(4, dataset1.Count);
        Assert.Equal("combined", dataset1.SourceType);
    }

    [Fact]
    public void Merge_SkipsDuplicateSamples()
    {
        // Arrange
        var dataset1 = EvaluationDataset.Create("Dataset 1", "First dataset");
        dataset1.AddSample(CreateSample("sample-1"));
        dataset1.AddSample(CreateSample("sample-2"));

        var dataset2 = EvaluationDataset.Create("Dataset 2", "Second dataset");
        dataset2.AddSample(CreateSample("sample-2")); // Duplicate
        dataset2.AddSample(CreateSample("sample-3"));

        // Act
        dataset1.Merge(dataset2);

        // Assert
        Assert.Equal(3, dataset1.Count); // Only 3, not 4
    }

    [Fact]
    public void Merge_WithNullDataset_ThrowsArgumentNullException()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => dataset.Merge(null!));
    }

    [Fact]
    public void ToJson_AndFromJson_RoundTrips()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test Dataset", "A test dataset", "meepleai_custom");
        dataset.AddSample(new EvaluationSample
        {
            Id = "sample-1",
            Question = "What is the setup?",
            ExpectedAnswer = "Set up the board",
            GameId = "azul",
            SourcePage = 5,
            Difficulty = "easy",
            Category = "setup",
            ExpectedKeywords = new[] { "board", "setup" },
            DatasetSource = "meepleai_custom"
        });

        // Act
        var json = dataset.ToJson();
        var restoredDataset = EvaluationDataset.FromJson(json);

        // Assert
        Assert.Equal(dataset.Name, restoredDataset.Name);
        Assert.Equal(dataset.Description, restoredDataset.Description);
        Assert.Equal(dataset.SourceType, restoredDataset.SourceType);
        Assert.Equal(dataset.Count, restoredDataset.Count);

        var originalSample = dataset.Samples[0];
        var restoredSample = restoredDataset.Samples[0];

        Assert.Equal(originalSample.Id, restoredSample.Id);
        Assert.Equal(originalSample.Question, restoredSample.Question);
        Assert.Equal(originalSample.ExpectedAnswer, restoredSample.ExpectedAnswer);
        Assert.Equal(originalSample.GameId, restoredSample.GameId);
        Assert.Equal(originalSample.SourcePage, restoredSample.SourcePage);
        Assert.Equal(originalSample.Difficulty, restoredSample.Difficulty);
        Assert.Equal(originalSample.Category, restoredSample.Category);
        Assert.Equal(originalSample.ExpectedKeywords, restoredSample.ExpectedKeywords);
    }

    [Fact]
    public void FromJson_WithInvalidJson_ThrowsException()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => EvaluationDataset.FromJson(invalidJson));
    }

    [Fact]
    public void FromJson_WithNullValues_UsesDefaults()
    {
        // Arrange
        var json = """
        {
            "samples": [
                {
                    "id": "sample-1",
                    "question": "Q1",
                    "expected_answer": "A1"
                }
            ]
        }
        """;

        // Act
        var dataset = EvaluationDataset.FromJson(json);

        // Assert
        Assert.Single(dataset.Samples);
        var sample = dataset.Samples[0];
        Assert.Equal("medium", sample.Difficulty); // Default
        Assert.Equal("gameplay", sample.Category); // Default
        Assert.Equal("unknown", sample.DatasetSource); // Default when not specified
    }

    [Fact]
    public void Count_ReflectsNumberOfSamples()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test");

        // Act & Assert
        Assert.Equal(0, dataset.Count);

        dataset.AddSample(CreateSample("sample-1"));
        Assert.Equal(1, dataset.Count);

        dataset.AddSample(CreateSample("sample-2"));
        Assert.Equal(2, dataset.Count);
    }

    [Fact]
    public void Samples_ReturnsReadOnlyCollection()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test");
        dataset.AddSample(CreateSample("sample-1"));

        // Act
        var samples = dataset.Samples;

        // Assert
        Assert.IsAssignableFrom<IReadOnlyList<EvaluationSample>>(samples);
    }
}
