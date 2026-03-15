using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Role = Api.SharedKernel.Domain.ValueObjects.Role;

namespace Api.Tests.UserNotifications.Application.EventHandlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class SharedGameSubmittedForApprovalNotificationHandlerTests : IAsyncLifetime
{
    private readonly Mock<INotificationDispatcher> _dispatcher;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<ISharedGameRepository> _sharedGameRepo;
    private readonly MeepleAiDbContext _dbContext;
    private readonly SharedGameSubmittedForApprovalNotificationHandler _sut;

    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _editorId = Guid.NewGuid();
    private readonly Guid _adminId1 = Guid.NewGuid();
    private readonly Guid _adminId2 = Guid.NewGuid();

    public SharedGameSubmittedForApprovalNotificationHandlerTests()
    {
        _dispatcher = new Mock<INotificationDispatcher>();
        _userRepo = new Mock<IUserRepository>();
        _sharedGameRepo = new Mock<ISharedGameRepository>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        _sut = new SharedGameSubmittedForApprovalNotificationHandler(
            _dbContext, _dispatcher.Object, _userRepo.Object, _sharedGameRepo.Object,
            new Mock<ILogger<SharedGameSubmittedForApprovalNotificationHandler>>().Object);
    }

    public async ValueTask InitializeAsync()
    {
        var admin1 = new UserBuilder().WithId(_adminId1).WithEmail("admin1@example.com").WithDisplayName("Admin One").WithRole(Role.Admin).Build();
        var admin2 = new UserBuilder().WithId(_adminId2).WithEmail("admin2@example.com").WithDisplayName("Admin Two").WithRole(Role.Admin).Build();
        _userRepo.Setup(r => r.GetAdminUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User> { admin1, admin2 });
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _dbContext.DisposeAsync();

    [Fact]
    public async Task Handle_DispatchesNotificationForAllAdmins()
    {
        var editor = new UserBuilder().WithId(_editorId).WithEmail("editor@example.com").WithDisplayName("Editor User").Build();
        var game = SharedGame.Create("Gloomhaven", 2017, "Epic dungeon crawler", 1, 4, 120, 14, 3.9m, 8.8m,
            "https://example.com/image.jpg", "https://example.com/thumb.jpg", null, _editorId, 174430);

        _userRepo.Setup(r => r.GetByIdAsync(_editorId, It.IsAny<CancellationToken>())).ReturnsAsync(editor);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _sut.Handle(new SharedGameSubmittedForApprovalEvent(_gameId, _editorId), CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m => m.Type == NotificationType.AdminSharedGameSubmitted),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenSubmitterNotFound_SkipsNotification()
    {
        _userRepo.Setup(r => r.GetByIdAsync(_editorId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        await _sut.Handle(new SharedGameSubmittedForApprovalEvent(_gameId, _editorId), CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNoAdminUsersExist_SkipsNotification()
    {
        _userRepo.Setup(r => r.GetAdminUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        var editor = new UserBuilder().WithId(_editorId).Build();
        var game = SharedGame.Create("Test Game", 2020, "Test", 1, 4, 60, 10, 2.0m, 7.0m,
            "https://example.com/img.jpg", "https://example.com/thumb.jpg", null, _editorId, null);

        _userRepo.Setup(r => r.GetByIdAsync(_editorId, It.IsAny<CancellationToken>())).ReturnsAsync(editor);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _sut.Handle(new SharedGameSubmittedForApprovalEvent(_gameId, _editorId), CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
