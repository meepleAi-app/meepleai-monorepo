using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Tests for the BadgeTier enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 5
/// </summary>
[Trait("Category", "Unit")]
public sealed class BadgeTierTests
{
    #region Enum Value Tests

    [Fact]
    public void BadgeTier_Bronze_HasCorrectValue()
    {
        ((int)BadgeTier.Bronze).Should().Be(0);
    }

    [Fact]
    public void BadgeTier_Silver_HasCorrectValue()
    {
        ((int)BadgeTier.Silver).Should().Be(1);
    }

    [Fact]
    public void BadgeTier_Gold_HasCorrectValue()
    {
        ((int)BadgeTier.Gold).Should().Be(2);
    }

    [Fact]
    public void BadgeTier_Platinum_HasCorrectValue()
    {
        ((int)BadgeTier.Platinum).Should().Be(3);
    }

    [Fact]
    public void BadgeTier_Diamond_HasCorrectValue()
    {
        ((int)BadgeTier.Diamond).Should().Be(4);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void BadgeTier_HasFiveValues()
    {
        var values = Enum.GetValues<BadgeTier>();
        values.Should().HaveCount(5);
    }

    [Fact]
    public void BadgeTier_AllValuesCanBeParsed()
    {
        var names = new[] { "Bronze", "Silver", "Gold", "Platinum", "Diamond" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<BadgeTier>(name);
            parsed.Should().BeOneOf(Enum.GetValues<BadgeTier>());
        }
    }

    [Fact]
    public void BadgeTier_ToString_ReturnsExpectedNames()
    {
        BadgeTier.Bronze.ToString().Should().Be("Bronze");
        BadgeTier.Silver.ToString().Should().Be("Silver");
        BadgeTier.Gold.ToString().Should().Be("Gold");
        BadgeTier.Platinum.ToString().Should().Be("Platinum");
        BadgeTier.Diamond.ToString().Should().Be("Diamond");
    }

    #endregion

    #region Tier Ordering Tests

    [Fact]
    public void BadgeTier_ValuesAreInAscendingOrder()
    {
        // Verify the tier values are in expected ascending order of prestige
        ((int)BadgeTier.Bronze).Should().BeLessThan((int)BadgeTier.Silver);
        ((int)BadgeTier.Silver).Should().BeLessThan((int)BadgeTier.Gold);
        ((int)BadgeTier.Gold).Should().BeLessThan((int)BadgeTier.Platinum);
        ((int)BadgeTier.Platinum).Should().BeLessThan((int)BadgeTier.Diamond);
    }

    [Fact]
    public void BadgeTier_CanBeComparedNumerically()
    {
        var tiers = Enum.GetValues<BadgeTier>().OrderBy(t => (int)t).ToArray();

        tiers[0].Should().Be(BadgeTier.Bronze);
        tiers[1].Should().Be(BadgeTier.Silver);
        tiers[2].Should().Be(BadgeTier.Gold);
        tiers[3].Should().Be(BadgeTier.Platinum);
        tiers[4].Should().Be(BadgeTier.Diamond);
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void BadgeTier_CastFromInt_ReturnsCorrectTiers()
    {
        ((BadgeTier)0).Should().Be(BadgeTier.Bronze);
        ((BadgeTier)1).Should().Be(BadgeTier.Silver);
        ((BadgeTier)2).Should().Be(BadgeTier.Gold);
        ((BadgeTier)3).Should().Be(BadgeTier.Platinum);
        ((BadgeTier)4).Should().Be(BadgeTier.Diamond);
    }

    [Fact]
    public void BadgeTier_IsDefined_ReturnsTrueForValidValues()
    {
        for (int i = 0; i <= 4; i++)
        {
            Enum.IsDefined(typeof(BadgeTier), i).Should().BeTrue();
        }
    }

    [Fact]
    public void BadgeTier_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(BadgeTier), 5).Should().BeFalse();
        Enum.IsDefined(typeof(BadgeTier), -1).Should().BeFalse();
        Enum.IsDefined(typeof(BadgeTier), 100).Should().BeFalse();
    }

    #endregion
}