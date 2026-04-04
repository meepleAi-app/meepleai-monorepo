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
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.UserNotifications.Application.EventHandlers;

public sealed class ShareRequestApprovedNotificationHandlerTests
{
    private readonly Mock<INotificationDispatcher> _dispatcher;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IShareRequestRepository> _shareRequestRepo;
    private readonly Mock<ISharedGameRepository> _sharedGameRepo;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ShareRequestApprovedNotificationHandler _sut;

    private readonly Guid _shareRequestId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _sharedGameId = Guid.NewGuid();

    public ShareRequestApprovedNotificationHandlerTests()
    {
        _dispatcher = new Mock<INotificationDispatcher>();
        _userRepo = new Mock<IUserRepository>();
        _shareRequestRepo = new Mock<IShareRequestRepository>();
        _sharedGameRepo = new Mock<ISharedGameRepository>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        _sut = new ShareRequestApprovedNotificationHandler(
            _dbContext, _dispatcher.Object, _userRepo.Object,
            _shareRequestRepo.Object, _sharedGameRepo.Object,
            new Mock<ILogger<ShareRequestApprovedNotificationHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ApproveAsNew_DispatchesShareRequestApprovedType()
    {
        var user = new UserBuilder().WithEmail("test@example.com").WithDisplayName("Test User").Build();
        var sourceGameId = Guid.NewGuid();
        var shareRequest = ShareRequest.Create(_userId, sourceGameId, ContributionType.NewGame, "Test notes");
        var sourceGame = SharedGame.Create("Test Game", 2020, "Test", 1, 4, 60, 13, 2.5m, 7.5m,
            "https://example.com/image.jpg", "https://example.com/thumb.jpg", null, Guid.NewGuid(), null);
        var targetGame = SharedGame.Create("Test Game", 2020, "Newly approved", 1, 4, 60, 13, 2.5m, null,
            "https://example.com/image.jpg", "https://example.com/thumb.jpg", null, _userId, null);

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>())).ReturnsAsync(shareRequest);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(sourceGameId, It.IsAny<CancellationToken>())).ReturnsAsync(sourceGame);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_sharedGameId, It.IsAny<CancellationToken>())).ReturnsAsync(targetGame);

        await _sut.Handle(new ShareRequestApprovedEvent(_shareRequestId, _adminId, _sharedGameId), CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m => m.Type == NotificationType.ShareRequestApproved && m.RecipientUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_KbMerge_DispatchesGameProposalKbMergedType()
    {
        var user = new UserBuilder().WithEmail("user@example.com").WithDisplayName("Test User").Build();
        var shareRequest = ShareRequest.Create(_userId, Guid.NewGuid(), ContributionType.NewGameProposal, "Test proposal");
        var sourceGame = SharedGame.Create("Catan", 1995, "Settlers of Catan", 3, 4, 90, 10, 2.3m, 7.8m,
            "https://example.com/catan.jpg", "https://example.com/catan-thumb.jpg", null, Guid.NewGuid(), null);
        var oldTargetGame = SharedGame.Create("Catan", 1995, "Existing Catan entry", 3, 4, 90, 10, 2.3m, 7.8m,
            "https://example.com/catan.jpg", "https://example.com/catan-thumb.jpg", null, Guid.NewGuid(), 13);

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>())).ReturnsAsync(shareRequest);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(shareRequest.SourceGameId, It.IsAny<CancellationToken>())).ReturnsAsync(sourceGame);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_sharedGameId, It.IsAny<CancellationToken>())).ReturnsAsync(oldTargetGame);

        await _sut.Handle(new ShareRequestApprovedEvent(_shareRequestId, _adminId, _sharedGameId), CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m => m.Type == NotificationType.GameProposalKbMerged),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenShareRequestNotFound_SkipsNotification()
    {
        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>())).ReturnsAsync((ShareRequest?)null);

        await _sut.Handle(new ShareRequestApprovedEvent(_shareRequestId, _adminId, _sharedGameId), CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
