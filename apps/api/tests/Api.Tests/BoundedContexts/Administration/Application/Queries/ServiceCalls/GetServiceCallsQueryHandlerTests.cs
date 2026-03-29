using Api.BoundedContexts.Administration.Application.Queries.ServiceCalls;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries.ServiceCalls;

public sealed class GetServiceCallsQueryHandlerTests
{
    private readonly Mock<IServiceCallLogRepository> _repoMock;
    private readonly GetServiceCallsQueryHandler _handler;

    public GetServiceCallsQueryHandlerTests()
    {
        _repoMock = new Mock<IServiceCallLogRepository>();
        _handler = new GetServiceCallsQueryHandler(_repoMock.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_ReturnsPaginatedResults()
    {
        // Arrange
        var entry = ServiceCallLogEntry.Create(
            serviceName: "EmbeddingService",
            httpMethod: "POST",
            requestUrl: "http://embedding-service/embed",
            statusCode: 200,
            latencyMs: 123,
            isSuccess: true,
            errorMessage: null,
            correlationId: "corr-1");

        IReadOnlyList<ServiceCallLogEntry> items = new List<ServiceCallLogEntry> { entry };
        const int totalCount = 1;

        _repoMock
            .Setup(r => r.GetPagedAsync(
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<long?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, totalCount));

        var query = new GetServiceCallsQuery(
            ServiceName: null, IsSuccess: null, CorrelationId: null,
            From: null, To: null, MinLatencyMs: null,
            Page: 1, PageSize: 50);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(totalCount);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(50);

        var dto = result.Items[0];
        dto.ServiceName.Should().Be("EmbeddingService");
        dto.HttpMethod.Should().Be("POST");
        dto.StatusCode.Should().Be(200);
        dto.LatencyMs.Should().Be(123);
        dto.IsSuccess.Should().BeTrue();
        dto.CorrelationId.Should().Be("corr-1");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_CapsPageSizeAt100()
    {
        // Arrange
        _repoMock
            .Setup(r => r.GetPagedAsync(
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<long?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ServiceCallLogEntry>)new List<ServiceCallLogEntry>(), 0));

        var query = new GetServiceCallsQuery(
            ServiceName: null, IsSuccess: null, CorrelationId: null,
            From: null, To: null, MinLatencyMs: null,
            Page: 1, PageSize: 500);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _repoMock.Verify(r => r.GetPagedAsync(
            It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<string?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<long?>(),
            It.IsAny<int>(),
            100,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
