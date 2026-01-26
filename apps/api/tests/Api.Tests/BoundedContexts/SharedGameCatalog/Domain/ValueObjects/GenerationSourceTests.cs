using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Tests for the GenerationSource enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 5
/// </summary>
[Trait("Category", "Unit")]
public sealed class GenerationSourceTests
{
    #region Enum Value Tests

    [Fact]
    public void GenerationSource_AI_HasCorrectValue()
    {
        ((int)GenerationSource.AI).Should().Be(0);
    }

    [Fact]
    public void GenerationSource_Manual_HasCorrectValue()
    {
        ((int)GenerationSource.Manual).Should().Be(1);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void GenerationSource_HasTwoValues()
    {
        var values = Enum.GetValues<GenerationSource>();
        values.Should().HaveCount(2);
    }

    [Fact]
    public void GenerationSource_AllValuesCanBeParsed()
    {
        var names = new[] { "AI", "Manual" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<GenerationSource>(name);
            parsed.Should().BeOneOf(Enum.GetValues<GenerationSource>());
        }
    }

    [Fact]
    public void GenerationSource_ToString_ReturnsExpectedNames()
    {
        GenerationSource.AI.ToString().Should().Be("AI");
        GenerationSource.Manual.ToString().Should().Be("Manual");
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void GenerationSource_CastFromInt_ReturnsCorrectSources()
    {
        ((GenerationSource)0).Should().Be(GenerationSource.AI);
        ((GenerationSource)1).Should().Be(GenerationSource.Manual);
    }

    [Fact]
    public void GenerationSource_IsDefined_ReturnsTrueForValidValues()
    {
        Enum.IsDefined(typeof(GenerationSource), 0).Should().BeTrue();
        Enum.IsDefined(typeof(GenerationSource), 1).Should().BeTrue();
    }

    [Fact]
    public void GenerationSource_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(GenerationSource), 2).Should().BeFalse();
        Enum.IsDefined(typeof(GenerationSource), -1).Should().BeFalse();
        Enum.IsDefined(typeof(GenerationSource), 100).Should().BeFalse();
    }

    #endregion
}
