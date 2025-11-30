using Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Integration tests for Admin 2FA Disable CQRS flow (Issue #575).
/// Tests the complete workflow: admin authorization → 2FA disable → domain event → email notification.
/// Uses Testcontainers with PostgreSQL for realistic database interactions.
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
[Collection("AdminDisable2FA")]
[Trait("Category", "Integration")]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "575")]
public class AdminDisable2FAIntegrationTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IMediator? _mediator;
    private Mock<IEmailService>? _mockEmailService;
    private readonly Action<string> _output;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private const string AdminEmail = "admin@test.meepleai.dev";
    private const string TargetUserEmail = "locked-out-user@test.meepleai.dev";
    private const string NonAdminEmail = "editor@test.meepleai.dev";

    public AdminDisable2FAIntegrationTests()
    {
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _output("Initializing AdminDisable2FA integration test infrastructure...");

        // Start isolated Postgres container
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "admin2fa_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(TestCancellationToken);
        var containerPort = _postgresContainer.GetMappedPublicPort(5432);
        var connectionString = $"Host=localhost;Port={containerPort};Database=admin2fa_test;Username=postgres;Password=postgres;";

        _output($"PostgreSQL started at localhost:{containerPort}");

        // Setup dependency injection
        var enforcedBuilder = new NpgsqlConnectionStringBuilder(connectionString)
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
            options.UseNpgsql(enforcedBuilder.ConnectionString)
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
        await _dbContext.Database.EnsureCreatedAsync(TestCancellationToken);
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

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }

        _output("Test infrastructure disposed");
    }

    #region Happy Path Tests

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
                TargetUserEmail,
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

    #endregion

    #region Authorization Tests

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

    #endregion

    #region Business Rule Tests

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

        await userRepository.AddAsync(targetUserWithout2FA, TestCancellationToken, TestContext.Current.CancellationToken);
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
            .WithEmail(AdminEmail)
            .AsAdmin()
            .With2FA()
            .Build();

        await userRepository.AddAsync(adminUser, TestCancellationToken, TestContext.Current.CancellationToken);
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
                AdminEmail,
                It.IsAny<string>(),
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _output("✓ Test passed: Admin can disable their own 2FA");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Seeds database with admin user (with admin role) and target user (with 2FA enabled).
    /// </summary>
    private async Task<(User adminUser, User targetUser)> SeedAdminAndTargetUserAsync()
    {
        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();

        var adminUser = new UserBuilder()
            .WithEmail(AdminEmail)
            .AsAdmin()
            .Build();

        var targetUser = new UserBuilder()
            .WithEmail(TargetUserEmail)
            .With2FA()
            .Build();

        await userRepository.AddAsync(adminUser, TestCancellationToken, TestContext.Current.CancellationToken);
        await userRepository.AddAsync(targetUser, TestCancellationToken, TestContext.Current.CancellationToken);
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
            .WithEmail(NonAdminEmail)
            .AsEditor()
            .Build();

        var targetUser = new UserBuilder()
            .WithEmail(TargetUserEmail)
            .With2FA()
            .Build();

        await userRepository.AddAsync(editorUser, TestCancellationToken, TestContext.Current.CancellationToken);
        await userRepository.AddAsync(targetUser, TestCancellationToken, TestContext.Current.CancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        _output($"Seeded editor user {editorUser.Id} and target user {targetUser.Id} with 2FA enabled");

        return (editorUser, targetUser);
    }

    #endregion
}
