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

        var query = new GetDiceRollHistoryQuery(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NoDiceRolls_ReturnsEmptyCollection()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var session = Session.Create(ownerId, Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _diceRollRepoMock.Setup(r => r.GetRecentBySessionIdAsync(session.Id, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DiceRoll>());

        var query = new GetDiceRollHistoryQuery(session.Id, ownerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_StrangerWhoIsNeitherOwnerNorParticipant_ThrowsForbiddenAndReadsNothing()
    {
        // #4883: the history of a session was answered to any authenticated user.
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var query = new GetDiceRollHistoryQuery(session.Id, Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(query, CancellationToken.None));
        _diceRollRepoMock.Verify(
            r => r.GetRecentBySessionIdAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
