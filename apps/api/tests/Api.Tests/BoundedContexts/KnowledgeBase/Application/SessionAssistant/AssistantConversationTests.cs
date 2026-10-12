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
}
