using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentProfileAdmin;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// #4404. The admin page edits Identity, Model, Generation and Prompts; every other section of the version comes
/// from the draft, or from the published version when there is no draft yet. The intent sections are among the
/// ones the page does not edit, so saving a draft must carry them over: a handler that rebuilt the content
/// without them would lose every section the first time anyone pressed «Salva», and nothing would fail.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4404")]
public sealed class SaveAgentProfileDraftKeepsIntentInstructionsTests
{
    private static readonly Dictionary<string, string> Sections = new(StringComparer.Ordinal)
    {
        ["tutor"] = "Teach it like a patient friend.",
        ["secca"] = "One line, then the page.",
    };

    private static SaveAgentProfileDraftCommand Save() => new(
        Name: "MeepleAI",
        Description: "Edited",
        Persona: "a precise board game rules assistant",
        PrimaryModelId: "deepseek-chat",
        FallbackModelId: AgentProfileTests.ReserveModel,
        Temperature: 0.5m,
        MaxResponseTokens: 1200,
        CitationStyle: CitationStyle.PageReferencesInText,
        SystemPrompts: new Dictionary<string, string>(StringComparer.Ordinal) { ["en"] = "You are MeepleAI, edited." },
        FallbackLanguage: "en");

    private static (SaveAgentProfileDraftCommandHandler Handler, AgentProfile Profile) HandlerOver(AgentProfile profile)
    {
        var repository = new Mock<IAgentProfileRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        return (new SaveAgentProfileDraftCommandHandler(repository.Object, new Mock<IUnitOfWork>().Object), profile);
    }

    [Fact]
    public async Task TheFirstDraft_StartsFromThePublishedSections()
    {
        var (handler, profile) = HandlerOver(
            AgentProfile.CreateWithPublishedVersion(AgentProfileTests.Content(intents: Sections)));

        await handler.Handle(Save(), CancellationToken.None);

        profile.Draft!.Content.IntentInstructions.Should().BeEquivalentTo(Sections);
        profile.Draft.Content.Description.Should().Be("Edited", "the edit itself went through");
    }

    [Fact]
    public async Task ASecondSave_KeepsTheSectionsOfTheDraft()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(AgentProfileTests.Content());
        profile.CreateDraft(AgentProfileTests.Content(intents: Sections));
        var (handler, _) = HandlerOver(profile);

        await handler.Handle(Save(), CancellationToken.None);

        profile.Draft!.Content.IntentInstructions.Should().BeEquivalentTo(Sections);
    }

    [Fact]
    public async Task AProfileWithNoSections_StaysWithNone()
    {
        var (handler, profile) = HandlerOver(AgentProfile.CreateWithPublishedVersion(AgentProfileTests.Content()));

        await handler.Handle(Save(), CancellationToken.None);

        profile.Draft!.Content.IntentInstructions.Should().BeEmpty();
    }
}
