using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>Spec §5.1: l'assistente risolve i nomi come la partita (esatto, nome di battesimo, contenimento).</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4240")]
public sealed class AssistantPlayerMatcherTests
{
    private static readonly AssistantPlayer MarcoRossi = new(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Marco Rossi", 0, true);
    private static readonly AssistantPlayer MarcoBianchi = new(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Marco Bianchi", 0, true);
    private static readonly AssistantPlayer Anna = new(Guid.Parse("00000000-0000-0000-0000-000000000003"), "Anna", 0, true);

    private readonly AssistantPlayerMatcher _matcher = new(new PlayerNameResolutionService());

    [Fact]
    public void Match_IgnoresCase() =>
        _matcher.Match("anna", [MarcoRossi, Anna]).PlayerId.Should().Be(Anna.Id);

    [Fact]
    public void Match_SharedFirstName_IsAmbiguous_WithBothCandidates()
    {
        var match = _matcher.Match("Marco", [MarcoRossi, MarcoBianchi, Anna]);

        match.IsAmbiguous.Should().BeTrue();
        match.Candidates.Should().BeEquivalentTo(["Marco Rossi", "Marco Bianchi"]);
    }

    [Fact]
    public void Match_UnknownName_IsNeitherResolvedNorAmbiguous()
    {
        var match = _matcher.Match("Paolo", [MarcoRossi, Anna]);

        match.PlayerId.Should().BeNull();
        match.IsAmbiguous.Should().BeFalse();
    }
}
