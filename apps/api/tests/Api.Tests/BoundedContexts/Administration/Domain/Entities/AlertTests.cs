using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.Tests.BoundedContexts.Administration.TestHelpers;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Domain tests for Alert aggregate.
/// Tests alert creation, lifecycle, and business rules.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AlertTests
{
    [Fact]
    public void Alert_Create_WithRequiredFields_Succeeds()
    {
        // Arrange
        var id = Guid.NewGuid();
        var alertType = "DatabaseError";
        var severity = AlertSeverity.Critical;
        var message = "Database connection failed";

        // Act
        var alert = new Alert(id, alertType, severity, message);

        // Assert
        Assert.Equal(id, alert.Id);
        Assert.Equal(alertType, alert.AlertType);
        Assert.Equal(severity, alert.Severity);
        Assert.Equal(message, alert.Message);
        Assert.True(alert.IsActive);
        Assert.Null(alert.ResolvedAt);
    }

    [Fact]
    public void Alert_Create_WithMetadata_StoresMetadata()
    {
        // Arrange
        var metadata = "{\"server\": \"db-01\", \"port\": 5432}";

        // Act
        var alert = new AlertBuilder()
            .WithMetadata(metadata)
            .Build();

        // Assert
        Assert.Equal(metadata, alert.Metadata);
    }

    [Fact]
    public void Alert_Resolve_WhenActive_MarksAsResolved()
    {
        // Arrange
        var alert = new AlertBuilder().Build();
        var beforeResolve = DateTime.UtcNow;

        // Act
        alert.Resolve();

        // Assert
        Assert.False(alert.IsActive);
        Assert.NotNull(alert.ResolvedAt);
        Assert.True(alert.ResolvedAt >= beforeResolve);
        Assert.True(alert.ResolvedAt <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void Alert_Resolve_WhenAlreadyResolved_DoesNothing()
    {
        // Arrange
        var alert = new AlertBuilder().ThatIsResolved().Build();
        var firstResolvedAt = alert.ResolvedAt;

        // Act
        alert.Resolve(); // Resolve again

        // Assert
        Assert.False(alert.IsActive);
        Assert.Equal(firstResolvedAt, alert.ResolvedAt); // Should not change
    }

    [Fact]
    public void Alert_CriticalSeverity_CreatesCorrectly()
    {
        // Arrange & Act
        var alert = new AlertBuilder()
            .ThatIsCritical()
            .WithMessage("Critical system failure")
            .Build();

        // Assert
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Equal("CriticalError", alert.AlertType);
    }

    [Fact]
    public void Alert_TriggeredAt_SetToUtcNow()
    {
        // Arrange
        var beforeCreate = DateTime.UtcNow;

        // Act
        var alert = new AlertBuilder().Build();

        // Assert
        Assert.True(alert.TriggeredAt >= beforeCreate);
        Assert.True(alert.TriggeredAt <= DateTime.UtcNow.AddSeconds(1));
    }
}