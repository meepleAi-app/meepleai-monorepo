using Api.BoundedContexts.UserNotifications.Application.Handlers;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.UserNotifications.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public sealed class GetEmailHistoryQueryHandlerTests
{
    private readonly Mock<IEmailQueueRepository> _emailQueueRepo;
    private readonly Mock<ILogger<GetEmailHistoryQueryHandler>> _logger;
    private readonly GetEmailHistoryQueryHandler _sut;

    private readonly Guid _userId = Guid.NewGuid();

    public GetEmailHistoryQueryHandlerTests()
    {
        _emailQueueRepo = new Mock<IEmailQueueRepository>();
        _logger = new Mock<ILogger<GetEmailHistoryQueryHandler>>();
        _sut = new GetEmailHistoryQueryHandler(_emailQueueRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsPagedResults()
    {
        var items = new List<EmailQueueItem>
        {
            EmailQueueItem.Reconstitute(Guid.NewGuid(), _userId, "a@test.com", "Sub1", "<html/>",
                EmailQueueStatus.Sent, 0, 3, null, null, DateTime.UtcNow, DateTime.UtcNow, null),
            EmailQueueItem.Reconstitute(Guid.NewGuid(), _userId, "b@test.com", "Sub2", "<html/>",
                EmailQueueStatus.Pending, 0, 3, null, null, DateTime.UtcNow, null, null),
        };

        _emailQueueRepo.Setup(r => r.GetByUserIdAsync(_userId, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        _emailQueueRepo.Setup(r => r.GetCountByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var query = new GetEmailHistoryQuery(_userId, 0, 20);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Skip.Should().Be(0);
        result.Take.Should().Be(20);
    }

    [Fact]
    public async Task Handle_ClampsPageSize()
    {
        _emailQueueRepo.Setup(r => r.GetByUserIdAsync(_userId, 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailQueueItem>());
        _emailQueueRepo.Setup(r => r.GetCountByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Request more than max page size (50)
        var query = new GetEmailHistoryQuery(_userId, 0, 100);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.Take.Should().Be(50); // Clamped to MaxPageSize
        _emailQueueRepo.Verify(r => r.GetByUserIdAsync(_userId, 0, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NegativeSkip_ClampedToZero()
    {
        _emailQueueRepo.Setup(r => r.GetByUserIdAsync(_userId, 0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailQueueItem>());
        _emailQueueRepo.Setup(r => r.GetCountByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var query = new GetEmailHistoryQuery(_userId, -5, 10);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.Skip.Should().Be(0);
    }

    [Fact]
    public async Task Handle_EmptyResult_ReturnsEmptyList()
    {
        _emailQueueRepo.Setup(r => r.GetByUserIdAsync(_userId, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailQueueItem>());
        _emailQueueRepo.Setup(r => r.GetCountByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var query = new GetEmailHistoryQuery(_userId);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MapsDtoFieldsCorrectly()
    {
        var emailId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddHours(-1);
        var processedAt = DateTime.UtcNow;
        var item = EmailQueueItem.Reconstitute(
            emailId, _userId, "user@test.com", "Test Subject", "<html/>",
            EmailQueueStatus.Sent, 1, 3, null, null, createdAt, processedAt, null);

        _emailQueueRepo.Setup(r => r.GetByUserIdAsync(_userId, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailQueueItem> { item });
        _emailQueueRepo.Setup(r => r.GetCountByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var query = new GetEmailHistoryQuery(_userId);
        var result = await _sut.Handle(query, CancellationToken.None);

        var dto = result.Items.Single();
        dto.Id.Should().Be(emailId);
        dto.UserId.Should().Be(_userId);
        dto.To.Should().Be("user@test.com");
        dto.Subject.Should().Be("Test Subject");
        dto.Status.Should().Be("sent");
        dto.RetryCount.Should().Be(1);
        dto.MaxRetries.Should().Be(3);
        dto.CreatedAt.Should().Be(createdAt);
        dto.ProcessedAt.Should().Be(processedAt);
    }
}
