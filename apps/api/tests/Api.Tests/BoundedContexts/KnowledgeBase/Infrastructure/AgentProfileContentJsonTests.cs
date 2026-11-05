using System.Text.Json.Nodes;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
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

    /// <summary>
    /// Review Focus 2: una versione salvata prima di questa fetta non ha la chiave. Deve caricarsi come
    /// «disabilitato», e un contenuto senza assistente non deve cominciare a scriverla: il jsonb delle
    /// versioni esistenti resta byte per byte quello di prima.
    /// </summary>
    [Fact]
    public void LegacyContentWithoutTheSection_LoadsAsDisabled()
    {
        var json = AgentProfileContentJson.Serialize(AgentProfileTests.Content());

        JsonNode.Parse(json)!.AsObject().ContainsKey("sessionAssistant").Should().BeFalse();
        AgentProfileContentJson.Deserialize(json).SessionAssistant.Should().Be(SessionAssistantSettings.Disabled);
    }

    [Fact]
    public void Content_WithTheAssistant_SurvivesARoundTrip()
    {
        var content = AgentProfileTests.Content(assistant: AgentProfileTests.Assistant(useReserveModel: true));

        var json = AgentProfileContentJson.Serialize(content);

        AgentProfileContentJson.Deserialize(json).Should().Be(content);
        var section = JsonNode.Parse(json)!["sessionAssistant"]!;
        section["enabled"]!.GetValue<bool>().Should().BeTrue();
        section["systemPrompts"]!["it"]!.GetValue<string>().Should().Be("Sei l'assistente al tavolo.");
        section["maxToolIterations"]!.GetValue<int>().Should().Be(3);
        section["turnTimeoutSeconds"]!.GetValue<int>().Should().Be(15);
        section["useReserveModel"]!.GetValue<bool>().Should().BeTrue();
    }

    /// <summary>
    /// La migrazione che abiliterà l'assistente scrive questo jsonb a mano: un campo numerico dimenticato
    /// deve valere il default misurato, non zero (zero giri vorrebbe dire un assistente senza strumenti).
    /// </summary>
    [Fact]
    public void SectionWithOnlyEnabledAndPrompts_TakesTheDefaults()
    {
        var json = JsonNode.Parse(AgentProfileContentJson.Serialize(AgentProfileTests.Content()))!;
        json["sessionAssistant"] = new JsonObject
        {
            ["enabled"] = true,
            ["systemPrompts"] = new JsonObject { ["en"] = "You are the table assistant." },
        };

        var assistant = AgentProfileContentJson.Deserialize(json.ToJsonString()).SessionAssistant;

        assistant.Enabled.Should().BeTrue();
        assistant.Temperature.Should().Be(0.2m);
        assistant.MaxResponseTokens.Should().Be(600);
        assistant.MaxToolIterations.Should().Be(3);
        assistant.TurnTimeoutSeconds.Should().Be(15);
        assistant.UseReserveModel.Should().BeFalse();
    }

    // ── La sezione degli intenti della risposta (#4404) ──────────────────────

    [Fact]
    public void ASectionOfIntentInstructions_SurvivesARoundTrip()
    {
        var content = AgentProfileTests.Content(intents: new Dictionary<string, string>
        {
            ["tutor"] = "Teach it like a patient friend.",
            ["secca"] = "One line, then the page.",
        });

        var restored = AgentProfileContentJson.Deserialize(AgentProfileContentJson.Serialize(content));

        restored.Should().Be(content);
        restored.IntentInstructions["tutor"].Should().Be("Teach it like a patient friend.");
    }

    /// <summary>
    /// Come per l'assistente in partita: una versione senza la sezione non comincia a scriverla, cosi' il jsonb
    /// delle versioni esistenti resta byte per byte quello di prima.
    /// </summary>
    [Fact]
    public void AContentWithoutTheSection_DoesNotWriteTheKey()
    {
        var json = JsonNode.Parse(AgentProfileContentJson.Serialize(AgentProfileTests.Content()))!;

        json.AsObject().ContainsKey("intentInstructions").Should().BeFalse();
    }

    [Fact]
    public void ASectionIsWrittenInCamelCase_UnderTheIdOfTheIntent()
    {
        var json = JsonNode.Parse(AgentProfileContentJson.Serialize(
            AgentProfileTests.Content(intents: new Dictionary<string, string> { ["secca"] = "One line." })))!;

        json["intentInstructions"]!["secca"]!.GetValue<string>().Should().Be("One line.");
    }

    [Fact]
    public void AVersionStoredBeforeTheSectionExisted_LoadsWithNone()
    {
        var json = JsonNode.Parse(AgentProfileContentJson.Serialize(AgentProfileTests.Content()))!;
        json.AsObject().Remove("intentInstructions");

        var restored = AgentProfileContentJson.Deserialize(json.ToJsonString());

        restored.IntentInstructions.Should().BeEmpty();
        restored.Should().Be(AgentProfileTests.Content());
    }

    [Fact]
    public void ASectionStoredUnderOlderRules_IsNotRevalidated_ButIsNotAnIntentEither()
    {
        // A key that is no longer an intent (here a retired id) loads, because history is immutable; the pipeline
        // only ever asks for tutor and secca, so it is never read.
        var json = JsonNode.Parse(AgentProfileContentJson.Serialize(AgentProfileTests.Content()))!;
        json["intentInstructions"] = new JsonObject { ["decisore"] = "Old." };

        var restored = AgentProfileContentJson.Deserialize(json.ToJsonString());

        restored.IntentInstructions.Should().ContainKey("decisore");
        restored.InstructionForIntent("decisore").Should().BeNull();
    }

    [Fact]
    public void AnAutoSectionInTheStoredJson_NeverReachesAnAnswer()
    {
        // Create refuses the key, Restore does not revalidate: a jsonb edited by hand must not hand every
        // «auto» answer an instruction.
        var json = JsonNode.Parse(AgentProfileContentJson.Serialize(AgentProfileTests.Content()))!;
        json["intentInstructions"] = new JsonObject { ["auto"] = "Always shout." };

        var restored = AgentProfileContentJson.Deserialize(json.ToJsonString());

        restored.InstructionForIntent("auto").Should().BeNull();
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public void ABlankSectionInTheStoredJson_FallsBackToTheBuiltInInstruction(string blank)
    {
        var json = JsonNode.Parse(AgentProfileContentJson.Serialize(AgentProfileTests.Content()))!;
        json["intentInstructions"] = new JsonObject { ["tutor"] = blank };

        var restored = AgentProfileContentJson.Deserialize(json.ToJsonString());

        restored.InstructionForIntent("tutor").Should().Be(AnswerIntent.DefaultInstructionFor("tutor"));
    }
}
