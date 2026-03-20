using Api.SharedKernel.Enums;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.SharedKernel.Enums;

/// <summary>
/// Unit tests for PerformanceBudgetStatus enum (Issue #2376).
/// Verifies enum values and type safety.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class PerformanceBudgetStatusTests
{
    [Fact]
    public void PerformanceBudgetStatus_ShouldHaveCorrectValues()
    {
        // Assert - Verify enum values match specification
        ((int)PerformanceBudgetStatus.Pass).Should().Be(0);
        ((int)PerformanceBudgetStatus.Warning).Should().Be(1);
        ((int)PerformanceBudgetStatus.Fail).Should().Be(2);
        ((int)PerformanceBudgetStatus.NoData).Should().Be(3);
    }

    [Fact]
    public void PerformanceBudgetStatus_ShouldHaveAllExpectedMembers()
    {
        // Arrange
        var expectedMembers = new[] { "Pass", "Warning", "Fail", "NoData" };

        // Act
        var actualMembers = Enum.GetNames(typeof(PerformanceBudgetStatus));

        // Assert
        actualMembers.Length.Should().Be(expectedMembers.Length);
        Assert.All(expectedMembers, expected => actualMembers.Should().Contain(expected));
    }

    [Theory]
    [InlineData(PerformanceBudgetStatus.Pass, "Pass")]
    [InlineData(PerformanceBudgetStatus.Warning, "Warning")]
    [InlineData(PerformanceBudgetStatus.Fail, "Fail")]
    [InlineData(PerformanceBudgetStatus.NoData, "NoData")]
    public void PerformanceBudgetStatus_ToString_ShouldReturnCorrectName(PerformanceBudgetStatus status, string expectedName)
    {
        // Act
        var result = status.ToString();

        // Assert
        result.Should().Be(expectedName);
    }

    [Theory]
    [InlineData("Pass", PerformanceBudgetStatus.Pass)]
    [InlineData("Warning", PerformanceBudgetStatus.Warning)]
    [InlineData("Fail", PerformanceBudgetStatus.Fail)]
    [InlineData("NoData", PerformanceBudgetStatus.NoData)]
    public void PerformanceBudgetStatus_Parse_ShouldParseCorrectly(string input, PerformanceBudgetStatus expected)
    {
        // Act
        var result = Enum.Parse<PerformanceBudgetStatus>(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void PerformanceBudgetStatus_Parse_WithInvalidValue_ShouldThrowException()
    {
        // Act & Assert
        ((Action)(() => Enum.Parse<PerformanceBudgetStatus>("Invalid"))).Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0, PerformanceBudgetStatus.Pass)]
    [InlineData(1, PerformanceBudgetStatus.Warning)]
    [InlineData(2, PerformanceBudgetStatus.Fail)]
    [InlineData(3, PerformanceBudgetStatus.NoData)]
    public void PerformanceBudgetStatus_CastFromInt_ShouldWorkCorrectly(int value, PerformanceBudgetStatus expected)
    {
        // Act
        var result = (PerformanceBudgetStatus)value;

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void PerformanceBudgetStatus_ShouldSupportComparison()
    {
        // Assert - Verify logical ordering: Pass < Warning < Fail < NoData
        (PerformanceBudgetStatus.Pass < PerformanceBudgetStatus.Warning).Should().BeTrue();
        (PerformanceBudgetStatus.Warning < PerformanceBudgetStatus.Fail).Should().BeTrue();
        (PerformanceBudgetStatus.Fail < PerformanceBudgetStatus.NoData).Should().BeTrue();
    }

    [Fact]
    public void PerformanceBudgetStatus_ShouldBeValueTypeEnum()
    {
        // Assert
        typeof(PerformanceBudgetStatus).IsEnum.Should().BeTrue();
        typeof(PerformanceBudgetStatus).IsValueType.Should().BeTrue();
        Enum.GetUnderlyingType(typeof(PerformanceBudgetStatus)).Should().Be(typeof(int));
    }
}
