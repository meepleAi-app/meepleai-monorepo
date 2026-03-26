using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class GetDiceRollHistoryQueryHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<IDiceRollRepository> _diceRollRepoMock = new();
    private readonly GetDiceRollHistoryQueryHandler _handler;

    public GetDiceRollHistoryQueryHandlerTests()
    {
        _handler = new GetDiceRollHistoryQueryHandler(
            _sessionRepoMock.Object, _diceRollRepoMock.Object);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _sessionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var query = new GetDiceRollHistoryQuery(Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NoDiceRolls_ReturnsEmptyCollection()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _diceRollRepoMock.Setup(r => r.GetRecentBySessionIdAsync(session.Id, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DiceRoll>());

        var query = new GetDiceRollHistoryQuery(session.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }
}
