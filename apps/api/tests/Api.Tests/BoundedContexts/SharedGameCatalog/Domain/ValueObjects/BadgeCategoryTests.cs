using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Tests for the BadgeCategory enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 5
/// </summary>
[Trait("Category", "Unit")]
public sealed class BadgeCategoryTests
{
    #region Enum Value Tests

    [Fact]
    public void BadgeCategory_Contribution_HasCorrectValue()
    {
        ((int)BadgeCategory.Contribution).Should().Be(0);
    }

    [Fact]
    public void BadgeCategory_Quality_HasCorrectValue()
    {
        ((int)BadgeCategory.Quality).Should().Be(1);
    }

    [Fact]
    public void BadgeCategory_Engagement_HasCorrectValue()
    {
        ((int)BadgeCategory.Engagement).Should().Be(2);
    }

    [Fact]
    public void BadgeCategory_Special_HasCorrectValue()
    {
        ((int)BadgeCategory.Special).Should().Be(3);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void BadgeCategory_HasFourValues()
    {
        var values = Enum.GetValues<BadgeCategory>();
        values.Should().HaveCount(4);
    }

    [Fact]
    public void BadgeCategory_AllValuesCanBeParsed()
    {
        var names = new[] { "Contribution", "Quality", "Engagement", "Special" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<BadgeCategory>(name);
            parsed.Should().BeOneOf(Enum.GetValues<BadgeCategory>());
        }
    }

    [Fact]
    public void BadgeCategory_ToString_ReturnsExpectedNames()
    {
        BadgeCategory.Contribution.ToString().Should().Be("Contribution");
        BadgeCategory.Quality.ToString().Should().Be("Quality");
        BadgeCategory.Engagement.ToString().Should().Be("Engagement");
        BadgeCategory.Special.ToString().Should().Be("Special");
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void BadgeCategory_CastFromInt_ReturnsCorrectCategories()
    {
        ((BadgeCategory)0).Should().Be(BadgeCategory.Contribution);
        ((BadgeCategory)1).Should().Be(BadgeCategory.Quality);
        ((BadgeCategory)2).Should().Be(BadgeCategory.Engagement);
        ((BadgeCategory)3).Should().Be(BadgeCategory.Special);
    }

    [Fact]
    public void BadgeCategory_IsDefined_ReturnsTrueForValidValues()
    {
        Enum.IsDefined(typeof(BadgeCategory), 0).Should().BeTrue();
        Enum.IsDefined(typeof(BadgeCategory), 1).Should().BeTrue();
        Enum.IsDefined(typeof(BadgeCategory), 2).Should().BeTrue();
        Enum.IsDefined(typeof(BadgeCategory), 3).Should().BeTrue();
    }

    [Fact]
    public void BadgeCategory_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(BadgeCategory), 4).Should().BeFalse();
        Enum.IsDefined(typeof(BadgeCategory), -1).Should().BeFalse();
        Enum.IsDefined(typeof(BadgeCategory), 100).Should().BeFalse();
    }

    #endregion
}