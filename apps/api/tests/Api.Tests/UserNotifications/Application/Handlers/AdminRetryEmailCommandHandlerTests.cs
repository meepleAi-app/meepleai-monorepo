using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Handlers;
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
public sealed class AdminRetryEmailCommandHandlerTests
{
    private readonly Mock<IEmailQueueRepository> _emailQueueRepo;
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<ILogger<AdminRetryEmailCommandHandler>> _logger;
    private readonly AdminRetryEmailCommandHandler _sut;

    public AdminRetryEmailCommandHandlerTests()
    {
        _emailQueueRepo = new Mock<IEmailQueueRepository>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _logger = new Mock<ILogger<AdminRetryEmailCommandHandler>>();
        _sut = new AdminRetryEmailCommandHandler(_emailQueueRepo.Object, _unitOfWork.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_DeadLetterEmail_ResetsStatusToPending()
    {
        // Arrange
        var emailId = Guid.NewGuid();
        var deadLetterItem = EmailQueueItem.Reconstitute(
            id: emailId,
            userId: Guid.NewGuid(),
            to: "user@test.com",
            subject: "Test Subject",
            htmlBody: "<html>test</html>",
            status: EmailQueueStatus.DeadLetter,
            retryCount: 3,
            maxRetries: 3,
            nextRetryAt: null,
            errorMessage: "SMTP timeout",
            createdAt: DateTime.UtcNow.AddHours(-2),
            processedAt: null,
            failedAt: DateTime.UtcNow.AddHours(-1));

        _emailQueueRepo.Setup(r => r.GetByIdAsync(emailId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deadLetterItem);

        // Act
        var result = await _sut.Handle(new AdminRetryEmailCommand(emailId), CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _emailQueueRepo.Verify(r => r.UpdateAsync(It.Is<EmailQueueItem>(e =>
            e.Status.IsPending), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmailNotFound_ReturnsFalse()
    {
        // Arrange
        var emailId = Guid.NewGuid();
        _emailQueueRepo.Setup(r => r.GetByIdAsync(emailId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailQueueItem?)null);

        // Act
        var result = await _sut.Handle(new AdminRetryEmailCommand(emailId), CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmailInSentStatus_ReturnsFalse()
    {
        // Arrange
        var emailId = Guid.NewGuid();
        var sentItem = EmailQueueItem.Reconstitute(
            id: emailId,
            userId: Guid.NewGuid(),
            to: "user@test.com",
            subject: "Test Subject",
            htmlBody: "<html>test</html>",
            status: EmailQueueStatus.Sent,
            retryCount: 0,
            maxRetries: 3,
            nextRetryAt: null,
            errorMessage: null,
            createdAt: DateTime.UtcNow.AddHours(-2),
            processedAt: DateTime.UtcNow.AddHours(-1),
            failedAt: null);

        _emailQueueRepo.Setup(r => r.GetByIdAsync(emailId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sentItem);

        // Act
        var result = await _sut.Handle(new AdminRetryEmailCommand(emailId), CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
