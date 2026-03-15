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

public sealed class ShareRequestCreatedNotificationHandlerTests
{
    private readonly Mock<INotificationDispatcher> _dispatcher;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IShareRequestRepository> _shareRequestRepo;
    private readonly Mock<ISharedGameRepository> _sharedGameRepo;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ShareRequestCreatedNotificationHandler _sut;

    private readonly Guid _shareRequestId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _sourceGameId = Guid.NewGuid();

    public ShareRequestCreatedNotificationHandlerTests()
    {
        _dispatcher = new Mock<INotificationDispatcher>();
        _userRepo = new Mock<IUserRepository>();
        _shareRequestRepo = new Mock<IShareRequestRepository>();
        _sharedGameRepo = new Mock<ISharedGameRepository>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        _sut = new ShareRequestCreatedNotificationHandler(
            _dbContext,
            _dispatcher.Object,
            _userRepo.Object,
            _shareRequestRepo.Object,
            _sharedGameRepo.Object,
            new Mock<ILogger<ShareRequestCreatedNotificationHandler>>().Object);
    }

    [Fact]
    public async Task Handle_DispatchesNotificationWithCorrectType()
    {
        // Arrange
        var user = new UserBuilder().WithEmail("test@example.com").WithDisplayName("Test User").Build();
        var shareRequest = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGame, "Test notes");
        var sourceGame = SharedGame.Create("Test Game", 2020, "Test description", 1, 4, 60, 13, 2.5m, 7.5m,
            "https://example.com/image.jpg", "https://example.com/thumb.jpg", null, Guid.NewGuid(), null);

        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>())).ReturnsAsync(shareRequest);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_sourceGameId, It.IsAny<CancellationToken>())).ReturnsAsync(sourceGame);

        var domainEvent = new ShareRequestCreatedEvent(_shareRequestId, _userId, _sourceGameId, ContributionType.NewGame);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m =>
                m.Type == NotificationType.ShareRequestCreated &&
                m.RecipientUserId == _userId &&
                m.Payload is ShareRequestPayload),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_SkipsNotification()
    {
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var domainEvent = new ShareRequestCreatedEvent(_shareRequestId, _userId, _sourceGameId, ContributionType.NewGame);

        await _sut.Handle(domainEvent, CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDispatcherFails_BaseHandlerPropagates()
    {
        var user = new UserBuilder().WithEmail("test@example.com").WithDisplayName("Test User").Build();
        var shareRequest = ShareRequest.Create(_userId, _sourceGameId, ContributionType.NewGame, "Test notes");
        var sourceGame = SharedGame.Create("Test Game", 2020, "Test", 1, 4, 60, 13, 2.5m, 7.5m,
            "https://example.com/image.jpg", "https://example.com/thumb.jpg", null, Guid.NewGuid(), null);

        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>())).ReturnsAsync(shareRequest);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_sourceGameId, It.IsAny<CancellationToken>())).ReturnsAsync(sourceGame);
        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("dispatch error"));

        var domainEvent = new ShareRequestCreatedEvent(_shareRequestId, _userId, _sourceGameId, ContributionType.NewGame);

        // DomainEventHandlerBase re-throws, so this should throw
        var act = async () => await _sut.Handle(domainEvent, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
