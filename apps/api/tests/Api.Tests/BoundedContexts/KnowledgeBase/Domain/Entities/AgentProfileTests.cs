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
    internal static AgentProfileContent Content(decimal temperature = 0.3m, string model = "deepseek-chat") =>
        AgentProfileContent.Create(
            name: "MeepleAI",
            description: "Assistente per le regole dei giochi da tavolo",
            persona: "a precise board game rules assistant",
            primaryModelId: model,
            fallbackModelId: "meta-llama/llama-3.3-70b-instruct:free",
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
            allowedCategories: [DocumentCategory.Rulebook, DocumentCategory.Errata]);

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

        profile.PublishDraft();

        profile.Published.VersionNumber.Should().Be(2);
        profile.Draft.Should().BeNull();
        profile.Versions.Single(v => v.VersionNumber == 1).Status.Should().Be(AgentProfileVersionStatus.Archived);
        PublishedCount(profile).Should().Be(1);
    }

    [Fact]
    public void PublishDraft_WithoutADraft_IsRejected()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());

        var act = () => profile.PublishDraft();

        act.Should().Throw<ConflictException>();
        profile.Published.VersionNumber.Should().Be(1);
    }

    [Fact]
    public void ArchivedVersion_CannotBeEdited_ThroughANewDraft()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(temperature: 0.5m));
        profile.PublishDraft();

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
        profile.PublishDraft();

        profile.RollbackTo(1);

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

        var act = () => profile.RollbackTo(1);

        act.Should().Throw<ConflictException>();
        profile.Versions.Should().HaveCount(1);
    }

    [Fact]
    public void RollbackTo_AnUnknownVersion_IsRejected()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());

        var act = () => profile.RollbackTo(42);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void RollbackTo_KeepsAnExistingDraft_SoThereIsStillAtMostOne()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content());
        profile.CreateDraft(Content(temperature: 0.5m));
        profile.PublishDraft();
        profile.CreateDraft(Content(temperature: 0.9m));

        profile.RollbackTo(1);

        profile.Versions.Count(v => v.Status == AgentProfileVersionStatus.Draft).Should().Be(1);
        profile.Draft!.Content.Temperature.Should().Be(0.9m);
        PublishedCount(profile).Should().Be(1);
        profile.Versions.Select(v => v.VersionNumber).Should().OnlyHaveUniqueItems();
    }
}
