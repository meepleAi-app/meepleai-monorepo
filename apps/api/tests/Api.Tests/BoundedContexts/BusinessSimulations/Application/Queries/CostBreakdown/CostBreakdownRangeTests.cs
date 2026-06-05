using Api.BoundedContexts.BusinessSimulations.Application.Queries.CostBreakdown;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Queries.CostBreakdown;

/// <summary>
/// Unit tests for <see cref="CostBreakdownRange"/> wire parsing (Issue #1838 SP5 F4-C5).
///
/// <para>Test data uses <see cref="int"/> (cast to the enum inside each test)
/// so the public xUnit theory signatures don't leak the internal enum —
/// mirrors the AlertChannelType test pattern from PR #1840.</para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
[Trait("Issue", "1838")]
public sealed class CostBreakdownRangeTests
{
    [Theory]
    [InlineData("7d", 7)]
    [InlineData("30d", 30)]
    [InlineData("90d", 90)]
    [InlineData("1y", 365)]
    [InlineData("365d", 365)]
    [InlineData("30D", 30)]
    public void FromWireValue_ParsesKnownValues(string wire, int expectedDays)
    {
        var expected = (CostBreakdownRange)expectedDays;
        CostBreakdownRangeExtensions.FromWireValue(wire).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nonsense")]
    public void FromWireValue_UnknownValue_DefaultsTo30Days(string? wire)
    {
        CostBreakdownRangeExtensions.FromWireValue(wire).Should().Be(CostBreakdownRange.ThirtyDays);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(365)]
    public void Days_ReturnsExpectedWindow(int rangeValue)
    {
        var range = (CostBreakdownRange)rangeValue;
        range.Days().Should().Be(rangeValue);
    }

    [Theory]
    [InlineData(7, "7d")]
    [InlineData(30, "30d")]
    [InlineData(90, "90d")]
    [InlineData(365, "1y")]
    public void ToWireValue_IsRoundTripStable(int rangeValue, string expected)
    {
        var range = (CostBreakdownRange)rangeValue;
        range.ToWireValue().Should().Be(expected);
    }
}
