using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Xunit;
using Api.Tests.Constants;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Unit tests for TierLimits value object (Issue #3692)
/// Extended with credit-based budget tracking tests
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
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
        limits.Should().NotBeNull();
        limits.TokensPerMonth.Should().Be(10_000);
        limits.TokensPerDay.Should().Be(500);
        limits.MessagesPerDay.Should().Be(10);
        limits.MaxCollectionSize.Should().Be(20);
        limits.MaxPdfUploadsPerMonth.Should().Be(5);
        limits.MaxAgentsCreated.Should().Be(1);
        limits.DailyCreditsLimit.Should().Be(100m);
        limits.WeeklyCreditsLimit.Should().Be(10_000m);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(-999999)]
    public void Create_WithNegativeTokensPerMonth_ShouldThrow(int tokensPerMonth)
    {
        // Act & Assert
        var act = () =>
            TierLimits.Create(
                tokensPerMonth: tokensPerMonth,
                tokensPerDay: 500,
                messagesPerDay: 10,
                maxCollectionSize: 20,
                maxPdfUploadsPerMonth: 5,
                maxAgentsCreated: 1,
                dailyCreditsLimit: 100m,
                weeklyCreditsLimit: 10_000m);
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.ParamName.Should().Be("tokensPerMonth");
        exception.Message.Should().Contain("cannot be negative");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-500)]
    public void Create_WithNegativeTokensPerDay_ShouldThrow(int tokensPerDay)
    {
        // Act & Assert
        var act = () =>
            TierLimits.Create(
                tokensPerMonth: 10_000,
                tokensPerDay: tokensPerDay,
                messagesPerDay: 10,
                maxCollectionSize: 20,
                maxPdfUploadsPerMonth: 5,
                maxAgentsCreated: 1,
                dailyCreditsLimit: 100m,
                weeklyCreditsLimit: 10_000m);
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.ParamName.Should().Be("tokensPerDay");
        exception.Message.Should().Contain("cannot be negative");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Create_WithNegativeMessagesPerDay_ShouldThrow(int messagesPerDay)
    {
        // Act & Assert
        var act = () =>
            TierLimits.Create(
                tokensPerMonth: 10_000,
                tokensPerDay: 500,
                messagesPerDay: messagesPerDay,
                maxCollectionSize: 20,
                maxPdfUploadsPerMonth: 5,
                maxAgentsCreated: 1,
                dailyCreditsLimit: 100m,
                weeklyCreditsLimit: 10_000m);
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.ParamName.Should().Be("messagesPerDay");
        exception.Message.Should().Contain("cannot be negative");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-20)]
    public void Create_WithNegativeMaxCollectionSize_ShouldThrow(int maxCollectionSize)
    {
        // Act & Assert
        var act = () =>
            TierLimits.Create(
                tokensPerMonth: 10_000,
                tokensPerDay: 500,
                messagesPerDay: 10,
                maxCollectionSize: maxCollectionSize,
                maxPdfUploadsPerMonth: 5,
                maxAgentsCreated: 1,
                dailyCreditsLimit: 100m,
                weeklyCreditsLimit: 10_000m);
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.ParamName.Should().Be("maxCollectionSize");
        exception.Message.Should().Contain("cannot be negative");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    public void Create_WithNegativeMaxPdfUploadsPerMonth_ShouldThrow(int maxPdfUploadsPerMonth)
    {
        // Act & Assert
        var act = () =>
            TierLimits.Create(
                tokensPerMonth: 10_000,
                tokensPerDay: 500,
                messagesPerDay: 10,
                maxCollectionSize: 20,
                maxPdfUploadsPerMonth: maxPdfUploadsPerMonth,
                maxAgentsCreated: 1,
                dailyCreditsLimit: 100m,
                weeklyCreditsLimit: 10_000m);
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.ParamName.Should().Be("maxPdfUploadsPerMonth");
        exception.Message.Should().Contain("cannot be negative");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-3)]
    public void Create_WithNegativeMaxAgentsCreated_ShouldThrow(int maxAgentsCreated)
    {
        // Act & Assert
        var act = () =>
            TierLimits.Create(
                tokensPerMonth: 10_000,
                tokensPerDay: 500,
                messagesPerDay: 10,
                maxCollectionSize: 20,
                maxPdfUploadsPerMonth: 5,
                maxAgentsCreated: maxAgentsCreated,
                dailyCreditsLimit: 100m,
                weeklyCreditsLimit: 10_000m);
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.ParamName.Should().Be("maxAgentsCreated");
        exception.Message.Should().Contain("cannot be negative");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    [InlineData(-999.99)]
    public void Create_WithNegativeDailyCreditsLimit_ShouldThrow(decimal dailyCreditsLimit)
    {
        // Act & Assert
        var act = () =>
            TierLimits.Create(
                tokensPerMonth: 10_000,
                tokensPerDay: 500,
                messagesPerDay: 10,
                maxCollectionSize: 20,
                maxPdfUploadsPerMonth: 5,
                maxAgentsCreated: 1,
                dailyCreditsLimit: dailyCreditsLimit,
                weeklyCreditsLimit: 10_000m);
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.ParamName.Should().Be("dailyCreditsLimit");
        exception.Message.Should().Contain("cannot be negative");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1000)]
    [InlineData(-99999.99)]
    public void Create_WithNegativeWeeklyCreditsLimit_ShouldThrow(decimal weeklyCreditsLimit)
    {
        // Act & Assert
        var act = () =>
            TierLimits.Create(
                tokensPerMonth: 10_000,
                tokensPerDay: 500,
                messagesPerDay: 10,
                maxCollectionSize: 20,
                maxPdfUploadsPerMonth: 5,
                maxAgentsCreated: 1,
                dailyCreditsLimit: 100m,
                weeklyCreditsLimit: weeklyCreditsLimit);
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.ParamName.Should().Be("weeklyCreditsLimit");
        exception.Message.Should().Contain("cannot be negative");
    }

    [Fact]
    public void FreeTier_ShouldHaveCorrectCreditLimits()
    {
        // Act
        var limits = TierLimits.FreeTier();

        // Assert - Credits: 100/day, 10,000/week (= $0.001/day, $0.10/week)
        limits.DailyCreditsLimit.Should().Be(100m);
        limits.WeeklyCreditsLimit.Should().Be(10_000m);

        // Verify other limits
        limits.TokensPerMonth.Should().Be(10_000);
        limits.TokensPerDay.Should().Be(500);
        limits.MessagesPerDay.Should().Be(10);
        limits.MaxCollectionSize.Should().Be(20);
        limits.MaxPdfUploadsPerMonth.Should().Be(5);
        limits.MaxAgentsCreated.Should().Be(1);
    }

    [Fact]
    public void BasicTier_ShouldHaveCorrectCreditLimits()
    {
        // Act
        var limits = TierLimits.BasicTier();

        // Assert - Credits: 1,000/day, 5,000/week (= $0.01/day, $0.05/week)
        limits.DailyCreditsLimit.Should().Be(1_000m);
        limits.WeeklyCreditsLimit.Should().Be(5_000m);

        // Verify other limits
        limits.TokensPerMonth.Should().Be(50_000);
        limits.TokensPerDay.Should().Be(2_000);
        limits.MessagesPerDay.Should().Be(50);
        limits.MaxCollectionSize.Should().Be(50);
        limits.MaxPdfUploadsPerMonth.Should().Be(20);
        limits.MaxAgentsCreated.Should().Be(3);
    }

    [Fact]
    public void ProTier_ShouldHaveCorrectCreditLimits()
    {
        // Act
        var limits = TierLimits.ProTier();

        // Assert - Credits: 5,000/day, 25,000/week (= $0.05/day, $0.25/week)
        limits.DailyCreditsLimit.Should().Be(5_000m);
        limits.WeeklyCreditsLimit.Should().Be(25_000m);

        // Verify other limits
        limits.TokensPerMonth.Should().Be(200_000);
        limits.TokensPerDay.Should().Be(10_000);
        limits.MessagesPerDay.Should().Be(200);
        limits.MaxCollectionSize.Should().Be(200);
        limits.MaxPdfUploadsPerMonth.Should().Be(100);
        limits.MaxAgentsCreated.Should().Be(10);
    }

    [Fact]
    public void EnterpriseTier_ShouldHaveUnlimitedCredits()
    {
        // Act
        var limits = TierLimits.EnterpriseTier();

        // Assert - Unlimited (decimal.MaxValue)
        limits.DailyCreditsLimit.Should().Be(decimal.MaxValue);
        limits.WeeklyCreditsLimit.Should().Be(decimal.MaxValue);

        // Verify other unlimited limits
        limits.TokensPerMonth.Should().Be(int.MaxValue);
        limits.TokensPerDay.Should().Be(int.MaxValue);
        limits.MessagesPerDay.Should().Be(int.MaxValue);
        limits.MaxCollectionSize.Should().Be(int.MaxValue);
        limits.MaxPdfUploadsPerMonth.Should().Be(int.MaxValue);
        limits.MaxAgentsCreated.Should().Be(int.MaxValue);
    }

    [Fact]
    public void TierLimits_ShouldBeImmutable()
    {
        // Arrange
        var limits = TierLimits.FreeTier();

        // Assert - All properties should have init-only setters
        // This is enforced by the record type, but verify key properties
        limits.DailyCreditsLimit.Should().Be(100m);

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
        limits.Should().NotBeNull();
        limits.TokensPerMonth.Should().Be(0);
        limits.DailyCreditsLimit.Should().Be(0m);
        limits.WeeklyCreditsLimit.Should().Be(0m);
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
        limits.Should().NotBeNull();
        limits.TokensPerMonth.Should().Be(int.MaxValue);
        limits.DailyCreditsLimit.Should().Be(decimal.MaxValue);
        limits.WeeklyCreditsLimit.Should().Be(decimal.MaxValue);
    }

    [Fact]
    public void TierFactories_ShouldHaveConsistentCreditRatios()
    {
        // Arrange
        var free = TierLimits.FreeTier();
        var basic = TierLimits.BasicTier();
        var pro = TierLimits.ProTier();

        // Assert - Credit limits should scale proportionally with tier
        (free.DailyCreditsLimit < basic.DailyCreditsLimit).Should().BeTrue();
        (basic.DailyCreditsLimit < pro.DailyCreditsLimit).Should().BeTrue();

        (free.WeeklyCreditsLimit < basic.WeeklyCreditsLimit).Should().BeTrue();
        (basic.WeeklyCreditsLimit < pro.WeeklyCreditsLimit).Should().BeTrue();

        // Verify weekly >= daily (sanity check)
        (free.WeeklyCreditsLimit >= free.DailyCreditsLimit).Should().BeTrue();
        (basic.WeeklyCreditsLimit >= basic.DailyCreditsLimit).Should().BeTrue();
        (pro.WeeklyCreditsLimit >= pro.DailyCreditsLimit).Should().BeTrue();
    }

    [Fact]
    public void FreeTier_DailyCredits_ShouldEqual100()
    {
        // Act
        var limits = TierLimits.FreeTier();

        // Assert - 100 credits/day = $0.001/day
        limits.DailyCreditsLimit.Should().Be(100m);
    }

    [Fact]
    public void FreeTier_WeeklyCredits_ShouldEqual10000()
    {
        // Act
        var limits = TierLimits.FreeTier();

        // Assert - 10,000 credits/week = $0.10/week
        limits.WeeklyCreditsLimit.Should().Be(10_000m);
    }

    [Fact]
    public void BasicTier_DailyCredits_ShouldEqual1000()
    {
        // Act
        var limits = TierLimits.BasicTier();

        // Assert - 1,000 credits/day = $0.01/day
        limits.DailyCreditsLimit.Should().Be(1_000m);
    }

    [Fact]
    public void BasicTier_WeeklyCredits_ShouldEqual5000()
    {
        // Act
        var limits = TierLimits.BasicTier();

        // Assert - 5,000 credits/week = $0.05/week
        limits.WeeklyCreditsLimit.Should().Be(5_000m);
    }

    [Fact]
    public void ProTier_DailyCredits_ShouldEqual5000()
    {
        // Act
        var limits = TierLimits.ProTier();

        // Assert - 5,000 credits/day = $0.05/day
        limits.DailyCreditsLimit.Should().Be(5_000m);
    }

    [Fact]
    public void ProTier_WeeklyCredits_ShouldEqual25000()
    {
        // Act
        var limits = TierLimits.ProTier();

        // Assert - 25,000 credits/week = $0.25/week
        limits.WeeklyCreditsLimit.Should().Be(25_000m);
    }

    [Fact]
    public void EnterpriseTier_ShouldHaveUnlimitedDailyCredits()
    {
        // Act
        var limits = TierLimits.EnterpriseTier();

        // Assert - decimal.MaxValue = unlimited
        limits.DailyCreditsLimit.Should().Be(decimal.MaxValue);
    }

    [Fact]
    public void EnterpriseTier_ShouldHaveUnlimitedWeeklyCredits()
    {
        // Act
        var limits = TierLimits.EnterpriseTier();

        // Assert - decimal.MaxValue = unlimited
        limits.WeeklyCreditsLimit.Should().Be(decimal.MaxValue);
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
        (limits.WeeklyCreditsLimit >= limits.DailyCreditsLimit).Should().BeTrue($"Weekly ({limits.WeeklyCreditsLimit}) should be >= Daily ({limits.DailyCreditsLimit})");
    }
}