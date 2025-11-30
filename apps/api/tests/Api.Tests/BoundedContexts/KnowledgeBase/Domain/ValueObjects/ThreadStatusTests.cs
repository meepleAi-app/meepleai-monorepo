using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Tests for ThreadStatus value object.
/// </summary>
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
        Assert.Equal(ThreadStatus.Active, status);
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
        Assert.Equal(ThreadStatus.Closed, status);
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
        var exception = Assert.Throws<DomainException>(() => ThreadStatus.From(value!));
        Assert.Contains("cannot be empty", exception.Message);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("pending")]
    [InlineData("archived")]
    public void ThreadStatus_From_WithInvalidValue_ThrowsDomainException(string value)
    {
        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => ThreadStatus.From(value));
        Assert.Contains("Invalid thread status", exception.Message);
    }

    [Fact]
    public void ThreadStatus_Equality_SameValue_ReturnsTrue()
    {
        // Arrange
        var status1 = ThreadStatus.Active;
        var status2 = ThreadStatus.From("active");

        // Act & Assert
        Assert.Equal(status1, status2);
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
        Assert.NotEqual(status1, status2);
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
        Assert.Equal("active", result);
    }

    [Fact]
    public void ThreadStatus_ImplicitConversion_ToString_Works()
    {
        // Arrange
        var status = ThreadStatus.Closed;

        // Act
        string value = status;

        // Assert
        Assert.Equal("closed", value);
    }
}

