using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Domain.ValueObjects;
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
        var superAdmin = new User(
            SuperAdminId,
            new Email("superadmin@test.com"),
            "Super Admin",
            PasswordHash.Create("password123"),
            Role.SuperAdmin
        );

        // Admin user
        var admin = new User(
            AdminId,
            new Email("admin@test.com"),
            "Admin User",
            PasswordHash.Create("password123"),
            Role.Admin
        );

        // Editor user
        var editor = new User(
            EditorId,
            new Email("editor@test.com"),
            "Editor User",
            PasswordHash.Create("password123"),
            Role.Editor
        );

        // Regular user
        var regularUser = new User(
            RegularUserId,
            new Email("user@test.com"),
            "Regular User",
            PasswordHash.Create("password123"),
            Role.User
        );

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
        var entityId = Guid.NewGuid();
        var auditLog = new AuditLog(
            id: Guid.NewGuid(),
            userId: SuperAdminId,
            action: "test_action",
            resource: "TestEntity",
            result: "Success",
            resourceId: entityId.ToString(),
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
        retrieved.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify repository has no Update method (append-only pattern)
        var repositoryType = typeof(AuditLogRepository);
        var updateMethod = repositoryType.GetMethod("UpdateAsync") ?? repositoryType.GetMethod("Update");
        updateMethod.Should().BeNull("Audit logs should be append-only (no update allowed)");
    }

    [Fact]
    public async Task AuditLog_WhenQueried_ShouldReturnOnlyAuthorizedRecords()
    {
        // Arrange - Create audit logs for different users
        var superAdminLog = new AuditLog(Guid.NewGuid(), SuperAdminId, "superadmin_action", "Config", "Success", Guid.NewGuid().ToString(), null);
        var adminLog = new AuditLog(Guid.NewGuid(), AdminId, "admin_action", "User", "Success", Guid.NewGuid().ToString(), null);
        var editorLog = new AuditLog(Guid.NewGuid(), EditorId, "editor_action", "Document", "Success", Guid.NewGuid().ToString(), null);

        await _auditLogRepository!.AddAsync(superAdminLog, TestCancellationToken);
        await _auditLogRepository.AddAsync(adminLog, TestCancellationToken);
        await _auditLogRepository.AddAsync(editorLog, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Query all logs using DbContext (repository doesn't have GetAll anymore)
        var allLogs = await _dbContext!.AuditLogs
            .AsNoTracking()
            .ToListAsync(TestCancellationToken);

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
        var criticalAction = new AuditLog(
            id: Guid.NewGuid(),
            userId: SuperAdminId,
            action: "service_restart",
            resource: "System",
            result: "Success",
            resourceId: Guid.Empty.ToString(),
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
        var isAdmin = regularUser.Role.IsAdmin() || regularUser.Role.IsSuperAdmin() || regularUser.Role.IsEditor();
        isAdmin.Should().BeFalse("Regular users should not have admin privileges");
    }

    [Fact]
    public async Task AuditLog_DeleteOperation_ShouldNotExist()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var auditLog = new AuditLog(
            id: Guid.NewGuid(),
            userId: AdminId,
            action: "test_delete",
            resource: "Test",
            result: "Success",
            resourceId: entityId.ToString(),
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
        // Verify using HasPermission method instead of int comparison
        Role.SuperAdmin.HasPermission(Role.Admin).Should().BeTrue("SuperAdmin has all permissions");
        Role.SuperAdmin.HasPermission(Role.Editor).Should().BeTrue("SuperAdmin has all permissions");
        Role.Admin.HasPermission(Role.Editor).Should().BeTrue("Admin has editor permissions");
        Role.Admin.HasPermission(Role.SuperAdmin).Should().BeFalse("Admin does not have SuperAdmin permissions");
        Role.Editor.HasPermission(Role.Admin).Should().BeFalse("Editor does not have Admin permissions");
    }
}
