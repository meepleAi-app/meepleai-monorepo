using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Issue #3176 - GetServiceHealthStatusesQuery is a CQRS pass-through so the RAG pipeline
/// health endpoint can obtain the raw ServiceHealthStatus collection via IMediator instead of
/// injecting IInfrastructureHealthService directly.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class GetServiceHealthStatusesQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllServiceHealthStatusesFromService()
    {
        IReadOnlyCollection<ServiceHealthStatus> expected = new List<ServiceHealthStatus>();
        var mockService = new Mock<IInfrastructureHealthService>();
        mockService
            .Setup(s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetServiceHealthStatusesQueryHandler(mockService.Object);

        var result = await handler.Handle(new GetServiceHealthStatusesQuery(), CancellationToken.None);

        result.Should().BeSameAs(expected);
        mockService.Verify(s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
