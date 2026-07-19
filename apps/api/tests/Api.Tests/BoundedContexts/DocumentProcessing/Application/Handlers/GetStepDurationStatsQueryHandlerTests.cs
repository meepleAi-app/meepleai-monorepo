using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Issue #3176 - GetStepDurationStatsQuery is a CQRS pass-through so the RAG pipeline health
/// endpoint can obtain the raw per-step StepDurationStats via IMediator instead of injecting
/// IProcessingMetricsService directly.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class GetStepDurationStatsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllStepStatisticsFromService()
    {
        var expected = new Dictionary<string, StepDurationStats>();
        var mockService = new Mock<IProcessingMetricsService>();
        mockService
            .Setup(s => s.GetAllStepStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetStepDurationStatsQueryHandler(mockService.Object);

        var result = await handler.Handle(new GetStepDurationStatsQuery(), CancellationToken.None);

        result.Should().BeSameAs(expected);
        mockService.Verify(s => s.GetAllStepStatisticsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
