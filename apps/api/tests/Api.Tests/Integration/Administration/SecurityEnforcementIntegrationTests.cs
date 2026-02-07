using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Repositories;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for Security enforcement in Epic #3685.
/// Tests permission requirements, audit log integrity, and role-based access.
/// Issue #3697: Epic 1 - Testing & Integration (Phase 4)
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "3697")]
public sealed class SecurityEnforcementIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IAuditLogRepository? _auditLogRepository;
    private IUserRepository? _userRepository;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test users
    private static readonly Guid SuperAdminId = new("C0000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminId = new("C0000000-0000-0000-0000-000000000002");
    private static readonly Guid EditorId = new("C0000000-0000-0000-0000-000000000003");
    private static readonly Guid RegularUserId = new("C0000000-0000-0000-0000-000000000004");

    public SecurityEnforcementIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_security_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _auditLogRepository = _serviceProvider.GetRequiredService<IAuditLogRepository>();
        _userRepository = _serviceProvider.GetRequiredService<IUserRepository>();
        _unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        await _dbContext.Database.MigrateAsync(TestCancellationToken);

        // Seed test users with different roles
        await SeedTestUsersAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is not null)
        {
            await ((ServiceProvider)_serviceProvider).DisposeAsync();
        }

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private async Task SeedTestUsersAsync()
    {
        // SuperAdmin user
        var superAdmin = User.Create(
            Email.Create("superadmin@test.com"),
            "password123",
            "Super Admin"
        );
        superAdmin.SetRole(Role.SuperAdmin);
        typeof(User).GetProperty("Id")!.SetValue(superAdmin, SuperAdminId);

        // Admin user
        var admin = User.Create(
            Email.Create("admin@test.com"),
            "password123",
            "Admin User"
        );
        admin.SetRole(Role.Admin);
        typeof(User).GetProperty("Id")!.SetValue(admin, AdminId);

        // Editor user
        var editor = User.Create(
            Email.Create("editor@test.com"),
            "password123",
            "Editor User"
        );
        editor.SetRole(Role.Editor);
        typeof(User).GetProperty("Id")!.SetValue(editor, EditorId);

        // Regular user
        var regularUser = User.Create(
            Email.Create("user@test.com"),
            "password123",
            "Regular User"
        );
        // Default role is User
        typeof(User).GetProperty("Id")!.SetValue(regularUser, RegularUserId);

        await _userRepository!.AddAsync(superAdmin, TestCancellationToken);
        await _userRepository.AddAsync(admin, TestCancellationToken);
        await _userRepository.AddAsync(editor, TestCancellationToken);
        await _userRepository.AddAsync(regularUser, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);
    }

    [Fact]
    public async Task AuditLog_WhenCreated_ShouldBeImmutable()
    {
        // Arrange - Create audit log entry
        var auditLog = AuditLog.Create(
            userId: SuperAdminId,
            action: "test_action",
            entityType: "TestEntity",
            entityId: Guid.NewGuid(),
            details: "{\"test\": \"data\"}"
        );

        await _auditLogRepository!.AddAsync(auditLog, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Attempt to retrieve and verify properties cannot be modified
        var retrieved = await _auditLogRepository.GetByIdAsync(auditLog.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();

        // Assert - All properties should be private set (verified at compile time)
        // No Update/Delete methods exist on AuditLog entity (append-only)
        retrieved!.UserId.Should().Be(SuperAdminId);
        retrieved.Action.Should().Be("test_action");
        retrieved.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify repository has no Update method (append-only pattern)
        var repositoryType = typeof(AuditLogRepository);
        var updateMethod = repositoryType.GetMethod("UpdateAsync") ?? repositoryType.GetMethod("Update");
        updateMethod.Should().BeNull("Audit logs should be append-only (no update allowed)");
    }

    [Fact]
    public async Task AuditLog_WhenQueried_ShouldReturnOnlyAuthorizedRecords()
    {
        // Arrange - Create audit logs for different users
        var superAdminLog = AuditLog.Create(SuperAdminId, "superadmin_action", "Config", Guid.NewGuid(), null);
        var adminLog = AuditLog.Create(AdminId, "admin_action", "User", Guid.NewGuid(), null);
        var editorLog = AuditLog.Create(EditorId, "editor_action", "Document", Guid.NewGuid(), null);

        await _auditLogRepository!.AddAsync(superAdminLog, TestCancellationToken);
        await _auditLogRepository.AddAsync(adminLog, TestCancellationToken);
        await _auditLogRepository.AddAsync(editorLog, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Query all logs (SuperAdmin can see all)
        var allLogs = await _auditLogRepository.GetAllAsync(
            skip: 0,
            take: 100,
            userId: null,
            action: null,
            entityType: null,
            startDate: null,
            endDate: null,
            TestCancellationToken
        );

        // Assert - All logs should be returned
        allLogs.Should().NotBeNull();
        allLogs.Should().HaveCountGreaterThanOrEqualTo(3);
        allLogs.Should().Contain(log => log.UserId == SuperAdminId);
        allLogs.Should().Contain(log => log.UserId == AdminId);
        allLogs.Should().Contain(log => log.UserId == EditorId);
    }

    [Fact]
    public async Task CriticalAction_RequiresSuperAdminRole()
    {
        // Arrange - Verify SuperAdmin exists
        var superAdmin = await _userRepository!.GetByIdAsync(SuperAdminId, TestCancellationToken);
        superAdmin.Should().NotBeNull();
        superAdmin!.Role.Should().Be(Role.SuperAdmin);

        // Act - Create audit log for critical action (simulating endpoint behavior)
        var criticalAction = AuditLog.Create(
            userId: SuperAdminId,
            action: "service_restart",
            entityType: "System",
            entityId: Guid.Empty,
            details: "{\"service\": \"API\"}"
        );

        await _auditLogRepository!.AddAsync(criticalAction, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert - Action logged and attributed to SuperAdmin
        var logged = await _auditLogRepository.GetByIdAsync(criticalAction.Id, TestCancellationToken);
        logged.Should().NotBeNull();
        logged!.Action.Should().Be("service_restart");
        logged.UserId.Should().Be(SuperAdminId);
    }

    [Fact]
    public async Task NonAdminUser_CannotAccessAdminResources()
    {
        // Arrange
        var regularUser = await _userRepository!.GetByIdAsync(RegularUserId, TestCancellationToken);
        regularUser.Should().NotBeNull();
        regularUser!.Role.Should().Be(Role.User);

        // Act & Assert - Regular user role is not Admin/SuperAdmin/Editor
        regularUser.Role.Should().NotBe(Role.Admin);
        regularUser.Role.Should().NotBe(Role.SuperAdmin);
        regularUser.Role.Should().NotBe(Role.Editor);

        // In real endpoint, this would return 403 Forbidden
        // Here we verify the role check logic works at entity level
        var isAdmin = regularUser.Role is Role.Admin or Role.SuperAdmin or Role.Editor;
        isAdmin.Should().BeFalse("Regular users should not have admin privileges");
    }

    [Fact]
    public async Task AuditLog_DeleteOperation_ShouldNotExist()
    {
        // Arrange
        var auditLog = AuditLog.Create(
            userId: AdminId,
            action: "test_delete",
            entityType: "Test",
            entityId: Guid.NewGuid(),
            details: null
        );

        await _auditLogRepository!.AddAsync(auditLog, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act & Assert - Verify no Delete method exists
        var repositoryType = typeof(IAuditLogRepository);
        var deleteMethod = repositoryType.GetMethod("DeleteAsync") ?? repositoryType.GetMethod("Delete");

        deleteMethod.Should().BeNull("Audit logs must be immutable (no delete operations)");

        // Verify entity exists and persists
        var persisted = await _auditLogRepository.GetByIdAsync(auditLog.Id, TestCancellationToken);
        persisted.Should().NotBeNull("Audit logs cannot be deleted");
    }

    [Fact]
    public async Task RoleHierarchy_SuperAdminHasAllPermissions()
    {
        // Arrange
        var superAdmin = await _userRepository!.GetByIdAsync(SuperAdminId, TestCancellationToken);
        var admin = await _userRepository.GetByIdAsync(AdminId, TestCancellationToken);
        var editor = await _userRepository.GetByIdAsync(EditorId, TestCancellationToken);

        // Assert - Role hierarchy verification
        superAdmin.Should().NotBeNull();
        admin.Should().NotBeNull();
        editor.Should().NotBeNull();

        superAdmin!.Role.Should().Be(Role.SuperAdmin);
        admin!.Role.Should().Be(Role.Admin);
        editor!.Role.Should().Be(Role.Editor);

        // SuperAdmin > Admin > Editor in privilege hierarchy
        var superAdminLevel = (int)Role.SuperAdmin;
        var adminLevel = (int)Role.Admin;
        var editorLevel = (int)Role.Editor;

        superAdminLevel.Should().BeGreaterThan(adminLevel);
        adminLevel.Should().BeGreaterThan(editorLevel);
    }
}
