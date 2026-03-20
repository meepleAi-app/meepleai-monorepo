using Api.BoundedContexts.Administration.Domain.Entities;
using Api.Tests.BoundedContexts.Administration.TestHelpers;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Domain tests for AuditLog aggregate.
/// Tests audit log creation and validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AuditLogTests
{
    [Fact]
    public void AuditLog_Create_WithRequiredFields_Succeeds()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var action = "user.login";
        var resource = "authentication";
        var result = "success";

        // Act
        var auditLog = new AuditLog(id, userId, action, resource, result);

        // Assert
        auditLog.Id.Should().Be(id);
        auditLog.UserId.Should().Be(userId);
        auditLog.Action.Should().Be(action);
        auditLog.Resource.Should().Be(resource);
        auditLog.Result.Should().Be(result);
    }

    [Fact]
    public void AuditLog_Create_WithNullAction_ThrowsException()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act & Assert
        var act = () =>
            new AuditLog(id, null, null!, "resource", "success");
act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AuditLog_Create_WithOptionalFields_StoresAllData()
    {
        // Arrange
        var builder = new AuditLogBuilder()
            .WithDetails("{\"attempt\": 1}")
            .WithIpAddress("192.168.1.1")
            .WithAction("user.logout");

        // Act
        var auditLog = builder.Build();

        // Assert
        auditLog.Details.Should().Be("{\"attempt\": 1}");
        auditLog.IpAddress.Should().Be("192.168.1.1");
        auditLog.Action.Should().Be("user.logout");
    }

    [Fact]
    public void AuditLog_ForUserLogin_SetsCorrectFields()
    {
        // Arrange & Act
        var auditLog = new AuditLogBuilder()
            .ForUserLogin()
            .Build();

        // Assert
        auditLog.Action.Should().Be("user.login");
        auditLog.Resource.Should().Be("authentication");
        auditLog.Result.Should().Be("success");
    }

    [Fact]
    public void AuditLog_ForFailedLogin_SetsFailureDetails()
    {
        // Arrange & Act
        var auditLog = new AuditLogBuilder()
            .ForFailedLogin()
            .Build();

        // Assert
        auditLog.Action.Should().Be("user.login");
        auditLog.Result.Should().Be("failure");
        auditLog.Details.Should().Contain("Invalid credentials");
    }

    [Fact]
    public void AuditLog_CreatedAt_SetToUtcNow()
    {
        // Arrange
        var beforeCreate = DateTime.UtcNow;

        // Act
        var auditLog = new AuditLogBuilder().Build();

        // Assert
        (auditLog.CreatedAt >= beforeCreate).Should().BeTrue();
        (auditLog.CreatedAt <= DateTime.UtcNow.AddSeconds(1)).Should().BeTrue();
    }
}