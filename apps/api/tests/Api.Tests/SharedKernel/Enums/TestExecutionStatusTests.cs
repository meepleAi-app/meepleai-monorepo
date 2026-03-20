using Api.SharedKernel.Enums;
using Xunit;
using FluentAssertions;
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
        ((int)TestExecutionStatus.Pass).Should().Be(0);
        ((int)TestExecutionStatus.Warning).Should().Be(1);
        ((int)TestExecutionStatus.Fail).Should().Be(2);
        ((int)TestExecutionStatus.NoData).Should().Be(3);
    }

    [Fact]
    public void TestExecutionStatus_ShouldHaveAllExpectedMembers()
    {
        // Arrange
        var expectedMembers = new[] { "Pass", "Warning", "Fail", "NoData" };

        // Act
        var actualMembers = Enum.GetNames(typeof(TestExecutionStatus));

        // Assert
        actualMembers.Length.Should().Be(expectedMembers.Length);
        expectedMembers.Should().AllSatisfy(expected => actualMembers.Should().Contain(expected));
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
        result.Should().Be(expectedName);
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
        result.Should().Be(expected);
    }

    [Fact]
    public void TestExecutionStatus_Parse_WithInvalidValue_ShouldThrowException()
    {
        // Act & Assert
        ((Action)(() => Enum.Parse<TestExecutionStatus>("Invalid"))).Should().Throw<ArgumentException>();
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
        result.Should().Be(expected);
    }

    [Fact]
    public void TestExecutionStatus_ShouldSupportComparison()
    {
        // Assert - Verify logical ordering: Pass < Warning < Fail < NoData
        (TestExecutionStatus.Pass < TestExecutionStatus.Warning).Should().BeTrue();
        (TestExecutionStatus.Warning < TestExecutionStatus.Fail).Should().BeTrue();
        (TestExecutionStatus.Fail < TestExecutionStatus.NoData).Should().BeTrue();
    }

    [Fact]
    public void TestExecutionStatus_ShouldBeValueTypeEnum()
    {
        // Assert
        typeof(TestExecutionStatus).IsEnum.Should().BeTrue();
        typeof(TestExecutionStatus).IsValueType.Should().BeTrue();
        Enum.GetUnderlyingType(typeof(TestExecutionStatus)).Should().Be(typeof(int));
    }
}
