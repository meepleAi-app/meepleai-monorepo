using System.Text.Json.Nodes;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Enums;
using Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

/// <summary>
/// Forma jsonb del contenuto di una versione di AgentProfile (#4167). La colonna è un contratto
/// persistito: la migration di seed la scrive a mano, quindi la forma deve restare leggibile e stabile.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4167")]
public sealed class AgentProfileContentJsonTests
{
    [Fact]
    public void Content_SurvivesARoundTrip()
    {
        var content = AgentProfileTests.Content();

        var restored = AgentProfileContentJson.Deserialize(AgentProfileContentJson.Serialize(content));

        restored.Should().Be(content);
    }

    [Fact]
    public void Serialize_WritesCamelCaseNamesAndEnumsAsStrings()
    {
        var json = JsonNode.Parse(AgentProfileContentJson.Serialize(AgentProfileTests.Content()))!;

        json["primaryModelId"]!.GetValue<string>().Should().Be("deepseek-chat");
        json["citationStyle"]!.GetValue<string>().Should().Be(nameof(CitationStyle.PageReferencesInText));
        json["allowedCategories"]!.AsArray().Select(n => n!.GetValue<string>())
            .Should().Equal(nameof(DocumentCategory.Rulebook), nameof(DocumentCategory.Errata));
        json["systemPrompts"]!["en"]!.GetValue<string>().Should().Be("You are MeepleAI.");
    }

    /// <summary>
    /// Ricostruire non valida, ma normalizza come <c>Create</c>: le categorie sono un insieme, e un
    /// jsonb scritto con un altro ordine deve risultare uguale allo stesso contenuto creato dal codice.
    /// </summary>
    [Fact]
    public void Deserialize_NormalizesTheCategoryOrder_LikeCreate()
    {
        var content = AgentProfileTests.Content();
        var json = JsonNode.Parse(AgentProfileContentJson.Serialize(content))!;
        json["allowedCategories"] = new JsonArray(
            nameof(DocumentCategory.Errata), nameof(DocumentCategory.Rulebook), nameof(DocumentCategory.Errata));

        var restored = AgentProfileContentJson.Deserialize(json.ToJsonString());

        restored.Should().Be(content);
    }

    /// <summary>
    /// Le versioni archiviate sono storia immutabile: se un giorno le regole di validazione si
    /// stringono, una versione salvata prima deve continuare a caricarsi. Altrimenti l'intero
    /// aggregato — e con lui l'agente — non si caricherebbe più.
    /// </summary>
    [Fact]
    public void Deserialize_DoesNotRevalidate_AContentStoredUnderOlderRules()
    {
        var json = JsonNode.Parse(AgentProfileContentJson.Serialize(AgentProfileTests.Content()))!;
        json["temperature"] = 3.5m;

        var restored = AgentProfileContentJson.Deserialize(json.ToJsonString());

        restored.Temperature.Should().Be(3.5m);
    }
}
