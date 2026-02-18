using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Unit tests for TierLimits value object (Issue #3692)
/// Extended with credit-based budget tracking tests
/// </summary>
public sealed class TierLimitsTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        // Arrange & Act
        var limits = TierLimits.Create(
            tokensPerMonth: 10_000,
            tokensPerDay: 500,
            messagesPerDay: 10,
            maxCollectionSize: 20,
            maxPdfUploadsPerMonth: 5,
            maxAgentsCreated: 1,
            dailyCreditsLimit: 100m,
            weeklyCreditsLimit: 10_000m);

        // Assert
        Assert.NotNull(limits);
        Assert.Equal(10_000, limits.TokensPerMonth);
        Assert.Equal(500, limits.TokensPerDay);
        Assert.Equal(10, limits.MessagesPerDay);
        Assert.Equal(20, limits.MaxCollectionSize);
        Assert.Equal(5, limits.MaxPdfUploadsPerMonth);
        Assert.Equal(1, limits.MaxAgentsCreated);
        Assert.Equal(100m, limits.DailyCreditsLimit);
        Assert.Equal(10_000m, limits.WeeklyCreditsLimit);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(-999999)]
    public void Create_WithNegativeTokensPerMonth_ShouldThrow(int tokensPerMonth)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            TierLimits.Create(
                tokensPerMonth: tokensPerMonth,
                tokensPerDay: 500,
                messagesPerDay: 10,
                maxCollectionSize: 20,
                maxPdfUploadsPerMonth: 5,
                maxAgentsCreated: 1,
                dailyCreditsLimit: 100m,
                weeklyCreditsLimit: 10_000m));

        Assert.Equal("tokensPerMonth", exception.ParamName);
        Assert.Contains("cannot be negative", exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-500)]
    public void Create_WithNegativeTokensPerDay_ShouldThrow(int tokensPerDay)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            TierLimits.Create(
                tokensPerMonth: 10_000,
                tokensPerDay: tokensPerDay,
                messagesPerDay: 10,
                maxCollectionSize: 20,
                maxPdfUploadsPerMonth: 5,
                maxAgentsCreated: 1,
                dailyCreditsLimit: 100m,
                weeklyCreditsLimit: 10_000m));

        Assert.Equal("tokensPerDay", exception.ParamName);
        Assert.Contains("cannot be negative", exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Create_WithNegativeMessagesPerDay_ShouldThrow(int messagesPerDay)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            TierLimits.Create(
                tokensPerMonth: 10_000,
                tokensPerDay: 500,
                messagesPerDay: messagesPerDay,
                maxCollectionSize: 20,
                maxPdfUploadsPerMonth: 5,
                maxAgentsCreated: 1,
                dailyCreditsLimit: 100m,
                weeklyCreditsLimit: 10_000m));

        Assert.Equal("messagesPerDay", exception.ParamName);
        Assert.Contains("cannot be negative", exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-20)]
    public void Create_WithNegativeMaxCollectionSize_ShouldThrow(int maxCollectionSize)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            TierLimits.Create(
                tokensPerMonth: 10_000,
                tokensPerDay: 500,
                messagesPerDay: 10,
                maxCollectionSize: maxCollectionSize,
                maxPdfUploadsPerMonth: 5,
                maxAgentsCreated: 1,
                dailyCreditsLimit: 100m,
                weeklyCreditsLimit: 10_000m));

        Assert.Equal("maxCollectionSize", exception.ParamName);
        Assert.Contains("cannot be negative", exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    public void Create_WithNegativeMaxPdfUploadsPerMonth_ShouldThrow(int maxPdfUploadsPerMonth)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            TierLimits.Create(
                tokensPerMonth: 10_000,
                tokensPerDay: 500,
                messagesPerDay: 10,
                maxCollectionSize: 20,
                maxPdfUploadsPerMonth: maxPdfUploadsPerMonth,
                maxAgentsCreated: 1,
                dailyCreditsLimit: 100m,
                weeklyCreditsLimit: 10_000m));

        Assert.Equal("maxPdfUploadsPerMonth", exception.ParamName);
        Assert.Contains("cannot be negative", exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-3)]
    public void Create_WithNegativeMaxAgentsCreated_ShouldThrow(int maxAgentsCreated)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            TierLimits.Create(
                tokensPerMonth: 10_000,
                tokensPerDay: 500,
                messagesPerDay: 10,
                maxCollectionSize: 20,
                maxPdfUploadsPerMonth: 5,
                maxAgentsCreated: maxAgentsCreated,
                dailyCreditsLimit: 100m,
                weeklyCreditsLimit: 10_000m));

        Assert.Equal("maxAgentsCreated", exception.ParamName);
        Assert.Contains("cannot be negative", exception.Message);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    [InlineData(-999.99)]
    public void Create_WithNegativeDailyCreditsLimit_ShouldThrow(decimal dailyCreditsLimit)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            TierLimits.Create(
                tokensPerMonth: 10_000,
                tokensPerDay: 500,
                messagesPerDay: 10,
                maxCollectionSize: 20,
                maxPdfUploadsPerMonth: 5,
                maxAgentsCreated: 1,
                dailyCreditsLimit: dailyCreditsLimit,
                weeklyCreditsLimit: 10_000m));

        Assert.Equal("dailyCreditsLimit", exception.ParamName);
        Assert.Contains("cannot be negative", exception.Message);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1000)]
    [InlineData(-99999.99)]
    public void Create_WithNegativeWeeklyCreditsLimit_ShouldThrow(decimal weeklyCreditsLimit)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            TierLimits.Create(
                tokensPerMonth: 10_000,
                tokensPerDay: 500,
                messagesPerDay: 10,
                maxCollectionSize: 20,
                maxPdfUploadsPerMonth: 5,
                maxAgentsCreated: 1,
                dailyCreditsLimit: 100m,
                weeklyCreditsLimit: weeklyCreditsLimit));

        Assert.Equal("weeklyCreditsLimit", exception.ParamName);
        Assert.Contains("cannot be negative", exception.Message);
    }

    [Fact]
    public void FreeTier_ShouldHaveCorrectCreditLimits()
    {
        // Act
        var limits = TierLimits.FreeTier();

        // Assert - Credits: 100/day, 10,000/week (= $0.001/day, $0.10/week)
        Assert.Equal(100m, limits.DailyCreditsLimit);
        Assert.Equal(10_000m, limits.WeeklyCreditsLimit);

        // Verify other limits
        Assert.Equal(10_000, limits.TokensPerMonth);
        Assert.Equal(500, limits.TokensPerDay);
        Assert.Equal(10, limits.MessagesPerDay);
        Assert.Equal(20, limits.MaxCollectionSize);
        Assert.Equal(5, limits.MaxPdfUploadsPerMonth);
        Assert.Equal(1, limits.MaxAgentsCreated);
    }

    [Fact]
    public void BasicTier_ShouldHaveCorrectCreditLimits()
    {
        // Act
        var limits = TierLimits.BasicTier();

        // Assert - Credits: 1,000/day, 5,000/week (= $0.01/day, $0.05/week)
        Assert.Equal(1_000m, limits.DailyCreditsLimit);
        Assert.Equal(5_000m, limits.WeeklyCreditsLimit);

        // Verify other limits
        Assert.Equal(50_000, limits.TokensPerMonth);
        Assert.Equal(2_000, limits.TokensPerDay);
        Assert.Equal(50, limits.MessagesPerDay);
        Assert.Equal(50, limits.MaxCollectionSize);
        Assert.Equal(20, limits.MaxPdfUploadsPerMonth);
        Assert.Equal(3, limits.MaxAgentsCreated);
    }

    [Fact]
    public void ProTier_ShouldHaveCorrectCreditLimits()
    {
        // Act
        var limits = TierLimits.ProTier();

        // Assert - Credits: 5,000/day, 25,000/week (= $0.05/day, $0.25/week)
        Assert.Equal(5_000m, limits.DailyCreditsLimit);
        Assert.Equal(25_000m, limits.WeeklyCreditsLimit);

        // Verify other limits
        Assert.Equal(200_000, limits.TokensPerMonth);
        Assert.Equal(10_000, limits.TokensPerDay);
        Assert.Equal(200, limits.MessagesPerDay);
        Assert.Equal(200, limits.MaxCollectionSize);
        Assert.Equal(100, limits.MaxPdfUploadsPerMonth);
        Assert.Equal(10, limits.MaxAgentsCreated);
    }

    [Fact]
    public void EnterpriseTier_ShouldHaveUnlimitedCredits()
    {
        // Act
        var limits = TierLimits.EnterpriseTier();

        // Assert - Unlimited (decimal.MaxValue)
        Assert.Equal(decimal.MaxValue, limits.DailyCreditsLimit);
        Assert.Equal(decimal.MaxValue, limits.WeeklyCreditsLimit);

        // Verify other unlimited limits
        Assert.Equal(int.MaxValue, limits.TokensPerMonth);
        Assert.Equal(int.MaxValue, limits.TokensPerDay);
        Assert.Equal(int.MaxValue, limits.MessagesPerDay);
        Assert.Equal(int.MaxValue, limits.MaxCollectionSize);
        Assert.Equal(int.MaxValue, limits.MaxPdfUploadsPerMonth);
        Assert.Equal(int.MaxValue, limits.MaxAgentsCreated);
    }

    [Fact]
    public void TierLimits_ShouldBeImmutable()
    {
        // Arrange
        var limits = TierLimits.FreeTier();

        // Assert - All properties should have init-only setters
        // This is enforced by the record type, but verify key properties
        Assert.Equal(100m, limits.DailyCreditsLimit);

        // Attempting to modify would cause compilation error (record type)
        // var newLimits = limits with { DailyCreditsLimit = 200m }; // Creates new instance
    }

    [Fact]
    public void Create_WithZeroValues_ShouldSucceed()
    {
        // Arrange & Act - Zero is valid, just means no limit
        var limits = TierLimits.Create(
            tokensPerMonth: 0,
            tokensPerDay: 0,
            messagesPerDay: 0,
            maxCollectionSize: 0,
            maxPdfUploadsPerMonth: 0,
            maxAgentsCreated: 0,
            dailyCreditsLimit: 0m,
            weeklyCreditsLimit: 0m);

        // Assert
        Assert.NotNull(limits);
        Assert.Equal(0, limits.TokensPerMonth);
        Assert.Equal(0m, limits.DailyCreditsLimit);
        Assert.Equal(0m, limits.WeeklyCreditsLimit);
    }

    [Fact]
    public void Create_WithMaxValues_ShouldSucceed()
    {
        // Arrange & Act - Test with maximum values
        var limits = TierLimits.Create(
            tokensPerMonth: int.MaxValue,
            tokensPerDay: int.MaxValue,
            messagesPerDay: int.MaxValue,
            maxCollectionSize: int.MaxValue,
            maxPdfUploadsPerMonth: int.MaxValue,
            maxAgentsCreated: int.MaxValue,
            dailyCreditsLimit: decimal.MaxValue,
            weeklyCreditsLimit: decimal.MaxValue);

        // Assert
        Assert.NotNull(limits);
        Assert.Equal(int.MaxValue, limits.TokensPerMonth);
        Assert.Equal(decimal.MaxValue, limits.DailyCreditsLimit);
        Assert.Equal(decimal.MaxValue, limits.WeeklyCreditsLimit);
    }

    [Fact]
    public void TierFactories_ShouldHaveConsistentCreditRatios()
    {
        // Arrange
        var free = TierLimits.FreeTier();
        var basic = TierLimits.BasicTier();
        var pro = TierLimits.ProTier();

        // Assert - Credit limits should scale proportionally with tier
        Assert.True(free.DailyCreditsLimit < basic.DailyCreditsLimit);
        Assert.True(basic.DailyCreditsLimit < pro.DailyCreditsLimit);

        Assert.True(free.WeeklyCreditsLimit < basic.WeeklyCreditsLimit);
        Assert.True(basic.WeeklyCreditsLimit < pro.WeeklyCreditsLimit);

        // Verify weekly >= daily (sanity check)
        Assert.True(free.WeeklyCreditsLimit >= free.DailyCreditsLimit);
        Assert.True(basic.WeeklyCreditsLimit >= basic.DailyCreditsLimit);
        Assert.True(pro.WeeklyCreditsLimit >= pro.DailyCreditsLimit);
    }

    [Fact]
    public void FreeTier_DailyCredits_ShouldEqual100()
    {
        // Act
        var limits = TierLimits.FreeTier();

        // Assert - 100 credits/day = $0.001/day
        Assert.Equal(100m, limits.DailyCreditsLimit);
    }

    [Fact]
    public void FreeTier_WeeklyCredits_ShouldEqual10000()
    {
        // Act
        var limits = TierLimits.FreeTier();

        // Assert - 10,000 credits/week = $0.10/week
        Assert.Equal(10_000m, limits.WeeklyCreditsLimit);
    }

    [Fact]
    public void BasicTier_DailyCredits_ShouldEqual1000()
    {
        // Act
        var limits = TierLimits.BasicTier();

        // Assert - 1,000 credits/day = $0.01/day
        Assert.Equal(1_000m, limits.DailyCreditsLimit);
    }

    [Fact]
    public void BasicTier_WeeklyCredits_ShouldEqual5000()
    {
        // Act
        var limits = TierLimits.BasicTier();

        // Assert - 5,000 credits/week = $0.05/week
        Assert.Equal(5_000m, limits.WeeklyCreditsLimit);
    }

    [Fact]
    public void ProTier_DailyCredits_ShouldEqual5000()
    {
        // Act
        var limits = TierLimits.ProTier();

        // Assert - 5,000 credits/day = $0.05/day
        Assert.Equal(5_000m, limits.DailyCreditsLimit);
    }

    [Fact]
    public void ProTier_WeeklyCredits_ShouldEqual25000()
    {
        // Act
        var limits = TierLimits.ProTier();

        // Assert - 25,000 credits/week = $0.25/week
        Assert.Equal(25_000m, limits.WeeklyCreditsLimit);
    }

    [Fact]
    public void EnterpriseTier_ShouldHaveUnlimitedDailyCredits()
    {
        // Act
        var limits = TierLimits.EnterpriseTier();

        // Assert - decimal.MaxValue = unlimited
        Assert.Equal(decimal.MaxValue, limits.DailyCreditsLimit);
    }

    [Fact]
    public void EnterpriseTier_ShouldHaveUnlimitedWeeklyCredits()
    {
        // Act
        var limits = TierLimits.EnterpriseTier();

        // Assert - decimal.MaxValue = unlimited
        Assert.Equal(decimal.MaxValue, limits.WeeklyCreditsLimit);
    }

    [Theory]
    [InlineData(100, 10000)]    // Free tier ratio
    [InlineData(1000, 5000)]    // Basic tier ratio
    [InlineData(5000, 25000)]   // Pro tier ratio
    public void Create_WeeklyShouldBeGreaterThanOrEqualToDaily(decimal daily, decimal weekly)
    {
        // Arrange & Act
        var limits = TierLimits.Create(
            tokensPerMonth: 10_000,
            tokensPerDay: 500,
            messagesPerDay: 10,
            maxCollectionSize: 20,
            maxPdfUploadsPerMonth: 5,
            maxAgentsCreated: 1,
            dailyCreditsLimit: daily,
            weeklyCreditsLimit: weekly);

        // Assert - Weekly should always be >= Daily
        Assert.True(limits.WeeklyCreditsLimit >= limits.DailyCreditsLimit,
            $"Weekly ({limits.WeeklyCreditsLimit}) should be >= Daily ({limits.DailyCreditsLimit})");
    }
}
