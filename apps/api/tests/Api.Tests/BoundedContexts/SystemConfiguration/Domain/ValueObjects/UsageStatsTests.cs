using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Tests for UsageStats value object
/// Issue #2589: Verify input/output token granularity tracking
/// </summary>
public class UsageStatsTests
{
    [Fact]
    public void RecordUsage_ShouldTrackInputOutputTokensSeparately()
    {
        // Arrange
        var stats = UsageStats.Empty;
        const int inputTokens = 100;
        const int outputTokens = 200;
        const decimal cost = 1.5m;

        // Act
        var updated = stats.RecordUsage(inputTokens, outputTokens, cost);

        // Assert
        updated.TotalRequests.Should().Be(1);
        updated.TotalInputTokens.Should().Be(inputTokens);
        updated.TotalOutputTokens.Should().Be(outputTokens);
        updated.TotalTokensUsed.Should().Be(inputTokens + outputTokens);
        updated.TotalCostUsd.Should().Be(cost);
        updated.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RecordUsage_MultipleRequests_ShouldAccumulateTokensSeparately()
    {
        // Arrange
        var stats = UsageStats.Empty;

        // Act - Record 3 requests with different token counts
        stats = stats.RecordUsage(inputTokens: 50, outputTokens: 100, costUsd: 0.5m);
        stats = stats.RecordUsage(inputTokens: 75, outputTokens: 150, costUsd: 0.8m);
        stats = stats.RecordUsage(inputTokens: 100, outputTokens: 200, costUsd: 1.2m);

        // Assert
        stats.TotalRequests.Should().Be(3);
        stats.TotalInputTokens.Should().Be(225);  // 50 + 75 + 100
        stats.TotalOutputTokens.Should().Be(450); // 100 + 150 + 200
        stats.TotalTokensUsed.Should().Be(675);   // 225 + 450
        stats.TotalCostUsd.Should().Be(2.5m);     // 0.5 + 0.8 + 1.2
    }

    [Fact]
    public void RecordUsage_ShouldCalculateTotalTokensFromInputPlusOutput()
    {
        // Arrange
        var stats = UsageStats.Empty;

        // Act
        var updated = stats.RecordUsage(inputTokens: 123, outputTokens: 456, costUsd: 1.0m);

        // Assert - Total should equal input + output
        updated.TotalTokensUsed.Should().Be(579); // 123 + 456
        updated.TotalInputTokens.Should().Be(123);
        updated.TotalOutputTokens.Should().Be(456);
    }

    [Fact]
    public void Constructor_WithNegativeInputTokens_ShouldThrowArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new UsageStats(totalInputTokens: -1));

        exception.ParamName.Should().Be("totalInputTokens");
    }

    [Fact]
    public void Constructor_WithNegativeOutputTokens_ShouldThrowArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new UsageStats(totalOutputTokens: -1));

        exception.ParamName.Should().Be("totalOutputTokens");
    }

    [Fact]
    public void Empty_ShouldHaveZeroValuesForAllTokenFields()
    {
        // Act
        var stats = UsageStats.Empty;

        // Assert
        stats.TotalRequests.Should().Be(0);
        stats.TotalInputTokens.Should().Be(0);
        stats.TotalOutputTokens.Should().Be(0);
        stats.TotalTokensUsed.Should().Be(0);
        stats.TotalCostUsd.Should().Be(0);
        stats.LastUsedAt.Should().BeNull();
    }

    [Fact]
    public void RecordUsage_WithOnlyInputTokens_ShouldTrackCorrectly()
    {
        // Arrange
        var stats = UsageStats.Empty;

        // Act - Simulate embedding request (only input tokens, no output)
        var updated = stats.RecordUsage(inputTokens: 500, outputTokens: 0, costUsd: 0.1m);

        // Assert
        updated.TotalInputTokens.Should().Be(500);
        updated.TotalOutputTokens.Should().Be(0);
        updated.TotalTokensUsed.Should().Be(500);
    }

    [Fact]
    public void RecordUsage_ImmutableBehavior_ShouldNotModifyOriginal()
    {
        // Arrange
        var original = UsageStats.Empty;

        // Act
        var updated = original.RecordUsage(100, 200, 1.0m);

        // Assert - Original unchanged (immutability)
        original.TotalRequests.Should().Be(0);
        original.TotalInputTokens.Should().Be(0);
        original.TotalOutputTokens.Should().Be(0);
        original.TotalTokensUsed.Should().Be(0);

        // Updated reflects changes
        updated.TotalRequests.Should().Be(1);
        updated.TotalInputTokens.Should().Be(100);
        updated.TotalOutputTokens.Should().Be(200);
    }
}
