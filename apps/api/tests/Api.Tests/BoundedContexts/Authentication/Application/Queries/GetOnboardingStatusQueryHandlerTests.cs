using Api.BoundedContexts.Authentication.Application.Queries.Onboarding;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public sealed class GetOnboardingStatusQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly GetOnboardingStatusQueryHandler _handler;

    public GetOnboardingStatusQueryHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetOnboardingStatusQueryHandler(_dbContext, _userRepoMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private static User CreateTestUser(Guid? userId = null)
    {
        return new User(
            userId ?? Guid.NewGuid(),
            new Email("test@example.com"),
            "Test User",
            PasswordHash.Create("TestPassword123!"),
            Role.User);
    }

    [Fact]
    public async Task Handle_NoGamesNoSessions_ReturnsAllFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var query = new GetOnboardingStatusQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.Steps.HasGames);
        Assert.False(result.Steps.HasSessions);
        Assert.False(result.Steps.HasCompletedProfile);
        Assert.Null(result.WizardSeenAt);
        Assert.Null(result.DismissedAt);
    }

    [Fact]
    public async Task Handle_WithGames_ReturnsHasGamesTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _dbContext.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SharedGameId = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();

        var query = new GetOnboardingStatusQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.Steps.HasGames);
        Assert.False(result.Steps.HasSessions);
    }

    [Fact]
    public async Task Handle_WithSessions_ReturnsHasSessionsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _dbContext.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = Guid.NewGuid(),
            SessionCode = "ABC123",
            SessionType = "Standard",
            Status = "Active",
            SessionDate = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var query = new GetOnboardingStatusQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.Steps.HasSessions);
        Assert.False(result.Steps.HasGames);
    }

    [Fact]
    public async Task Handle_WithAvatarUrl_ReturnsHasCompletedProfileTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        user.UpdateAvatarUrl("https://example.com/avatar.png");

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var query = new GetOnboardingStatusQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.Steps.HasCompletedProfile);
    }

    [Fact]
    public async Task Handle_WithBio_ReturnsHasCompletedProfileTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        user.UpdateBio("Board game enthusiast");

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var query = new GetOnboardingStatusQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.Steps.HasCompletedProfile);
    }

    [Fact]
    public async Task Handle_ReturnsWizardSeenAt_AndDismissedAt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        user.MarkOnboardingWizardSeen();
        user.DismissOnboarding();

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var query = new GetOnboardingStatusQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result.WizardSeenAt);
        Assert.NotNull(result.DismissedAt);
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var query = new GetOnboardingStatusQuery(userId);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.Handle(query, CancellationToken.None));
    }
}
