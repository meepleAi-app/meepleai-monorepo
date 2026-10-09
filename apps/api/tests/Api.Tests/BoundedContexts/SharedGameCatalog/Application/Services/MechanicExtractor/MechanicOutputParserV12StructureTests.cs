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

    [Fact]
    public void Parse_Summary_IgnoresStructureFields()
    {
        const string json = """{"summary":{"text":"t","kind":"exception","priority":"card","trigger":{"phase":"x"},"citations":[{"pdf_page":1,"quote":"q"}]}}""";
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Summary] = json });
        var claim = claims.Single();
        claim.Kind.Should().Be(MechanicClaimKind.Rule);
        claim.Priority.Should().Be(MechanicRulePriority.Base);
        claim.Trigger.Should().BeNull();
    }

    private static string MechanicItems(params string[] overridesPerItem) =>
        "{\"mechanics\":[" + string.Join(",", overridesPerItem.Select((o, i) =>
            $"{{\"name\":\"M{i}\",\"description\":\"d\",\"kind\":\"exception\",\"overrides\":{o},\"citations\":[{{\"pdf_page\":1,\"quote\":\"q\"}}]}}")) + "]}";

    [Fact]
    public void Parse_TwoNodeCycle_DropsEdgeFromHigherIndexItem()
    {
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = MechanicItems("[1]", "[0]") });
        claims[0].Overrides.Should().Equal(claims[1].Id);
        claims[1].Overrides.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ThreeNodeCycle_IsBrokenDeterministically()
    {
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = MechanicItems("[1]", "[2]", "[0]") });
        claims[0].Overrides.Should().Equal(claims[1].Id);
        claims[1].Overrides.Should().Equal(claims[2].Id);
        claims[2].Overrides.Should().BeEmpty();
    }

    [Fact]
    public void Parse_Phases_OrdinalsUseRawSourceIndex_WhenOrderResorts()
    {
        const string json = """
        {"phases":[
          {"name":"General","description":"d","order":2,"citations":[{"pdf_page":1,"quote":"q"}]},
          {"name":"Exc","description":"d","order":1,"kind":"exception","overrides":[0],"citations":[{"pdf_page":1,"quote":"q"}]}
        ]}
        """;
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Phases] = json });
        claims.Should().HaveCount(2);
        claims[0].SourceAnchor.Should().Be("$.phases[1]");
        claims[0].DisplayOrder.Should().Be(0);
        claims[0].Overrides.Should().Equal(claims.Single(c => c.SourceAnchor == "$.phases[0]").Id);
    }

    [Fact]
    public void Parse_OrdinalToSkippedItem_IsDropped()
    {
        const string json = """
        {"mechanics":[
          {"name":"Skipped","description":"no citations"},
          {"name":"Exc","description":"d","kind":"exception","overrides":[0],"citations":[{"pdf_page":1,"quote":"q"}]}
        ]}
        """;
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json });
        claims.Single().Overrides.Should().BeEmpty();
    }

    [Fact]
    public void Parse_Victory_ReadsKindPriorityTrigger_ButNeverOverrides()
    {
        // The primary is the only addressable victory item: ordinal 0 is itself, so `overrides` never resolves.
        const string json = """
        {"victory":{"primary":"vince chi ha piu punti","kind":"exception","priority":"scenario",
          "trigger":{"phase":"Fine Partita"},"overrides":[0],
          "alternatives":["vittoria immediata con 10 carte"],
          "citations":[{"pdf_page":9,"quote":"piu punti"}]}}
        """;
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Victory] = json });

        claims.Should().HaveCount(2);
        var primary = claims[0];
        primary.Kind.Should().Be(MechanicClaimKind.Exception);
        primary.Priority.Should().Be(MechanicRulePriority.Scenario);
        primary.Trigger!.Phase.Should().Be("fine partita");
        primary.Overrides.Should().BeEmpty();
        claims[1].Kind.Should().Be(MechanicClaimKind.Rule, "alternatives keep the default structure");
        claims[1].Overrides.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EdgeToExample_IsDropped()
    {
        // item[2] overrides a Rule (0) and an Example (1): only the edge to the Example is dropped.
        const string json = """
        {"mechanics":[
          {"name":"General","description":"d","citations":[{"pdf_page":1,"quote":"q"}]},
          {"name":"Esempio","description":"d","kind":"example","citations":[{"pdf_page":1,"quote":"q"}]},
          {"name":"Exc","description":"d","kind":"exception","overrides":[0,1],"citations":[{"pdf_page":1,"quote":"q"}]}
        ]}
        """;
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json });

        claims.Should().HaveCount(3);
        claims[1].Kind.Should().Be(MechanicClaimKind.Example);
        claims[2].Kind.Should().Be(MechanicClaimKind.Exception);
        claims[2].Overrides.Should().Equal(claims[0].Id);
    }

    [Fact]
    public void Parse_ExampleWithOverrides_LosesThem()
    {
        const string json = """
        {"mechanics":[
          {"name":"General","description":"d","citations":[{"pdf_page":1,"quote":"q"}]},
          {"name":"Esempio","description":"d","kind":"example","overrides":[0],"citations":[{"pdf_page":1,"quote":"q"}]}
        ]}
        """;
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json });

        claims.Should().HaveCount(2);
        claims[1].Kind.Should().Be(MechanicClaimKind.Example);
        claims[1].Overrides.Should().BeEmpty();
        claims[0].Overrides.Should().BeEmpty();
    }
}
