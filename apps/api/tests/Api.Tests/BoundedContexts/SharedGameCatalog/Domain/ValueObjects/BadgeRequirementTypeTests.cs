using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Tests for the BadgeRequirementType enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 11
/// </summary>
[Trait("Category", "Unit")]
public sealed class BadgeRequirementTypeTests
{
    #region Enum Value Tests

    [Fact]
    public void ContributionCount_HasCorrectValue()
    {
        ((int)BadgeRequirementType.ContributionCount).Should().Be(0);
    }

    [Fact]
    public void DocumentCount_HasCorrectValue()
    {
        ((int)BadgeRequirementType.DocumentCount).Should().Be(1);
    }

    [Fact]
    public void QualityStreak_HasCorrectValue()
    {
        ((int)BadgeRequirementType.QualityStreak).Should().Be(2);
    }

    [Fact]
    public void TopContributor_HasCorrectValue()
    {
        ((int)BadgeRequirementType.TopContributor).Should().Be(3);
    }

    [Fact]
    public void FirstContribution_HasCorrectValue()
    {
        ((int)BadgeRequirementType.FirstContribution).Should().Be(4);
    }

    [Fact]
    public void Custom_HasCorrectValue()
    {
        ((int)BadgeRequirementType.Custom).Should().Be(5);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void BadgeRequirementType_HasSixValues()
    {
        var values = Enum.GetValues<BadgeRequirementType>();
        values.Should().HaveCount(6);
    }

    [Fact]
    public void BadgeRequirementType_AllValuesCanBeParsed()
    {
        var names = new[]
        {
            "ContributionCount",
            "DocumentCount",
            "QualityStreak",
            "TopContributor",
            "FirstContribution",
            "Custom"
        };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<BadgeRequirementType>(name);
            parsed.Should().BeOneOf(Enum.GetValues<BadgeRequirementType>());
        }
    }

    [Fact]
    public void BadgeRequirementType_ToString_ReturnsExpectedNames()
    {
        BadgeRequirementType.ContributionCount.ToString().Should().Be("ContributionCount");
        BadgeRequirementType.DocumentCount.ToString().Should().Be("DocumentCount");
        BadgeRequirementType.QualityStreak.ToString().Should().Be("QualityStreak");
        BadgeRequirementType.TopContributor.ToString().Should().Be("TopContributor");
        BadgeRequirementType.FirstContribution.ToString().Should().Be("FirstContribution");
        BadgeRequirementType.Custom.ToString().Should().Be("Custom");
    }

    #endregion

    #region Semantic Tests

    [Fact]
    public void ContributionCount_IsDefaultType()
    {
        // ContributionCount = 0 is the most common requirement type
        var defaultType = default(BadgeRequirementType);
        defaultType.Should().Be(BadgeRequirementType.ContributionCount);
    }

    [Fact]
    public void TypeValues_AreContiguousAndSequential()
    {
        var values = Enum.GetValues<BadgeRequirementType>()
            .Cast<int>()
            .OrderBy(x => x)
            .ToArray();

        for (int i = 0; i < values.Length; i++)
        {
            values[i].Should().Be(i, $"Expected type at position {i} to have value {i}");
        }
    }

    #endregion

    #region Conversion Tests

    [Theory]
    [InlineData(0, BadgeRequirementType.ContributionCount)]
    [InlineData(1, BadgeRequirementType.DocumentCount)]
    [InlineData(2, BadgeRequirementType.QualityStreak)]
    [InlineData(3, BadgeRequirementType.TopContributor)]
    [InlineData(4, BadgeRequirementType.FirstContribution)]
    [InlineData(5, BadgeRequirementType.Custom)]
    public void BadgeRequirementType_CastFromInt_ReturnsCorrectValue(int value, BadgeRequirementType expected)
    {
        ((BadgeRequirementType)value).Should().Be(expected);
    }

    [Fact]
    public void BadgeRequirementType_IsDefined_ReturnsTrueForValidValues()
    {
        for (int i = 0; i <= 5; i++)
        {
            Enum.IsDefined(typeof(BadgeRequirementType), i).Should().BeTrue();
        }
    }

    [Fact]
    public void BadgeRequirementType_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(BadgeRequirementType), 6).Should().BeFalse();
        Enum.IsDefined(typeof(BadgeRequirementType), -1).Should().BeFalse();
    }

    #endregion

    #region Semantic Category Tests

    [Fact]
    public void CountBasedTypes_AreFirstThree()
    {
        // Count-based requirements: ContributionCount, DocumentCount, QualityStreak
        var countBased = new[]
        {
            BadgeRequirementType.ContributionCount,
            BadgeRequirementType.DocumentCount,
            BadgeRequirementType.QualityStreak
        };

        foreach (var type in countBased)
        {
            ((int)type).Should().BeLessThan(3);
        }
    }

    [Fact]
    public void SpecialTypes_AreLaterValues()
    {
        // Special requirements: TopContributor, FirstContribution, Custom
        var special = new[]
        {
            BadgeRequirementType.TopContributor,
            BadgeRequirementType.FirstContribution,
            BadgeRequirementType.Custom
        };

        foreach (var type in special)
        {
            ((int)type).Should().BeGreaterThanOrEqualTo(3);
        }
    }

    [Fact]
    public void Custom_IsLastType()
    {
        // Custom has the highest value, indicating it's for special/complex cases
        var maxValue = Enum.GetValues<BadgeRequirementType>().Cast<int>().Max();
        ((int)BadgeRequirementType.Custom).Should().Be(maxValue);
    }

    #endregion
}
