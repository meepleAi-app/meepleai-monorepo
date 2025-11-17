using Api.BoundedContexts.Administration.Domain.Entities;
using Api.Tests.BoundedContexts.Administration.TestHelpers;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Domain tests for AuditLog aggregate.
/// Tests audit log creation and validation.
/// </summary>
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
        Assert.Equal(id, auditLog.Id);
        Assert.Equal(userId, auditLog.UserId);
        Assert.Equal(action, auditLog.Action);
        Assert.Equal(resource, auditLog.Resource);
        Assert.Equal(result, auditLog.Result);
    }

    [Fact]
    public void AuditLog_Create_WithNullAction_ThrowsException()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AuditLog(id, null, null!, "resource", "success"));
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
        Assert.Equal("{\"attempt\": 1}", auditLog.Details);
        Assert.Equal("192.168.1.1", auditLog.IpAddress);
        Assert.Equal("user.logout", auditLog.Action);
    }

    [Fact]
    public void AuditLog_ForUserLogin_SetsCorrectFields()
    {
        // Arrange & Act
        var auditLog = new AuditLogBuilder()
            .ForUserLogin()
            .Build();

        // Assert
        Assert.Equal("user.login", auditLog.Action);
        Assert.Equal("authentication", auditLog.Resource);
        Assert.Equal("success", auditLog.Result);
    }

    [Fact]
    public void AuditLog_ForFailedLogin_SetsFailureDetails()
    {
        // Arrange & Act
        var auditLog = new AuditLogBuilder()
            .ForFailedLogin()
            .Build();

        // Assert
        Assert.Equal("user.login", auditLog.Action);
        Assert.Equal("failure", auditLog.Result);
        Assert.Contains("Invalid credentials", auditLog.Details);
    }

    [Fact]
    public void AuditLog_CreatedAt_SetToUtcNow()
    {
        // Arrange
        var beforeCreate = DateTime.UtcNow;

        // Act
        var auditLog = new AuditLogBuilder().Build();

        // Assert
        Assert.True(auditLog.CreatedAt >= beforeCreate);
        Assert.True(auditLog.CreatedAt <= DateTime.UtcNow.AddSeconds(1));
    }
}
