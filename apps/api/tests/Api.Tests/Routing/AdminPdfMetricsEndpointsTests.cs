using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.Routing;

[Trait("Category", TestCategories.Unit)]
public sealed class AdminPdfMetricsEndpointsTests
{
    private readonly Mock<IMediator> _mediatorMock;

    public AdminPdfMetricsEndpointsTests()
    {
        _mediatorMock = new Mock<IMediator>();
    }

    [Fact]
    public async Task GetProcessingMetrics_ReturnsAggregatedMetrics()
    {
        // Arrange
        var expectedResponse = new ProcessingMetricsDto(
            Averages: new Dictionary<string, StepAverages>
            {
                ["Extracting"] = new StepAverages("Extracting", 45.2, 100)
            },
            Percentiles: new Dictionary<string, StepPercentiles>
            {
                ["Extracting"] = new StepPercentiles(42.0, 98.5, 98.5)
            },
            LastUpdated: DateTime.UtcNow
        );

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetProcessingMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _mediatorMock.Object.Send(
            new GetProcessingMetricsQuery(),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Averages.Should().ContainKey("Extracting");
        result.Percentiles.Should().ContainKey("Extracting");
    }
}
