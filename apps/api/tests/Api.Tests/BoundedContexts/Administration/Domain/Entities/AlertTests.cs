using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.Tests.BoundedContexts.Administration.TestHelpers;
using Xunit;
using FluentAssertions;
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
        alert.Id.Should().Be(id);
        alert.AlertType.Should().Be(alertType);
        alert.Severity.Should().Be(severity);
        alert.Message.Should().Be(message);
        alert.IsActive.Should().BeTrue();
        alert.ResolvedAt.Should().BeNull();
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
        alert.Metadata.Should().Be(metadata);
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
        alert.IsActive.Should().BeFalse();
        alert.ResolvedAt.Should().NotBeNull();
        (alert.ResolvedAt >= beforeResolve).Should().BeTrue();
        (alert.ResolvedAt <= DateTime.UtcNow.AddSeconds(1)).Should().BeTrue();
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
        alert.IsActive.Should().BeFalse();
        alert.ResolvedAt.Should().Be(firstResolvedAt); // Should not change
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
        alert.Severity.Should().Be(AlertSeverity.Critical);
        alert.AlertType.Should().Be("CriticalError");
    }

    [Fact]
    public void Alert_TriggeredAt_SetToUtcNow()
    {
        // Arrange
        var beforeCreate = DateTime.UtcNow;

        // Act
        var alert = new AlertBuilder().Build();

        // Assert
        (alert.TriggeredAt >= beforeCreate).Should().BeTrue();
        (alert.TriggeredAt <= DateTime.UtcNow.AddSeconds(1)).Should().BeTrue();
    }
}
