using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;
using Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;
using Api.Infrastructure.Entities.UserNotifications;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.UserNotifications.Infrastructure;

/// <summary>
/// Integration tests for the email queue pipeline.
/// Uses real InMemory DB + real repository + mock IEmailService.
/// Issue #4431: Email queue integration test covering full lifecycle.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class EmailQueueIntegrationTests : IDisposable
{
    private readonly string _dbName;
    private readonly Mock<IEmailService> _emailService;
    private readonly Mock<ILogger<EmailProcessorJob>> _logger;
    private readonly Mock<IJobExecutionContext> _jobContext;

    public EmailQueueIntegrationTests()
    {
        _dbName = $"email_queue_integration_{Guid.NewGuid()}";
        _emailService = new Mock<IEmailService>();
        _logger = new Mock<ILogger<EmailProcessorJob>>();
        _jobContext = new Mock<IJobExecutionContext>();
        _jobContext.Setup(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        _jobContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
    }

    public void Dispose()
    {
        // InMemory databases with unique names are GC'd automatically
    }

    [Fact]
    public async Task HappyPath_EnqueuePendingEmail_ProcessorSendsAndMarksSent()
    {
        // Arrange: seed a pending email directly in the DB
        var emailId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using (var seedCtx = CreateDbContext())
        {
            seedCtx.Set<EmailQueueEntity>().Add(new EmailQueueEntity
            {
                Id = emailId,
                UserId = userId,
                To = "user@test.com",
                Subject = "Your PDF is Ready: test.pdf - MeepleAI",
                HtmlBody = "<html><body>Your PDF is ready</body></html>",
                Status = "pending",
                RetryCount = 0,
                MaxRetries = 3,
                CreatedAt = DateTime.UtcNow
            });
            await seedCtx.SaveChangesAsync();
        }

        // Act: run the processor job with a fresh context
        using (var jobCtx = CreateDbContext())
        {
            var repo = CreateRepository(jobCtx);
            var job = new EmailProcessorJob(repo, _emailService.Object, jobCtx, _logger.Object);
            await job.Execute(_jobContext.Object);
        }

        // Assert: verify the email was sent and status is now "sent"
        using (var verifyCtx = CreateDbContext())
        {
            var entity = await verifyCtx.Set<EmailQueueEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == emailId);

            entity.Should().NotBeNull();
            entity!.Status.Should().Be("sent");
            entity.ProcessedAt.Should().NotBeNull();
            entity.ErrorMessage.Should().BeNull();
            entity.RetryCount.Should().Be(0);
        }

        _emailService.Verify(s => s.SendRawEmailAsync(
            "user@test.com",
            "Your PDF is Ready: test.pdf - MeepleAI",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetryPath_FailedEmailWithPastRetryTime_ReprocessedAndSent()
    {
        // Arrange: seed a failed email with NextRetryAt in the past (eligible for retry)
        var emailId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using (var seedCtx = CreateDbContext())
        {
            seedCtx.Set<EmailQueueEntity>().Add(new EmailQueueEntity
            {
                Id = emailId,
                UserId = userId,
                To = "user@test.com",
                Subject = "PDF Processing Failed: report.pdf - MeepleAI",
                HtmlBody = "<html><body>Failed</body></html>",
                Status = "failed",
                RetryCount = 1,
                MaxRetries = 3,
                NextRetryAt = DateTime.UtcNow.AddMinutes(-5), // Past → eligible for pickup
                ErrorMessage = "SMTP timeout",
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                FailedAt = DateTime.UtcNow.AddMinutes(-10)
            });
            await seedCtx.SaveChangesAsync();
        }

        // Act: run the processor job — should pick up the failed email and retry
        _emailService.Setup(s => s.SendRawEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using (var jobCtx = CreateDbContext())
        {
            var repo = CreateRepository(jobCtx);
            var job = new EmailProcessorJob(repo, _emailService.Object, jobCtx, _logger.Object);
            await job.Execute(_jobContext.Object);
        }

        // Assert: email should now be sent
        using (var verifyCtx = CreateDbContext())
        {
            var entity = await verifyCtx.Set<EmailQueueEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == emailId);

            entity.Should().NotBeNull();
            entity!.Status.Should().Be("sent");
            entity.ProcessedAt.Should().NotBeNull();
        }

        _emailService.Verify(s => s.SendRawEmailAsync(
            "user@test.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeadLetterPath_ThirdFailure_MovesToDeadLetterAndNotPickedUp()
    {
        // Arrange: seed an email at retryCount=2, failed status with NextRetryAt in the past.
        // Next failure (retryCount becomes 3 >= maxRetries=3) → dead letter.
        var emailId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using (var seedCtx = CreateDbContext())
        {
            seedCtx.Set<EmailQueueEntity>().Add(new EmailQueueEntity
            {
                Id = emailId,
                UserId = userId,
                To = "user@test.com",
                Subject = "Your PDF is Ready: doc.pdf - MeepleAI",
                HtmlBody = "<html><body>Ready</body></html>",
                Status = "failed",
                RetryCount = 2,
                MaxRetries = 3,
                NextRetryAt = DateTime.UtcNow.AddMinutes(-1), // Past → eligible for pickup
                ErrorMessage = "Connection refused",
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                FailedAt = DateTime.UtcNow.AddMinutes(-30)
            });
            await seedCtx.SaveChangesAsync();
        }

        // Make send fail again
        _emailService.Setup(s => s.SendRawEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection refused"));

        // Act: run the processor — should fail and move to dead letter
        using (var jobCtx = CreateDbContext())
        {
            var repo = CreateRepository(jobCtx);
            var job = new EmailProcessorJob(repo, _emailService.Object, jobCtx, _logger.Object);
            await job.Execute(_jobContext.Object);
        }

        // Assert: email should be dead_letter with retryCount=3
        using (var verifyCtx = CreateDbContext())
        {
            var entity = await verifyCtx.Set<EmailQueueEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == emailId);

            entity.Should().NotBeNull();
            entity!.Status.Should().Be("dead_letter");
            entity.RetryCount.Should().Be(3);
            entity.NextRetryAt.Should().BeNull();
            entity.ErrorMessage.Should().Contain("Connection refused");
        }

        // Act 2: run the processor again — dead letter email should NOT be picked up
        _emailService.Invocations.Clear();

        using (var jobCtx2 = CreateDbContext())
        {
            var repo2 = CreateRepository(jobCtx2);
            var job2 = new EmailProcessorJob(repo2, _emailService.Object, jobCtx2, _logger.Object);
            await job2.Execute(_jobContext.Object);
        }

        // Assert: no emails were sent on second run
        _emailService.Verify(s => s.SendRawEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private Api.Infrastructure.MeepleAiDbContext CreateDbContext()
    {
        return TestDbContextFactory.CreateInMemoryDbContext(_dbName);
    }

    private static EmailQueueRepository CreateRepository(Api.Infrastructure.MeepleAiDbContext dbContext)
    {
        var eventCollector = TestDbContextFactory.CreateMockEventCollector();
        return new EmailQueueRepository(dbContext, eventCollector.Object);
    }
}
