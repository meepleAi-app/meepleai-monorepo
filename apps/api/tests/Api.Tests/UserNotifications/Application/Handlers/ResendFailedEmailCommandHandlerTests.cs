using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.UserNotifications.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public sealed class ResendFailedEmailCommandHandlerTests
{
    private readonly Mock<IEmailQueueRepository> _emailQueueRepo;
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<ILogger<ResendFailedEmailCommandHandler>> _logger;
    private readonly ResendFailedEmailCommandHandler _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _emailId = Guid.NewGuid();

    public ResendFailedEmailCommandHandlerTests()
    {
        _emailQueueRepo = new Mock<IEmailQueueRepository>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _logger = new Mock<ILogger<ResendFailedEmailCommandHandler>>();

        _sut = new ResendFailedEmailCommandHandler(
            _emailQueueRepo.Object,
            _unitOfWork.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_EmailNotFound_ReturnsFalse()
    {
        _emailQueueRepo.Setup(r => r.GetByIdAsync(_emailId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailQueueItem?)null);

        var command = new ResendFailedEmailCommand(_emailId, _userId);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        var otherUserId = Guid.NewGuid();
        var emailItem = CreateDeadLetterEmail(_userId);

        _emailQueueRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emailItem);

        var command = new ResendFailedEmailCommand(emailItem.Id, otherUserId);
        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_DeadLetterEmail_ResetsToPendingAndSaves()
    {
        var emailItem = CreateDeadLetterEmail(_userId);

        _emailQueueRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emailItem);

        var command = new ResendFailedEmailCommand(emailItem.Id, _userId);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.Should().BeTrue();
        _emailQueueRepo.Verify(r => r.UpdateAsync(It.IsAny<EmailQueueItem>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static EmailQueueItem CreateDeadLetterEmail(Guid userId)
    {
        return EmailQueueItem.Reconstitute(
            id: Guid.NewGuid(),
            userId: userId,
            to: "user@test.com",
            subject: "Test",
            htmlBody: "<html/>",
            status: EmailQueueStatus.DeadLetter,
            retryCount: 3,
            maxRetries: 3,
            nextRetryAt: null,
            errorMessage: "Max retries exceeded",
            createdAt: DateTime.UtcNow.AddHours(-1),
            processedAt: null,
            failedAt: DateTime.UtcNow.AddMinutes(-30));
    }
}
