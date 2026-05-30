using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers.Integration;

/// <summary>
/// Integration tests for SuspendUserCommandHandler.
/// Tests audit logging with real database persistence.
/// Issue #2886: Verify audit log entries are persisted correctly.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
public sealed class SuspendUserCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _dbName = null!;
    private MeepleAiDbContext _dbContext = null!;
    private IUserRepository _userRepository = null!;
    private IAuditLogRepository _auditLogRepository = null!;
    private IUnitOfWork _unitOfWork = null!;
    private SuspendUserCommandHandler _handler = null!;

    public SuspendUserCommandHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _dbName = $"test_suspend_user_{Guid.NewGuid():N}";

        // Create isolated database
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_dbName);

        var optionsBuilder = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector()) // Issue #3547
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors();

        var mockMediator = TestDbContextFactory.CreateMockMediator();
        var mockEventCollector = TestDbContextFactory.CreateMockEventCollector();

        _dbContext = new MeepleAiDbContext(optionsBuilder.Options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync();

        // Setup repositories and handler
        _userRepository = new UserRepository(_dbContext, mockEventCollector.Object);
        _auditLogRepository = new AuditLogRepository(_dbContext, mockEventCollector.Object);
        _unitOfWork = new EfCoreUnitOfWork(_dbContext);
        _handler = new SuspendUserCommandHandler(_userRepository, _auditLogRepository, _unitOfWork);
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.DropIsolatedDatabaseAsync(_dbName);
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Handle_WithValidCommand_PersistsAuditLog()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var reason = "Test suspension";

        var user = new User(
            userId,
            new Email("user@example.com"),
            "Test User",
            PasswordHash.Create("hashedPwd1234"),
            Role.User
        );

        // Seed user
        await _userRepository.AddAsync(user, CancellationToken.None);
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);
        _dbContext.ChangeTracker.Clear();

        var command = new SuspendUserCommand(user.Id.ToString(), requesterId, reason);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - verify user suspended
        result.IsSuspended.Should().BeTrue();
        result.SuspendReason.Should().Be(reason);

        // Assert - verify audit log persisted
        var auditLogs = await _dbContext.AuditLogs
            .Where(a => a.Action == "user_suspended" && a.ResourceId == user.Id.ToString())
            .ToListAsync();

        auditLogs.Should().HaveCount(1);
        var auditLog = auditLogs.First();
        auditLog.UserId.Should().Be(requesterId);
        auditLog.Resource.Should().Be("User");
        auditLog.Result.Should().Be("success");
        auditLog.Details.Should().Contain(user.Email.Value);
        auditLog.Details.Should().Contain(reason);
    }

    [Fact]
    public async Task Handle_WithValidCommand_RaisesUserSuspendedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var user = new User(
            userId,
            new Email("event@example.com"),
            "Event Test User",
            PasswordHash.Create("hashedPwd1234"),
            Role.User
        );

        await _userRepository.AddAsync(user, CancellationToken.None);
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);
        _dbContext.ChangeTracker.Clear();

        var command = new SuspendUserCommand(user.Id.ToString(), requesterId, "Event test");

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - verify domain event was raised
        // Note: Event raising is tested via UserEntity tests
        // Here we verify the full flow completes successfully
        var suspendedUser = await _userRepository.GetByIdAsync(user.Id, CancellationToken.None);
        suspendedUser.Should().NotBeNull();
        suspendedUser!.IsSuspended.Should().BeTrue();
    }

    // Issue #1533 — admin commands must not wipe persisted 2FA backup codes when
    // 2FA is mid-setup (TotpSecret set, IsTwoFactorEnabled=false) or in a stale state.
    [Fact]
    public async Task Handle_SuspendingUserMid2FASetup_PreservesBackupCodes()
    {
        // Arrange — user with persisted backup codes but IsTwoFactorEnabled=false
        // (mimics TotpService.SetupAsync run, no OTP confirmation yet).
        var userId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var user = new User(
            userId,
            new Email("midsetup@s.test"),
            "Mid Setup User",
            PasswordHash.Create("hashedPwd1234"),
            Role.User
        );

        await _userRepository.AddAsync(user, CancellationToken.None);
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Set TotpSecretEncrypted directly on persistence row + seed 6 codes.
        // IsTwoFactorEnabled stays false on purpose (setup-in-progress window).
        var persistedRow = await _dbContext.Users.FirstAsync(u => u.Id == userId);
        persistedRow.TotpSecretEncrypted = "fake_setup_secret_pending_confirmation";

        for (var i = 0; i < 6; i++)
        {
            _dbContext.UserBackupCodes.Add(new Api.Infrastructure.Entities.UserBackupCodeEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CodeHash = $"midsetup-hash-{i}-{Guid.NewGuid():N}",
                IsUsed = false,
                CreatedAt = DateTime.UtcNow,
            });
        }
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var command = new SuspendUserCommand(userId.ToString(), requesterId, "Admin action during 2FA setup");

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — codes still present so the user can resume enrollment.
        var codeCount = await _dbContext.UserBackupCodes
            .AsNoTracking()
            .CountAsync(c => c.UserId == userId);
        codeCount.Should().Be(6,
            because: "SuspendUserCommand MUST NOT wipe persisted backup codes during 2FA setup (#1533)");
    }
}
