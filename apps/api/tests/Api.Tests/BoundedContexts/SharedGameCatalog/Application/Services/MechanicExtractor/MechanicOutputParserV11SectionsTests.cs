using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

// v1.1.0 (#539 follow-up): Setup / Components / EndgameScoring sections.
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicOutputParserV11SectionsTests
{
    [Fact]
    public void Parse_Setup_OrdersByDeclaredOrder_AndAppendsPlayerCountNote()
    {
        // item[0] has order=2, item[1] has order=1 → item[1] must be emitted first, with its
        // playerCountNote appended and the RAW source-index anchor ($.setup[1]).
        var json = """
        {"setup":[
          {"description":"Distribuisci le plance","order":2,"citations":[{"pdf_page":3,"quote":"plance ai giocatori"}]},
          {"description":"Prepara il sacchetto","order":1,"playerCountNote":"in 2 rimuovi 20 tessere","citations":[{"pdf_page":3,"quote":"riempi il sacchetto"}]}
        ]}
        """;
        var outputs = new Dictionary<MechanicSection, string> { [MechanicSection.Setup] = json };

        var claims = MechanicOutputParser.Parse(Guid.NewGuid(), outputs);

        claims.Should().HaveCount(2);
        claims.Should().OnlyContain(c => c.Section == MechanicSection.Setup);
        claims[0].Text.Should().Contain("Prepara il sacchetto").And.Contain("in 2 rimuovi 20 tessere");
        claims[0].SourceAnchor.Should().Be("$.setup[1]");
        claims[1].Text.Should().Contain("Distribuisci le plance");
    }

    [Fact]
    public void Parse_Components_IncludesNameAndQuantity()
    {
        var json = """
        {"components":[
          {"name":"Tessere","description":"pezzi in ceramica","quantity":"100","citations":[{"pdf_page":2,"quote":"cento tessere"}]}
        ]}
        """;
        var outputs = new Dictionary<MechanicSection, string> { [MechanicSection.Components] = json };

        var claims = MechanicOutputParser.Parse(Guid.NewGuid(), outputs);

        claims.Should().ContainSingle();
        claims[0].Section.Should().Be(MechanicSection.Components);
        claims[0].Text.Should().Contain("Tessere").And.Contain("100").And.Contain("pezzi in ceramica");
        claims[0].SourceAnchor.Should().Be("$.components[0]");
    }

    [Fact]
    public void Parse_Endgame_EmitsNamedScoringClaimsWithRawAnchors()
    {
        var json = """
        {"endgame":[
          {"name":"Trigger","description":"finisce quando un giocatore completa una riga","citations":[{"pdf_page":9,"quote":"riga completata"}]},
          {"description":"punti per colonne complete","citations":[{"pdf_page":9,"quote":"colonne complete"}]}
        ]}
        """;
        var outputs = new Dictionary<MechanicSection, string> { [MechanicSection.EndgameScoring] = json };

        var claims = MechanicOutputParser.Parse(Guid.NewGuid(), outputs);

        claims.Should().HaveCount(2);
        claims.Should().OnlyContain(c => c.Section == MechanicSection.EndgameScoring);
        claims[0].Text.Should().StartWith("Trigger:");
        claims[0].SourceAnchor.Should().Be("$.endgame[0]");
        claims[1].SourceAnchor.Should().Be("$.endgame[1]");
    }

    [Fact]
    public void Parse_NewSection_DropsItemsWithoutCitations()
    {
        // Domain factory requires ≥1 citation (ADR-051 T3) — items without one are dropped.
        var json = """
        {"components":[
          {"name":"NoCite","description":"manca la citazione"},
          {"name":"Good","description":"con citazione","citations":[{"pdf_page":2,"quote":"ok"}]}
        ]}
        """;
        var outputs = new Dictionary<MechanicSection, string> { [MechanicSection.Components] = json };

        var claims = MechanicOutputParser.Parse(Guid.NewGuid(), outputs);

        claims.Should().ContainSingle();
        claims[0].Text.Should().Contain("Good");
        claims[0].SourceAnchor.Should().Be("$.components[1]");
    }
}
