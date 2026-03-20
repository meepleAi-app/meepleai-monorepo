using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Tests for ThreadStatus value object.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ThreadStatusTests
{
    [Theory]
    [InlineData("active")]
    [InlineData("Active")]
    [InlineData("ACTIVE")]
    [InlineData("  active  ")]
    public void ThreadStatus_From_WithValidActiveValue_ReturnsActive(string value)
    {
        // Act
        var status = ThreadStatus.From(value);

        // Assert
        status.Should().Be(ThreadStatus.Active);
        Assert.True(status.IsActive);
        Assert.False(status.IsClosed);
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("Closed")]
    [InlineData("CLOSED")]
    [InlineData("  closed  ")]
    public void ThreadStatus_From_WithValidClosedValue_ReturnsClosed(string value)
    {
        // Act
        var status = ThreadStatus.From(value);

        // Assert
        status.Should().Be(ThreadStatus.Closed);
        Assert.True(status.IsClosed);
        Assert.False(status.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ThreadStatus_From_WithEmptyValue_ThrowsDomainException(string? value)
    {
        // Act & Assert
        var exception = ((Action)(() => ThreadStatus.From(value!))).Should().Throw<DomainException>().Which;
        exception.Message.Should().Contain("cannot be empty");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("pending")]
    [InlineData("archived")]
    public void ThreadStatus_From_WithInvalidValue_ThrowsDomainException(string value)
    {
        // Act & Assert
        var exception = ((Action)(() => ThreadStatus.From(value))).Should().Throw<DomainException>().Which;
        exception.Message.Should().Contain("Invalid thread status");
    }

    [Fact]
    public void ThreadStatus_Equality_SameValue_ReturnsTrue()
    {
        // Arrange
        var status1 = ThreadStatus.Active;
        var status2 = ThreadStatus.From("active");

        // Act & Assert
        status2.Should().Be(status1);
        Assert.True(status1 == status2);
        Assert.False(status1 != status2);
    }

    [Fact]
    public void ThreadStatus_Equality_DifferentValue_ReturnsFalse()
    {
        // Arrange
        var status1 = ThreadStatus.Active;
        var status2 = ThreadStatus.Closed;

        // Act & Assert
        status2.Should().NotBe(status1);
        Assert.False(status1 == status2);
        Assert.True(status1 != status2);
    }

    [Fact]
    public void ThreadStatus_ToString_ReturnsValue()
    {
        // Arrange
        var status = ThreadStatus.Active;

        // Act
        var result = status.ToString();

        // Assert
        result.Should().Be("active");
    }

    [Fact]
    public void ThreadStatus_ImplicitConversion_ToString_Works()
    {
        // Arrange
        var status = ThreadStatus.Closed;

        // Act
        string value = status;

        // Assert
        value.Should().Be("closed");
    }
}

