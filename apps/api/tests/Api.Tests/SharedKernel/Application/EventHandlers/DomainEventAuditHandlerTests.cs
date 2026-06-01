using Api.BoundedContexts.Administration.Application;
using Api.Services;
using Api.SharedKernel.Application.EventHandlers;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.SharedKernel.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="DomainEventAuditHandler{TEvent}"/> — issue #1534 audit-path collapse.
/// Verifies the open-generic handler enqueues one audit_outbox row per domain event with the
/// canonical payload shape (Action, Resource, ResourceId, UserId, Details, Timestamp).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class DomainEventAuditHandlerTests
{
    // Test event: bare minimum IDomainEvent
    public sealed record SystemEvent(string Reason) : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
        public Guid EventId { get; init; } = Guid.NewGuid();
    }

    // Test event: with UserId property (convention-based extraction)
    public sealed record UserScopedEvent(Guid UserId, string Action) : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
        public Guid EventId { get; init; } = Guid.NewGuid();
    }

    // Test event: with UserId as nullable Guid
    public sealed record OptionallyUserScopedEvent(Guid? UserId) : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
        public Guid EventId { get; init; } = Guid.NewGuid();
    }

    private readonly Mock<AuditService> _auditServiceMock;
    private readonly Mock<ILogger<DomainEventAuditHandler<SystemEvent>>> _systemLoggerMock;
    private readonly Mock<ILogger<DomainEventAuditHandler<UserScopedEvent>>> _userScopedLoggerMock;
    private readonly Mock<ILogger<DomainEventAuditHandler<OptionallyUserScopedEvent>>> _optionalLoggerMock;

    public DomainEventAuditHandlerTests()
    {
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var auditLoggerMock = new Mock<ILogger<AuditService>>();
        _auditServiceMock = new Mock<AuditService>(dbContext, auditLoggerMock.Object, (TimeProvider?)null)
        {
            CallBase = false,
        };
        _auditServiceMock
            .Setup(s => s.EnqueueAuditAsync(It.IsAny<AuditOutboxPayload>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _systemLoggerMock = new Mock<ILogger<DomainEventAuditHandler<SystemEvent>>>();
        _userScopedLoggerMock = new Mock<ILogger<DomainEventAuditHandler<UserScopedEvent>>>();
        _optionalLoggerMock = new Mock<ILogger<DomainEventAuditHandler<OptionallyUserScopedEvent>>>();
    }

    [Fact]
    public async Task Handle_SystemEvent_EnqueuesAuditOutboxRowWithCanonicalShape()
    {
        // Arrange
        var handler = new DomainEventAuditHandler<SystemEvent>(_auditServiceMock.Object, _systemLoggerMock.Object);
        var occurredAt = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var eventId = Guid.NewGuid();
        var notification = new SystemEvent("Healthcheck failed") { OccurredAt = occurredAt, EventId = eventId };

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        _auditServiceMock.Verify(
            s => s.EnqueueAuditAsync(
                It.Is<AuditOutboxPayload>(p =>
                    p.Action == "DomainEvent.SystemEvent" &&
                    p.Resource == "SystemEvent" &&
                    p.ResourceId == eventId.ToString() &&
                    p.UserId == null &&
                    p.Result == "Success" &&
                    p.RequestType == "IDomainEvent" &&
                    p.Timestamp == new DateTimeOffset(occurredAt, TimeSpan.Zero)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SystemEvent_SerializesEventBodyInDetails()
    {
        // Arrange
        var handler = new DomainEventAuditHandler<SystemEvent>(_auditServiceMock.Object, _systemLoggerMock.Object);
        var notification = new SystemEvent("queue saturated");

        AuditOutboxPayload? captured = null;
        _auditServiceMock
            .Setup(s => s.EnqueueAuditAsync(It.IsAny<AuditOutboxPayload>(), It.IsAny<CancellationToken>()))
            .Callback<AuditOutboxPayload, CancellationToken>((p, _) => captured = p)
            .Returns(Task.CompletedTask);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.Details.Should().NotBeNullOrWhiteSpace();
        captured.Details.Should().Contain("queue saturated");
        captured.Details.Should().Contain("Reason");
    }

    [Fact]
    public async Task Handle_UserScopedEvent_ExtractsUserIdViaConvention()
    {
        // Arrange
        var handler = new DomainEventAuditHandler<UserScopedEvent>(_auditServiceMock.Object, _userScopedLoggerMock.Object);
        var userId = Guid.NewGuid();
        var notification = new UserScopedEvent(userId, "RoleChanged");

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        _auditServiceMock.Verify(
            s => s.EnqueueAuditAsync(
                It.Is<AuditOutboxPayload>(p =>
                    p.UserId == userId.ToString() &&
                    p.Action == "DomainEvent.UserScopedEvent"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OptionallyUserScopedEvent_WithNullUserId_DoesNotExtractUserId()
    {
        // Arrange
        var handler = new DomainEventAuditHandler<OptionallyUserScopedEvent>(_auditServiceMock.Object, _optionalLoggerMock.Object);
        var notification = new OptionallyUserScopedEvent(UserId: null);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        _auditServiceMock.Verify(
            s => s.EnqueueAuditAsync(
                It.Is<AuditOutboxPayload>(p => p.UserId == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AuditServiceThrows_DoesNotPropagate_AndLogsError()
    {
        // Arrange — resilience: audit failures must never break event handling
        _auditServiceMock
            .Setup(s => s.EnqueueAuditAsync(It.IsAny<AuditOutboxPayload>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db connection lost"));

        var handler = new DomainEventAuditHandler<SystemEvent>(_auditServiceMock.Object, _systemLoggerMock.Object);
        var notification = new SystemEvent("event");

        // Act
        var act = async () => await handler.Handle(notification, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();

        _systemLoggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to enqueue audit")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ResourceIdMatchesEventIdAsString()
    {
        // Arrange
        var handler = new DomainEventAuditHandler<SystemEvent>(_auditServiceMock.Object, _systemLoggerMock.Object);
        var eventId = Guid.NewGuid();
        var notification = new SystemEvent("x") { EventId = eventId };

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        _auditServiceMock.Verify(
            s => s.EnqueueAuditAsync(
                It.Is<AuditOutboxPayload>(p => p.ResourceId == eventId.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
