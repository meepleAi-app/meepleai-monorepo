using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Unit tests for BackgroundAnalysisProgress value object.
/// Issue #2525: Background Rulebook Analysis Tests - Progress Tracking
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class BackgroundAnalysisProgressTests
{
    #region Create - Happy Path

    [Fact]
    public void Create_WithValidData_ReturnsProgress()
    {
        // Arrange & Act
        var progress = BackgroundAnalysisProgress.Create(
            AnalysisPhase.ChunkAnalysis,
            percentageComplete: 50,
            statusMessage: "Analyzing chunks",
            estimatedTimeRemaining: TimeSpan.FromMinutes(5));

        // Assert
        progress.Should().NotBeNull();
        progress.CurrentPhase.Should().Be(AnalysisPhase.ChunkAnalysis);
        progress.PercentageComplete.Should().Be(50);
        progress.StatusMessage.Should().Be("Analyzing chunks");
        progress.EstimatedTimeRemaining.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Create_WithoutEstimatedTime_ReturnsProgressWithNullTime()
    {
        // Arrange & Act
        var progress = BackgroundAnalysisProgress.Create(
            AnalysisPhase.OverviewExtraction,
            percentageComplete: 5,
            statusMessage: "Starting overview");

        // Assert
        progress.EstimatedTimeRemaining.Should().BeNull();
    }

    #endregion

    #region Create - Percentage Validation

    [Fact]
    public void Create_WithNegativePercentage_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => BackgroundAnalysisProgress.Create(
            AnalysisPhase.OverviewExtraction,
            percentageComplete: -1,
            statusMessage: "Invalid");

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("percentageComplete");
    }

    [Fact]
    public void Create_WithPercentageOver100_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => BackgroundAnalysisProgress.Create(
            AnalysisPhase.MergeAndValidation,
            percentageComplete: 101,
            statusMessage: "Invalid");

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("percentageComplete");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Create_WithValidPercentage_ReturnsProgress(int percentage)
    {
        // Act
        var progress = BackgroundAnalysisProgress.Create(
            AnalysisPhase.ChunkAnalysis,
            percentageComplete: percentage,
            statusMessage: "Test");

        // Assert
        progress.PercentageComplete.Should().Be(percentage);
    }

    #endregion

    #region ForPhaseStart Tests

    [Theory]
    [InlineData(AnalysisPhase.OverviewExtraction, 0, "Overview extraction")]
    [InlineData(AnalysisPhase.SemanticChunking, 10, "Semantic chunking")]
    [InlineData(AnalysisPhase.ChunkAnalysis, 20, "Analyzing chunks")]
    [InlineData(AnalysisPhase.MergeAndValidation, 80, "Merging results")]
    public void ForPhaseStart_ReturnsProgressAtPhaseBase(
        AnalysisPhase phase,
        int expectedPercentage,
        string expectedMessage)
    {
        // Act
        var progress = BackgroundAnalysisProgress.ForPhaseStart(phase);

        // Assert
        progress.CurrentPhase.Should().Be(phase);
        progress.PercentageComplete.Should().Be(expectedPercentage);
        progress.StatusMessage.Should().Be(expectedMessage);
        progress.EstimatedTimeRemaining.Should().BeNull();
    }

    #endregion

    #region ForPhaseProgress Tests

    [Fact]
    public void ForPhaseProgress_AtPhaseStart_ReturnsBaseProgress()
    {
        // Arrange
        var phase = AnalysisPhase.ChunkAnalysis;

        // Act
        var progress = BackgroundAnalysisProgress.ForPhaseProgress(
            phase,
            phaseProgress: 0.0, // 0% of phase
            totalItems: 10,
            processedItems: 0);

        // Assert
        progress.PercentageComplete.Should().Be(20); // Base progress for ChunkAnalysis
    }

    [Fact]
    public void ForPhaseProgress_AtPhaseMiddle_ReturnsCorrectPercentage()
    {
        // Arrange
        var phase = AnalysisPhase.ChunkAnalysis; // Weight: 60, Base: 20

        // Act
        var progress = BackgroundAnalysisProgress.ForPhaseProgress(
            phase,
            phaseProgress: 0.5, // 50% of phase
            totalItems: 10,
            processedItems: 5);

        // Assert
        // 20 (base) + 60 * 0.5 = 20 + 30 = 50
        progress.PercentageComplete.Should().Be(50);
    }

    [Fact]
    public void ForPhaseProgress_AtPhaseEnd_ReturnsNextPhaseBase()
    {
        // Arrange
        var phase = AnalysisPhase.ChunkAnalysis;

        // Act
        var progress = BackgroundAnalysisProgress.ForPhaseProgress(
            phase,
            phaseProgress: 1.0, // 100% of phase
            totalItems: 10,
            processedItems: 10);

        // Assert
        // 20 (base) + 60 * 1.0 = 80 (next phase base)
        progress.PercentageComplete.Should().Be(80);
    }

    [Fact]
    public void ForPhaseProgress_ForChunkAnalysis_IncludesItemCountInMessage()
    {
        // Act
        var progress = BackgroundAnalysisProgress.ForPhaseProgress(
            AnalysisPhase.ChunkAnalysis,
            phaseProgress: 0.3,
            totalItems: 10,
            processedItems: 3);

        // Assert
        progress.StatusMessage.Should().Contain("Analyzing chunks");
        progress.StatusMessage.Should().Contain("(3/10)");
    }

    [Fact]
    public void ForPhaseProgress_ForNonChunkAnalysisPhase_UsesDisplayName()
    {
        // Act
        var progress = BackgroundAnalysisProgress.ForPhaseProgress(
            AnalysisPhase.SemanticChunking,
            phaseProgress: 0.5,
            totalItems: 10,
            processedItems: 5);

        // Assert
        progress.StatusMessage.Should().Be("Semantic chunking");
        progress.StatusMessage.Should().NotContain("(");
    }

    [Fact]
    public void ForPhaseProgress_ForChunkAnalysis_CalculatesEstimatedTime()
    {
        // Act
        var progress = BackgroundAnalysisProgress.ForPhaseProgress(
            AnalysisPhase.ChunkAnalysis,
            phaseProgress: 0.5,
            totalItems: 10,
            processedItems: 5);

        // Assert
        progress.EstimatedTimeRemaining.Should().NotBeNull();
        progress.EstimatedTimeRemaining.Should().BeGreaterThan(TimeSpan.Zero);
        // 5 remaining chunks * 30s = 150s = 2.5 minutes
        progress.EstimatedTimeRemaining.Should().Be(TimeSpan.FromSeconds(150));
    }

    [Fact]
    public void ForPhaseProgress_ForChunkAnalysisWithZeroProcessed_NoEstimatedTime()
    {
        // Act
        var progress = BackgroundAnalysisProgress.ForPhaseProgress(
            AnalysisPhase.ChunkAnalysis,
            phaseProgress: 0.0,
            totalItems: 10,
            processedItems: 0);

        // Assert
        progress.EstimatedTimeRemaining.Should().BeNull();
    }

    [Fact]
    public void ForPhaseProgress_ForNonChunkAnalysisPhase_NoEstimatedTime()
    {
        // Act
        var progress = BackgroundAnalysisProgress.ForPhaseProgress(
            AnalysisPhase.OverviewExtraction,
            phaseProgress: 0.5,
            totalItems: 10,
            processedItems: 5);

        // Assert
        progress.EstimatedTimeRemaining.Should().BeNull();
    }

    #endregion

    #region Completed Tests

    [Fact]
    public void Completed_Returns100PercentWithCompletedMessage()
    {
        // Act
        var progress = BackgroundAnalysisProgress.Completed();

        // Assert
        progress.CurrentPhase.Should().Be(AnalysisPhase.MergeAndValidation);
        progress.PercentageComplete.Should().Be(100);
        progress.StatusMessage.Should().Be("Completed");
        progress.EstimatedTimeRemaining.Should().BeNull();
    }

    #endregion

    #region Progress Calculation Scenarios (from Issue #2525)

    [Fact]
    public void ProgressCalculation_OverviewExtractionStart_Returns0Percent()
    {
        // Act
        var progress = BackgroundAnalysisProgress.ForPhaseStart(AnalysisPhase.OverviewExtraction);

        // Assert
        progress.PercentageComplete.Should().Be(0);
    }

    [Fact]
    public void ProgressCalculation_OverviewExtractionComplete_Returns10Percent()
    {
        // Act
        var progress = BackgroundAnalysisProgress.ForPhaseProgress(
            AnalysisPhase.OverviewExtraction,
            phaseProgress: 1.0,
            totalItems: 1,
            processedItems: 1);

        // Assert
        progress.PercentageComplete.Should().Be(10);
    }

    [Fact]
    public void ProgressCalculation_SemanticChunkingComplete_Returns20Percent()
    {
        // Act
        var progress = BackgroundAnalysisProgress.ForPhaseProgress(
            AnalysisPhase.SemanticChunking,
            phaseProgress: 1.0,
            totalItems: 1,
            processedItems: 1);

        // Assert
        progress.PercentageComplete.Should().Be(20);
    }

    [Fact]
    public void ProgressCalculation_ChunkAnalysisComplete_Returns80Percent()
    {
        // Act
        var progress = BackgroundAnalysisProgress.ForPhaseProgress(
            AnalysisPhase.ChunkAnalysis,
            phaseProgress: 1.0,
            totalItems: 10,
            processedItems: 10);

        // Assert
        progress.PercentageComplete.Should().Be(80);
    }

    [Fact]
    public void ProgressCalculation_ChunkAnalysisHalfway_Returns50Percent()
    {
        // Act - Phase 3 starts at 20%, weight 60 → 50% = 20 + 30 = 50%
        var progress = BackgroundAnalysisProgress.ForPhaseProgress(
            AnalysisPhase.ChunkAnalysis,
            phaseProgress: 0.5,
            totalItems: 10,
            processedItems: 5);

        // Assert
        progress.PercentageComplete.Should().Be(50);
    }

    [Fact]
    public void ProgressCalculation_MergeComplete_Returns100Percent()
    {
        // Act
        var progress = BackgroundAnalysisProgress.ForPhaseProgress(
            AnalysisPhase.MergeAndValidation,
            phaseProgress: 1.0,
            totalItems: 1,
            processedItems: 1);

        // Assert
        progress.PercentageComplete.Should().Be(100);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Create_WithZeroPercentage_ReturnsProgress()
    {
        // Act
        var progress = BackgroundAnalysisProgress.Create(
            AnalysisPhase.OverviewExtraction,
            percentageComplete: 0,
            statusMessage: "Starting");

        // Assert
        progress.PercentageComplete.Should().Be(0);
    }

    [Fact]
    public void Create_With100Percentage_ReturnsProgress()
    {
        // Act
        var progress = BackgroundAnalysisProgress.Create(
            AnalysisPhase.MergeAndValidation,
            percentageComplete: 100,
            statusMessage: "Completed");

        // Assert
        progress.PercentageComplete.Should().Be(100);
    }

    [Fact]
    public void ForPhaseProgress_WithZeroProgress_ReturnsBasePercentage()
    {
        // Arrange
        var phases = Enum.GetValues<AnalysisPhase>();

        foreach (var phase in phases)
        {
            // Act
            var progress = BackgroundAnalysisProgress.ForPhaseProgress(
                phase,
                phaseProgress: 0.0,
                totalItems: 10,
                processedItems: 0);

            // Assert
            progress.PercentageComplete.Should().Be(phase.GetBaseProgress(),
                $"phase {phase} at 0% should return base progress");
        }
    }

    #endregion

    #region Record Equality

    [Fact]
    public void Equality_WithIdenticalValues_AreEqual()
    {
        // Arrange
        var progress1 = BackgroundAnalysisProgress.Create(
            AnalysisPhase.ChunkAnalysis,
            50,
            "Analyzing chunks");
        var progress2 = BackgroundAnalysisProgress.Create(
            AnalysisPhase.ChunkAnalysis,
            50,
            "Analyzing chunks");

        // Assert
        progress1.Should().Be(progress2);
    }

    [Fact]
    public void Equality_WithDifferentPercentage_AreNotEqual()
    {
        // Arrange
        var progress1 = BackgroundAnalysisProgress.Create(
            AnalysisPhase.ChunkAnalysis,
            50,
            "Test");
        var progress2 = BackgroundAnalysisProgress.Create(
            AnalysisPhase.ChunkAnalysis,
            51,
            "Test");

        // Assert
        progress1.Should().NotBe(progress2);
    }

    #endregion
}