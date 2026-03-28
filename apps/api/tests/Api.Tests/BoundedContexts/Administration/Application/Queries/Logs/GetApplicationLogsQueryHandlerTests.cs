using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.Logs;
using Api.BoundedContexts.Administration.Infrastructure.External;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries.Logs;

public sealed class GetApplicationLogsQueryHandlerTests
{
    private readonly Mock<ISeqQueryClient> _seqClientMock;
    private readonly GetApplicationLogsQueryHandler _handler;

    public GetApplicationLogsQueryHandlerTests()
    {
        _seqClientMock = new Mock<ISeqQueryClient>();
        _handler = new GetApplicationLogsQueryHandler(_seqClientMock.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_ReturnsLogsFromSeq()
    {
        // Arrange
        var items = new List<ApplicationLogDto>
        {
            new("event-1", DateTime.UtcNow, "Information", "First message", null, null, null, null),
            new("event-2", DateTime.UtcNow, "Warning",     "Second message", null, null, null, null),
        };
        const int remainingCount = 42;

        _seqClientMock
            .Setup(c => c.QueryEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ApplicationLogDto>)items, (int?)remainingCount));

        var query = new GetApplicationLogsQuery(
            Search: null, Level: null, Source: null, CorrelationId: null,
            From: null, To: null, Count: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.Should().BeEquivalentTo(items);
        result.LastId.Should().Be("event-2");
        result.RemainingCount.Should().Be(remainingCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_CapsCountAt200()
    {
        // Arrange
        _seqClientMock
            .Setup(c => c.QueryEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ApplicationLogDto>)new List<ApplicationLogDto>(), (int?)null));

        var query = new GetApplicationLogsQuery(
            Search: null, Level: null, Source: null, CorrelationId: null,
            From: null, To: null, Count: 999);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _seqClientMock.Verify(c => c.QueryEventsAsync(
            It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
            200,
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_WithSourceFilter_BuildsSeqFilter()
    {
        // Arrange
        string? capturedFilter = null;

        _seqClientMock
            .Setup(c => c.QueryEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string?, string?, DateTime?, DateTime?, int, string?, CancellationToken>(
                (filter, _, _, _, _, _, _) => capturedFilter = filter)
            .ReturnsAsync(((IReadOnlyList<ApplicationLogDto>)new List<ApplicationLogDto>(), (int?)null));

        var query = new GetApplicationLogsQuery(
            Search: null, Level: null, Source: "KnowledgeBase", CorrelationId: null,
            From: null, To: null, Count: 50);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        capturedFilter.Should().NotBeNullOrWhiteSpace();
        capturedFilter.Should().Contain("SourceContext");
        capturedFilter.Should().Contain("KnowledgeBase");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_EmptyResult_ReturnsNullLastId()
    {
        // Arrange
        _seqClientMock
            .Setup(c => c.QueryEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ApplicationLogDto>)new List<ApplicationLogDto>(), (int?)null));

        var query = new GetApplicationLogsQuery(
            Search: null, Level: null, Source: null, CorrelationId: null,
            From: null, To: null, Count: 50);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
        result.LastId.Should().BeNull();
        result.RemainingCount.Should().BeNull();
    }
}
