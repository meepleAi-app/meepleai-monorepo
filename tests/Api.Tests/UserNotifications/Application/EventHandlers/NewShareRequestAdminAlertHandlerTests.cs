using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.UserNotifications.Application.EventHandlers;

/// <summary>
/// Integration tests for NewShareRequestAdminAlertHandler with in-memory database.
/// ISSUE-2740: Admin alert system for new share requests.
/// ISSUE-3005: Fixed DbContext mocking issues by using InMemoryDatabase.
/// </summary>
public sealed class NewShareRequestAdminAlertHandlerTests : IAsyncLifetime
{
    private readonly DbContextOptions<MeepleAiDbContext> _options;
    private readonly Mock<INotificationRepository> _notificationRepositoryMock;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IDomainEventCollector> _mockEventCollector;
    private readonly Mock<ILogger<NewShareRequestAdminAlertHandler>> _loggerMock;
    private MeepleAiDbContext _dbContext = null!;
    private NewShareRequestAdminAlertHandler _handler = null!;

    public NewShareRequestAdminAlertHandlerTests()
    {
        _options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"NewShareRequestAdminAlertTest_{Guid.NewGuid()}")
            .Options;

        _notificationRepositoryMock = new Mock<INotificationRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockEventCollector = new Mock<IDomainEventCollector>();
        _mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());
        _loggerMock = new Mock<ILogger<NewShareRequestAdminAlertHandler>>();
    }

    public async Task InitializeAsync()
    {
        _dbContext = new MeepleAiDbContext(_options, _mockMediator.Object, _mockEventCollector.Object);
        _handler = new NewShareRequestAdminAlertHandler(
            _notificationRepositoryMock.Object,
            _dbContext,
            _loggerMock.Object);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Handle_WithAdminUsers_CreatesNotifications()
    {
        // Arrange
        var admin1Id = Guid.NewGuid();
        var admin2Id = Guid.NewGuid();

        // Seed admin users in database
        var admin1 = new UserEntity
        {
            Id = admin1Id,
            Email = "admin1@test.com",
            DisplayName = "Admin One",
            Role = "admin",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };
        var admin2 = new UserEntity
        {
            Id = admin2Id,
            Email = "admin2@test.com",
            DisplayName = "Admin Two",
            Role = "admin",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.Set<UserEntity>().AddRangeAsync(admin1, admin2);
        await _dbContext.SaveChangesAsync();

        var shareRequestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var domainEvent = new ShareRequestCreatedEvent(
            shareRequestId,
            userId,
            Guid.NewGuid(),
            ContributionType.NewGame);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        // Verify notification repository was called twice (once for each admin)
        _notificationRepositoryMock.Verify(
            r => r.AddAsync(
                It.Is<Notification>(n =>
                    n.Type.Value == NotificationType.AdminNewShareRequest.Value &&
                    n.Severity.Value == NotificationSeverity.Info.Value &&
                    (n.UserId == admin1Id || n.UserId == admin2Id)),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created 2 admin notifications")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Arrange
        using var dbContext = new MeepleAiDbContext(_options, _mockMediator.Object, _mockEventCollector.Object);

        // Act & Assert
        var act = () => new NewShareRequestAdminAlertHandler(
            null!,
            dbContext,
            _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("notificationRepository");
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new NewShareRequestAdminAlertHandler(
            _notificationRepositoryMock.Object,
            null!,
            _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("dbContext");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        using var dbContext = new MeepleAiDbContext(_options, _mockMediator.Object, _mockEventCollector.Object);

        // Act & Assert
        var act = () => new NewShareRequestAdminAlertHandler(
            _notificationRepositoryMock.Object,
            dbContext,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
