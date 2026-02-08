using Api.BoundedContexts.Administration.Application.Attributes;
using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Services;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
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
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class AuditLoggingBehaviorTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly Mock<AuditService> _auditServiceMock;
    private readonly ILogger<AuditLoggingBehavior<TestAuditableCommand, string>> _logger;

    public AuditLoggingBehaviorTests()
    {
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();

        var dbOptions = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"AuditBehaviorTests_{Guid.NewGuid()}")
            .Options;
        var dbContext = new MeepleAiDbContext(dbOptions, Mock.Of<IMediator>(), Mock.Of<IDomainEventCollector>());

        _auditServiceMock = new Mock<AuditService>(
            dbContext,
            Mock.Of<ILogger<AuditService>>(),
            null);
        _logger = NullLogger<AuditLoggingBehavior<TestAuditableCommand, string>>.Instance;
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
        Assert.True(nextCalled);
        Assert.Equal("result", result);
        _auditServiceMock.Verify(
            x => x.LogAsync(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Should_Log_Success_For_Auditable_Commands()
    {
        // Arrange
        SetupHttpContext();
        var behavior = CreateBehavior<TestAuditableCommand, string>();
        var command = new TestAuditableCommand(Guid.NewGuid());
        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("ok");

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        Assert.Equal("ok", result);
        _auditServiceMock.Verify(
            x => x.LogAsync(
                It.IsAny<string?>(),
                "TestAction",
                "TestResource",
                It.IsAny<string?>(),
                "Success",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Log_Error_When_Handler_Throws()
    {
        // Arrange
        SetupHttpContext();
        var behavior = CreateBehavior<TestAuditableCommand, string>();
        var command = new TestAuditableCommand(Guid.NewGuid());
        RequestHandlerDelegate<string> next = (ct) =>
            throw new InvalidOperationException("test failure");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(command, next, CancellationToken.None));

        _auditServiceMock.Verify(
            x => x.LogAsync(
                It.IsAny<string?>(),
                "TestAction",
                "TestResource",
                It.IsAny<string?>(),
                "Error",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Continue_When_AuditService_Fails()
    {
        // Arrange: AuditService throws, but behavior should NOT propagate
        SetupHttpContext();
        _auditServiceMock
            .Setup(x => x.LogAsync(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("DB connection lost"));

        var behavior = CreateBehavior<TestAuditableCommand, string>();
        var command = new TestAuditableCommand(Guid.NewGuid());
        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("still works");

        // Act - should NOT throw despite audit failure
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        Assert.Equal("still works", result);
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

        // Assert - verify the admin user ID was passed
        _auditServiceMock.Verify(
            x => x.LogAsync(
                adminId.ToString(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_Handle_Missing_HttpContext()
    {
        // Arrange - no HttpContext
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);
        var behavior = CreateBehavior<TestAuditableCommand, string>();
        var command = new TestAuditableCommand(Guid.NewGuid());
        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("ok");

        // Act - should NOT throw
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        Assert.Equal("ok", result);
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
                user,
                DateTime.UtcNow.AddHours(1),
                DateTime.UtcNow);
        }

        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);
    }

    private AuditLoggingBehavior<TRequest, TResponse> CreateBehavior<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>
    {
        return new AuditLoggingBehavior<TRequest, TResponse>(
            _httpContextAccessorMock.Object,
            _auditServiceMock.Object,
            NullLogger<AuditLoggingBehavior<TRequest, TResponse>>.Instance);
    }
}

// Test commands
[AuditableAction("TestAction", "TestResource", Level = 2)]
internal record TestAuditableCommand(Guid Id) : IRequest<string>;

internal record NonAuditableCommand(string Data) : IRequest<string>;
