using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Enums;

/// <summary>
/// Tests for the LlmEnvironmentType enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 9
/// </summary>
[Trait("Category", "Unit")]
public sealed class LlmEnvironmentTypeTests
{
    #region Enum Value Tests

    [Fact]
    public void Production_HasCorrectValue()
    {
        ((int)LlmEnvironmentType.Production).Should().Be(0);
    }

    [Fact]
    public void Test_HasCorrectValue()
    {
        ((int)LlmEnvironmentType.Test).Should().Be(1);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void LlmEnvironmentType_HasTwoValues()
    {
        var values = Enum.GetValues<LlmEnvironmentType>();
        values.Should().HaveCount(2);
    }

    [Fact]
    public void LlmEnvironmentType_AllValuesCanBeParsed()
    {
        var names = new[] { "Production", "Test" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<LlmEnvironmentType>(name);
            parsed.Should().BeOneOf(Enum.GetValues<LlmEnvironmentType>());
        }
    }

    [Fact]
    public void LlmEnvironmentType_ToString_ReturnsExpectedNames()
    {
        LlmEnvironmentType.Production.ToString().Should().Be("Production");
        LlmEnvironmentType.Test.ToString().Should().Be("Test");
    }

    #endregion

    #region Semantic Tests

    [Fact]
    public void Production_IsDefaultValue()
    {
        // Production = 0 is the default/primary environment
        var defaultType = default(LlmEnvironmentType);
        defaultType.Should().Be(LlmEnvironmentType.Production);
    }

    [Fact]
    public void Test_HasHigherValueThanProduction()
    {
        // Test environment has higher int value
        ((int)LlmEnvironmentType.Test).Should().BeGreaterThan((int)LlmEnvironmentType.Production);
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void LlmEnvironmentType_CastFromInt_ReturnsCorrectValues()
    {
        ((LlmEnvironmentType)0).Should().Be(LlmEnvironmentType.Production);
        ((LlmEnvironmentType)1).Should().Be(LlmEnvironmentType.Test);
    }

    [Fact]
    public void LlmEnvironmentType_IsDefined_ReturnsTrueForValidValues()
    {
        Enum.IsDefined(typeof(LlmEnvironmentType), 0).Should().BeTrue();
        Enum.IsDefined(typeof(LlmEnvironmentType), 1).Should().BeTrue();
    }

    [Fact]
    public void LlmEnvironmentType_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(LlmEnvironmentType), 2).Should().BeFalse();
        Enum.IsDefined(typeof(LlmEnvironmentType), -1).Should().BeFalse();
    }

    #endregion
}
