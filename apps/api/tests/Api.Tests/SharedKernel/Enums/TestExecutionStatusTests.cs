using Api.SharedKernel.Enums;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.SharedKernel.Enums;

/// <summary>
/// Unit tests for TestExecutionStatus enum (Issue #2376).
/// Verifies enum values and type safety.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class TestExecutionStatusTests
{
    [Fact]
    public void TestExecutionStatus_ShouldHaveCorrectValues()
    {
        // Assert - Verify enum values match specification
        Assert.Equal(0, (int)TestExecutionStatus.Pass);
        Assert.Equal(1, (int)TestExecutionStatus.Warning);
        Assert.Equal(2, (int)TestExecutionStatus.Fail);
        Assert.Equal(3, (int)TestExecutionStatus.NoData);
    }

    [Fact]
    public void TestExecutionStatus_ShouldHaveAllExpectedMembers()
    {
        // Arrange
        var expectedMembers = new[] { "Pass", "Warning", "Fail", "NoData" };

        // Act
        var actualMembers = Enum.GetNames(typeof(TestExecutionStatus));

        // Assert
        Assert.Equal(expectedMembers.Length, actualMembers.Length);
        Assert.All(expectedMembers, expected => Assert.Contains(expected, actualMembers));
    }

    [Theory]
    [InlineData(TestExecutionStatus.Pass, "Pass")]
    [InlineData(TestExecutionStatus.Warning, "Warning")]
    [InlineData(TestExecutionStatus.Fail, "Fail")]
    [InlineData(TestExecutionStatus.NoData, "NoData")]
    public void TestExecutionStatus_ToString_ShouldReturnCorrectName(TestExecutionStatus status, string expectedName)
    {
        // Act
        var result = status.ToString();

        // Assert
        Assert.Equal(expectedName, result);
    }

    [Theory]
    [InlineData("Pass", TestExecutionStatus.Pass)]
    [InlineData("Warning", TestExecutionStatus.Warning)]
    [InlineData("Fail", TestExecutionStatus.Fail)]
    [InlineData("NoData", TestExecutionStatus.NoData)]
    public void TestExecutionStatus_Parse_ShouldParseCorrectly(string input, TestExecutionStatus expected)
    {
        // Act
        var result = Enum.Parse<TestExecutionStatus>(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestExecutionStatus_Parse_WithInvalidValue_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Enum.Parse<TestExecutionStatus>("Invalid"));
    }

    [Theory]
    [InlineData(0, TestExecutionStatus.Pass)]
    [InlineData(1, TestExecutionStatus.Warning)]
    [InlineData(2, TestExecutionStatus.Fail)]
    [InlineData(3, TestExecutionStatus.NoData)]
    public void TestExecutionStatus_CastFromInt_ShouldWorkCorrectly(int value, TestExecutionStatus expected)
    {
        // Act
        var result = (TestExecutionStatus)value;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestExecutionStatus_ShouldSupportComparison()
    {
        // Assert - Verify logical ordering: Pass < Warning < Fail < NoData
        Assert.True(TestExecutionStatus.Pass < TestExecutionStatus.Warning);
        Assert.True(TestExecutionStatus.Warning < TestExecutionStatus.Fail);
        Assert.True(TestExecutionStatus.Fail < TestExecutionStatus.NoData);
    }

    [Fact]
    public void TestExecutionStatus_ShouldBeValueTypeEnum()
    {
        // Assert
        Assert.True(typeof(TestExecutionStatus).IsEnum);
        Assert.True(typeof(TestExecutionStatus).IsValueType);
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(TestExecutionStatus)));
    }
}
