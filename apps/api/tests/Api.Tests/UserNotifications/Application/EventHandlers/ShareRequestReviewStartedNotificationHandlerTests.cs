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

public sealed class ShareRequestReviewStartedNotificationHandlerTests
{
    private readonly Mock<INotificationDispatcher> _dispatcher;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IShareRequestRepository> _shareRequestRepo;
    private readonly Mock<ISharedGameRepository> _sharedGameRepo;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ShareRequestReviewStartedNotificationHandler _sut;

    private readonly Guid _shareRequestId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _sourceGameId = Guid.NewGuid();

    public ShareRequestReviewStartedNotificationHandlerTests()
    {
        _dispatcher = new Mock<INotificationDispatcher>();
        _userRepo = new Mock<IUserRepository>();
        _shareRequestRepo = new Mock<IShareRequestRepository>();
        _sharedGameRepo = new Mock<ISharedGameRepository>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        _sut = new ShareRequestReviewStartedNotificationHandler(
            _dbContext, _dispatcher.Object, _userRepo.Object,
            _shareRequestRepo.Object, _sharedGameRepo.Object,
            new Mock<ILogger<ShareRequestReviewStartedNotificationHandler>>().Object);
    }

    [Fact]
    public async Task Handle_DispatchesGameProposalInReviewNotification()
    {
        var user = new UserBuilder().WithEmail("user@example.com").WithDisplayName("Test User").Build();
        var shareRequest = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGameProposal, "Test proposal");
        var sourceGame = SharedGame.Create("Catan", 1995, "Settlers of Catan", 3, 4, 90, 10, 2.3m, 7.8m,
            "https://example.com/catan.jpg", "https://example.com/catan-thumb.jpg", null, Guid.NewGuid(), null);

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>())).ReturnsAsync(shareRequest);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_sourceGameId, It.IsAny<CancellationToken>())).ReturnsAsync(sourceGame);

        await _sut.Handle(new ShareRequestReviewStartedEvent(_shareRequestId, _adminId), CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m =>
                m.Type == NotificationType.GameProposalInReview &&
                m.RecipientUserId == _userId &&
                m.DeepLinkPath == $"/contributions/requests/{_shareRequestId}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenShareRequestNotFound_SkipsNotification()
    {
        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>())).ReturnsAsync((ShareRequest?)null);

        await _sut.Handle(new ShareRequestReviewStartedEvent(_shareRequestId, _adminId), CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_SkipsNotification()
    {
        var shareRequest = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGameProposal, "Test proposal");
        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>())).ReturnsAsync(shareRequest);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        await _sut.Handle(new ShareRequestReviewStartedEvent(_shareRequestId, _adminId), CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
