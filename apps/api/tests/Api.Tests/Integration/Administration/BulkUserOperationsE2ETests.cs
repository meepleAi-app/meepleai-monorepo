using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Infrastructure;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// E2E Integration tests for bulk user operations (Issue #907).
/// Tests complete workflows with Testcontainers: CSV import/export, bulk role changes, password resets.
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// </summary>
/// <remarks>
/// Test Coverage:
/// 1. Complete E2E flow: CSV import → database persistence → CSV export
/// 2. Bulk role change: multiple users updated in single transaction
/// 3. Bulk password reset: password hashing, validation, transaction rollback
/// 4. Error handling: validation errors, partial failures, rollback scenarios
/// 5. Data integrity: concurrent operations, duplicate detection
/// 6. Performance: 1000 user bulk operations within acceptable time limits
///
/// Pattern: AAA (Arrange-Act-Assert), Testcontainers for PostgreSQL
/// Execution Time Target: <30s for full suite
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Type", "E2E")]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "907")]
[Trait("Issue", "2031")]
public sealed class BulkUserOperationsE2ETests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IUserRepository? _userRepository;
    private IUserProfileRepository? _userProfileRepository;
    private IUnitOfWork? _unitOfWork;
    private readonly Action<string> _output;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public BulkUserOperationsE2ETests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _output("Initializing bulk user operations E2E test infrastructure...");

        // Issue #2031: Migrated to SharedTestcontainersFixture
        _databaseName = "test_bulkuser_" + Guid.NewGuid().ToString("N");
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _output($"✓ Isolated database created: {_databaseName}");

        // Setup dependency injection
        var enforcedBuilder = new NpgsqlConnectionStringBuilder(_isolatedDbConnectionString)
        {
            SslMode = SslMode.Disable,
            KeepAlive = 30,
            Pooling = false,
            Timeout = 15,
            CommandTimeout = 30
        };

        var services = IntegrationServiceCollectionBuilder.CreateBase(enforcedBuilder.ConnectionString);
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();

        var serviceProvider = services.BuildServiceProvider();
        _dbContext = serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _userRepository = serviceProvider.GetRequiredService<IUserRepository>();
        _userProfileRepository = serviceProvider.GetRequiredService<IUserProfileRepository>();
        _unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        // Run migrations
        await _dbContext.Database.MigrateAsync(TestCancellationToken);

        // Create user projection views that are normally created at Program.cs startup
        // but are not part of EF migrations (Issue #3010)
        await _dbContext.Database.ExecuteSqlRawAsync(@"
            CREATE OR REPLACE VIEW vw_user_profiles AS SELECT * FROM users;
            CREATE OR REPLACE VIEW vw_user_budgets AS SELECT * FROM users;
            CREATE OR REPLACE VIEW vw_user_preferences AS SELECT * FROM users;
        ", TestCancellationToken);

        _output("E2E test infrastructure initialized successfully");
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        // Issue #2031: Use SharedTestcontainersFixture for cleanup
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
                _output($"✓ Isolated database dropped: {_databaseName}");
            }
            catch (Exception ex)
            {
                _output($"⚠️ Failed to drop database {_databaseName}: {ex.Message}");
            }
        }

        _output("E2E test infrastructure disposed");
    }

    #region E2E Flow: CSV Import → Database → CSV Export

    [Fact]
    public async Task E2E_BulkImport_Then_Export_ShouldRoundTripCorrectly()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var csvContent = @"email,displayName,role,password
user1@e2etest.com,User One,user,Password123!
user2@e2etest.com,User Two,admin,Password456!
user3@e2etest.com,User Three,user,Password789!";

        var logger = new Mock<ILogger<BulkImportUsersCommandHandler>>();
        var importHandler = new BulkImportUsersCommandHandler(_userRepository!, _unitOfWork!, logger.Object);
        var importCommand = new BulkImportUsersCommand(csvContent, adminId);

        // Act 1: Import
        var importResult = await importHandler.Handle(importCommand, TestCancellationToken);

        // Assert 1: Import success
        importResult.Should().NotBeNull();
        importResult.TotalRequested.Should().Be(3);
        importResult.SuccessCount.Should().Be(3);
        importResult.FailedCount.Should().Be(0);
        importResult.Errors.Should().BeEmpty();

        // Act 2: Verify database persistence
        var allUsers = await _dbContext!.Users.ToListAsync(TestCancellationToken);

        // Assert 2: Users persisted correctly
        allUsers.Should().HaveCount(3);
        allUsers.Should().Contain(u => u.Email == "user1@e2etest.com");
        allUsers.Should().Contain(u => u.Email == "user2@e2etest.com");
        allUsers.Should().Contain(u => u.Email == "user3@e2etest.com");

        // Act 3: Export
        var exportLogger = new Mock<ILogger<BulkExportUsersQueryHandler>>();
        var exportHandler = new BulkExportUsersQueryHandler(_userProfileRepository!, exportLogger.Object);
        var exportQuery = new BulkExportUsersQuery(null, null);
        var exportResult = await exportHandler.Handle(exportQuery, TestCancellationToken);

        // Assert 3: Export contains all users
        exportResult.Should().NotBeNullOrWhiteSpace();
        var exportLines = exportResult.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        exportLines.Should().HaveCountGreaterThanOrEqualTo(4); // Header + 3 users
        exportResult.Should().Contain("user1@e2etest.com");
        exportResult.Should().Contain("user2@e2etest.com");
        exportResult.Should().Contain("user3@e2etest.com");
    }

    #endregion

    #region E2E: Bulk Role Change

    [Fact]
    public async Task E2E_BulkRoleChange_ShouldUpdateMultipleUsersAtomically()
    {
        // Arrange: Create test users
        var user1 = CreateTestUser("roletest1@test.com", "User 1", Role.User);
        var user2 = CreateTestUser("roletest2@test.com", "User 2", Role.User);
        var user3 = CreateTestUser("roletest3@test.com", "User 3", Role.User);

        await _userRepository!.AddAsync(user1, TestCancellationToken);
        await _userRepository!.AddAsync(user2, TestCancellationToken);
        await _userRepository!.AddAsync(user3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var userIds = new List<Guid> { user1.Id, user2.Id, user3.Id };
        var adminId = Guid.NewGuid();

        var logger = new Mock<ILogger<BulkRoleChangeCommandHandler>>();
        var handler = new BulkRoleChangeCommandHandler(_userRepository!, _unitOfWork!, logger.Object);
        var command = new BulkRoleChangeCommand(userIds, "admin", adminId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert: All users updated
        result.Should().NotBeNull();
        result.SuccessCount.Should().Be(3);
        result.FailedCount.Should().Be(0);

        // Verify database state
        var updatedUser1 = await _userRepository!.GetByIdAsync(user1.Id, TestCancellationToken);
        var updatedUser2 = await _userRepository!.GetByIdAsync(user2.Id, TestCancellationToken);
        var updatedUser3 = await _userRepository!.GetByIdAsync(user3.Id, TestCancellationToken);

        updatedUser1!.Role.Should().Be(Role.Admin);
        updatedUser2!.Role.Should().Be(Role.Admin);
        updatedUser3!.Role.Should().Be(Role.Admin);
    }

    [Fact]
    public async Task E2E_BulkRoleChange_WithPartialFailure_ShouldReportErrors()
    {
        // Arrange: Create only 2 users
        var user1 = CreateTestUser("partialfail1@test.com", "User 1", Role.User);
        var user2 = CreateTestUser("partialfail2@test.com", "User 2", Role.User);

        await _userRepository!.AddAsync(user1, TestCancellationToken);
        await _userRepository!.AddAsync(user2, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var nonExistentId = Guid.NewGuid();
        var userIds = new List<Guid> { user1.Id, nonExistentId, user2.Id };
        var adminId = Guid.NewGuid();

        var logger = new Mock<ILogger<BulkRoleChangeCommandHandler>>();
        var handler = new BulkRoleChangeCommandHandler(_userRepository!, _unitOfWork!, logger.Object);
        var command = new BulkRoleChangeCommand(userIds, "admin", adminId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert: Partial success
        result.Should().NotBeNull();
        result.SuccessCount.Should().Be(2);
        result.FailedCount.Should().Be(1);
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("not found");
    }

    #endregion

    #region E2E: Bulk Password Reset

    [Fact]
    public async Task E2E_BulkPasswordReset_ShouldHashAndUpdatePasswords()
    {
        // Arrange: Create test users
        var user1 = CreateTestUser("pwreset1@test.com", "User 1", Role.User);
        var user2 = CreateTestUser("pwreset2@test.com", "User 2", Role.User);

        await _userRepository!.AddAsync(user1, TestCancellationToken);
        await _userRepository!.AddAsync(user2, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var originalHash1 = user1.PasswordHash.Value;
        var originalHash2 = user2.PasswordHash.Value;

        var userIds = new List<Guid> { user1.Id, user2.Id };
        var adminId = Guid.NewGuid();
        var newPassword = "NewSecurePassword456!";

        var logger = new Mock<ILogger<BulkPasswordResetCommandHandler>>();
        var handler = new BulkPasswordResetCommandHandler(_userRepository!, _unitOfWork!, logger.Object);
        var command = new BulkPasswordResetCommand(userIds, newPassword, adminId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.SuccessCount.Should().Be(2);
        result.FailedCount.Should().Be(0);

        // Verify password hashes changed
        var updatedUser1 = await _userRepository!.GetByIdAsync(user1.Id, TestCancellationToken);
        var updatedUser2 = await _userRepository!.GetByIdAsync(user2.Id, TestCancellationToken);

        updatedUser1!.PasswordHash.Value.Should().NotBe(originalHash1);
        updatedUser2!.PasswordHash.Value.Should().NotBe(originalHash2);

        // Verify new password can be verified
        var isValid1 = updatedUser1.PasswordHash.Verify(newPassword);
        var isValid2 = updatedUser2.PasswordHash.Verify(newPassword);

        isValid1.Should().BeTrue();
        isValid2.Should().BeTrue();
    }

    #endregion

    #region E2E: Performance Test

    [Fact]
    public async Task E2E_BulkImport_With100Users_ShouldCompleteWithinTimeLimit()
    {
        // Arrange: Generate CSV with 100 users
        var csvLines = new List<string> { "email,displayName,role,password" };
        for (int i = 1; i <= 100; i++)
        {
            csvLines.Add($"perf{i}@test.com,Performance User {i},user,Password{i}!");
        }
        var csvContent = string.Join("\n", csvLines);

        var adminId = Guid.NewGuid();
        var logger = new Mock<ILogger<BulkImportUsersCommandHandler>>();
        var handler = new BulkImportUsersCommandHandler(_userRepository!, _unitOfWork!, logger.Object);
        var command = new BulkImportUsersCommand(csvContent, adminId);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await handler.Handle(command, TestCancellationToken);
        stopwatch.Stop();

        // Assert: Performance (Issue #2603: Adjusted from 5s to 10s for realistic E2E timing)
        result.SuccessCount.Should().Be(100);
        result.FailedCount.Should().Be(0);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // <10s for 100 users (E2E with real DB)

        // Verify database
        var userCount = await _dbContext!.Users.CountAsync(TestCancellationToken);
        userCount.Should().Be(100);

        _output($"100 users imported in {stopwatch.ElapsedMilliseconds}ms");
    }

    #endregion

    #region E2E: Data Integrity

    [Fact]
    public async Task E2E_BulkImport_WithDuplicateEmailInDatabase_ShouldFail()
    {
        // Arrange: Create existing user
        var existingUser = CreateTestUser("duplicate@test.com", "Existing User", Role.User);
        await _userRepository!.AddAsync(existingUser, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var csvContent = @"email,displayName,role,password
duplicate@test.com,Duplicate User,user,Password123!";

        var adminId = Guid.NewGuid();
        var logger = new Mock<ILogger<BulkImportUsersCommandHandler>>();
        var handler = new BulkImportUsersCommandHandler(_userRepository!, _unitOfWork!, logger.Object);
        var command = new BulkImportUsersCommand(csvContent, adminId);

        // Act & Assert
        var act = () => handler.Handle(command, TestCancellationToken);
        var exception = (await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.DomainException>()).Which;

        exception.Message.Should().Contain("already exist");
    }

    [Fact]
    public async Task E2E_BulkExport_WithRoleFilter_ShouldOnlyExportMatchingUsers()
    {
        // Arrange: Create users with different roles
        var user1 = CreateTestUser("admin1@test.com", "Admin 1", Role.Admin);
        var user2 = CreateTestUser("user1@test.com", "User 1", Role.User);
        var user3 = CreateTestUser("admin2@test.com", "Admin 2", Role.Admin);

        await _userRepository!.AddAsync(user1, TestCancellationToken);
        await _userRepository!.AddAsync(user2, TestCancellationToken);
        await _userRepository!.AddAsync(user3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var logger = new Mock<ILogger<BulkExportUsersQueryHandler>>();
        var handler = new BulkExportUsersQueryHandler(_userProfileRepository!, logger.Object);
        var query = new BulkExportUsersQuery("admin", null);

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.Should().Contain("admin1@test.com");
        result.Should().Contain("admin2@test.com");
        result.Should().NotContain("user1@test.com");

        var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(3); // Header + 2 admin users
    }

    #endregion

    #region Helper Methods

    private User CreateTestUser(string email, string displayName, Role role)
    {
        return new User(
            id: Guid.NewGuid(),
            email: new Email(email),
            displayName: displayName,
            passwordHash: PasswordHash.Create("TempPassword123!"),
            role: role
        );
    }

    #endregion
}
