using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Enums;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Invarianti di D2 (ADR-095) sull'aggregato singleton <see cref="AgentProfile"/>:
/// al più una bozza, esattamente una versione pubblicata, versioni pubblicate e archiviate
/// non modificabili, rollback come nuova versione.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4167")]
public sealed class AgentProfileTests
{
    internal const string ReserveModel = "meta-llama/llama-3.3-70b-instruct:free";

    internal static AgentProfileContent Content(
        decimal temperature = 0.3m,
        string model = "deepseek-chat",
        SessionAssistantSettings? assistant = null,
        string? fallbackModel = ReserveModel) =>
        AgentProfileContent.Create(
            name: "MeepleAI",
            description: "Assistente per le regole dei giochi da tavolo",
            persona: "a precise board game rules assistant",
            primaryModelId: model,
            fallbackModelId: fallbackModel,
            temperature: temperature,
            maxResponseTokens: 1500,
            citationStyle: CitationStyle.PageReferencesInText,
            systemPrompts: new Dictionary<string, string>(StringComparer.Ordinal) { ["en"] = "You are MeepleAI." },
            fallbackLanguage: "en",
            candidatePoolSize: 20,
            finalTopK: 5,
            minScore: 0.55m,
            vectorWeight: 0.7m,
            keywordWeight: 0.3m,
            rerankerEnabled: true,
            allowedCategories: [DocumentCategory.Rulebook, DocumentCategory.Errata],
            sessionAssistant: assistant ?? SessionAssistantSettings.Disabled);

    /// <summary>Una sezione «Assistente in partita» valida per <see cref="Content"/>, che ha «en» come lingua di riserva.</summary>
    internal static SessionAssistantSettings Assistant(bool enabled = true, bool useReserveModel = false) =>
        SessionAssistantSettings.Create(
            enabled,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["en"] = "You are the table assistant.",
                ["it"] = "Sei l'assistente al tavolo.",
            },
            useReserveModel: useReserveModel);

    /// <summary>I modelli che questi test usano, tutti con un prezzo: la regola D5 ha i suoi test, in fondo.</summary>
    private static readonly IReadOnlySet<string> PricedModels =
        new HashSet<string>(StringComparer.Ordinal) { "deepseek-chat", "deepseek-flash", ReserveModel, "un-modello-qualunque" };

    internal static readonly AgentProfileModelCatalog NoToolCallingModels =
        new(PricedModels, new HashSet<string>(StringComparer.Ordinal));

    internal static readonly AgentProfileModelCatalog FlashHasTools =
        new(PricedModels, new HashSet<string>(StringComparer.Ordinal) { "deepseek-flash" });

    /// <summary>
    /// Un catalogo che dà un prezzo proprio ai modelli di <paramref name="content"/>: per i test che
    /// pubblicano modelli di prova per osservarne l'effetto, non la regola D5.
    /// </summary>
    internal static AgentProfileModelCatalog PricingTheModelsOf(
        AgentProfileContent content, IReadOnlySet<string>? toolCallingModels = null)
    {
        var priced = new HashSet<string>(StringComparer.Ordinal) { content.PrimaryModelId };
        if (content.FallbackModelId is { } fallback)
        {
            priced.Add(fallback);
        }

        return new AgentProfileModelCatalog(priced, toolCallingModels ?? new HashSet<string>(StringComparer.Ordinal));
    }

    private static int PublishedCount(AgentProfile p) =>
        p.Versions.Count(v => v.Status == AgentProfileVersionStatus.Published);

    [Fact]
    public void CreateWithPublishedVersion_StartsWithVersionOnePublishedAndNoDraft()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());

        profile.Published.VersionNumber.Should().Be(1);
        profile.Published.Content.Temperature.Should().Be(0.3m);
        profile.Draft.Should().BeNull();
        PublishedCount(profile).Should().Be(1);
    }

    [Fact]
    public void CreateDraft_AddsTheNextVersionAsDraft_AndLeavesThePublishedOneUntouched()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());

        profile.CreateDraft(Content(temperature: 0.5m));

        profile.Draft!.VersionNumber.Should().Be(2);
        profile.Draft.Content.Temperature.Should().Be(0.5m);
        profile.Published.VersionNumber.Should().Be(1);
        profile.Published.Content.Temperature.Should().Be(0.3m);
    }

    [Fact]
    public void CreateDraft_WhenADraftAlreadyExists_IsRejected()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(temperature: 0.5m));

        var act = () => profile.CreateDraft(Content(temperature: 0.7m));

        act.Should().Throw<ConflictException>();
        profile.Versions.Count(v => v.Status == AgentProfileVersionStatus.Draft).Should().Be(1);
    }

    [Fact]
    public void UpdateDraft_ReplacesTheDraftContent()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(temperature: 0.5m));

        profile.UpdateDraft(Content(temperature: 0.9m));

        profile.Draft!.Content.Temperature.Should().Be(0.9m);
        profile.Draft.VersionNumber.Should().Be(2);
    }

    [Fact]
    public void UpdateDraft_WithoutADraft_IsRejected_SoThePublishedVersionCannotBeEdited()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());

        var act = () => profile.UpdateDraft(Content(temperature: 0.9m));

        act.Should().Throw<ConflictException>();
        profile.Published.Content.Temperature.Should().Be(0.3m);
    }

    [Fact]
    public void PublishDraft_PublishesTheDraft_AndArchivesThePreviouslyPublishedVersion()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(temperature: 0.5m));

        profile.PublishDraft(NoToolCallingModels);

        profile.Published.VersionNumber.Should().Be(2);
        profile.Draft.Should().BeNull();
        profile.Versions.Single(v => v.VersionNumber == 1).Status.Should().Be(AgentProfileVersionStatus.Archived);
        PublishedCount(profile).Should().Be(1);
    }

    [Fact]
    public void PublishDraft_WithoutADraft_IsRejected()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());

        var act = () => profile.PublishDraft(NoToolCallingModels);

        act.Should().Throw<ConflictException>();
        profile.Published.VersionNumber.Should().Be(1);
    }

    [Fact]
    public void ArchivedVersion_CannotBeEdited_ThroughANewDraft()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(temperature: 0.5m));
        profile.PublishDraft(NoToolCallingModels);

        profile.CreateDraft(Content(temperature: 0.9m));

        profile.Versions.Single(v => v.VersionNumber == 1).Content.Temperature.Should().Be(0.3m);
        profile.Versions.Single(v => v.VersionNumber == 2).Content.Temperature.Should().Be(0.5m);
        profile.Draft!.VersionNumber.Should().Be(3);
    }

    [Fact]
    public void RollbackTo_CreatesANewPublishedVersion_WithTheArchivedContent()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(temperature: 0.5m));
        profile.PublishDraft(NoToolCallingModels);

        profile.RollbackTo(1, NoToolCallingModels);

        profile.Published.VersionNumber.Should().Be(3);
        profile.Published.Content.Should().Be(profile.Versions.Single(v => v.VersionNumber == 1).Content);
        profile.Versions.Single(v => v.VersionNumber == 1).Status.Should().Be(AgentProfileVersionStatus.Archived);
        profile.Versions.Single(v => v.VersionNumber == 2).Status.Should().Be(AgentProfileVersionStatus.Archived);
        PublishedCount(profile).Should().Be(1);
    }

    [Fact]
    public void RollbackTo_ThePublishedVersion_IsRejected()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());

        var act = () => profile.RollbackTo(1, NoToolCallingModels);

        act.Should().Throw<ConflictException>();
        profile.Versions.Should().HaveCount(1);
    }

    [Fact]
    public void RollbackTo_AnUnknownVersion_IsRejected()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());

        var act = () => profile.RollbackTo(42, NoToolCallingModels);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void RollbackTo_KeepsAnExistingDraft_SoThereIsStillAtMostOne()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(temperature: 0.5m));
        profile.PublishDraft(NoToolCallingModels);
        profile.CreateDraft(Content(temperature: 0.9m));

        profile.RollbackTo(1, NoToolCallingModels);

        profile.Versions.Count(v => v.Status == AgentProfileVersionStatus.Draft).Should().Be(1);
        profile.Draft!.Content.Temperature.Should().Be(0.9m);
        PublishedCount(profile).Should().Be(1);
        profile.Versions.Select(v => v.VersionNumber).Should().OnlyHaveUniqueItems();
    }

    // ─── Spec 2026-10-11 §3.2: l'assistente in partita vuole un modello con tool calling ───

    [Fact]
    public void PublishDraft_WithTheAssistantOnAModelWithoutToolCalling_IsRejected_AndNothingChanges()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(model: "deepseek-flash", assistant: Assistant()));

        var act = () => profile.PublishDraft(NoToolCallingModels);

        act.Should().Throw<ConflictException>().Which.ErrorCode.Should().Be(AgentProfile.AssistantModelWithoutToolsCode);
        profile.Published.VersionNumber.Should().Be(1);
        profile.Draft.Should().NotBeNull("la bozza resta, da correggere");
    }

    [Fact]
    public void PublishDraft_WithTheAssistantOnAToolCallingModel_Publishes()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(model: "deepseek-flash", assistant: Assistant()));

        profile.PublishDraft(FlashHasTools);

        profile.Published.Content.SessionAssistant.Enabled.Should().BeTrue();
    }

    [Fact]
    public void PublishDraft_WithTheAssistantUsingTheReserve_NeedsTheReserveToSupportToolsToo()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(model: "deepseek-flash", assistant: Assistant(useReserveModel: true)));

        var act = () => profile.PublishDraft(FlashHasTools);

        act.Should().Throw<ConflictException>().Which.ErrorCode.Should().Be(AgentProfile.AssistantModelWithoutToolsCode);

        var both = new HashSet<string>(StringComparer.Ordinal) { "deepseek-flash", ReserveModel };
        profile.PublishDraft(new AgentProfileModelCatalog(PricedModels, both));
        profile.Published.Content.SessionAssistant.UseReserveModel.Should().BeTrue();
    }

    [Fact]
    public void PublishDraft_WithTheAssistantUsingAReserveTheProfileDoesNotHave_IsRejected()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(model: "deepseek-flash", assistant: Assistant(useReserveModel: true), fallbackModel: null));

        var act = () => profile.PublishDraft(FlashHasTools);

        act.Should().Throw<ConflictException>().Which.ErrorCode.Should().Be(AgentProfile.AssistantModelWithoutToolsCode);
    }

    [Fact]
    public void PublishDraft_WithTheAssistantDisabled_IgnoresTheCatalog()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(model: "un-modello-qualunque", assistant: Assistant(enabled: false)));

        profile.PublishDraft(NoToolCallingModels);

        profile.Published.Content.PrimaryModelId.Should().Be("un-modello-qualunque");
    }

    [Fact]
    public void RollbackTo_AVersionWhoseModelLostToolCalling_IsRejected()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content(model: "deepseek-flash", assistant: Assistant()));
        profile.CreateDraft(Content());
        profile.PublishDraft(NoToolCallingModels);

        var act = () => profile.RollbackTo(1, NoToolCallingModels);

        act.Should().Throw<ConflictException>().Which.ErrorCode.Should().Be(AgentProfile.AssistantModelWithoutToolsCode);
        profile.Published.VersionNumber.Should().Be(2);

        profile.RollbackTo(1, FlashHasTools);
        profile.Published.VersionNumber.Should().Be(3);
    }

    // ─── ADR-095 D5: un modello senza prezzo non si pubblica ───

    private static readonly IReadOnlySet<string> NoTools = new HashSet<string>(StringComparer.Ordinal);

    [Fact]
    public void PublishDraft_WithAPrimaryModelWithoutAPrice_IsRejected_AndNothingChanges()
    {
        // Senza prezzo ogni risposta finirebbe nel registro dei costi a $0, fuori da ogni tetto.
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(model: "modello-senza-prezzo"));

        var act = () => profile.PublishDraft(NoToolCallingModels);

        act.Should().Throw<ConflictException>().Which.ErrorCode.Should().Be(AgentProfile.ModelWithoutPriceCode);
        profile.Published.VersionNumber.Should().Be(1);
        profile.Draft.Should().NotBeNull("la bozza resta, da correggere");
    }

    [Fact]
    public void PublishDraft_WithAReserveModelWithoutAPrice_IsRejected()
    {
        // La riserva risponde fra la soglia di degrado e il tetto (D6): conta come il principale.
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(fallbackModel: "riserva-senza-prezzo"));

        var act = () => profile.PublishDraft(NoToolCallingModels);

        act.Should().Throw<ConflictException>().Which.ErrorCode.Should().Be(AgentProfile.ModelWithoutPriceCode);
    }

    [Fact]
    public void PublishDraft_WithoutAReserveModel_NeedsOnlyThePrimaryToBePriced()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(fallbackModel: null));

        profile.PublishDraft(new AgentProfileModelCatalog(new HashSet<string>(StringComparer.Ordinal) { "deepseek-chat" }, NoTools));

        profile.Published.VersionNumber.Should().Be(2);
    }

    [Fact]
    public void RollbackTo_AVersionWhoseModelLostItsPrice_IsRejected()
    {
        // Il catalogo può essere cambiato da quando quella versione era pubblicata.
        var profile = AgentProfile.CreateWithPublishedVersion(Content(model: "deepseek-chat"));
        profile.CreateDraft(Content(model: "deepseek-flash"));
        profile.PublishDraft(NoToolCallingModels);
        var chatNoLongerPriced = new AgentProfileModelCatalog(
            new HashSet<string>(StringComparer.Ordinal) { "deepseek-flash", ReserveModel }, NoTools);

        var act = () => profile.RollbackTo(1, chatNoLongerPriced);

        act.Should().Throw<ConflictException>().Which.ErrorCode.Should().Be(AgentProfile.ModelWithoutPriceCode);
        profile.Published.VersionNumber.Should().Be(2);
    }
}
