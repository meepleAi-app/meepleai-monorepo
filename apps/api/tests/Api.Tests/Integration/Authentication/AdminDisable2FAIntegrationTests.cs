using Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Integration tests for Admin 2FA Disable CQRS flow (Issue #575).
/// Tests the complete workflow: admin authorization → 2FA disable → domain event → email notification.
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// </summary>
/// <remarks>
/// Tests Cover:
/// 1. Complete E2E flow: admin disables 2FA for locked-out user
/// 2. Admin authorization enforcement
/// 3. Domain event publishing (TwoFactorDisabledEvent)
/// 4. Email notification verification
/// 5. Audit trail creation via domain events
/// 6. Error scenarios: non-admin, target user not found, 2FA not enabled
///
/// Pattern: AAA (Arrange-Act-Assert), Testcontainers for PostgreSQL
/// </remarks>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "575")]
[Trait("Issue", "2031")]
public sealed class AdminDisable2FAIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IMediator? _mediator;
    private Mock<IEmailService>? _mockEmailService;
    private readonly Action<string> _output;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AdminDisable2FAIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _output("Initializing AdminDisable2FA integration test infrastructure...");

        // Issue #2031: Migrated to SharedTestcontainersFixture for Docker hijack prevention and performance
        _databaseName = $"test_admin2fa_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _output($"Isolated database created: {_databaseName}");

        // Setup dependency injection
        var enforcedBuilder = new NpgsqlConnectionStringBuilder(_isolatedDbConnectionString)
        {
            SslMode = SslMode.Disable,
            KeepAlive = 30,
            Pooling = false,
            Timeout = 15,
            CommandTimeout = 30
        };

        var services = new ServiceCollection();

        // DbContext
        services.AddDbContext<MeepleAiDbContext>(options =>
            options.UseNpgsql(enforcedBuilder.ConnectionString, o => o.UseVector()) // Issue #3547
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        // Repositories and Unit of Work
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Domain event infrastructure
        services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector, Api.SharedKernel.Application.Services.DomainEventCollector>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        // Mock Email Service
        _mockEmailService = new Mock<IEmailService>();
        _mockEmailService
            .Setup(x => x.SendTwoFactorDisabledEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton(_mockEmailService.Object);

        // Logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Run migrations
        await _dbContext.Database.MigrateAsync(TestCancellationToken);
        _output("Database migrations completed");
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }

        // Issue #2031: Use SharedTestcontainersFixture for cleanup
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
                _output($"Isolated database dropped: {_databaseName}");
            }
            catch (Exception ex)
            {
                _output($"Warning: Failed to drop database {_databaseName}: {ex.Message}");
            }
        }

        _output("Test infrastructure disposed");
    }
    [Fact]
    public async Task AdminDisable2FA_ValidFlow_DisablesSuccessfullyAndSendsEmail()
    {
        // Arrange
        _output("Test: Admin successfully disables 2FA for locked-out user");

        var (adminUser, targetUser) = await SeedAdminAndTargetUserAsync();

        var command = new AdminDisable2FACommand(
            AdminUserId: adminUser.Id,
            TargetUserId: targetUser.Id);

        // Act
        _output("Executing AdminDisable2FACommand...");
        var result = await _mediator!.Send(command, TestCancellationToken);

        // Assert
        _output("Verifying results...");
        Assert.NotNull(result);
        Assert.True(result.Success, $"Expected Success=true, got: {result.ErrorMessage}");
        Assert.Null(result.ErrorMessage);

        // Verify 2FA is disabled in database
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var updatedTargetUser = await userRepository.GetByIdAsync(targetUser.Id, TestCancellationToken);
        Assert.NotNull(updatedTargetUser);
        Assert.False(updatedTargetUser.IsTwoFactorEnabled, "2FA should be disabled");
        Assert.Null(updatedTargetUser.TwoFactorEnabledAt);

        // Verify email notification was triggered
        _mockEmailService!.Verify(
            x => x.SendTwoFactorDisabledEmailAsync(
                targetUser.Email.Value,
                It.IsAny<string>(),
                true, // wasAdminOverride should be true
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Email notification should be sent exactly once with wasAdminOverride=true");

        _output("✓ Test passed: 2FA disabled successfully with email notification");
    }

    [Fact]
    public async Task AdminDisable2FA_DomainEvent_RaisedWithCorrectMetadata()
    {
        // Arrange
        _output("Test: Verify TwoFactorDisabledEvent is raised with admin override flag");

        var (adminUser, targetUser) = await SeedAdminAndTargetUserAsync();

        var command = new AdminDisable2FACommand(
            AdminUserId: adminUser.Id,
            TargetUserId: targetUser.Id);

        // Act
        await _mediator!.Send(command, TestCancellationToken);

        // Assert - Verify domain event was processed (email with admin override flag)
        _mockEmailService!.Verify(
            x => x.SendTwoFactorDisabledEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                true, // WasAdminOverride flag from domain event
                It.IsAny<CancellationToken>()),
            Times.Once);

        _output("✓ Test passed: Domain event processed with correct admin override flag");
    }
    [Fact]
    public async Task AdminDisable2FA_NonAdminUser_ReturnsUnauthorizedError()
    {
        // Arrange
        _output("Test: Non-admin user attempts to disable 2FA");

        var (editorUser, targetUser) = await SeedNonAdminAndTargetUserAsync();

        var command = new AdminDisable2FACommand(
            AdminUserId: editorUser.Id,
            TargetUserId: targetUser.Id);

        // Act
        var result = await _mediator!.Send(command, TestCancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Unauthorized", result.ErrorMessage);
        Assert.Contains("Admin role required", result.ErrorMessage);

        // Verify 2FA is still enabled
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var updatedTargetUser = await userRepository.GetByIdAsync(targetUser.Id, TestCancellationToken);
        Assert.NotNull(updatedTargetUser);
        Assert.True(updatedTargetUser.IsTwoFactorEnabled, "2FA should remain enabled");

        // Verify no email was sent
        _mockEmailService!.Verify(
            x => x.SendTwoFactorDisabledEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "Email should not be sent for unauthorized attempts");

        _output("✓ Test passed: Non-admin user rejected with appropriate error");
    }

    [Fact]
    public async Task AdminDisable2FA_AdminNotFound_ReturnsError()
    {
        // Arrange
        _output("Test: Admin user ID not found in database");

        var (_, targetUser) = await SeedAdminAndTargetUserAsync();
        var nonExistentAdminId = Guid.NewGuid();

        var command = new AdminDisable2FACommand(
            AdminUserId: nonExistentAdminId,
            TargetUserId: targetUser.Id);

        // Act
        var result = await _mediator!.Send(command, TestCancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Admin user not found", result.ErrorMessage);

        _output("✓ Test passed: Non-existent admin user handled correctly");
    }
    [Fact]
    public async Task AdminDisable2FA_TargetUserNotFound_ReturnsError()
    {
        // Arrange
        _output("Test: Target user not found in database");

        var (adminUser, _) = await SeedAdminAndTargetUserAsync();
        var nonExistentTargetId = Guid.NewGuid();

        var command = new AdminDisable2FACommand(
            AdminUserId: adminUser.Id,
            TargetUserId: nonExistentTargetId);

        // Act
        var result = await _mediator!.Send(command, TestCancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Target user not found", result.ErrorMessage);

        _output("✓ Test passed: Non-existent target user handled correctly");
    }

    [Fact]
    public async Task AdminDisable2FA_Target2FANotEnabled_ReturnsError()
    {
        // Arrange
        _output("Test: Target user does not have 2FA enabled");

        var (adminUser, _) = await SeedAdminAndTargetUserAsync();

        // Create user WITHOUT 2FA enabled
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var targetUserWithout2FA = new UserBuilder()
            .WithEmail("no-2fa-user@test.meepleai.dev")
            .Build(); // No .With2FA() call

        await userRepository.AddAsync(targetUserWithout2FA, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        var command = new AdminDisable2FACommand(
            AdminUserId: adminUser.Id,
            TargetUserId: targetUserWithout2FA.Id);

        // Act
        var result = await _mediator!.Send(command, TestCancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not enabled", result.ErrorMessage);

        _output("✓ Test passed: Attempting to disable non-enabled 2FA handled correctly");
    }

    [Fact]
    public async Task AdminDisable2FA_AdminDisablesOwnAccount_Succeeds()
    {
        // Arrange
        _output("Test: Admin disables their own 2FA (edge case)");

        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var adminUser = new UserBuilder()
            .WithEmail($"admin-{Guid.NewGuid()}@test.meepleai.dev")
            .AsAdmin()
            .With2FA()
            .Build();

        await userRepository.AddAsync(adminUser, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        var command = new AdminDisable2FACommand(
            AdminUserId: adminUser.Id,
            TargetUserId: adminUser.Id); // Same user

        // Act
        var result = await _mediator!.Send(command, TestCancellationToken);

        // Assert - Admin should be allowed to disable their own 2FA via this endpoint
        Assert.True(result.Success);

        var updatedAdmin = await userRepository.GetByIdAsync(adminUser.Id, TestCancellationToken);
        Assert.NotNull(updatedAdmin);
        Assert.False(updatedAdmin.IsTwoFactorEnabled);

        // Email should be sent to admin
        _mockEmailService!.Verify(
            x => x.SendTwoFactorDisabledEmailAsync(
                adminUser.Email.Value,
                It.IsAny<string>(),
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _output("✓ Test passed: Admin can disable their own 2FA");
    }
    /// <summary>
    /// Seeds database with admin user (with admin role) and target user (with 2FA enabled).
    /// </summary>
    private async Task<(User adminUser, User targetUser)> SeedAdminAndTargetUserAsync()
    {
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var adminUser = new UserBuilder()
            .WithEmail($"admin-{Guid.NewGuid()}@test.meepleai.dev")
            .AsAdmin()
            .Build();

        var targetUser = new UserBuilder()
            .WithEmail($"target-{Guid.NewGuid()}@test.meepleai.dev")
            .With2FA()
            .Build();

        await userRepository.AddAsync(adminUser, TestCancellationToken);
        await userRepository.AddAsync(targetUser, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        _output($"Seeded admin user {adminUser.Id} and target user {targetUser.Id} with 2FA enabled");

        return (adminUser, targetUser);
    }

    /// <summary>
    /// Seeds database with non-admin user (editor role) and target user (with 2FA enabled).
    /// </summary>
    private async Task<(User editorUser, User targetUser)> SeedNonAdminAndTargetUserAsync()
    {
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var editorUser = new UserBuilder()
            .WithEmail($"editor-{Guid.NewGuid()}@test.meepleai.dev")
            .AsEditor()
            .Build();

        var targetUser = new UserBuilder()
            .WithEmail($"target-{Guid.NewGuid()}@test.meepleai.dev")
            .With2FA()
            .Build();

        await userRepository.AddAsync(editorUser, TestCancellationToken);
        await userRepository.AddAsync(targetUser, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        _output($"Seeded editor user {editorUser.Id} and target user {targetUser.Id} with 2FA enabled");

        return (editorUser, targetUser);
    }
}
