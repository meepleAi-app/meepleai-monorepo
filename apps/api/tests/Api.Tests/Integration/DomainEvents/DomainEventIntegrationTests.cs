using Api.BoundedContexts.Authentication.Application.EventHandlers;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Tests.TestHelpers;
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
using Api.Tests.Constants;

namespace Api.Tests.Integration.DomainEvents;

/// <summary>
/// Integration tests for domain event dispatching and handling.
/// Tests the complete flow: Aggregate → Event → Handler → Audit Log.
/// </summary>
[Trait("Category", TestCategories.Integration)]
public class DomainEventIntegrationTests : IAsyncLifetime
{
    private MeepleAiDbContext _dbContext = null!;
    private IMediator _mediator = null!;
    private IServiceScope _scope = null!;

    public async ValueTask InitializeAsync()
    {
        // Setup in-memory database with shared database name
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .Options;

        // Create mediator with real handler registration. Issue #1534: RegisterServicesFromAssemblyContaining
        // also auto-registers the open-generic DomainEventAuditHandler<TEvent> (same assembly as
        // PasswordChangedEventHandler), so no explicit AddTransient call is needed.
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<PasswordChangedEventHandler>());
        services.AddSingleton(options);
        services.AddScoped<MeepleAiDbContext>(sp => new MeepleAiDbContext(options, sp.GetRequiredService<IMediator>(), sp.GetRequiredService<IDomainEventCollector>()));
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddScoped<Api.Services.AuditService>();
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        _scope = serviceProvider.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();
        // Use the SAME DbContext instance from DI - critical for audit log visibility
        _dbContext = _scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
        _scope?.Dispose();
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldDispatchDomainEvents_AndCreateAuditLog()
    {
        // Arrange
        var user = new User(
            id: Guid.NewGuid(),
            email: Email.Parse("test@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("OldUnusualPwd123!"),
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
        var newPasswordHash = PasswordHash.Create("NewUnusualPwd123!");
        user.ChangePassword("OldUnusualPwd123!", newPasswordHash);

        // Verify event was raised
        user.DomainEvents.Should().HaveCount(1);
        var passwordChangedEvent = (PasswordChangedEvent)user.DomainEvents.ElementAt(0);

        // Manually dispatch event (simulating what SaveChangesAsync does)
        await _mediator.Publish(passwordChangedEvent, CancellationToken.None);
        user.ClearDomainEvents();

        // Assert — Issue #1534: audit is now enqueued to audit_outbox (single-path).
        // AuditOutboxProcessor materializes the row into audit_logs asynchronously; we assert
        // on the outbox directly to keep the test deterministic without polling the processor.
        var outboxRows = await _dbContext.AuditOutbox.ToListAsync(CancellationToken.None);
        outboxRows.Should().HaveCount(1);

        var outboxRow = outboxRows[0];
        outboxRow.PayloadJson.Should().Contain("\"Action\":\"DomainEvent.PasswordChangedEvent\"");
        outboxRow.PayloadJson.Should().Contain("\"Resource\":\"PasswordChangedEvent\"");
        outboxRow.PayloadJson.Should().Contain("\"Result\":\"Success\"");
        outboxRow.PayloadJson.Should().Contain(user.Id.ToString());
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
        var domainEvent = user.DomainEvents.ElementAt(0);
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
        var newPasswordHash = PasswordHash.Create("NewUnusualPwd123!");
        user.ChangePassword("UniqueT3stPwd!", newPasswordHash);

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
            passwordHash: PasswordHash.Create("UniqueT3stPwd!"),
            role: Role.User
        );
    }
}
