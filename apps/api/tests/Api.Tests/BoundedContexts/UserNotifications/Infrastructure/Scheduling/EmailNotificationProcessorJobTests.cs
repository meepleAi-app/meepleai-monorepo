using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Email;
using Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;
using Api.BoundedContexts.UserNotifications.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Scheduling;

/// <summary>
/// Unit tests for <see cref="EmailNotificationProcessorJob"/> (issue #3026).
/// Verifies the job drains the Email channel of notification_queue_items, renders via the generic
/// builder, sends via IEmailService.SendRawEmailAsync with the resolved recipient + friendly subject,
/// and drives the queue-item state machine (Sent / Failed-retry / dead-letter on missing address).
/// Mirrors SlackNotificationProcessorJobTests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
#pragma warning disable S3881
public class EmailNotificationProcessorJobTests : IDisposable
#pragma warning restore S3881
{
    private readonly Mock<INotificationQueueRepository> _queueRepoMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<EmailNotificationProcessorJob>> _loggerMock = new();
    private readonly Mock<IJobExecutionContext> _jobContextMock = new();
    private bool _disposed;

    public EmailNotificationProcessorJobTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _jobContextMock.Setup(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        _jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _dbContext.Dispose();
            _disposed = true;
        }
    }

    private EmailNotificationProcessorJob CreateSut()
    {
        var templateService = new EmailTemplateService(new ConfigurationBuilder().Build());
        var genericBuilder = new GenericEmailBuilder(templateService);
        var builderFactory = new EmailMessageBuilderFactory(
            Enumerable.Empty<IEmailMessageBuilder>(), genericBuilder);

        return new EmailNotificationProcessorJob(
            _queueRepoMock.Object,
            builderFactory,
            _emailServiceMock.Object,
            _dbContext,
            _loggerMock.Object);
    }

    private async Task SeedUserAsync(Guid userId, string email, string? displayName)
    {
        _dbContext.Set<UserEntity>().Add(new UserEntity { Id = userId, Email = email, DisplayName = displayName });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static NotificationQueueItem CreateEmailItem(
        Guid recipientUserId,
        NotificationType? type = null,
        INotificationPayload? payload = null,
        string? deepLinkPath = "/share-requests/1")
    {
        return NotificationQueueItem.Create(
            channelType: NotificationChannelType.Email,
            recipientUserId: recipientUserId,
            notificationType: type ?? NotificationType.ShareRequestCreated,
            payload: payload ?? new GenericPayload("Share request", "You have a pending share request."),
            deepLinkPath: deepLinkPath);
    }

    private void SetupPending(params NotificationQueueItem[] items)
    {
        _queueRepoMock
            .Setup(r => r.GetPendingByChannelAsync(NotificationChannelType.Email, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items.ToList());
    }

    [Fact]
    public async Task Execute_PendingEmailItem_ResolvesRecipient_SendsRawEmail_MarksSent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId, "alice@example.com", "Alice");
        var item = CreateEmailItem(
            userId,
            NotificationType.ShareRequestCreated,
            new GenericPayload("t", "Please review the pending share request."),
            "/share-requests/9");
        SetupPending(item);

        var sut = CreateSut();

        // Act
        await sut.Execute(_jobContextMock.Object);

        // Assert — fetched the EMAIL channel specifically
        _queueRepoMock.Verify(
            r => r.GetPendingByChannelAsync(NotificationChannelType.Email, 10, It.IsAny<CancellationToken>()),
            Times.Once);

        // Sent via SendRawEmailAsync with resolved To + friendly subject + rendered body
        _emailServiceMock.Verify(
            s => s.SendRawEmailAsync(
                "alice@example.com",
                "Nuova Share Request",
                It.Is<string>(b =>
                    b.Contains("Please review the pending share request.") && b.Contains("Open in MeepleAI")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        item.Status.IsSent.Should().BeTrue();
        // processing + sent => two persistence transitions
        _queueRepoMock.Verify(
            r => r.UpdateAsync(item, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Execute_SendThrows_MarksFailedForRetry_NotDeadLetter()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId, "bob@example.com", "Bob");
        var item = CreateEmailItem(userId);
        SetupPending(item);

        _emailServiceMock
            .Setup(s => s.SendRawEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp relay unavailable"));

        var sut = CreateSut();

        // Act
        await sut.Execute(_jobContextMock.Object);

        // Assert — first failure schedules a retry, does not dead-letter (MaxRetries = 3)
        item.Status.IsFailed.Should().BeTrue();
        item.RetryCount.Should().Be(1);
        _emailServiceMock.Verify(
            s => s.SendRawEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        // processing + failed => two persistence transitions
        _queueRepoMock.Verify(
            r => r.UpdateAsync(item, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Execute_RecipientHasNoEmailOnFile_DeadLetters_WithoutSending()
    {
        // Arrange — recipient user is NOT seeded, so no address can be resolved
        var userId = Guid.NewGuid();
        var item = CreateEmailItem(userId);
        SetupPending(item);

        var sut = CreateSut();

        // Act
        await sut.Execute(_jobContextMock.Object);

        // Assert — never attempts SMTP, dead-letters with a clear reason (does not crash the batch)
        _emailServiceMock.Verify(
            s => s.SendRawEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        item.Status.IsDeadLetter.Should().BeTrue();
        item.LastError.Should().Contain("No email address");
    }

    [Fact]
    public async Task Execute_NoPendingItems_DoesNothing()
    {
        // Arrange
        SetupPending();
        var sut = CreateSut();

        // Act
        await sut.Execute(_jobContextMock.Object);

        // Assert
        _emailServiceMock.Verify(
            s => s.SendRawEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _queueRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<NotificationQueueItem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
