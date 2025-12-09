using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

[Trait("Category", TestCategories.Unit)]

public class SessionStatusTests
{
    [Fact]
    public void SessionStatus_Setup_HasCorrectValue()
    {
        // Arrange & Act
        var status = SessionStatus.Setup;

        // Assert
        Assert.Equal("Setup", status.Value);
        Assert.True(status.IsActive);
        Assert.False(status.IsFinished);
    }

    [Fact]
    public void SessionStatus_InProgress_HasCorrectValue()
    {
        // Arrange & Act
        var status = SessionStatus.InProgress;

        // Assert
        Assert.Equal("InProgress", status.Value);
        Assert.True(status.IsActive);
        Assert.False(status.IsFinished);
    }

    [Fact]
    public void SessionStatus_Completed_HasCorrectValue()
    {
        // Arrange & Act
        var status = SessionStatus.Completed;

        // Assert
        Assert.Equal("Completed", status.Value);
        Assert.False(status.IsActive);
        Assert.True(status.IsFinished);
    }

    [Fact]
    public void SessionStatus_Abandoned_HasCorrectValue()
    {
        // Arrange & Act
        var status = SessionStatus.Abandoned;

        // Assert
        Assert.Equal("Abandoned", status.Value);
        Assert.False(status.IsActive);
        Assert.True(status.IsFinished);
    }

    [Fact]
    public void SessionStatus_EqualityComparison_WorksCorrectly()
    {
        // Arrange
        var status1 = SessionStatus.InProgress;
        var status2 = SessionStatus.InProgress;
        var status3 = SessionStatus.Completed;

        // Act & Assert
        Assert.Equal(status1, status2);
        Assert.NotEqual(status1, status3);
    }

    [Fact]
    public void SessionStatus_ImplicitStringConversion_Works()
    {
        // Arrange
        var status = SessionStatus.InProgress;

        // Act
        string statusString = status;

        // Assert
        Assert.Equal("InProgress", statusString);
    }
}

