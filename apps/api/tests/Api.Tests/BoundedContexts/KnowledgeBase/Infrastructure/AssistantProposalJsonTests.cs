using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

/// <summary>
/// La forma jsonb delle tre colonne di <c>assistant_proposals</c>. È un contratto persistito: rinominare una
/// proprietà del dominio non deve cambiare ciò che è scritto nel database.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4275")]
public sealed class AssistantProposalJsonTests
{
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Fact]
    public void Payload_IsCamelCase_AndSurvivesARoundTrip()
    {
        var payload = new AssistantProposalPayload(Marco, "Marco", "punti", 2, -3);

        var json = AssistantProposalJson.Serialize(payload);

        json.Should().Be("""{"playerId":"00000000-0000-0000-0000-000000000001","playerName":"Marco","dimension":"punti","round":2,"points":-3}""");
        AssistantProposalJson.DeserializePayload(json).Should().Be(payload);
    }

    [Fact]
    public void Expectation_AndConfirmation_SurviveARoundTrip()
    {
        var expected = new AssistantProposalExpectation(CellValue: 7, TurnIndex: null);
        var confirmed = new AssistantProposalConfirmation(3);

        AssistantProposalJson.DeserializeExpectation(AssistantProposalJson.Serialize(expected)).Should().Be(expected);
        AssistantProposalJson.DeserializeConfirmation(AssistantProposalJson.Serialize(confirmed)).Should().Be(confirmed);
        AssistantProposalJson.Serialize(expected).Should().Be("""{"cellValue":7,"turnIndex":null}""");
    }
}
