using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicCardContentV3Tests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FromAnalysis_ProjectsStructure_AndBumpsSchemaVersionTo3()
    {
        var a = MechanicAnalysis.Create(Guid.NewGuid(), Guid.NewGuid(), "v1.2.0", Guid.NewGuid(), Now, "m", "p", 1m);
        var cit = MechanicCitation.Create(Guid.NewGuid(), 4, "q", null, 0);
        var general = MechanicClaim.Create(a.Id, MechanicSection.Phases, "regola", 0, new[] { cit });
        var exc = MechanicClaim.Create(a.Id, MechanicSection.Phases, "eccezione", 1, new[] { MechanicCitation.Create(Guid.NewGuid(), 12, "q2", null, 0) });
        a.AddClaim(general); a.AddClaim(exc);
        a.SetClaimStructure(exc.Id, new MechanicClaimStructure(MechanicClaimKind.Exception, MechanicRulePriority.Card, new[] { general.Id }, new MechanicTrigger("azione", null, "carta fretta")));
        var ctx = new MechanicCardGameContext { SharedGameId = a.SharedGameId, SharedGameName = "G" };

        var content = MechanicCardContent.FromAnalysis(a, ctx, Now);
        var json = content.ToJson();

        content.SchemaVersion.Should().Be(3);
        var snap = content.Claims.Single(c => c.Id == exc.Id);
        snap.Kind.Should().Be("Exception");
        snap.Priority.Should().Be("Card");
        snap.Overrides.Should().Equal(general.Id);
        snap.Trigger!.Phase.Should().Be("azione");
        json.Should().Contain("\"kind\":\"Exception\"").And.Contain("\"trigger\":{\"phase\":\"azione\"");
        // chiavi v2 invariate
        json.Should().Contain("\"schema_version\":3").And.Contain("\"claims\":[").And.Contain("\"citations\":[").And.Contain("\"validations\":[");
    }

    [Fact]
    public void Deserialize_V2Json_WithoutNewKeys_UsesDefaults()
    {
        const string v2 = """{"schema_version":2,"snapshot_at":"2026-07-10T12:00:00Z","source_analysis_id":"00000000-0000-0000-0000-000000000001","source_prompt_version":"v1.1.0","claims":[{"id":"00000000-0000-0000-0000-000000000002","section":"Phases","ordinal":0,"claim":"x","citations":[],"validations":[]}],"metadata":{"shared_game_id":"00000000-0000-0000-0000-000000000003","shared_game_name":"G","publisher":null,"language":"it"}}""";
        var content = JsonSerializer.Deserialize<MechanicCardContent>(v2)!;
        var c = content.Claims.Single();
        c.Kind.Should().Be("Rule");
        c.Priority.Should().Be("Base");
        c.Overrides.Should().BeEmpty();
        c.Trigger.Should().BeNull();
    }
}
