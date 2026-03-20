using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Tests for CriticalSectionClassifier and related types.
/// Issue #5452: Critical section quality gate.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CriticalSectionClassifierTests
{
    #region Classify Tests

    [Theory]
    [InlineData("Victory Conditions", CriticalSectionType.VictoryConditions)]
    [InlineData("How to Win", CriticalSectionType.VictoryConditions)]
    [InlineData("End Game Scoring", CriticalSectionType.VictoryConditions)]
    [InlineData("Game Setup", CriticalSectionType.Setup)]
    [InlineData("Set Up the Game", CriticalSectionType.Setup)]
    [InlineData("Preparation", CriticalSectionType.Setup)]
    [InlineData("Turn Structure", CriticalSectionType.TurnStructure)]
    [InlineData("On Your Turn", CriticalSectionType.TurnStructure)]
    [InlineData("Playing the Game", CriticalSectionType.TurnStructure)]
    public void Classify_WithCriticalHeader_ReturnsCorrectType(string header, CriticalSectionType expected)
    {
        // Act
        var result = CriticalSectionClassifier.Classify(header, "Some content");

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Classify_WithNonCriticalContent_ReturnsNull()
    {
        // Act
        var result = CriticalSectionClassifier.Classify("Flavor Text", "The kingdom was at peace");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Classify_WithNullHeader_ChecksContent()
    {
        // Act
        var result = CriticalSectionClassifier.Classify(null, "This section describes how to win the game");

        // Assert
        result.Should().Be(CriticalSectionType.VictoryConditions);
    }

    #endregion

    #region EvaluateResults Tests

    [Fact]
    public void EvaluateResults_AllSuccessful_ReturnsComplete()
    {
        // Arrange
        var chunks = new List<SemanticChunk>
        {
            CreateChunk(0, true, CriticalSectionType.VictoryConditions),
            CreateChunk(1, true, CriticalSectionType.Setup),
            CreateChunk(2, false, null),
            CreateChunk(3, false, null)
        };

        // Act
        var (status, missingSections) = CriticalSectionClassifier.EvaluateResults(chunks, new List<int>());

        // Assert
        status.Should().Be(AnalysisCompletionStatus.Complete);
        missingSections.Should().BeEmpty();
    }

    [Fact]
    public void EvaluateResults_CriticalSectionFailed_ReturnsPartiallyComplete()
    {
        // Arrange
        var chunks = new List<SemanticChunk>
        {
            CreateChunk(0, true, CriticalSectionType.VictoryConditions),
            CreateChunk(1, true, CriticalSectionType.Setup),
            CreateChunk(2, false, null),
        };

        // Act - Victory conditions chunk failed
        var (status, missingSections) = CriticalSectionClassifier.EvaluateResults(chunks, new List<int> { 0 });

        // Assert
        status.Should().Be(AnalysisCompletionStatus.PartiallyComplete);
        missingSections.Should().Contain("VictoryConditions");
    }

    [Fact]
    public void EvaluateResults_NonCriticalBelowThreshold_ReturnsFailed()
    {
        // Arrange - 4 non-critical chunks, 3 failed = 25% success < 75%
        var chunks = new List<SemanticChunk>
        {
            CreateChunk(0, false, null),
            CreateChunk(1, false, null),
            CreateChunk(2, false, null),
            CreateChunk(3, false, null)
        };

        // Act
        var (status, missingSections) = CriticalSectionClassifier.EvaluateResults(
            chunks, new List<int> { 0, 1, 2 });

        // Assert
        status.Should().Be(AnalysisCompletionStatus.Failed);
        missingSections.Should().NotBeEmpty();
    }

    [Fact]
    public void EvaluateResults_NonCriticalAboveThreshold_ReturnsComplete()
    {
        // Arrange - 4 non-critical chunks, 1 failed = 75% success = threshold
        var chunks = new List<SemanticChunk>
        {
            CreateChunk(0, false, null),
            CreateChunk(1, false, null),
            CreateChunk(2, false, null),
            CreateChunk(3, false, null)
        };

        // Act
        var (status, missingSections) = CriticalSectionClassifier.EvaluateResults(
            chunks, new List<int> { 0 });

        // Assert
        status.Should().Be(AnalysisCompletionStatus.Complete);
    }

    #endregion

    #region RulebookAnalysis Integration Tests

    [Fact]
    public void MarkAsPartiallyComplete_UpdatesStatus()
    {
        // Arrange
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", "Trading game",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.85m, Guid.NewGuid());

        // Act
        analysis.MarkAsPartiallyComplete(new List<string> { "VictoryConditions", "Setup" });

        // Assert
        analysis.CompletionStatus.Should().Be(AnalysisCompletionStatus.PartiallyComplete);
        analysis.MissingSections.Should().HaveCount(2);
        analysis.MissingSections.Should().Contain("VictoryConditions");
    }

    [Fact]
    public void CreateFromAI_DefaultCompletionStatus_IsComplete()
    {
        // Act
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", "Trading game",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.85m, Guid.NewGuid());

        // Assert
        analysis.CompletionStatus.Should().Be(AnalysisCompletionStatus.Complete);
        analysis.MissingSections.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithCompletionStatus_ReconstitutesCorrectly()
    {
        // Act
        var analysis = new RulebookAnalysis(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.85m, "1.0", true, GenerationSource.AI, DateTime.UtcNow, Guid.NewGuid(),
            completionStatus: AnalysisCompletionStatus.PartiallyComplete,
            missingSections: new List<string> { "TurnStructure" });

        // Assert
        analysis.CompletionStatus.Should().Be(AnalysisCompletionStatus.PartiallyComplete);
        analysis.MissingSections.Should().Contain("TurnStructure");
    }

    #endregion

    #region SemanticChunk Critical Section Tests

    [Fact]
    public void SemanticChunk_Create_WithCriticalSection_SetsFlags()
    {
        // Act
        var chunk = SemanticChunk.Create(0, "Victory content", 0, 100,
            sectionHeader: "Victory",
            isCriticalSection: true,
            criticalSectionType: CriticalSectionType.VictoryConditions);

        // Assert
        chunk.IsCriticalSection.Should().BeTrue();
        chunk.CriticalSectionType.Should().Be(CriticalSectionType.VictoryConditions);
    }

    [Fact]
    public void SemanticChunk_Create_DefaultNotCritical()
    {
        // Act
        var chunk = SemanticChunk.Create(0, "Normal content", 0, 100);

        // Assert
        chunk.IsCriticalSection.Should().BeFalse();
        chunk.CriticalSectionType.Should().BeNull();
    }

    #endregion

    private static SemanticChunk CreateChunk(int index, bool isCritical, CriticalSectionType? type) =>
        SemanticChunk.Create(index, $"Content for chunk {index}", index * 100, (index + 1) * 100,
            isCriticalSection: isCritical, criticalSectionType: type);
}
