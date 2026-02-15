using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;
using Api.Infrastructure;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.UserNotifications.Infrastructure;

[Trait("Category", TestCategories.Unit)]
public sealed class EmailProcessorJobTests
{
    private readonly Mock<IEmailQueueRepository> _emailQueueRepo;
    private readonly Mock<IEmailService> _emailService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<EmailProcessorJob>> _logger;
    private readonly EmailProcessorJob _sut;
    private readonly Mock<IJobExecutionContext> _jobContext;

    public EmailProcessorJobTests()
    {
        _emailQueueRepo = new Mock<IEmailQueueRepository>();
        _emailService = new Mock<IEmailService>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _logger = new Mock<ILogger<EmailProcessorJob>>();

        _sut = new EmailProcessorJob(
            _emailQueueRepo.Object,
            _emailService.Object,
            _dbContext,
            _logger.Object);

        _jobContext = new Mock<IJobExecutionContext>();
        _jobContext.Setup(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        _jobContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
    }

    [Fact]
    public async Task Execute_NoPendingEmails_CompletesSuccessfully()
    {
        _emailQueueRepo.Setup(r => r.GetPendingAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailQueueItem>());

        await _sut.Execute(_jobContext.Object);

        _emailService.Verify(s => s.SendRawEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_PendingEmail_SendsAndMarksAsSent()
    {
        var email = EmailQueueItem.Create(Guid.NewGuid(), "user@test.com", "Subject", "<html/>");
        _emailQueueRepo.Setup(r => r.GetPendingAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailQueueItem> { email });

        await _sut.Execute(_jobContext.Object);

        _emailService.Verify(s => s.SendRawEmailAsync(
            "user@test.com", "Subject", "<html/>", It.IsAny<CancellationToken>()),
            Times.Once);
        _emailQueueRepo.Verify(r => r.UpdateAsync(It.IsAny<EmailQueueItem>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)); // Processing + Sent
    }

    [Fact]
    public async Task Execute_SendFailure_MarksAsFailedWithRetry()
    {
        var email = EmailQueueItem.Create(Guid.NewGuid(), "user@test.com", "Subject", "<html/>");
        _emailQueueRepo.Setup(r => r.GetPendingAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailQueueItem> { email });
        _emailService.Setup(s => s.SendRawEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP timeout"));

        await _sut.Execute(_jobContext.Object);

        // Should update 3 times: Processing, then Failed status update
        _emailQueueRepo.Verify(r => r.UpdateAsync(It.IsAny<EmailQueueItem>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)); // Processing + Failed
    }

    [Fact]
    public async Task Execute_MultiplePendingEmails_ProcessesAll()
    {
        var emails = Enumerable.Range(0, 3).Select(_ =>
            EmailQueueItem.Create(Guid.NewGuid(), $"user{_}@test.com", "Subject", "<html/>"))
            .ToList();

        _emailQueueRepo.Setup(r => r.GetPendingAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emails);

        await _sut.Execute(_jobContext.Object);

        _emailService.Verify(s => s.SendRawEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Execute_PartialFailure_ContinuesProcessingRemaining()
    {
        var email1 = EmailQueueItem.Create(Guid.NewGuid(), "fail@test.com", "Subject", "<html/>");
        var email2 = EmailQueueItem.Create(Guid.NewGuid(), "success@test.com", "Subject", "<html/>");

        _emailQueueRepo.Setup(r => r.GetPendingAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailQueueItem> { email1, email2 });

        _emailService.Setup(s => s.SendRawEmailAsync("fail@test.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Failed"));
        _emailService.Setup(s => s.SendRawEmailAsync("success@test.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.Execute(_jobContext.Object);

        // Both emails were attempted
        _emailService.Verify(s => s.SendRawEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
