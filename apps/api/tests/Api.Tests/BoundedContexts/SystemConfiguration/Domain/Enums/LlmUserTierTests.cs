using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Enums;

/// <summary>
/// Tests for the LlmUserTier enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 9
/// </summary>
[Trait("Category", "Unit")]
public sealed class LlmUserTierTests
{
    #region Enum Value Tests

    [Fact]
    public void Anonymous_HasCorrectValue()
    {
        ((int)LlmUserTier.Anonymous).Should().Be(0);
    }

    [Fact]
    public void User_HasCorrectValue()
    {
        ((int)LlmUserTier.User).Should().Be(1);
    }

    [Fact]
    public void Editor_HasCorrectValue()
    {
        ((int)LlmUserTier.Editor).Should().Be(2);
    }

    [Fact]
    public void Admin_HasCorrectValue()
    {
        ((int)LlmUserTier.Admin).Should().Be(3);
    }

    [Fact]
    public void Premium_HasCorrectValue()
    {
        ((int)LlmUserTier.Premium).Should().Be(4);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void LlmUserTier_HasFiveValues()
    {
        var values = Enum.GetValues<LlmUserTier>();
        values.Should().HaveCount(5);
    }

    [Fact]
    public void LlmUserTier_AllValuesCanBeParsed()
    {
        var names = new[] { "Anonymous", "User", "Editor", "Admin", "Premium" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<LlmUserTier>(name);
            parsed.Should().BeOneOf(Enum.GetValues<LlmUserTier>());
        }
    }

    [Fact]
    public void LlmUserTier_ToString_ReturnsExpectedNames()
    {
        LlmUserTier.Anonymous.ToString().Should().Be("Anonymous");
        LlmUserTier.User.ToString().Should().Be("User");
        LlmUserTier.Editor.ToString().Should().Be("Editor");
        LlmUserTier.Admin.ToString().Should().Be("Admin");
        LlmUserTier.Premium.ToString().Should().Be("Premium");
    }

    #endregion

    #region Tier Hierarchy Tests

    [Fact]
    public void Anonymous_IsLowestTier()
    {
        var allTiers = Enum.GetValues<LlmUserTier>();
        var minTier = allTiers.Min(t => (int)t);
        ((int)LlmUserTier.Anonymous).Should().Be(minTier);
    }

    [Fact]
    public void Premium_IsHighestTier()
    {
        var allTiers = Enum.GetValues<LlmUserTier>();
        var maxTier = allTiers.Max(t => (int)t);
        ((int)LlmUserTier.Premium).Should().Be(maxTier);
    }

    [Fact]
    public void TierValues_AreContiguousAndSequential()
    {
        var values = Enum.GetValues<LlmUserTier>()
            .Cast<int>()
            .OrderBy(x => x)
            .ToArray();

        for (int i = 0; i < values.Length; i++)
        {
            values[i].Should().Be(i, $"Expected tier at position {i} to have value {i}");
        }
    }

    #endregion

    #region Conversion Tests

    [Theory]
    [InlineData(0, LlmUserTier.Anonymous)]
    [InlineData(1, LlmUserTier.User)]
    [InlineData(2, LlmUserTier.Editor)]
    [InlineData(3, LlmUserTier.Admin)]
    [InlineData(4, LlmUserTier.Premium)]
    public void LlmUserTier_CastFromInt_ReturnsCorrectValue(int value, LlmUserTier expected)
    {
        ((LlmUserTier)value).Should().Be(expected);
    }

    [Fact]
    public void LlmUserTier_IsDefined_ReturnsTrueForValidValues()
    {
        for (int i = 0; i <= 4; i++)
        {
            Enum.IsDefined(typeof(LlmUserTier), i).Should().BeTrue();
        }
    }

    [Fact]
    public void LlmUserTier_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(LlmUserTier), 5).Should().BeFalse();
        Enum.IsDefined(typeof(LlmUserTier), -1).Should().BeFalse();
    }

    #endregion

    #region Default Value Tests

    [Fact]
    public void DefaultValue_IsAnonymous()
    {
        var defaultTier = default(LlmUserTier);
        defaultTier.Should().Be(LlmUserTier.Anonymous);
    }

    #endregion
}
