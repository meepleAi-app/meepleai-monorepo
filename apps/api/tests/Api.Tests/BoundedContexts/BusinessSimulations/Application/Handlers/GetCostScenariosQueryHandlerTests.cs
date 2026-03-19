using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Handlers;

/// <summary>
/// Unit tests for GetCostScenariosQueryHandler (Issue #3725)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class GetCostScenariosQueryHandlerTests
{
    private readonly Mock<ICostScenarioRepository> _repositoryMock;
    private readonly GetCostScenariosQueryHandler _handler;

    public GetCostScenariosQueryHandlerTests()
    {
        _repositoryMock = new Mock<ICostScenarioRepository>();
        _handler = new GetCostScenariosQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingScenarios_ShouldReturnPaginatedResults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var scenarios = new List<CostScenario>
        {
            CostScenario.Create("Scenario 1", "Fast", "model-1", 100, 10, 500, 0m, 0m, 0m, null, userId),
            CostScenario.Create("Scenario 2", "Balanced", "model-2", 1000, 100, 1000, 0.001m, 100m, 3000m, null, userId),
        };

        _repositoryMock
            .Setup(r => r.GetByUserAsync(userId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((scenarios.AsReadOnly() as IReadOnlyList<CostScenario>, 2));

        var query = new GetCostScenariosQuery(userId, 1, 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.Items[0].Name.Should().Be("Scenario 1");
        result.Items[1].Name.Should().Be("Scenario 2");
    }

    [Fact]
    public async Task Handle_WithNoScenarios_ShouldReturnEmptyResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByUserAsync(userId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<CostScenario>().AsReadOnly() as IReadOnlyList<CostScenario>, 0));

        var query = new GetCostScenariosQuery(userId, 1, 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => _handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
