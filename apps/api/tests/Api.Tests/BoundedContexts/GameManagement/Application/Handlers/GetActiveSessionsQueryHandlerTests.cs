using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for GetActiveSessionsQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetActiveSessionsQueryHandlerTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly GetActiveSessionsQueryHandler _handler;

    /// <summary>
    /// #4080: il proprietario di cui la query chiede le sessioni.
    ///
    /// <para>⚠️ Questi test montano un <c>Mock&lt;IGameSessionRepository&gt;</c>, quindi qui si
    /// puo' verificare che l'handler <b>propaghi</b> questo id al repository — mai che il
    /// repository filtri davvero: un mock restituisce cio' che gli si dice. La guardia contro
    /// il difetto di #4080 e' in <c>Integration/GameManagement/ActiveSessionsOwnerScopeTests</c>,
    /// che gira contro il database.</para>
    /// </summary>
    private static readonly Guid Owner = Guid.NewGuid();

    public GetActiveSessionsQueryHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _handler = new GetActiveSessionsQueryHandler(_sessionRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithActiveSessions_ReturnsSessionList()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var sessions = new List<GameSession>
        {
            CreateActiveSession(gameId),
            CreateActiveSession(gameId)
        };
        var query = new GetActiveSessionsQuery(Owner);

        _sessionRepositoryMock
            .Setup(r => r.FindActiveAsync(Owner, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        _sessionRepositoryMock
            .Setup(r => r.CountActiveByUserIdAsync(Owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Sessions.Count.Should().Be(2);
        result.Total.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithNoActiveSessions_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetActiveSessionsQuery(Owner);

        _sessionRepositoryMock
            .Setup(r => r.FindActiveAsync(Owner, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());
        _sessionRepositoryMock
            .Setup(r => r.CountActiveByUserIdAsync(Owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Sessions.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithLimit_PassesToRepository()
    {
        // Arrange
        var query = new GetActiveSessionsQuery(Owner, Limit: 10);

        _sessionRepositoryMock
            .Setup(r => r.FindActiveAsync(Owner, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());
        _sessionRepositoryMock
            .Setup(r => r.CountActiveByUserIdAsync(Owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _sessionRepositoryMock.Verify(r => r.FindActiveAsync(Owner, 10, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithOffset_PassesToRepository()
    {
        // Arrange
        var query = new GetActiveSessionsQuery(Owner, Offset: 5);

        _sessionRepositoryMock
            .Setup(r => r.FindActiveAsync(Owner, null, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());
        _sessionRepositoryMock
            .Setup(r => r.CountActiveByUserIdAsync(Owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _sessionRepositoryMock.Verify(r => r.FindActiveAsync(Owner, null, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithLimitAndOffset_PassesBothToRepository()
    {
        // Arrange
        var query = new GetActiveSessionsQuery(Owner, Limit: 10, Offset: 5);

        _sessionRepositoryMock
            .Setup(r => r.FindActiveAsync(Owner, 10, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());
        _sessionRepositoryMock
            .Setup(r => r.CountActiveByUserIdAsync(Owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _sessionRepositoryMock.Verify(r => r.FindActiveAsync(Owner, 10, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNegativeLimit_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetActiveSessionsQuery(Owner, Limit: -1);

        // Act & Assert
        var act =
            () => _handler.Handle(query, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<ArgumentException>()).Which;

        exception.Message.Should().Contain("non-negative");
    }

    [Fact]
    public async Task Handle_WithLimitExceeding1000_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetActiveSessionsQuery(Owner, Limit: 1001);

        // Act & Assert
        var act =
            () => _handler.Handle(query, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<ArgumentException>()).Which;

        exception.Message.Should().Contain("1000");
    }

    [Fact]
    public async Task Handle_WithNegativeOffset_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetActiveSessionsQuery(Owner, Offset: -1);

        // Act & Assert
        var act =
            () => _handler.Handle(query, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<ArgumentException>()).Which;

        exception.Message.Should().Contain("non-negative");
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var query = new GetActiveSessionsQuery(Owner);
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _sessionRepositoryMock
            .Setup(r => r.FindActiveAsync(Owner, null, null, token))
            .ReturnsAsync(new List<GameSession>());
        _sessionRepositoryMock
            .Setup(r => r.CountActiveByUserIdAsync(Owner, token))
            .ReturnsAsync(0);

        // Act
        await _handler.Handle(query, token);

        // Assert
        _sessionRepositoryMock.Verify(r => r.FindActiveAsync(Owner, null, null, token), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #4080 — le due cose che a questo livello SONO esprimibili.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "#4080: l'handler propaga OwnerUserId a entrambe le chiamate al repository")]
    public async Task Handle_ForwardsOwnerUserId_ToFindAndCount()
    {
        // Il proprietario di questo test e' distinto da `Owner`, cosi' un handler che passasse
        // una costante o il valore sbagliato non potrebbe superare le Verify sotto.
        var owner = Guid.NewGuid();
        var query = new GetActiveSessionsQuery(owner, Limit: 7, Offset: 3);

        _sessionRepositoryMock
            .Setup(r => r.FindActiveAsync(owner, 7, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());
        _sessionRepositoryMock
            .Setup(r => r.CountActiveByUserIdAsync(owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _handler.Handle(query, TestContext.Current.CancellationToken);

        _sessionRepositoryMock.Verify(
            r => r.FindActiveAsync(owner, 7, 3, It.IsAny<CancellationToken>()), Times.Once);

        // 🔴 Il conteggio va filtrato con lo STESSO proprietario della pagina. Un totale globale
        // annuncerebbe pagine irraggiungibili e rivelerebbe quante sessioni esistono in tutto.
        _sessionRepositoryMock.Verify(
            r => r.CountActiveByUserIdAsync(owner, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "#4080: OwnerUserId vuoto viene rifiutato e il repository non viene toccato")]
    public async Task Handle_WithEmptyOwner_Throws_AndDoesNotQuery()
    {
        var query = new GetActiveSessionsQuery(Guid.Empty);

        var act = () => _handler.Handle(query, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<ArgumentException>()).Which;

        exception.Message.Should().Contain("OwnerUserId");

        // L'asserzione che conta: non basta che lanci, deve lanciare PRIMA di interrogare —
        // un Guid.Empty che arrivasse al repository equivarrebbe a non filtrare.
        _sessionRepositoryMock.Verify(
            r => r.FindActiveAsync(It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _sessionRepositoryMock.Verify(
            r => r.CountActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static GameSession CreateActiveSession(Guid gameId)
    {
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Player 1", 1, "Red")
        };

        return new GameSession(
            id: Guid.NewGuid(),
            gameId: gameId,
            players: players
        );
    }
}
