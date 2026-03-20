using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the QuestionCategory enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 10
/// </summary>
[Trait("Category", "Unit")]
public sealed class QuestionCategoryTests
{
    #region Enum Value Tests

    [Fact]
    public void Gameplay_HasCorrectValue()
    {
        ((int)QuestionCategory.Gameplay).Should().Be(0);
    }

    [Fact]
    public void Rules_HasCorrectValue()
    {
        ((int)QuestionCategory.Rules).Should().Be(1);
    }

    [Fact]
    public void Winning_HasCorrectValue()
    {
        ((int)QuestionCategory.Winning).Should().Be(2);
    }

    [Fact]
    public void Setup_HasCorrectValue()
    {
        ((int)QuestionCategory.Setup).Should().Be(3);
    }

    [Fact]
    public void Strategy_HasCorrectValue()
    {
        ((int)QuestionCategory.Strategy).Should().Be(4);
    }

    [Fact]
    public void Clarifications_HasCorrectValue()
    {
        ((int)QuestionCategory.Clarifications).Should().Be(5);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void QuestionCategory_HasSixValues()
    {
        var values = Enum.GetValues<QuestionCategory>();
        values.Should().HaveCount(6);
    }

    [Fact]
    public void QuestionCategory_AllValuesCanBeParsed()
    {
        var names = new[] { "Gameplay", "Rules", "Winning", "Setup", "Strategy", "Clarifications" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<QuestionCategory>(name);
            parsed.Should().BeOneOf(Enum.GetValues<QuestionCategory>());
        }
    }

    [Fact]
    public void QuestionCategory_ToString_ReturnsExpectedNames()
    {
        QuestionCategory.Gameplay.ToString().Should().Be("Gameplay");
        QuestionCategory.Rules.ToString().Should().Be("Rules");
        QuestionCategory.Winning.ToString().Should().Be("Winning");
        QuestionCategory.Setup.ToString().Should().Be("Setup");
        QuestionCategory.Strategy.ToString().Should().Be("Strategy");
        QuestionCategory.Clarifications.ToString().Should().Be("Clarifications");
    }

    #endregion

    #region Semantic Tests

    [Fact]
    public void Gameplay_IsDefaultCategory()
    {
        var defaultCategory = default(QuestionCategory);
        defaultCategory.Should().Be(QuestionCategory.Gameplay);
    }

    [Fact]
    public void CategoryValues_AreContiguousAndSequential()
    {
        var values = Enum.GetValues<QuestionCategory>()
            .Cast<int>()
            .OrderBy(x => x)
            .ToArray();

        for (int i = 0; i < values.Length; i++)
        {
            values[i].Should().Be(i, $"Expected category at position {i} to have value {i}");
        }
    }

    #endregion

    #region Conversion Tests

    [Theory]
    [InlineData(0, QuestionCategory.Gameplay)]
    [InlineData(1, QuestionCategory.Rules)]
    [InlineData(2, QuestionCategory.Winning)]
    [InlineData(3, QuestionCategory.Setup)]
    [InlineData(4, QuestionCategory.Strategy)]
    [InlineData(5, QuestionCategory.Clarifications)]
    public void QuestionCategory_CastFromInt_ReturnsCorrectValue(int value, QuestionCategory expected)
    {
        ((QuestionCategory)value).Should().Be(expected);
    }

    [Fact]
    public void QuestionCategory_IsDefined_ReturnsTrueForValidValues()
    {
        for (int i = 0; i <= 5; i++)
        {
            Enum.IsDefined(typeof(QuestionCategory), i).Should().BeTrue();
        }
    }

    [Fact]
    public void QuestionCategory_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(QuestionCategory), 6).Should().BeFalse();
        Enum.IsDefined(typeof(QuestionCategory), -1).Should().BeFalse();
    }

    #endregion
}