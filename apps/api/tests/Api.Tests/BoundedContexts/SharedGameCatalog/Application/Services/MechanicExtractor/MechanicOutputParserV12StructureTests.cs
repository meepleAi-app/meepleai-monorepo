using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicOutputParserV12StructureTests
{
    private const string Phases = """
    {"phases":[
      {"name":"Azione","description":"ogni giocatore fa due azioni","order":1,"kind":"rule","priority":"base",
       "citations":[{"pdf_page":4,"quote":"due azioni"}]},
      {"name":"Carta Fretta","description":"con la carta Fretta si fanno tre azioni","order":2,"kind":"exception","priority":"card",
       "overrides":[0],"trigger":{"phase":"Azione","component":"Carta Fretta"},
       "citations":[{"pdf_page":12,"quote":"tre azioni"}]},
      {"name":"Esempio","description":"Anna gioca Fretta e fa tre azioni","order":3,"kind":"example",
       "citations":[{"pdf_page":12,"quote":"Anna"}]}
    ]}
    """;

    [Fact]
    public void Parse_ReadsKindPriorityTriggerAndResolvesOverridesByOrdinal()
    {
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Phases] = Phases });

        claims.Should().HaveCount(3);
        var general = claims[0];
        var exc = claims[1];
        general.Kind.Should().Be(MechanicClaimKind.Rule);
        exc.Kind.Should().Be(MechanicClaimKind.Exception);
        exc.Priority.Should().Be(MechanicRulePriority.Card);
        exc.Overrides.Should().Equal(general.Id);
        exc.Trigger!.Phase.Should().Be("azione");
        exc.Trigger.Component.Should().Be("carta fretta");
        claims[2].Kind.Should().Be(MechanicClaimKind.Example);
    }

    [Fact]
    public void Parse_MissingFields_DefaultToBaseRule()
    {
        const string json = """{"mechanics":[{"name":"X","description":"d","citations":[{"pdf_page":1,"quote":"q"}]}]}""";
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json });
        claims.Single().Kind.Should().Be(MechanicClaimKind.Rule);
        claims.Single().Priority.Should().Be(MechanicRulePriority.Base);
        claims.Single().Overrides.Should().BeEmpty();
    }

    [Fact]
    public void Parse_OutOfRangeOrSelfOrdinal_IsIgnoredNotThrown()
    {
        const string json = """
        {"mechanics":[
          {"name":"A","description":"d","kind":"exception","priority":"card","overrides":[0, 7, -1],"citations":[{"pdf_page":1,"quote":"q"}]}
        ]}
        """;
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json });
        claims.Single().Overrides.Should().BeEmpty();
        claims.Single().Kind.Should().Be(MechanicClaimKind.Exception);
    }

    [Fact]
    public void Parse_UnknownKindOrPriority_FallsBackToDefault()
    {
        const string json = """{"mechanics":[{"name":"X","description":"d","kind":"banana","priority":"max","citations":[{"pdf_page":1,"quote":"q"}]}]}""";
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json });
        claims.Single().Kind.Should().Be(MechanicClaimKind.Rule);
        claims.Single().Priority.Should().Be(MechanicRulePriority.Base);
    }

    [Fact]
    public void Parse_OrdinalIsRawArrayIndex_EvenWhenAnEarlierItemIsSkipped()
    {
        // item[0] has no citations → skipped; item[2] overrides [1] by RAW index, not by valid-claim index.
        const string json = """
        {"mechanics":[
          {"name":"Skipped","description":"no citations here"},
          {"name":"General","description":"d","citations":[{"pdf_page":1,"quote":"q"}]},
          {"name":"Exc","description":"d","kind":"exception","priority":"card","overrides":[1],"citations":[{"pdf_page":1,"quote":"q"}]}
        ]}
        """;
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json });
        claims.Should().HaveCount(2);
        claims[1].Overrides.Should().Equal(claims[0].Id);
        claims[1].SourceAnchor.Should().Be("$.mechanics[2]");
    }
}
