using Api.BoundedContexts.UserLibrary.Application.Queries.Labels;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers.Labels;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GetLabelsQueryHandlerTests
{
    private readonly Mock<IGameLabelRepository> _labelRepoMock = new();
    private readonly GetLabelsQueryHandler _handler;

    public GetLabelsQueryHandlerTests()
    {
        _handler = new GetLabelsQueryHandler(_labelRepoMock.Object);
    }

    [Fact]
    public async Task Handle_LabelsExist_ReturnsMappedDtos()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var labels = new List<GameLabel>
        {
            GameLabel.CreatePredefined("Strategy", "#0000FF"),
            GameLabel.CreateCustom(userId, "My Custom", "#FF0000")
        };

        _labelRepoMock.Setup(r => r.GetAvailableLabelsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(labels);

        var query = new GetLabelsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Strategy", result[0].Name);
        Assert.True(result[0].IsPredefined);
        Assert.Equal("My Custom", result[1].Name);
        Assert.False(result[1].IsPredefined);
    }

    [Fact]
    public async Task Handle_NoLabels_ReturnsEmptyList()
    {
        // Arrange
        _labelRepoMock.Setup(r => r.GetAvailableLabelsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameLabel>());

        var query = new GetLabelsQuery(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }
}
