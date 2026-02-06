using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Tests for the ContributionType enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 5
/// </summary>
[Trait("Category", "Unit")]
public sealed class ContributionTypeTests
{
    #region Enum Value Tests

    [Fact]
    public void ContributionType_NewGame_HasCorrectValue()
    {
        ((int)ContributionType.NewGame).Should().Be(0);
    }

    [Fact]
    public void ContributionType_AdditionalContent_HasCorrectValue()
    {
        ((int)ContributionType.AdditionalContent).Should().Be(1);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void ContributionType_HasThreeValues()
    {
        var values = Enum.GetValues<ContributionType>();
        values.Should().HaveCount(3);
    }

    [Fact]
    public void ContributionType_AllValuesCanBeParsed()
    {
        var names = new[] { "NewGame", "AdditionalContent", "NewGameProposal" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<ContributionType>(name);
            parsed.Should().BeOneOf(Enum.GetValues<ContributionType>());
        }
    }

    [Fact]
    public void ContributionType_ToString_ReturnsExpectedNames()
    {
        ContributionType.NewGame.ToString().Should().Be("NewGame");
        ContributionType.AdditionalContent.ToString().Should().Be("AdditionalContent");
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void ContributionType_CastFromInt_ReturnsCorrectTypes()
    {
        ((ContributionType)0).Should().Be(ContributionType.NewGame);
        ((ContributionType)1).Should().Be(ContributionType.AdditionalContent);
    }

    [Fact]
    public void ContributionType_IsDefined_ReturnsTrueForValidValues()
    {
        Enum.IsDefined(typeof(ContributionType), 0).Should().BeTrue();
        Enum.IsDefined(typeof(ContributionType), 1).Should().BeTrue();
    }

    [Fact]
    public void ContributionType_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(ContributionType), 3).Should().BeFalse();
        Enum.IsDefined(typeof(ContributionType), -1).Should().BeFalse();
        Enum.IsDefined(typeof(ContributionType), 100).Should().BeFalse();
    }

    #endregion
}
