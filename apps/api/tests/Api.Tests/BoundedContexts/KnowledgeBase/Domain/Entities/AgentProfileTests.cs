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

    internal static readonly IReadOnlySet<string> NoToolCallingModels = new HashSet<string>(StringComparer.Ordinal);

    internal static readonly IReadOnlySet<string> FlashHasTools =
        new HashSet<string>(StringComparer.Ordinal) { "deepseek-flash" };

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
        profile.PublishDraft(both);
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
}
