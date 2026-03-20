using Api.BoundedContexts.Authentication.Application.EventHandlers;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="EmailChangedEventHandler"/>.
/// Tests audit logging for email change events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class EmailChangedEventHandlerTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<EmailChangedEventHandler>> _mockLogger;
    private readonly EmailChangedEventHandler _handler;
    private bool _disposed;

    public EmailChangedEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockLogger = new Mock<ILogger<EmailChangedEventHandler>>();
        _handler = new EmailChangedEventHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidEvent_CreatesAuditLogEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldEmail = new Email("old@example.com");
        var newEmail = new Email("new@example.com");
        var @event = new EmailChangedEvent(userId, oldEmail, newEmail);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);

        var auditLog = auditLogs.First();
        auditLog.UserId.Should().Be(userId);
        auditLog.Resource.Should().Be(nameof(EmailChangedEvent));
        auditLog.Action.Should().Contain("EmailChangedEvent");
        auditLog.Result.Should().Be("Success");
        auditLog.Details.Should().Contain("old@example.com");
        auditLog.Details.Should().Contain("new@example.com");
    }

    [Fact]
    public async Task Handle_CapturesOldAndNewEmailInMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldEmail = new Email("original@test.com");
        var newEmail = new Email("updated@test.com");
        var @event = new EmailChangedEvent(userId, oldEmail, newEmail);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain("original@test.com");
        auditLog.Details.Should().Contain("updated@test.com");
        auditLog.Details.Should().Contain("EmailChanged");
    }

    [Fact]
    public async Task Handle_LogsEventHandling()
    {
        // Arrange
        var @event = new EmailChangedEvent(
            Guid.NewGuid(),
            new Email("old@test.com"),
            new Email("new@test.com"));

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
    public async Task Handle_WithSameEmailUpperCaseLowerCase_RecordsChange()
    {
        // Arrange - Email value object normalizes to lowercase
        var userId = Guid.NewGuid();
        var oldEmail = new Email("USER@EXAMPLE.COM");  // Will be normalized
        var newEmail = new Email("user@newdomain.com");
        var @event = new EmailChangedEvent(userId, oldEmail, newEmail);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        // Email is normalized to lowercase by value object
        auditLog.Details.Should().Contain("user@example.com");
        auditLog.Details.Should().Contain("user@newdomain.com");
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