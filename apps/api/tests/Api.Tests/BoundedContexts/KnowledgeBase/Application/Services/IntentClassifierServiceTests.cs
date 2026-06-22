using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class IntentClassifierServiceTests
{
    private readonly IntentClassifierService _svc = new();

    [Theory]
    [InlineData("come si setupa per 4 giocatori", GameBookRole.Tutorial | GameBookRole.Setup)]
    [InlineData("setup 4 players", GameBookRole.Tutorial | GameBookRole.Setup)]
    [InlineData("qual è la regola del fuoco", GameBookRole.RulesReference)]
    [InlineData("posso fare l'attacco doppio?", GameBookRole.RulesReference)]
    [InlineData("traduci paragrafo 147", GameBookRole.Narrative)]
    [InlineData("§289 cosa dice", GameBookRole.Narrative)]
    [InlineData("incontro con il drago", GameBookRole.Encounter)]
    [InlineData("totally random gibberish abcxyz", GameBookRole.RulesReference)]
    public void ClassifyIntent_ReturnsExpectedRole(string query, GameBookRole expected)
    {
        var result = _svc.ClassifyIntent(query);
        result.Should().Be(expected);
    }
}
