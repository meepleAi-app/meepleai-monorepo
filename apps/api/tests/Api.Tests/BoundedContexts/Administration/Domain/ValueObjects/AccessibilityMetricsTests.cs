using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Enums;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Tests for the AccessibilityMetrics value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 3
/// </summary>
[Trait("Category", "Unit")]
public sealed class AccessibilityMetricsTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidData_CreatesInstance()
    {
        // Arrange
        var lighthouseScore = 95m;
        var axeViolations = 0;
        var wcagLevels = new List<string> { "A", "AA" };
        var lastRunAt = DateTime.UtcNow;
        var status = TestExecutionStatus.Pass;

        // Act
        var metrics = new AccessibilityMetrics(lighthouseScore, axeViolations, wcagLevels, lastRunAt, status);

        // Assert
        metrics.LighthouseScore.Should().Be(lighthouseScore);
        metrics.AxeViolations.Should().Be(axeViolations);
        metrics.WcagLevels.Should().BeEquivalentTo(wcagLevels);
        metrics.LastRunAt.Should().Be(lastRunAt);
        metrics.Status.Should().Be(status);
    }

    [Fact]
    public void Constructor_WithMinLighthouseScore_CreatesInstance()
    {
        // Arrange
        var metrics = CreateTestMetrics(lighthouseScore: 0);

        // Assert
        metrics.LighthouseScore.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithMaxLighthouseScore_CreatesInstance()
    {
        // Arrange
        var metrics = CreateTestMetrics(lighthouseScore: 100);

        // Assert
        metrics.LighthouseScore.Should().Be(100);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.1)]
    public void Constructor_WithNegativeLighthouseScore_ThrowsValidationException(decimal score)
    {
        // Act
        var action = () => CreateTestMetrics(lighthouseScore: score);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("Lighthouse score must be between 0 and 100");
    }

    [Theory]
    [InlineData(100.1)]
    [InlineData(101)]
    [InlineData(200)]
    public void Constructor_WithLighthouseScoreAbove100_ThrowsValidationException(decimal score)
    {
        // Act
        var action = () => CreateTestMetrics(lighthouseScore: score);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("Lighthouse score must be between 0 and 100");
    }

    [Fact]
    public void Constructor_WithNegativeViolations_ThrowsValidationException()
    {
        // Act
        var action = () => CreateTestMetrics(axeViolations: -1);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("Axe violations cannot be negative");
    }

    [Fact]
    public void Constructor_WithZeroViolations_Succeeds()
    {
        // Arrange
        var metrics = CreateTestMetrics(axeViolations: 0);

        // Assert
        metrics.AxeViolations.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNullWcagLevels_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new AccessibilityMetrics(
            95m,
            0,
            null!,
            DateTime.UtcNow,
            TestExecutionStatus.Pass);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("wcagLevels");
    }

    [Fact]
    public void Constructor_WithEmptyWcagLevels_Succeeds()
    {
        // Arrange
        var metrics = CreateTestMetrics(wcagLevels: new List<string>());

        // Assert
        metrics.WcagLevels.Should().BeEmpty();
    }

    #endregion

    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsInstance()
    {
        // Arrange
        var lighthouseScore = 90m;
        var axeViolations = 2;
        var wcagLevels = new List<string> { "A" };
        var lastRunAt = DateTime.UtcNow;
        var status = TestExecutionStatus.Warning;

        // Act
        var metrics = AccessibilityMetrics.Create(lighthouseScore, axeViolations, wcagLevels, lastRunAt, status);

        // Assert
        metrics.Should().NotBeNull();
        metrics.LighthouseScore.Should().Be(lighthouseScore);
        metrics.AxeViolations.Should().Be(axeViolations);
        metrics.Status.Should().Be(status);
    }

    #endregion

    #region MeetsQualityStandards Tests

    [Fact]
    public void MeetsQualityStandards_WithPerfectMetrics_ReturnsTrue()
    {
        // Arrange - Lighthouse >= 90, no violations, WCAG AA
        var metrics = new AccessibilityMetrics(
            95m,
            0,
            new List<string> { "A", "AA" },
            DateTime.UtcNow,
            TestExecutionStatus.Pass);

        // Act & Assert
        metrics.MeetsQualityStandards.Should().BeTrue();
    }

    [Fact]
    public void MeetsQualityStandards_WithExactly90Score_ReturnsTrue()
    {
        // Arrange
        var metrics = new AccessibilityMetrics(
            90m,
            0,
            new List<string> { "AA" },
            DateTime.UtcNow,
            TestExecutionStatus.Pass);

        // Act & Assert
        metrics.MeetsQualityStandards.Should().BeTrue();
    }

    [Fact]
    public void MeetsQualityStandards_WithScoreBelow90_ReturnsFalse()
    {
        // Arrange
        var metrics = new AccessibilityMetrics(
            89m,
            0,
            new List<string> { "A", "AA" },
            DateTime.UtcNow,
            TestExecutionStatus.Pass);

        // Act & Assert
        metrics.MeetsQualityStandards.Should().BeFalse();
    }

    [Fact]
    public void MeetsQualityStandards_WithViolations_ReturnsFalse()
    {
        // Arrange
        var metrics = new AccessibilityMetrics(
            95m,
            1,
            new List<string> { "A", "AA" },
            DateTime.UtcNow,
            TestExecutionStatus.Pass);

        // Act & Assert
        metrics.MeetsQualityStandards.Should().BeFalse();
    }

    [Fact]
    public void MeetsQualityStandards_WithoutWcagAA_ReturnsFalse()
    {
        // Arrange - Only A level, missing AA
        var metrics = new AccessibilityMetrics(
            95m,
            0,
            new List<string> { "A" },
            DateTime.UtcNow,
            TestExecutionStatus.Pass);

        // Act & Assert
        metrics.MeetsQualityStandards.Should().BeFalse();
    }

    [Fact]
    public void MeetsQualityStandards_WithWcagAAOnly_ReturnsTrue()
    {
        // Arrange - Only AA level (without A)
        var metrics = new AccessibilityMetrics(
            95m,
            0,
            new List<string> { "AA" },
            DateTime.UtcNow,
            TestExecutionStatus.Pass);

        // Act & Assert
        metrics.MeetsQualityStandards.Should().BeTrue();
    }

    [Theory]
    [InlineData("aa")]
    [InlineData("Aa")]
    [InlineData("aA")]
    public void MeetsQualityStandards_WithDifferentCaseAA_ReturnsTrue(string wcagLevel)
    {
        // Arrange - Case-insensitive comparison
        var metrics = new AccessibilityMetrics(
            95m,
            0,
            new List<string> { wcagLevel },
            DateTime.UtcNow,
            TestExecutionStatus.Pass);

        // Act & Assert
        metrics.MeetsQualityStandards.Should().BeTrue();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var lastRunAt = DateTime.UtcNow;
        var wcagLevels = new List<string> { "A", "AA" };

        var metrics1 = new AccessibilityMetrics(95m, 0, wcagLevels, lastRunAt, TestExecutionStatus.Pass);
        var metrics2 = new AccessibilityMetrics(95m, 0, wcagLevels, lastRunAt, TestExecutionStatus.Pass);

        // Act & Assert
        metrics1.Should().Be(metrics2);
    }

    [Fact]
    public void Equals_WithDifferentLighthouseScore_ReturnsFalse()
    {
        // Arrange
        var lastRunAt = DateTime.UtcNow;
        var wcagLevels = new List<string> { "A", "AA" };

        var metrics1 = new AccessibilityMetrics(95m, 0, wcagLevels, lastRunAt, TestExecutionStatus.Pass);
        var metrics2 = new AccessibilityMetrics(90m, 0, wcagLevels, lastRunAt, TestExecutionStatus.Pass);

        // Act & Assert
        metrics1.Should().NotBe(metrics2);
    }

    [Fact]
    public void Equals_WithDifferentViolations_ReturnsFalse()
    {
        // Arrange
        var lastRunAt = DateTime.UtcNow;
        var wcagLevels = new List<string> { "A", "AA" };

        var metrics1 = new AccessibilityMetrics(95m, 0, wcagLevels, lastRunAt, TestExecutionStatus.Pass);
        var metrics2 = new AccessibilityMetrics(95m, 1, wcagLevels, lastRunAt, TestExecutionStatus.Pass);

        // Act & Assert
        metrics1.Should().NotBe(metrics2);
    }

    [Fact]
    public void Equals_WithDifferentStatus_ReturnsFalse()
    {
        // Arrange
        var lastRunAt = DateTime.UtcNow;
        var wcagLevels = new List<string> { "A", "AA" };

        var metrics1 = new AccessibilityMetrics(95m, 0, wcagLevels, lastRunAt, TestExecutionStatus.Pass);
        var metrics2 = new AccessibilityMetrics(95m, 0, wcagLevels, lastRunAt, TestExecutionStatus.Fail);

        // Act & Assert
        metrics1.Should().NotBe(metrics2);
    }

    [Fact]
    public void GetHashCode_SameValues_ReturnsSameHash()
    {
        // Arrange
        var lastRunAt = DateTime.UtcNow;
        var wcagLevels = new List<string> { "A", "AA" };

        var metrics1 = new AccessibilityMetrics(95m, 0, wcagLevels, lastRunAt, TestExecutionStatus.Pass);
        var metrics2 = new AccessibilityMetrics(95m, 0, wcagLevels, lastRunAt, TestExecutionStatus.Pass);

        // Act & Assert
        metrics1.GetHashCode().Should().Be(metrics2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var metrics = new AccessibilityMetrics(
            95m, // Use integer to avoid locale-specific decimal formatting
            2,
            new List<string> { "A", "AA" },
            DateTime.UtcNow,
            TestExecutionStatus.Warning);

        // Act
        var result = metrics.ToString();

        // Assert
        result.Should().Contain("Accessibility:");
        result.Should().Contain("Lighthouse=95"); // Culture-independent check
        result.Should().Contain("Violations=2");
        result.Should().Contain("WCAG=A,AA");
        result.Should().Contain("Status=Warning");
    }

    #endregion

    #region Status Tests

    [Theory]
    [InlineData(TestExecutionStatus.Pass)]
    [InlineData(TestExecutionStatus.Fail)]
    [InlineData(TestExecutionStatus.Warning)]
    [InlineData(TestExecutionStatus.NoData)]
    public void Constructor_WithAnyStatus_Succeeds(TestExecutionStatus status)
    {
        // Act
        var metrics = CreateTestMetrics(status: status);

        // Assert
        metrics.Status.Should().Be(status);
    }

    #endregion

    #region Helper Methods

    private static AccessibilityMetrics CreateTestMetrics(
        decimal lighthouseScore = 95m,
        int axeViolations = 0,
        IReadOnlyList<string>? wcagLevels = null,
        DateTime? lastRunAt = null,
        TestExecutionStatus status = TestExecutionStatus.Pass)
    {
        return new AccessibilityMetrics(
            lighthouseScore,
            axeViolations,
            wcagLevels ?? new List<string> { "A", "AA" },
            lastRunAt ?? DateTime.UtcNow,
            status);
    }

    #endregion
}