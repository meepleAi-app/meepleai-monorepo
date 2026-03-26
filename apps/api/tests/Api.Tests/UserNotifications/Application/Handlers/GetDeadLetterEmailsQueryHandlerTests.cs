using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.UserNotifications.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public sealed class GetDeadLetterEmailsQueryHandlerTests
{
    private readonly Mock<IEmailQueueRepository> _emailQueueRepo;
    private readonly GetDeadLetterEmailsQueryHandler _sut;

    public GetDeadLetterEmailsQueryHandlerTests()
    {
        _emailQueueRepo = new Mock<IEmailQueueRepository>();
        _sut = new GetDeadLetterEmailsQueryHandler(_emailQueueRepo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsPaginatedDeadLetterEmails()
    {
        // Arrange
        var deadLetterItem = EmailQueueItem.Reconstitute(
            id: Guid.NewGuid(),
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

        _emailQueueRepo.Setup(r => r.GetDeadLetterAsync(0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailQueueItem> { deadLetterItem });
        _emailQueueRepo.Setup(r => r.GetDeadLetterCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.Handle(new GetDeadLetterEmailsQuery(), CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Items[0].Status.Should().Be("dead_letter");
        result.Items[0].ErrorMessage.Should().Be("SMTP timeout");
    }

    [Fact]
    public async Task Handle_ClampsPageSize()
    {
        // Arrange
        _emailQueueRepo.Setup(r => r.GetDeadLetterAsync(0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailQueueItem>());
        _emailQueueRepo.Setup(r => r.GetDeadLetterCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act: request 100 items but handler clamps to 50
        var result = await _sut.Handle(new GetDeadLetterEmailsQuery(Skip: 0, Take: 100), CancellationToken.None);

        // Assert
        result.Take.Should().Be(50);
        _emailQueueRepo.Verify(r => r.GetDeadLetterAsync(0, 50, It.IsAny<CancellationToken>()), Times.Once);
    }
}
