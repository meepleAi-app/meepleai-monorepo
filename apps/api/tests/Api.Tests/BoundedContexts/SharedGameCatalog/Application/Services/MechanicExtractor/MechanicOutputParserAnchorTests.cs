using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicOutputParserAnchorTests
{
    [Fact]
    public void Parse_StampsRawSourceIndexAnchor_EvenWhenEarlierItemsDropped()
    {
        // item[0] has NO citations → dropped; item[1] emitted. Anchor must be $.mechanics[1].
        var json = """
        {"mechanics":[
          {"description":"no cite mechanic"},
          {"description":"good mechanic","citations":[{"pdf_page":2,"quote":"do the thing"}]}
        ]}
        """;
        var outputs = new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json };

        var claims = MechanicOutputParser.Parse(Guid.NewGuid(), outputs);

        claims.Should().ContainSingle();
        claims[0].SourceAnchor.Should().Be("$.mechanics[1]");
        claims[0].DisplayOrder.Should().Be(0); // compacted — proves anchor != displayOrder
    }

    [Fact]
    public void Parse_Victory_AnchorsPrimaryAndAlternatives_ToVictoryObject()
    {
        var json = """
        {"victory":{"primary":"most points wins","alternatives":["instant win on 10 gems"],
          "citations":[{"pdf_page":5,"quote":"points win"}]}}
        """;
        var outputs = new Dictionary<MechanicSection, string> { [MechanicSection.Victory] = json };

        var claims = MechanicOutputParser.Parse(Guid.NewGuid(), outputs);

        claims.Should().HaveCount(2);
        claims.Should().OnlyContain(c => c.SourceAnchor == "$.victory");
    }
}
