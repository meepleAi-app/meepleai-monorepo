using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Unit tests for AnalysisPhase enum and extension methods.
/// Issue #2525: Background Rulebook Analysis Tests - Phase Management
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AnalysisPhaseTests
{
    #region Enum Values

    [Fact]
    public void AnalysisPhase_HasCorrectEnumValues()
    {
        // Assert
        ((int)AnalysisPhase.OverviewExtraction).Should().Be(1);
        ((int)AnalysisPhase.SemanticChunking).Should().Be(2);
        ((int)AnalysisPhase.ChunkAnalysis).Should().Be(3);
        ((int)AnalysisPhase.MergeAndValidation).Should().Be(4);
    }

    [Fact]
    public void AnalysisPhase_HasExactly4Phases()
    {
        // Act
        var phases = Enum.GetValues<AnalysisPhase>();

        // Assert
        phases.Should().HaveCount(4);
    }

    #endregion

    #region GetDisplayName Tests

    [Fact]
    public void GetDisplayName_OverviewExtraction_ReturnsCorrectName()
    {
        // Arrange
        var phase = AnalysisPhase.OverviewExtraction;

        // Act
        var displayName = phase.GetDisplayName();

        // Assert
        displayName.Should().Be("Overview extraction");
    }

    [Fact]
    public void GetDisplayName_SemanticChunking_ReturnsCorrectName()
    {
        // Arrange
        var phase = AnalysisPhase.SemanticChunking;

        // Act
        var displayName = phase.GetDisplayName();

        // Assert
        displayName.Should().Be("Semantic chunking");
    }

    [Fact]
    public void GetDisplayName_ChunkAnalysis_ReturnsCorrectName()
    {
        // Arrange
        var phase = AnalysisPhase.ChunkAnalysis;

        // Act
        var displayName = phase.GetDisplayName();

        // Assert
        displayName.Should().Be("Analyzing chunks");
    }

    [Fact]
    public void GetDisplayName_MergeAndValidation_ReturnsCorrectName()
    {
        // Arrange
        var phase = AnalysisPhase.MergeAndValidation;

        // Act
        var displayName = phase.GetDisplayName();

        // Assert
        displayName.Should().Be("Merging results");
    }

    [Fact]
    public void GetDisplayName_WithInvalidPhase_ReturnsUnknown()
    {
        // Arrange
        var invalidPhase = (AnalysisPhase)999;

        // Act
        var displayName = invalidPhase.GetDisplayName();

        // Assert
        displayName.Should().Be("Unknown");
    }

    #endregion

    #region GetBaseProgress Tests

    [Theory]
    [InlineData(AnalysisPhase.OverviewExtraction, 0)]
    [InlineData(AnalysisPhase.SemanticChunking, 10)]
    [InlineData(AnalysisPhase.ChunkAnalysis, 20)]
    [InlineData(AnalysisPhase.MergeAndValidation, 80)]
    public void GetBaseProgress_ReturnsCorrectValue(AnalysisPhase phase, int expected)
    {
        // Act
        var baseProgress = phase.GetBaseProgress();

        // Assert
        baseProgress.Should().Be(expected);
    }

    [Fact]
    public void GetBaseProgress_WithInvalidPhase_Returns0()
    {
        // Arrange
        var invalidPhase = (AnalysisPhase)999;

        // Act
        var baseProgress = invalidPhase.GetBaseProgress();

        // Assert
        baseProgress.Should().Be(0);
    }

    [Fact]
    public void GetBaseProgress_SequentialPhases_HaveIncreasingProgress()
    {
        // Arrange & Act
        var phase1Progress = AnalysisPhase.OverviewExtraction.GetBaseProgress();
        var phase2Progress = AnalysisPhase.SemanticChunking.GetBaseProgress();
        var phase3Progress = AnalysisPhase.ChunkAnalysis.GetBaseProgress();
        var phase4Progress = AnalysisPhase.MergeAndValidation.GetBaseProgress();

        // Assert - Each phase has higher base progress than previous
        phase2Progress.Should().BeGreaterThan(phase1Progress);
        phase3Progress.Should().BeGreaterThan(phase2Progress);
        phase4Progress.Should().BeGreaterThan(phase3Progress);
    }

    #endregion

    #region GetProgressWeight Tests

    [Theory]
    [InlineData(AnalysisPhase.OverviewExtraction, 10)]
    [InlineData(AnalysisPhase.SemanticChunking, 10)]
    [InlineData(AnalysisPhase.ChunkAnalysis, 60)]
    [InlineData(AnalysisPhase.MergeAndValidation, 20)]
    public void GetProgressWeight_ReturnsCorrectValue(AnalysisPhase phase, int expected)
    {
        // Act
        var weight = phase.GetProgressWeight();

        // Assert
        weight.Should().Be(expected);
    }

    [Fact]
    public void GetProgressWeight_WithInvalidPhase_Returns0()
    {
        // Arrange
        var invalidPhase = (AnalysisPhase)999;

        // Act
        var weight = invalidPhase.GetProgressWeight();

        // Assert
        weight.Should().Be(0);
    }

    [Fact]
    public void GetProgressWeight_AllPhases_SumTo100Percent()
    {
        // Arrange
        var phases = Enum.GetValues<AnalysisPhase>();

        // Act
        var totalWeight = phases.Sum(p => p.GetProgressWeight());

        // Assert
        totalWeight.Should().Be(100, "all phase weights should sum to 100%");
    }

    #endregion

    #region Progress Calculation Verification

    [Fact]
    public void BaseProgressPlusWeight_ForEachPhase_MatchesNextPhaseBase()
    {
        // Assert - Phase 1: 0 + 10 = 10 (Phase 2 base)
        (AnalysisPhase.OverviewExtraction.GetBaseProgress() +
         AnalysisPhase.OverviewExtraction.GetProgressWeight())
            .Should().Be(AnalysisPhase.SemanticChunking.GetBaseProgress());

        // Assert - Phase 2: 10 + 10 = 20 (Phase 3 base)
        (AnalysisPhase.SemanticChunking.GetBaseProgress() +
         AnalysisPhase.SemanticChunking.GetProgressWeight())
            .Should().Be(AnalysisPhase.ChunkAnalysis.GetBaseProgress());

        // Assert - Phase 3: 20 + 60 = 80 (Phase 4 base)
        (AnalysisPhase.ChunkAnalysis.GetBaseProgress() +
         AnalysisPhase.ChunkAnalysis.GetProgressWeight())
            .Should().Be(AnalysisPhase.MergeAndValidation.GetBaseProgress());

        // Assert - Phase 4: 80 + 20 = 100 (completion)
        (AnalysisPhase.MergeAndValidation.GetBaseProgress() +
         AnalysisPhase.MergeAndValidation.GetProgressWeight())
            .Should().Be(100);
    }

    #endregion
}