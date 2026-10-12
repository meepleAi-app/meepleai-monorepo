using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>Spec §4.1 e §6.3: ordine dei messaggi per la cache del prompt e azioni dichiarate senza proposta.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4240")]
public sealed class AssistantConversationTests
{
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static AssistantSessionState State() => new(
        Guid.Parse("00000000-0000-0000-0000-0000000000aa"), "Azul", "InProgress", true, true,
        [new(Marco, "Marco", 37, true)], Marco, 4, 5, "Acquisto", ["punti"],
        [new(Marco, 1, "punti", 37)], ["Si pesca una tessera in più al primo turno."]);

    [Fact]
    public void Build_OrdersFromTheMostStableToTheMostVolatile()
    {
        var messages = AssistantConversation.Build("it", State(),
            [new AssistantHistoryTurn("user", "chi tocca?"), new AssistantHistoryTurn("assistant", "Tocca a Marco.")],
            "Marco fa 5 punti");

        messages.Select(m => m.Role).Should().Equal(ChatRole.System, ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.User);
        messages[0].Text.Should().Be(SessionAssistantPrompts.For("it"));
        messages[^1].Text.Should().Be("Marco fa 5 punti");
    }

    [Fact]
    public void Build_StaticContextHasGamePlayersDimensionsAndHouseRules_ButNoScoresTurnOrPhase()
    {
        var context = AssistantConversation.Build("it", State(), [], "x")[1].Text;

        context.Should().Contain("Azul").And.Contain("Marco").And.Contain("punti").And.Contain("tessera in più");
        context.Should().NotContain("37", "i punteggi arrivano solo da stato_partita (spec §4.1)");
        context.Should().NotContain("Acquisto");
    }

    [Fact]
    public void Prompts_ExistForItalianAndEnglish_AndFallBackToItalian()
    {
        SessionAssistantPrompts.For("en").Should().Contain("never");
        SessionAssistantPrompts.For("de").Should().Be(SessionAssistantPrompts.For("it"));
    }

    [Theory]
    [InlineData("Ho segnato 5 punti a Marco.", true)]
    [InlineData("Ho registrato il punteggio.", true)]
    [InlineData("Ho passato il turno a Giulia.", true)]
    [InlineData("I've recorded 5 points for Marco.", true)]
    [InlineData("I added 3 points to Giulia.", true)]
    [InlineData("Ti propongo +5 a Marco: conferma la card.", false)]
    [InlineData("Marco ha 12 punti.", false)]
    [InlineData("I prepared a proposal: please confirm it.", false)]
    public void ClaimedActionDetector_FlagsCompletedActions(string text, bool expected) =>
        ClaimedActionDetector.ClaimsAction(text).Should().Be(expected);

    // Spike #4240: su «next player please» il modello ha risposto «I've prepared a proposal… please confirm it»
    // senza chiamare proponi_turno. Dire che una proposta esiste è giusto solo se esiste: lo decide chi conosce
    // le proposte del turno, il rilevatore riconosce la frase.
    [Theory]
    [InlineData("Current turn: Luca (round 2). I've prepared a proposal to pass the turn to the next player — please confirm it.", true)]
    [InlineData("I prepared a proposal: please confirm it.", true)]
    [InlineData("I have just created two proposals for you.", true)]
    [InlineData("Ho preparato una proposta: +5 punti a Marco (round 2). Confermi?", true)]
    [InlineData("Ho preparato la proposta per passare il turno al giocatore successivo.", true)]
    [InlineData("Ho preparato due proposte da confermare.", true)]
    [InlineData("La proposta è pronta: confermala.", true)]
    [InlineData("Non ho preparato nessuna proposta: quale Marco intendi?", false)]
    [InlineData("La partita è conclusa, quindi non posso preparare una proposta di punteggio.", false)]
    [InlineData("Dimmi chi ha fatto punti e preparo la proposta da confermare.", false)]
    [InlineData("I haven't prepared a proposal yet: which Marco do you mean?", false)]
    [InlineData("Tocca a Luca (round 2).", false)]
    [InlineData("", false)]
    public void ClaimedActionDetector_FlagsDeclaredProposals(string text, bool expected) =>
        ClaimedActionDetector.ClaimsProposal(text).Should().Be(expected);
}
