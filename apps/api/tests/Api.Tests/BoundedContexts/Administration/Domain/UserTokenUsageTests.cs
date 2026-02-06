using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain;

/// <summary>
/// Unit tests for UserTokenUsage aggregate (Issue #3786)
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public sealed class UserTokenUsageTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateUserTokenUsage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tierId = Guid.NewGuid();

        // Act
        var usage = UserTokenUsage.Create(userId, tierId);

        // Assert
        usage.Should().NotBeNull();
        usage.Id.Should().NotBe(Guid.Empty);
        usage.UserId.Should().Be(userId);
        usage.TierId.Should().Be(tierId);
        usage.TokensUsed.Should().Be(0);
        usage.MessagesCount.Should().Be(0);
        usage.Cost.Should().Be(0m);
        usage.IsBlocked.Should().BeFalse();
        usage.IsNearLimit.Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrowArgumentException()
    {
        // Arrange
        var tierId = Guid.NewGuid();

        // Act
        var act = () => UserTokenUsage.Create(Guid.Empty, tierId);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Fact]
    public void RecordUsage_WithValidData_ShouldIncrementCounters()
    {
        // Arrange
        var usage = UserTokenUsage.Create(Guid.NewGuid(), Guid.NewGuid());

        // Act
        usage.RecordUsage(1000, 0.05m);

        // Assert
        usage.TokensUsed.Should().Be(1000);
        usage.MessagesCount.Should().Be(1);
        usage.Cost.Should().Be(0.05m);
    }

    [Fact]
    public void CheckLimits_At79Percent_ShouldNotSetWarning()
    {
        // Arrange
        var usage = UserTokenUsage.Create(Guid.NewGuid(), Guid.NewGuid());
        var limits = TierLimits.FreeTier(); // 10K tokens/month
        usage.RecordUsage(7900, 1m); // 79%

        // Act
        usage.CheckLimits(limits);

        // Assert
        usage.IsNearLimit.Should().BeFalse();
        usage.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void CheckLimits_At80Percent_ShouldSetWarningFlag()
    {
        // Arrange
        var usage = UserTokenUsage.Create(Guid.NewGuid(), Guid.NewGuid());
        var limits = TierLimits.FreeTier(); // 10K tokens/month
        usage.RecordUsage(8000, 1m); // 80%

        // Act
        usage.CheckLimits(limits);

        // Assert
        usage.IsNearLimit.Should().BeTrue();
        usage.IsBlocked.Should().BeFalse();
        usage.Warnings.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void CheckLimits_At100Percent_ShouldBlockUser()
    {
        // Arrange
        var usage = UserTokenUsage.Create(Guid.NewGuid(), Guid.NewGuid());
        var limits = TierLimits.FreeTier(); // 10K tokens/month
        usage.RecordUsage(10000, 1m); // 100%

        // Act
        usage.CheckLimits(limits);

        // Assert
        usage.IsBlocked.Should().BeTrue();
        usage.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public void ResetMonthlyUsage_ShouldClearCountersAndArchiveHistory()
    {
        // Arrange
        var usage = UserTokenUsage.Create(Guid.NewGuid(), Guid.NewGuid());
        usage.RecordUsage(5000, 2.5m);
        usage.RecordUsage(3000, 1.5m);

        // Act
        usage.ResetMonthlyUsage();

        // Assert
        usage.TokensUsed.Should().Be(0);
        usage.MessagesCount.Should().Be(0);
        usage.Cost.Should().Be(0m);
        usage.IsBlocked.Should().BeFalse();
        usage.IsNearLimit.Should().BeFalse();
        usage.Warnings.Should().BeEmpty();
        usage.History.Should().HaveCount(1); // Previous month archived
    }

    [Fact]
    public void Unblock_ShouldSetIsBlockedToFalse()
    {
        // Arrange
        var usage = UserTokenUsage.Create(Guid.NewGuid(), Guid.NewGuid());
        var limits = TierLimits.FreeTier();
        usage.RecordUsage(10000, 1m);
        usage.CheckLimits(limits); // Block user

        // Act
        usage.Unblock();

        // Assert
        usage.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void ChangeTier_WithValidTierId_ShouldUpdateTier()
    {
        // Arrange
        var usage = UserTokenUsage.Create(Guid.NewGuid(), Guid.NewGuid());
        var newTierId = Guid.NewGuid();

        // Act
        usage.ChangeTier(newTierId);

        // Assert
        usage.TierId.Should().Be(newTierId);
    }

    [Fact]
    public void ResetMonthlyUsage_WithMultipleResets_ShouldKeepLast12Months()
    {
        // Arrange
        var usage = UserTokenUsage.Create(Guid.NewGuid(), Guid.NewGuid());

        // Act - Simulate 15 months of usage
        for (int i = 0; i < 15; i++)
        {
            usage.RecordUsage(1000, 0.5m);
            usage.ResetMonthlyUsage();
        }

        // Assert
        usage.History.Should().HaveCount(12); // Only last 12 months retained
    }
}
