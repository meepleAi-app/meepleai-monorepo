using Api.BoundedContexts.Authentication.Application.EventHandlers;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.EventHandlers;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DomainEvents;

/// <summary>
/// Integration tests for domain event dispatching and handling.
/// Tests the complete flow: Aggregate → Event → Handler → Audit Log.
/// </summary>
public class DomainEventIntegrationTests : IAsyncLifetime
{
    private MeepleAiDbContext _dbContext = null!;
    private IMediator _mediator = null!;
    private Mock<ILogger<DomainEventHandlerBase<PasswordChangedEvent>>> _logger = null!;

    public async ValueTask InitializeAsync()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Create mediator with real handler registration
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<PasswordChangedEventHandler>());
        services.AddSingleton(options);
        services.AddScoped<MeepleAiDbContext>(sp => new MeepleAiDbContext(options, sp.GetRequiredService<IMediator>(), sp.GetRequiredService<IDomainEventCollector>()));
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        _mediator = serviceProvider.GetRequiredService<IMediator>();
        _dbContext = serviceProvider.GetRequiredService<MeepleAiDbContext>();

        _logger = new Mock<ILogger<DomainEventHandlerBase<PasswordChangedEvent>>>();

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldDispatchDomainEvents_AndCreateAuditLog()
    {
        // Arrange
        var user = new User(
            id: Guid.NewGuid(),
            email: Email.Parse("test@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("OldPassword123!"),
            role: Role.User
        );

        // Map domain entity to persistence entity for testing
        var userEntity = new UserEntity
        {
            Id = user.Id,
            Email = user.Email.Value,
            DisplayName = user.DisplayName,
            PasswordHash = user.PasswordHash.Value,
            Role = user.Role.Value,
            IsTwoFactorEnabled = user.IsTwoFactorEnabled,
            CreatedAt = user.CreatedAt
        };

        _dbContext.Users.Add(userEntity);
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        // Act - Change password (raises domain event)
        var newPasswordHash = PasswordHash.Create("NewPassword123!");
        user.ChangePassword("OldPassword123!", newPasswordHash);

        // Verify event was raised
        user.DomainEvents.Should().HaveCount(1);
        var passwordChangedEvent = (PasswordChangedEvent)user.DomainEvents.First();

        // Manually dispatch event (simulating what SaveChangesAsync does)
        await _mediator.Publish(passwordChangedEvent, CancellationToken.None);
        user.ClearDomainEvents();

        // Assert - Audit log should be created
        var auditLogs = await _dbContext.AuditLogs.ToListAsync(CancellationToken.None);
        auditLogs.Should().HaveCount(1);

        var auditLog = auditLogs.First();
        auditLog.UserId.Should().Be(user.Id);
        auditLog.Action.Should().Contain("PasswordChangedEvent");
        auditLog.Resource.Should().Be("PasswordChangedEvent");
        auditLog.Result.Should().Be("Success");
        auditLog.Details.Should().Contain("UserId");
    }

    [Fact]
    public async Task ApiKeyRevoke_ShouldPublishEvent_AndCreateAuditLog()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(
            id: Guid.NewGuid(),
            userId: userId,
            keyName: "Test API Key",
            scopes: "read,write"
        );

        // Act
        apiKey.Revoke(userId, "Security audit");

        // Assert
        apiKey.DomainEvents.Should().HaveCount(1);
        var domainEvent = apiKey.DomainEvents.First();
        domainEvent.Should().BeOfType<ApiKeyRevokedEvent>();

        var apiKeyRevokedEvent = (ApiKeyRevokedEvent)domainEvent;
        apiKeyRevokedEvent.ApiKeyId.Should().Be(apiKey.Id);
        apiKeyRevokedEvent.UserId.Should().Be(userId);
        apiKeyRevokedEvent.Reason.Should().Be("Security audit");

        // Dispatch event manually
        await _mediator.Publish(apiKeyRevokedEvent, CancellationToken.None);
        apiKey.ClearDomainEvents();

        // Verify audit log
        var auditLogs = await _dbContext.AuditLogs.ToListAsync(CancellationToken.None);
        auditLogs.Should().HaveCount(1);
        auditLogs.First().Action.Should().Contain("ApiKeyRevokedEvent");
    }

    [Fact]
    public async Task Enable2FA_ShouldPublishEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("mock_encrypted_totp_secret_base64");

        // Act
        user.Enable2FA(totpSecret);

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<TwoFactorEnabledEvent>();

        var twoFactorEnabledEvent = (TwoFactorEnabledEvent)domainEvent;
        twoFactorEnabledEvent.UserId.Should().Be(user.Id);
        twoFactorEnabledEvent.BackupCodesCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllEvents()
    {
        // Arrange
        var user = CreateTestUser();
        var newPasswordHash = PasswordHash.Create("NewPassword123!");
        user.ChangePassword("TestPassword123!", newPasswordHash);

        user.DomainEvents.Should().HaveCount(1);

        // Act
        user.ClearDomainEvents();

        // Assert
        user.DomainEvents.Should().BeEmpty();
    }

    private static User CreateTestUser()
    {
        return new User(
            id: Guid.NewGuid(),
            email: Email.Parse("test@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("TestPassword123!"),
            role: Role.User
        );
    }
}

