using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundServices;
using Api.Infrastructure.Entities.Authentication;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.BackgroundServices;

/// <summary>
/// Unit tests for <see cref="InvitationCleanupService"/>.
/// Admin Invitation Flow: verifies that expired pending users are cleaned up
/// and their invitation tokens are marked as expired.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public sealed class InvitationCleanupServiceTests : IDisposable
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IInvitationTokenRepository> _invitationRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly MeepleAiDbContext _dbContext;
    private readonly TestTimeProvider _timeProvider;
    private readonly InvitationCleanupService _sut;

    private static readonly DateTime BaseTime = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    public InvitationCleanupServiceTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _invitationRepoMock = new Mock<IInvitationTokenRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _timeProvider = new TestTimeProvider(BaseTime);

        // Wire scope chain
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(MeepleAiDbContext)))
            .Returns(_dbContext);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IInvitationTokenRepository)))
            .Returns(_invitationRepoMock.Object);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IUserRepository)))
            .Returns(_userRepoMock.Object);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IUnitOfWork)))
            .Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _sut = new InvitationCleanupService(
            _scopeFactoryMock.Object,
            _timeProvider,
            NullLogger<InvitationCleanupService>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task SeedUserEntity(Guid userId, string email, string status, DateTime? invitationExpiresAt)
    {
        _dbContext.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = email,
            Status = status,
            InvitationExpiresAt = invitationExpiresAt,
            DisplayName = "Test User",
            CreatedAt = BaseTime.AddDays(-7)
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedInvitationTokenEntity(Guid tokenId, Guid pendingUserId, string status)
    {
        _dbContext.InvitationTokens.Add(new InvitationTokenEntity
        {
            Id = tokenId,
            Email = "test@example.com",
            Role = "User",
            TokenHash = Convert.ToBase64String(new byte[32]),
            InvitedByUserId = Guid.NewGuid(),
            Status = status,
            ExpiresAt = BaseTime.AddDays(-1),
            CreatedAt = BaseTime.AddDays(-7),
            PendingUserId = pendingUserId
        });
        await _dbContext.SaveChangesAsync();
    }

    // ── No expired users ────────────────────────────────────────────────────

    [Fact]
    public async Task CleanupExpiredInvitations_WhenNoExpiredPendingUsers_DoesNothing()
    {
        // Arrange: user is pending but invitation hasn't expired yet
        var userId = Guid.NewGuid();
        await SeedUserEntity(userId, "active@example.com", "Pending", BaseTime.AddDays(1));

        // Act
        await _sut.CleanupExpiredInvitationsAsync(CancellationToken.None);

        // Assert
        _userRepoMock.Verify(
            r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Expired pending user is deleted ─────────────────────────────────────

    [Fact]
    public async Task CleanupExpiredInvitations_WithExpiredPendingUser_DeletesUserAndMarksTokenExpired()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        await SeedUserEntity(userId, "expired@example.com", "Pending", BaseTime.AddDays(-1));
        await SeedInvitationTokenEntity(tokenId, userId, "Pending");

        // Create a real InvitationToken via factory (pending status)
        var invitation = InvitationToken.Create(
            "expired@example.com", "User", "fakehash", Guid.NewGuid());
        _invitationRepoMock
            .Setup(r => r.GetByIdAsync(tokenId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _invitationRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<InvitationToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Create a real User via CreatePending factory
        var user = User.CreatePending(
            Api.BoundedContexts.Authentication.Domain.ValueObjects.Email.Parse("expired@example.com"),
            "Test User",
            Api.SharedKernel.Domain.ValueObjects.Role.Parse("User"),
            Api.SharedKernel.Domain.ValueObjects.UserTier.Parse("free"),
            Guid.NewGuid(),
            BaseTime.AddDays(-1),
            _timeProvider);
        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepoMock
            .Setup(r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CleanupExpiredInvitationsAsync(CancellationToken.None);

        // Assert: token was fetched and updated (MarkExpired called)
        _invitationRepoMock.Verify(
            r => r.GetByIdAsync(tokenId, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(InvitationStatus.Expired, invitation.Status);

        // Assert: user was deleted
        _userRepoMock.Verify(
            r => r.DeleteAsync(user, It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert: changes were saved
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Active users are not touched ────────────────────────────────────────

    [Fact]
    public async Task CleanupExpiredInvitations_ActiveUsersAreNotTouched()
    {
        // Arrange: active user (not pending), even with old invitation date
        var userId = Guid.NewGuid();
        await SeedUserEntity(userId, "active@example.com", "Active", BaseTime.AddDays(-30));

        // Act
        await _sut.CleanupExpiredInvitationsAsync(CancellationToken.None);

        // Assert
        _userRepoMock.Verify(
            r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Pending user without expiry is not touched ──────────────────────────

    [Fact]
    public async Task CleanupExpiredInvitations_PendingUserWithoutExpiry_IsNotTouched()
    {
        // Arrange: pending user with no InvitationExpiresAt set
        var userId = Guid.NewGuid();
        await SeedUserEntity(userId, "noexpiry@example.com", "Pending", null);

        // Act
        await _sut.CleanupExpiredInvitationsAsync(CancellationToken.None);

        // Assert
        _userRepoMock.Verify(
            r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Error for one user does not stop cleanup for others ─────────────────

    [Fact]
    public async Task CleanupExpiredInvitations_ErrorForOneUser_ContinuesWithOthers()
    {
        // Arrange: two expired pending users
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        await SeedUserEntity(userId1, "expired1@example.com", "Pending", BaseTime.AddDays(-1));
        await SeedUserEntity(userId2, "expired2@example.com", "Pending", BaseTime.AddDays(-2));

        // First user lookup throws, second succeeds
        var user2 = User.CreatePending(
            Api.BoundedContexts.Authentication.Domain.ValueObjects.Email.Parse("expired2@example.com"),
            "Test User 2",
            Api.SharedKernel.Domain.ValueObjects.Role.Parse("User"),
            Api.SharedKernel.Domain.ValueObjects.UserTier.Parse("free"),
            Guid.NewGuid(),
            BaseTime.AddDays(-2),
            _timeProvider);

        var callCount = 0;
        _userRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) =>
            {
                callCount++;
                if (id == userId1)
                    throw new InvalidOperationException("Simulated DB failure");
                return Task.FromResult<User?>(user2);
            });

        _userRepoMock
            .Setup(r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act — must not throw
        await _sut.CleanupExpiredInvitationsAsync(CancellationToken.None);

        // Assert — both users were attempted
        Assert.Equal(2, callCount);
    }

    // ── Multiple expired users are all cleaned up ───────────────────────────

    [Fact]
    public async Task CleanupExpiredInvitations_MultipleExpiredUsers_AllAreCleaned()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        await SeedUserEntity(userId1, "expired1@example.com", "Pending", BaseTime.AddDays(-1));
        await SeedUserEntity(userId2, "expired2@example.com", "Pending", BaseTime.AddDays(-3));

        var userForCleanup = User.CreatePending(
            Api.BoundedContexts.Authentication.Domain.ValueObjects.Email.Parse("generic@example.com"),
            "Test User",
            Api.SharedKernel.Domain.ValueObjects.Role.Parse("User"),
            Api.SharedKernel.Domain.ValueObjects.UserTier.Parse("free"),
            Guid.NewGuid(),
            BaseTime.AddDays(-1),
            _timeProvider);

        _userRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userForCleanup);
        _userRepoMock
            .Setup(r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CleanupExpiredInvitationsAsync(CancellationToken.None);

        // Assert — both users were deleted
        _userRepoMock.Verify(
            r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
