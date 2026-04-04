using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Xunit;
using Api.Tests.Constants;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Evaluation;

/// <summary>
/// Unit tests for EvaluationDataset.
/// ADR-016 Phase 0: Validates dataset creation, validation, filtering, and JSON serialization.
/// </summary>
[Trait("Category", TestCategories.Unit)]
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
        dataset.Name.Should().Be("Test Dataset");
        dataset.Description.Should().Be("A test dataset");
        dataset.SourceType.Should().Be("meepleai_custom");
        dataset.Version.Should().Be("1.0.0");
        dataset.Count.Should().Be(0);
        dataset.Samples.Should().BeEmpty();
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
        dataset.SourceType.Should().Be("mozilla");
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
        dataset.Count.Should().Be(1);
        dataset.Samples.Should().Contain(sample);
    }

    [Fact]
    public void AddSample_WithDuplicateId_ThrowsInvalidOperationException()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");
        dataset.AddSample(CreateSample("duplicate-id"));

        // Act & Assert
        Action act = () =>
            dataset.AddSample(CreateSample("duplicate-id"));
        var ex = act.Should().Throw<InvalidOperationException>().Which;

        ex.Message.Should().Contain("duplicate-id");
        ex.Message.Should().Contain("already exists");
    }

    [Fact]
    public void AddSample_WithNullSample_ThrowsArgumentNullException()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");

        // Act & Assert
        Action act = () => dataset.AddSample(null!);
        act.Should().Throw<ArgumentNullException>();
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
        dataset.Count.Should().Be(3);
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
        easySamples.Count.Should().Be(2);
        easySamples.Should().AllSatisfy(s => s.Difficulty.Should().Be("easy"));
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
        easySamples.Count.Should().Be(2);
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
        setupSamples.Count.Should().Be(2);
        setupSamples.Should().AllSatisfy(s => s.Category.Should().Be("setup"));
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
        azulSamples.Count.Should().Be(2);
        azulSamples.Should().AllSatisfy(s => s.GameId.Should().Be("azul"));
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
        mozillaSamples.Should().ContainSingle();
        mozillaSamples[0].DatasetSource.Should().Be("mozilla");
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
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
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
        isValid.Should().BeFalse();
        errors.Should().ContainSingle();
        errors[0].Should().Contain("at least 30 samples");
        errors[0].Should().Contain("29");
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
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("name is required"));
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
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("empty questions"));
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
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("empty expected answers"));
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
        dataset1.Count.Should().Be(4);
        dataset1.SourceType.Should().Be("combined");
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
        dataset1.Count.Should().Be(3); // Only 3, not 4
    }

    [Fact]
    public void Merge_WithNullDataset_ThrowsArgumentNullException()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test");

        // Act & Assert
        Action act = () => dataset.Merge(null!);
        act.Should().Throw<ArgumentNullException>();
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
        restoredDataset.Name.Should().Be(dataset.Name);
        restoredDataset.Description.Should().Be(dataset.Description);
        restoredDataset.SourceType.Should().Be(dataset.SourceType);
        restoredDataset.Count.Should().Be(dataset.Count);

        var originalSample = dataset.Samples[0];
        var restoredSample = restoredDataset.Samples[0];

        restoredSample.Id.Should().Be(originalSample.Id);
        restoredSample.Question.Should().Be(originalSample.Question);
        restoredSample.ExpectedAnswer.Should().Be(originalSample.ExpectedAnswer);
        restoredSample.GameId.Should().Be(originalSample.GameId);
        restoredSample.SourcePage.Should().Be(originalSample.SourcePage);
        restoredSample.Difficulty.Should().Be(originalSample.Difficulty);
        restoredSample.Category.Should().Be(originalSample.Category);
        restoredSample.ExpectedKeywords.Should().BeEquivalentTo(originalSample.ExpectedKeywords);
    }

    [Fact]
    public void FromJson_WithInvalidJson_ThrowsException()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act & Assert
        Action act = () => EvaluationDataset.FromJson(invalidJson);
        act.Should().Throw<Exception>();
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
        dataset.Samples.Should().ContainSingle();
        var sample = dataset.Samples[0];
        sample.Difficulty.Should().Be("medium"); // Default
        sample.Category.Should().Be("gameplay"); // Default
        sample.DatasetSource.Should().Be("unknown"); // Default when not specified
    }

    [Fact]
    public void Count_ReflectsNumberOfSamples()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test");

        // Act & Assert
        dataset.Count.Should().Be(0);

        dataset.AddSample(CreateSample("sample-1"));
        dataset.Count.Should().Be(1);

        dataset.AddSample(CreateSample("sample-2"));
        dataset.Count.Should().Be(2);
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
        samples.Should().BeAssignableTo<IReadOnlyList<EvaluationSample>>();
    }
}
