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

/// <summary>
/// #535 ME-M3.3: the handler fans out an in-app admin notification per admin (incl. superadmin) when a
/// mechanic card is suppressed, with per-event dedup and a body carrying game title + reason.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class MechanicCardSuppressedAdminNotificationHandlerTests : IAsyncLifetime
{
    private readonly Mock<INotificationDispatcher> _dispatcher = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ISharedGameRepository> _sharedGameRepo = new();
    private readonly MeepleAiDbContext _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
    private MechanicCardSuppressedAdminNotificationHandler _sut = null!;

    private readonly Guid _cardId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _superAdminId = Guid.NewGuid();

    public ValueTask InitializeAsync()
    {
        _sut = new MechanicCardSuppressedAdminNotificationHandler(
            _dbContext, _dispatcher.Object, _userRepo.Object, _sharedGameRepo.Object,
            new Mock<ILogger<MechanicCardSuppressedAdminNotificationHandler>>().Object);

        var admin = new UserBuilder().WithId(_adminId).WithEmail("admin@example.com")
            .WithDisplayName("Admin One").WithRole(Role.Admin).Build();
        var superAdmin = new UserBuilder().WithId(_superAdminId).WithEmail("super@example.com")
            .WithDisplayName("Super Admin").WithRole(Role.SuperAdmin).Build();
        _userRepo.Setup(r => r.GetAdminUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { admin, superAdmin });

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _dbContext.DisposeAsync();

    private static SharedGame Catan() => SharedGame.Create(
        "Catan", 2020, "desc", 1, 4, 60, 10, 2.0m, 7.0m,
        "https://example.com/img.jpg", "https://example.com/thumb.jpg", null, Guid.NewGuid(), null);

    [Fact]
    public async Task Handle_DispatchesToAllAdmins_WithTypeDedupAndBody()
    {
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>())).ReturnsAsync(Catan());
        var evt = new MechanicCardSuppressedEvent(_cardId, _gameId, _actorId,
            "auto_feedback: 5 error reports, feedback score 0.20 below 0.50 threshold");

        await _sut.Handle(evt, CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m =>
                m.Type == NotificationType.AdminMechanicCardSuppressed
                && m.SourceEventId == evt.EventId
                && m.DeepLinkPath == "/admin/knowledge-base/mechanic-extractor/dashboard"
                && ((GenericPayload)m.Payload).Body.Contains("Catan")
                && ((GenericPayload)m.Payload).Body.Contains("auto_feedback")),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenGameMissing_UsesFallbackTitle()
    {
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>())).ReturnsAsync((SharedGame?)null);
        var evt = new MechanicCardSuppressedEvent(_cardId, _gameId, _actorId, "manual takedown for legal reasons here");

        await _sut.Handle(evt, CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m => ((GenericPayload)m.Payload).Body.Contains("un gioco")),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenNoAdmins_SkipsDispatch()
    {
        _userRepo.Setup(r => r.GetAdminUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<User>());
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>())).ReturnsAsync(Catan());
        var evt = new MechanicCardSuppressedEvent(_cardId, _gameId, _actorId, "manual takedown for legal reasons here");

        await _sut.Handle(evt, CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
