using Api.BoundedContexts.UserLibrary.Application.Queries.Labels;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers.Labels;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GetGameLabelsQueryHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _libraryRepoMock = new();
    private readonly Mock<IGameLabelRepository> _labelRepoMock = new();
    private readonly GetGameLabelsQueryHandler _handler;

    public GetGameLabelsQueryHandlerTests()
    {
        _handler = new GetGameLabelsQueryHandler(
            _libraryRepoMock.Object, _labelRepoMock.Object);
    }

    [Fact]
    public async Task Handle_GameNotInLibrary_ThrowsNotFoundException()
    {
        // Arrange
        _libraryRepoMock.Setup(r => r.GetByUserAndGameAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        var query = new GetGameLabelsQuery(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_GameWithLabels_ReturnsMappedDtos()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        _libraryRepoMock.Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var labels = new List<GameLabel>
        {
            GameLabel.CreatePredefined("Co-op", "#00FF00")
        };

        _labelRepoMock.Setup(r => r.GetLabelsForEntryAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(labels);

        var query = new GetGameLabelsQuery(userId, gameId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("Co-op", result[0].Name);
    }
}
