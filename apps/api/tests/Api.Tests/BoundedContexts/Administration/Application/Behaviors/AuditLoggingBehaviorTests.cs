using Api.BoundedContexts.Administration.Application;
using Api.BoundedContexts.Administration.Application.Attributes;
using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Behaviors;

/// <summary>
/// Tests for AuditLoggingBehavior pipeline behavior.
/// Issue #3691: Audit Log System.
/// SP5 S1 T3: Updated to assert EnqueueAuditAsync (outbox path) instead of LogAsync.
/// SP5 S1 T3b: Added atomic-path unit tests (routing to EnqueueAuditAtomicAsync vs EnqueueAuditAsync).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class AuditLoggingBehaviorTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly Mock<AuditService> _auditServiceMock;
    private readonly ScopedAuditSnapshotSink _sink;
    private readonly MeepleAiDbContext _dbContext;

    public AuditLoggingBehaviorTests()
    {
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();

        var dbOptions = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"AuditBehaviorTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(dbOptions, Mock.Of<IMediator>(), Mock.Of<IDomainEventCollector>());

        _auditServiceMock = new Mock<AuditService>(
            _dbContext,
            Mock.Of<ILogger<AuditService>>(),
            null!);

        _sink = new ScopedAuditSnapshotSink();
    }

    [Fact]
    public async Task Should_Skip_Logging_For_NonAuditable_Commands()
    {
        // Arrange
        var behavior = CreateBehavior<NonAuditableCommand, string>();
        var command = new NonAuditableCommand("test");
        var nextCalled = false;
        RequestHandlerDelegate<string> next = (ct) =>
        {
            nextCalled = true;
            return Task.FromResult("result");
        };

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
        result.Should().Be("result");
        _auditServiceMock.Verify(
            x => x.EnqueueAuditAsync(
                It.IsAny<AuditOutboxPayload>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Should_Enqueue_Success_For_Auditable_Commands()
    {
        // Arrange
        SetupHttpContext();
        var behavior = CreateBehavior<TestAuditableCommand, string>();
        var command = new TestAuditableCommand(Guid.NewGuid());
        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("ok");

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        result.Should().Be("ok");
        _auditServiceMock.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditOutboxPayload>(p =>
                    p.Action == "TestAction" &&
                    p.Resource == "TestResource" &&
                    p.Result == "Success"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Enqueue_Error_When_Handler_Throws()
    {
        // Arrange
        SetupHttpContext();
        var behavior = CreateBehavior<TestAuditableCommand, string>();
        var command = new TestAuditableCommand(Guid.NewGuid());
        RequestHandlerDelegate<string> next = (ct) =>
            throw new InvalidOperationException("test failure");

        // Act & Assert
        var act = () => behavior.Handle(command, next, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();

        _auditServiceMock.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditOutboxPayload>(p =>
                    p.Action == "TestAction" &&
                    p.Resource == "TestResource" &&
                    p.Result == "Error"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Continue_When_EnqueueAudit_Fails()
    {
        // Arrange: EnqueueAuditAsync throws internally, but behavior should NOT propagate
        SetupHttpContext();
        _auditServiceMock
            .Setup(x => x.EnqueueAuditAsync(
                It.IsAny<AuditOutboxPayload>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("DB connection lost"));

        var behavior = CreateBehavior<TestAuditableCommand, string>();
        var command = new TestAuditableCommand(Guid.NewGuid());
        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("still works");

        // Act — should NOT throw despite audit failure
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        result.Should().Be("still works");
    }

    [Fact]
    public async Task Should_Extract_AdminUserId_From_HttpContext()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupHttpContext(adminId, "admin@test.com");
        var behavior = CreateBehavior<TestAuditableCommand, string>();
        var command = new TestAuditableCommand(Guid.NewGuid());
        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("ok");

        // Act
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert — verify the admin user ID was passed in the payload
        _auditServiceMock.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditOutboxPayload>(p => p.UserId == adminId.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Handle_Missing_HttpContext()
    {
        // Arrange — no HttpContext
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);
        var behavior = CreateBehavior<TestAuditableCommand, string>();
        var command = new TestAuditableCommand(Guid.NewGuid());
        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("ok");

        // Act — should NOT throw
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Should_Include_Snapshots_From_Sink_In_Payload()
    {
        // Arrange — the next() delegate records a snapshot into the sink to simulate the
        // AuditingSaveChangesInterceptor firing during the command's SaveChanges call.
        // (Pre-loading before Handle() would be cleared by the pre-next() Clear() — Fix 1.)
        SetupHttpContext();
        var behavior = CreateBehavior<TestAuditableCommand, string>();
        var command = new TestAuditableCommand(Guid.NewGuid());
        RequestHandlerDelegate<string> next = (ct) =>
        {
            _sink.Record(new AuditSnapshot(
                EntityType: "UserEntity",
                PrimaryKey: Guid.NewGuid().ToString(),
                BeforeJson: null,
                AfterJson: "{\"id\":\"abc\"}",
                Operation: AuditOperation.Insert));
            return Task.FromResult("ok");
        };

        // Act
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert — payload must contain the snapshot recorded during next()
        _auditServiceMock.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditOutboxPayload>(p =>
                    p.Snapshots.Count == 1 &&
                    p.Snapshots[0].EntityType == "UserEntity" &&
                    p.Snapshots[0].Operation == "Insert"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Detect_Oversize_Snapshot_In_Payload()
    {
        // Arrange — next() records a snapshot with the oversize flag set by PayloadTruncator,
        // simulating the interceptor firing during the command's SaveChanges.
        // (Pre-loading before Handle() would be cleared by the pre-next() Clear() — Fix 1.)
        SetupHttpContext();
        var behavior = CreateBehavior<TestAuditableCommand, string>();
        var command = new TestAuditableCommand(Guid.NewGuid());
        RequestHandlerDelegate<string> next = (ct) =>
        {
            _sink.Record(new AuditSnapshot(
                EntityType: "UserEntity",
                PrimaryKey: Guid.NewGuid().ToString(),
                BeforeJson: null,
                AfterJson: "{\"field\":\"value\",\"_oversize\":true}",
                Operation: AuditOperation.Insert));
            return Task.FromResult("ok");
        };

        // Act
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert — Oversize must be flagged
        _auditServiceMock.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditOutboxPayload>(p => p.Oversize),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Clear_Sink_After_Draining()
    {
        // Arrange — next() records a snapshot (simulating the interceptor); after Handle() the sink
        // must be empty so a subsequent command in the same scope sees a clean slate.
        SetupHttpContext();
        var behavior = CreateBehavior<TestAuditableCommand, string>();
        RequestHandlerDelegate<string> next = (ct) =>
        {
            _sink.Record(new AuditSnapshot("UserEntity", Guid.NewGuid().ToString(), null, "{}", AuditOperation.Insert));
            return Task.FromResult("ok");
        };

        // Act
        await behavior.Handle(new TestAuditableCommand(Guid.NewGuid()), next, CancellationToken.None);

        // Assert — sink must be empty after behavior drains it (finally block clears unconditionally)
        _sink.Snapshots.Should().BeEmpty("sink should be cleared after drain to avoid cross-command bleed");
    }

    [Fact]
    public async Task Should_Clear_PreHandler_Snapshots_Before_Capturing_Command_Mutations()
    {
        // Arrange: pre-load the sink with a "stale" snapshot that simulates an earlier SaveChanges
        // in the same request (e.g. a session-touch or a prior [AuditableAction] command).
        // The behavior must clear these before calling next() so they do NOT bleed into THIS command's
        // audit payload.
        SetupHttpContext();
        _sink.Record(new AuditSnapshot("StaleEntity", "stale-pk", null, "{\"x\":1}", AuditOperation.Insert));

        var behavior = CreateBehavior<TestAuditableCommand, string>();
        var command = new TestAuditableCommand(Guid.NewGuid());

        // The next() delegate records a "real" snapshot — simulating the interceptor firing during
        // THIS command's SaveChanges.
        RequestHandlerDelegate<string> next = (ct) =>
        {
            _sink.Record(new AuditSnapshot("RealEntity", "real-pk", null, "{\"y\":2}", AuditOperation.Insert));
            return Task.FromResult("ok");
        };

        AuditOutboxPayload? capturedPayload = null;
        _auditServiceMock
            .Setup(x => x.EnqueueAuditAsync(
                It.IsAny<AuditOutboxPayload>(),
                It.IsAny<CancellationToken>()))
            .Callback<AuditOutboxPayload, CancellationToken>((p, _) => capturedPayload = p);

        // Act
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert: the enqueued payload contains ONLY the RealEntity snapshot, NOT the StaleEntity one.
        // This proves _sink.Clear() fired before next() (Fix 1 — sink contamination guard).
        capturedPayload.Should().NotBeNull();
        capturedPayload!.Snapshots.Should().ContainSingle(
            because: "only the snapshot recorded during next() should be in the payload");
        capturedPayload.Snapshots.Should().Contain(s => s.EntityType == "RealEntity",
            because: "the command's own SaveChanges snapshot must be present");
        capturedPayload.Snapshots.Should().NotContain(s => s.EntityType == "StaleEntity",
            because: "pre-handler snapshots from earlier saves must be discarded by the pre-next() Clear()");
    }

    private void SetupHttpContext(Guid? userId = null, string? email = null)
    {
        var context = new DefaultHttpContext();
        var user = userId.HasValue
            ? new UserDto(
                userId.Value,
                email ?? "test@admin.com",
                "Test Admin",
                "Admin",
                "Enterprise",
                DateTime.UtcNow,
                false,
                null,
                1,
                0)
            : null;

        if (user is not null)
        {
            context.Items[nameof(SessionStatusDto)] = new SessionStatusDto(
                true,
                new Principal(user, Actor: null),
                DateTime.UtcNow.AddHours(1),
                DateTime.UtcNow);
        }

        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // T3b: Atomic-path routing tests
    // These tests verify that [AtomicAudit] commands route to EnqueueAuditAtomicAsync (not
    // EnqueueAuditAsync), and that non-atomic [AuditableAction] commands still use EnqueueAuditAsync.
    // Full atomicity/rollback guarantees are proved by the integration tests (AtomicAuditIntegrationTests).
    // The InMemory provider doesn't support transactions, but CreateExecutionStrategy() returns a
    // non-retrying strategy that executes the delegate once without a real transaction — sufficient
    // for routing assertions here.
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_UseEnqueueAtomicAsync_For_AtomicAudit_Commands()
    {
        // Arrange
        SetupHttpContext();
        // Make the atomic mock succeed by default (returns CompletedTask)
        _auditServiceMock
            .Setup(x => x.EnqueueAuditAtomicAsync(
                It.IsAny<AuditOutboxPayload>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var behavior = CreateBehavior<TestAtomicAuditableCommand, string>();
        var command = new TestAtomicAuditableCommand(Guid.NewGuid());
        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("atomic-ok");

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert: atomic path uses EnqueueAuditAtomicAsync, NOT the best-effort EnqueueAuditAsync
        result.Should().Be("atomic-ok");
        _auditServiceMock.Verify(
            x => x.EnqueueAuditAtomicAsync(
                It.Is<AuditOutboxPayload>(p =>
                    p.Action == "AtomicTestAction" &&
                    p.Resource == "AtomicTestResource" &&
                    p.Result == "Success"),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "[AtomicAudit] commands must route to EnqueueAuditAtomicAsync");

        _auditServiceMock.Verify(
            x => x.EnqueueAuditAsync(
                It.IsAny<AuditOutboxPayload>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "[AtomicAudit] commands must NOT use the best-effort EnqueueAuditAsync path");
    }

    [Fact]
    public async Task Should_UseEnqueueAuditAsync_For_NonAtomic_AuditableAction_Commands_RegressionGuard()
    {
        // Arrange: regression guard — non-atomic [AuditableAction] command must still use best-effort path
        SetupHttpContext();
        var behavior = CreateBehavior<TestAuditableCommand, string>();
        var command = new TestAuditableCommand(Guid.NewGuid());
        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("ok");

        // Act
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert: non-atomic command uses best-effort EnqueueAuditAsync, NOT the atomic variant
        _auditServiceMock.Verify(
            x => x.EnqueueAuditAsync(
                It.IsAny<AuditOutboxPayload>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "non-[AtomicAudit] commands must still use the best-effort EnqueueAuditAsync path");

        _auditServiceMock.Verify(
            x => x.EnqueueAuditAtomicAsync(
                It.IsAny<AuditOutboxPayload>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "non-[AtomicAudit] commands must NOT call EnqueueAuditAtomicAsync");
    }

    [Fact]
    public async Task Should_PropagateException_For_AtomicAudit_Command_WhenHandlerThrows()
    {
        // Arrange: for atomic commands, handler exceptions propagate without any audit write.
        // (No Error audit is written — the transaction rolled back, nothing was committed.)
        SetupHttpContext();
        var behavior = CreateBehavior<TestAtomicAuditableCommand, string>();
        var command = new TestAtomicAuditableCommand(Guid.NewGuid());
        RequestHandlerDelegate<string> next = (ct) =>
            throw new InvalidOperationException("atomic handler failure");

        // Act & Assert
        var act = () => behavior.Handle(command, next, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("atomic handler failure");

        // No audit row of any kind written when the atomic path fails
        _auditServiceMock.Verify(
            x => x.EnqueueAuditAtomicAsync(
                It.IsAny<AuditOutboxPayload>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "atomic path must NOT write an Error audit on failure — the tx rolled back");
        _auditServiceMock.Verify(
            x => x.EnqueueAuditAsync(
                It.IsAny<AuditOutboxPayload>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "atomic path must NOT fall back to best-effort audit on failure");
    }

    private AuditLoggingBehavior<TRequest, TResponse> CreateBehavior<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>
    {
        return new AuditLoggingBehavior<TRequest, TResponse>(
            _httpContextAccessorMock.Object,
            _auditServiceMock.Object,
            _sink,
            NullLogger<AuditLoggingBehavior<TRequest, TResponse>>.Instance,
            _dbContext,
            new DomainEventCollector());
    }
}

// Test commands for best-effort (non-atomic) path
[AuditableAction("TestAction", "TestResource", Level = 2)]
internal record TestAuditableCommand(Guid Id) : IRequest<string>;

internal record NonAuditableCommand(string Data) : IRequest<string>;

// Test command for atomic path — both attributes required
[AuditableAction("AtomicTestAction", "AtomicTestResource", Level = 2)]
[AtomicAudit]
internal record TestAtomicAuditableCommand(Guid Id) : IRequest<string>;
