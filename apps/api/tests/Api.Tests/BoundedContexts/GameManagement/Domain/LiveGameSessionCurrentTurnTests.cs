using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

/// <summary>
/// #4112 — <see cref="LiveGameSession.GetCurrentTurnPlayerId"/> indicizzava fuori range prima
/// dell'avvio della sessione.
/// </summary>
/// <remarks>
/// <para>
/// Il metodo calcolava <c>(CurrentTurnIndex - 1) % n</c>. <c>CurrentTurnIndex</c> vale 0 alla
/// creazione e diventa 1 solo in <c>Start()</c>, mentre <c>AddPlayer</c> appende già a
/// <c>_turnOrder</c>: quindi l'uscita anticipata su <c>_turnOrder.Count == 0</c> non scattava.
/// In C# il <c>%</c> tiene il segno del dividendo, quindi <c>(0 - 1) % n</c> è <b>-1</b> per
/// ogni n &gt; 1 e l'indicizzazione lanciava.
/// </para>
/// <para>
/// Con <b>un solo</b> giocatore <c>(0-1) % 1</c> fa 0 e tutto sembrava funzionare: è il motivo
/// per cui il difetto è sopravvissuto. Per questo i casi qui sotto partono da 1 giocatore e
/// arrivano a N — un test con un solo giocatore avrebbe dato il verde su un metodo rotto.
/// </para>
/// <para>
/// Il metodo non aveva <b>nessun</b> test: <c>grep -rl GetCurrentTurnPlayerId</c> sulla suite
/// non trovava nulla.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
public sealed class LiveGameSessionCurrentTurnTests
{
    private static readonly PlayerColor[] s_colors =
    {
        PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow
    };

    private static LiveGameSession NewSessionWith(int playerCount)
    {
        var session = LiveGameSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Game");

        for (var i = 0; i < playerCount; i++)
        {
            session.AddPlayer(null, $"P{i + 1}", s_colors[i]);
        }

        return session;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void GetCurrentTurnPlayerId_BeforeStart_ReturnsFirstPlayerInsteadOfThrowing(int playerCount)
    {
        var session = NewSessionWith(playerCount);

        // `CurrentTurnIndex` è ancora 0: è il caso che lanciava da due giocatori in su.
        session.CurrentTurnIndex.Should().Be(0, "la sessione non è stata avviata");

        var current = session.GetCurrentTurnPlayerId();

        current.Should().Be(
            session.Players[0].Id,
            "prima dell'avvio il giocatore di turno è il primo in ordine di turno");
    }

    [Fact]
    public void GetCurrentTurnPlayerId_WithNoPlayers_ReturnsEmpty()
    {
        var session = NewSessionWith(0);

        session.GetCurrentTurnPlayerId().Should().Be(Guid.Empty);
    }

    [Fact]
    public void GetCurrentTurnPlayerId_AfterStart_FollowsTurnOrder()
    {
        var session = NewSessionWith(3);
        session.Start();

        // `Start()` porta `CurrentTurnIndex` a 1, cioè il primo turno.
        session.CurrentTurnIndex.Should().Be(1);
        session.GetCurrentTurnPlayerId().Should().Be(session.Players[0].Id);
    }

    [Fact]
    public void GetCurrentTurnPlayerId_WrapsWhenTurnIndexExceedsPlayerCount()
    {
        // Il caso opposto a quello del difetto: i turni avanzano oltre il numero di giocatori e
        // il giro deve ripartire dal primo. È ciò che il modulo serve a fare, e il motivo per
        // cui non è stato rimosso insieme al segno negativo.
        var session = NewSessionWith(3);
        session.Start();

        var seen = new List<Guid>();
        for (var turn = 0; turn < 6; turn++)
        {
            seen.Add(session.GetCurrentTurnPlayerId());
            session.AdvanceTurn();
        }

        seen.Should().Equal(
            session.Players[0].Id, session.Players[1].Id, session.Players[2].Id,
            session.Players[0].Id, session.Players[1].Id, session.Players[2].Id);
    }

    [Fact]
    public void GetCurrentTurnPlayerId_SkipsSpectators()
    {
        var session = LiveGameSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Test Game");
        session.AddPlayer(null, "Spettatore", PlayerColor.Red, role: PlayerRole.Spectator);
        var player = session.AddPlayer(null, "Giocatore", PlayerColor.Blue);

        // Lo spettatore è il primo in `_turnOrder` ma non deve mai essere «di turno».
        session.GetCurrentTurnPlayerId().Should().Be(player.Id);
    }
}
