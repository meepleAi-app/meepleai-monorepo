using Api.SharedKernel.Enums;
using Xunit;
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
        Assert.Equal(0, (int)PerformanceBudgetStatus.Pass);
        Assert.Equal(1, (int)PerformanceBudgetStatus.Warning);
        Assert.Equal(2, (int)PerformanceBudgetStatus.Fail);
        Assert.Equal(3, (int)PerformanceBudgetStatus.NoData);
    }

    [Fact]
    public void PerformanceBudgetStatus_ShouldHaveAllExpectedMembers()
    {
        // Arrange
        var expectedMembers = new[] { "Pass", "Warning", "Fail", "NoData" };

        // Act
        var actualMembers = Enum.GetNames(typeof(PerformanceBudgetStatus));

        // Assert
        Assert.Equal(expectedMembers.Length, actualMembers.Length);
        Assert.All(expectedMembers, expected => Assert.Contains(expected, actualMembers));
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
        Assert.Equal(expectedName, result);
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
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PerformanceBudgetStatus_Parse_WithInvalidValue_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Enum.Parse<PerformanceBudgetStatus>("Invalid"));
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
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PerformanceBudgetStatus_ShouldSupportComparison()
    {
        // Assert - Verify logical ordering: Pass < Warning < Fail < NoData
        Assert.True(PerformanceBudgetStatus.Pass < PerformanceBudgetStatus.Warning);
        Assert.True(PerformanceBudgetStatus.Warning < PerformanceBudgetStatus.Fail);
        Assert.True(PerformanceBudgetStatus.Fail < PerformanceBudgetStatus.NoData);
    }

    [Fact]
    public void PerformanceBudgetStatus_ShouldBeValueTypeEnum()
    {
        // Assert
        Assert.True(typeof(PerformanceBudgetStatus).IsEnum);
        Assert.True(typeof(PerformanceBudgetStatus).IsValueType);
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(PerformanceBudgetStatus)));
    }
}
