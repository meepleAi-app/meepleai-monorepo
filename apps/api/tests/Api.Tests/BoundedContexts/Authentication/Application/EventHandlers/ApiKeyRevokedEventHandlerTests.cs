using Api.BoundedContexts.Authentication.Application.EventHandlers;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="ApiKeyRevokedEventHandler"/>.
/// Tests audit logging and event handling for API key revocation events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ApiKeyRevokedEventHandlerTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<ApiKeyRevokedEventHandler>> _mockLogger;
    private readonly ApiKeyRevokedEventHandler _handler;
    private bool _disposed;

    public ApiKeyRevokedEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockLogger = new Mock<ILogger<ApiKeyRevokedEventHandler>>();
        _handler = new ApiKeyRevokedEventHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidEvent_CreatesAuditLogEntry()
    {
        // Arrange
        var apiKeyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reason = "Manual revocation by user";
        var @event = new ApiKeyRevokedEvent(apiKeyId, userId, reason);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);

        var auditLog = auditLogs.First();
        auditLog.UserId.Should().Be(userId);
        auditLog.Resource.Should().Be(nameof(ApiKeyRevokedEvent));
        auditLog.Action.Should().Contain("ApiKeyRevokedEvent");
        auditLog.Result.Should().Be("Success");
        auditLog.Details.Should().Contain(apiKeyId.ToString());
        auditLog.Details.Should().Contain(reason);
    }

    [Fact]
    public async Task Handle_WithNullReason_CreatesAuditLogWithoutReason()
    {
        // Arrange
        var apiKeyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var @event = new ApiKeyRevokedEvent(apiKeyId, userId, reason: null);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);

        var auditLog = auditLogs.First();
        auditLog.UserId.Should().Be(userId);
        auditLog.Details.Should().Contain("ApiKeyRevoked");
    }

    [Fact]
    public async Task Handle_LogsEventHandling()
    {
        // Arrange
        var @event = new ApiKeyRevokedEvent(Guid.NewGuid(), Guid.NewGuid(), "test");

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Handling domain event")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_WithCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var @event = new ApiKeyRevokedEvent(Guid.NewGuid(), Guid.NewGuid(), "test");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        var act = async () => await _handler.Handle(@event, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _dbContext.Dispose();
        }
        _disposed = true;
    }
}