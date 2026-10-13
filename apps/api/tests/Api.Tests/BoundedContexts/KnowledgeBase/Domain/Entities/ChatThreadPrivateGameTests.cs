using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Issue #4244. Il gioco di un thread è uno, del catalogo o privato; in tabella sta in due colonne,
/// perché <c>ChatThreads.GameId</c> è una chiave esterna verso <c>shared_games</c>. Chi legge il thread
/// usa <c>GameId</c>; chi lo salva usa le due viste, di cui al più una è valorizzata.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4244")]
public sealed class ChatThreadPrivateGameTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Game = Guid.NewGuid();

    private static ChatThread ThreadOnAPrivateGame(Guid game) =>
        new(Guid.NewGuid(), Owner, gameId: game, gameIsPrivate: true);

    [Fact]
    public void AThreadOnAPrivateGame_KeepsTheGame_InThePrivateView()
    {
        var thread = ThreadOnAPrivateGame(Game);

        thread.GameId.Should().Be(Game, "per chi legge, il gioco del thread è questo");
        thread.GameIsPrivate.Should().BeTrue();
        thread.PrivateGameId.Should().Be(Game);
        thread.SharedGameId.Should().BeNull("non va nella colonna con la chiave esterna verso il catalogo");
    }

    [Fact]
    public void AThreadOnACatalogGame_KeepsTheGame_InTheSharedView()
    {
        var thread = new ChatThread(Guid.NewGuid(), Owner, gameId: Game);

        thread.GameId.Should().Be(Game);
        thread.GameIsPrivate.Should().BeFalse();
        thread.SharedGameId.Should().Be(Game);
        thread.PrivateGameId.Should().BeNull();
    }

    [Fact]
    public void AThreadWithNoGame_HasNeither()
    {
        var thread = new ChatThread(Guid.NewGuid(), Owner);

        thread.SharedGameId.Should().BeNull();
        thread.PrivateGameId.Should().BeNull();
    }

    [Fact]
    public void APrivateGameWithoutTheGame_IsRefused()
    {
        var act = () => new ChatThread(Guid.NewGuid(), Owner, gameId: null, gameIsPrivate: true);

        act.Should().Throw<ArgumentException>();
    }

    // ─── La cronologia vale per il suo gioco, anche quando è privato (#4242) ───

    [Fact]
    public void TheHistoryOfAPrivateGameThread_MayBeUsedOnThatGame()
    {
        ChatThreadHistoryPolicy.WhyNot(ThreadOnAPrivateGame(Game), Owner, Game).Should().BeNull();
    }

    [Fact]
    public void TheHistoryOfAPrivateGameThread_IsRefusedOnAnotherGame()
    {
        ChatThreadHistoryPolicy.WhyNot(ThreadOnAPrivateGame(Game), Owner, Guid.NewGuid())
            .Should().Be(ChatThreadHistoryRefusal.AnotherGame);
    }

    [Fact]
    public void TheHistoryOfAPrivateGameThread_IsRefusedToAnotherUser()
    {
        ChatThreadHistoryPolicy.WhyNot(ThreadOnAPrivateGame(Game), Guid.NewGuid(), Game)
            .Should().Be(ChatThreadHistoryRefusal.NotTheOwner);
    }
}
