using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Unit tests for UserTier enum.
/// </summary>
public class UserTierTests
{
    #region Enum Values Tests

    [Fact]
    public void UserTier_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<UserTier>().Should().HaveCount(4);
    }

    [Fact]
    public void Free_HasValue0()
    {
        // Assert
        ((int)UserTier.Free).Should().Be(0);
    }

    [Fact]
    public void Premium_HasValue1()
    {
        // Assert
        ((int)UserTier.Premium).Should().Be(1);
    }

    [Fact]
    public void Pro_HasValue2()
    {
        // Assert
        ((int)UserTier.Pro).Should().Be(2);
    }

    [Fact]
    public void Admin_HasValue3()
    {
        // Assert
        ((int)UserTier.Admin).Should().Be(3);
    }

    #endregion

    #region Ordering Tests

    [Fact]
    public void Tiers_AreOrderedByPrivilege()
    {
        // Assert - cast to int for comparison
        ((int)UserTier.Free).Should().BeLessThan((int)UserTier.Premium);
        ((int)UserTier.Premium).Should().BeLessThan((int)UserTier.Pro);
        ((int)UserTier.Pro).Should().BeLessThan((int)UserTier.Admin);
    }

    #endregion

    #region ToString Tests

    [Theory]
    [InlineData(UserTier.Free, "Free")]
    [InlineData(UserTier.Premium, "Premium")]
    [InlineData(UserTier.Pro, "Pro")]
    [InlineData(UserTier.Admin, "Admin")]
    public void ToString_ReturnsExpectedString(UserTier tier, string expected)
    {
        // Assert
        tier.ToString().Should().Be(expected);
    }

    #endregion

    #region Parse Tests

    [Theory]
    [InlineData("Free", UserTier.Free)]
    [InlineData("Premium", UserTier.Premium)]
    [InlineData("Pro", UserTier.Pro)]
    [InlineData("Admin", UserTier.Admin)]
    public void Parse_WithValidString_ReturnsCorrectTier(string value, UserTier expected)
    {
        // Act
        var result = Enum.Parse<UserTier>(value);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("free")]
    [InlineData("PREMIUM")]
    [InlineData("pRo")]
    [InlineData("ADMIN")]
    public void Parse_WithIgnoreCase_ReturnsCorrectTier(string value)
    {
        // Act
        var result = Enum.Parse<UserTier>(value, ignoreCase: true);

        // Assert
        result.Should().BeOneOf(UserTier.Free, UserTier.Premium, UserTier.Pro, UserTier.Admin);
    }

    [Fact]
    public void Parse_WithInvalidString_ThrowsException()
    {
        // Act
        var act = () => Enum.Parse<UserTier>("InvalidTier");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region IsDefined Tests

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(-1, false)]
    [InlineData(100, false)]
    public void IsDefined_ReturnsExpectedResult(int value, bool expected)
    {
        // Assert
        Enum.IsDefined(typeof(UserTier), value).Should().Be(expected);
    }

    #endregion

    #region All Values Tests

    [Fact]
    public void GetValues_ReturnsAllTiers()
    {
        // Act
        var values = Enum.GetValues<UserTier>();

        // Assert
        values.Should().BeEquivalentTo(new[]
        {
            UserTier.Free,
            UserTier.Premium,
            UserTier.Pro,
            UserTier.Admin
        });
    }

    [Fact]
    public void GetNames_ReturnsAllTierNames()
    {
        // Act
        var names = Enum.GetNames<UserTier>();

        // Assert
        names.Should().BeEquivalentTo(new[]
        {
            "Free",
            "Premium",
            "Pro",
            "Admin"
        });
    }

    #endregion
}
