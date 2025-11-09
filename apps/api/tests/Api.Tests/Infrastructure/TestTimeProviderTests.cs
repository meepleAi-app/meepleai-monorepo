using Api.Tests.Helpers;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Example tests demonstrating TestTimeProvider usage.
/// These tests validate the time provider infrastructure itself.
/// </summary>
public class TestTimeProviderTests
{
    private readonly ITestOutputHelper _output;

    [Fact]
    public void CreateTimeProvider_DefaultsTo2025Jan1()
    {
        // Arrange & Act
        var provider = TimeTestHelpers.CreateTimeProvider();

        // Assert
        var now = provider.GetUtcNow();
        now.Year.Should().Be(2025);
        now.Month.Should().Be(1);
        now.Day.Should().Be(1);
        now.Hour.Should().Be(0);
        now.Minute.Should().Be(0);
        now.Second.Should().Be(0);
    }

    [Fact]
    public void CreateTimeProvider_CustomStartTime()
    {
        // Arrange & Act
        var start = new DateTimeOffset(2025, 3, 15, 10, 30, 45, TimeSpan.Zero);
        var provider = new FakeTimeProvider(start);

        // Assert
        var now = provider.GetUtcNow();
        now.Should().Be(start);
    }

    [Fact]
    public void AdvanceSeconds_IncreasesTime()
    {
        // Arrange
        var provider = TimeTestHelpers.CreateTimeProvider();
        var before = provider.GetUtcNow();

        // Act
        provider.Advance(TimeSpan.FromSeconds(30));

        // Assert
        var after = provider.GetUtcNow();
        (after - before).TotalSeconds.Should().Be(30);
    }

    [Fact]
    public void AdvanceMinutes_IncreasesTime()
    {
        // Arrange
        var provider = TimeTestHelpers.CreateTimeProvider();
        var before = provider.GetUtcNow();

        // Act
        provider.Advance(TimeSpan.FromMinutes(5));

        // Assert
        var after = provider.GetUtcNow();
        (after - before).TotalMinutes.Should().Be(5);
    }

    [Fact]
    public void AdvanceHours_IncreasesTime()
    {
        // Arrange
        var provider = TimeTestHelpers.CreateTimeProvider();
        var before = provider.GetUtcNow();

        // Act
        provider.Advance(TimeSpan.FromHours(2));

        // Assert
        var after = provider.GetUtcNow();
        (after - before).TotalHours.Should().Be(2);
    }

    [Fact]
    public void AdvanceDays_IncreasesTime()
    {
        // Arrange
        var provider = TimeTestHelpers.CreateTimeProvider();
        var before = provider.GetUtcNow();

        // Act
        provider.Advance(TimeSpan.FromDays(30));

        // Assert
        var after = provider.GetUtcNow();
        (after - before).TotalDays.Should().Be(30);
    }

    [Fact]
    public void SetTime_ChangesTimeAbsolutely()
    {
        // Arrange
        var provider = TimeTestHelpers.CreateTimeProvider();

        // Act
        var target = new DateTimeOffset(2025, 12, 25, 23, 59, 59, TimeSpan.Zero);
        provider.SetUtcNow(target);

        // Assert
        provider.GetUtcNow().Should().Be(target);
    }

    [Fact]
    public void Reset_RestoresDefaultTime()
    {
        // Arrange
        var provider = TimeTestHelpers.CreateTimeProvider();
        provider.Advance(TimeSpan.FromDays(100));

        // Act
        var defaultTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        provider.SetUtcNow(defaultTime);

        // Assert
        var now = provider.GetUtcNow();
        now.Should().Be(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Advance_NegativeDuration_ThrowsException()
    {
        // Arrange
        var provider = TimeTestHelpers.CreateTimeProvider();

        // Act & Assert
        var act = () => provider.Advance(TimeSpan.FromMinutes(-5));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetTimestamp_ReturnsConsistentValue()
    {
        // Arrange
        var provider = TimeTestHelpers.CreateTimeProvider();

        // Act
        var timestamp1 = provider.GetTimestamp();
        var timestamp2 = provider.GetTimestamp();

        // Assert (should be same since time hasn't advanced)
        timestamp2.Should().Be(timestamp1);
    }

    [Fact]
    public void GetTimestamp_ChangesAfterAdvance()
    {
        // Arrange
        var provider = TimeTestHelpers.CreateTimeProvider();
        var timestamp1 = provider.GetTimestamp();

        // Act
        provider.Advance(TimeSpan.FromSeconds(10));
        var timestamp2 = provider.GetTimestamp();

        // Assert
        (timestamp2 > timestamp1).Should().BeTrue();
    }

    [Fact]
    public void GetElapsedTime_CalculatesCorrectly()
    {
        // Arrange
        var provider = TimeTestHelpers.CreateTimeProvider();
        var start = provider.GetTimestamp();

        // Act
        provider.Advance(TimeSpan.FromSeconds(5));
        var end = provider.GetTimestamp();
        var elapsed = provider.GetElapsedTime(start, end);

        // Assert
        (elapsed.TotalSeconds >= 4.9 && elapsed.TotalSeconds <= 5.1).Should().BeTrue();
    }

    [Fact]
    public void LocalTimeZone_ReturnsUtc()
    {
        // Arrange & Act
        var provider = TimeTestHelpers.CreateTimeProvider();

        // Assert
        provider.LocalTimeZone.Should().Be(TimeZoneInfo.Utc);
    }

    [Fact]
    public void GetLocalNow_ReturnsSameAsUtcNow()
    {
        // Arrange
        var provider = TimeTestHelpers.CreateTimeProvider();

        // Act
        var utcNow = provider.GetUtcNow();
        var localNow = provider.GetLocalNow();

        // Assert (for testing, local = UTC)
        localNow.Should().Be(utcNow);
    }
}

/// <summary>
/// Example tests demonstrating TimeTestHelpers usage patterns.
/// </summary>
public class TimeTestHelpersTests
{
    [Fact]
    public void CreateTimeProvider_WithYear_CreatesCorrectTime()
    {
        // Act
        var provider = TimeTestHelpers.CreateTimeProvider(2025, 6, 15);

        // Assert
        var now = provider.GetUtcNow();
        now.Year.Should().Be(2025);
        now.Month.Should().Be(6);
        now.Day.Should().Be(15);
    }

    [Fact]
    public void AdvanceSeconds_Extension_WorksCorrectly()
    {
        // Arrange
        var provider = TimeTestHelpers.CreateTimeProvider();
        var before = provider.GetUtcNow();

        // Act
        provider.AdvanceSeconds(45);

        // Assert
        var after = provider.GetUtcNow();
        (after - before).TotalSeconds.Should().Be(45);
    }

    [Fact]
    public void AdvanceMinutes_Extension_WorksCorrectly()
    {
        // Arrange
        var provider = TimeTestHelpers.CreateTimeProvider();
        var before = provider.GetUtcNow();

        // Act
        provider.AdvanceMinutes(10);

        // Assert
        var after = provider.GetUtcNow();
        (after - before).TotalMinutes.Should().Be(10);
    }

    [Fact]
    public void AdvanceToSessionExpiration_DefaultIs30Days()
    {
        // Arrange
        var provider = TimeTestHelpers.CreateTimeProvider();
        var before = provider.GetUtcNow();

        // Act
        provider.AdvanceToSessionExpiration();

        // Assert
        var after = provider.GetUtcNow();
        (after - before).TotalDays.Should().Be(30);
    }

    [Fact]
    public void AdvanceToTempSessionExpiration_DefaultIs5Minutes()
    {
        // Arrange
        var provider = TimeTestHelpers.CreateTimeProvider();
        var before = provider.GetUtcNow();

        // Act
        provider.AdvanceToTempSessionExpiration();

        // Assert
        var after = provider.GetUtcNow();
        (after - before).TotalMinutes.Should().Be(5);
    }

    [Fact]
    public void ChainedOperations_WorkCorrectly()
    {
        // Arrange & Act
        var provider = TimeTestHelpers.CreateTimeProvider(2025, 1, 1)
            .AdvanceHours(6)
            .AdvanceMinutes(30)
            .AdvanceSeconds(15);

        // Assert
        var now = provider.GetUtcNow();
        now.Should().Be(new DateTimeOffset(2025, 1, 1, 6, 30, 15, TimeSpan.Zero));
    }
}

/// <summary>
/// Example tests demonstrating TimeAssertions helpers.
/// </summary>
public class TimeAssertionsTests
{
    [Fact]
    public void AssertTimeNear_WithinTolerance_Passes()
    {
        // Arrange
        var actual = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var expected = new DateTimeOffset(2025, 1, 1, 12, 0, 0, 500, TimeSpan.Zero); // 500ms diff

        // Act & Assert (should not throw)
        TimeAssertions.AssertTimeNear(actual, expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AssertTimeNear_OutsideTolerance_Throws()
    {
        // Arrange
        var actual = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var expected = new DateTimeOffset(2025, 1, 1, 12, 0, 5, TimeSpan.Zero); // 5 seconds diff

        // Act & Assert
        var act = () => TimeAssertions.AssertTimeNear(actual, expected, TimeSpan.FromSeconds(1));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AssertElapsedTime_WithinTolerance_Passes()
    {
        // Arrange
        var actual = TimeSpan.FromSeconds(5.05);
        var expected = TimeSpan.FromSeconds(5);

        // Act & Assert (should not throw)
        TimeAssertions.AssertElapsedTime(actual, expected, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void AssertTimeAfter_Correct_Passes()
    {
        // Arrange
        var after = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var before = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act & Assert (should not throw)
        TimeAssertions.AssertTimeAfter(after, before);
    }

    [Fact]
    public void AssertTimeBefore_Correct_Passes()
    {
        // Arrange
        var before = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var after = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero);

        // Act & Assert (should not throw)
        TimeAssertions.AssertTimeBefore(before, after);
    }
}