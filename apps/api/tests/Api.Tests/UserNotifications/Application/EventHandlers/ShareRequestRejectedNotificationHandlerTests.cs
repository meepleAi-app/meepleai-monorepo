using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.UserNotifications.Application.EventHandlers;

public sealed class ShareRequestRejectedNotificationHandlerTests
{
    private readonly Mock<INotificationDispatcher> _dispatcher;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IShareRequestRepository> _shareRequestRepo;
    private readonly Mock<ISharedGameRepository> _sharedGameRepo;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ShareRequestRejectedNotificationHandler _sut;

    private readonly Guid _shareRequestId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();

    public ShareRequestRejectedNotificationHandlerTests()
    {
        _dispatcher = new Mock<INotificationDispatcher>();
        _userRepo = new Mock<IUserRepository>();
        _shareRequestRepo = new Mock<IShareRequestRepository>();
        _sharedGameRepo = new Mock<ISharedGameRepository>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        _sut = new ShareRequestRejectedNotificationHandler(
            _dbContext, _dispatcher.Object, _userRepo.Object,
            _shareRequestRepo.Object, _sharedGameRepo.Object,
            new Mock<ILogger<ShareRequestRejectedNotificationHandler>>().Object);
    }

    [Fact]
    public async Task Handle_DispatchesRejectedNotification()
    {
        var user = new UserBuilder().WithEmail("test@example.com").WithDisplayName("Test User").Build();
        var shareRequest = ShareRequest.Create(_userId, Guid.NewGuid(), ContributionType.NewGame, "Test notes");
        var sourceGame = SharedGame.Create("Test Game", 2020, "Test", 1, 4, 60, 13, 2.5m, 7.5m,
            "https://example.com/image.jpg", "https://example.com/thumb.jpg", null, Guid.NewGuid(), null);

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>())).ReturnsAsync(shareRequest);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(sourceGame);

        await _sut.Handle(new ShareRequestRejectedEvent(_shareRequestId, _adminId, "Incomplete"), CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m => m.Type == NotificationType.ShareRequestRejected && m.RecipientUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenShareRequestNotFound_SkipsNotification()
    {
        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>())).ReturnsAsync((ShareRequest?)null);

        await _sut.Handle(new ShareRequestRejectedEvent(_shareRequestId, _adminId, "Test reason"), CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
