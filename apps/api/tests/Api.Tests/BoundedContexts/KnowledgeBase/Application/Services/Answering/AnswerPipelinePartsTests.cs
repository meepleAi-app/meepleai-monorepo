using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Api.SharedKernel.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4168 (ADR-095 D3): i pezzi della pipeline di risposta che hanno una regola propria — la lingua,
/// il prompt, la lettura del profilo, il controllo all'avvio. L'effetto di ogni campo del profilo
/// sulla risposta vera è in <c>AgentProfileFieldEffectTests</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4168")]
public sealed class AnswerPipelinePartsTests
{
    private readonly LanguageDetectionService _detector = new(NullLogger<LanguageDetectionService>.Instance);

    // ─── Rilevamento con certezza ───────────────────────────────────────────────

    [Theory]
    [InlineData("Setup?")]
    [InlineData("Catan?")]
    [InlineData("")]
    public void Detect_TooShortOrEmpty_IsNotConfident(string text)
    {
        _detector.Detect(text).IsConfident.Should().BeFalse();
    }

    [Theory]
    [InlineData("Quante carte pesca ogni giocatore durante la preparazione della partita?", "it")]
    [InlineData("How many cards does each player draw when the game starts?", "en")]
    [InlineData("Wie viele Karten zieht jeder Spieler, wenn das Spiel beginnt?", "de")]
    public void Detect_AClearSentence_IsConfident(string text, string language)
    {
        _detector.Detect(text).Should().Be(new LanguageDetection(language, IsConfident: true));
    }

    [Fact]
    public async Task DetectLanguageAsync_KeepsItsAnswer_ForTheSameText()
    {
        const string text = "Quante carte pesca ogni giocatore durante la preparazione della partita?";

        (await _detector.DetectLanguageAsync(text)).Should().Be(_detector.Detect(text).Language);
    }

    // ─── Lingua della risposta ──────────────────────────────────────────────────

    [Fact]
    public void Resolve_AConfidentDetection_WinsOverAcceptLanguage()
    {
        var language = AnswerLanguageResolver.Resolve(
            _detector, "Quante carte pesca ogni giocatore durante la preparazione della partita?", "en-US,en;q=0.9");

        language.Should().Be(new AnswerLanguage("it", AnswerLanguageSource.Detected));
    }

    [Theory]
    [InlineData("it-IT,it;q=0.9,en;q=0.8", "it")]
    [InlineData("en;q=0.5, de;q=0.9", "de")]
    [InlineData("pt-BR,fr;q=0.7", "fr")]
    [InlineData("FR", "fr")]
    public void Resolve_AShortQuestion_UsesTheBestSupportedAcceptLanguage(string header, string expected)
    {
        AnswerLanguageResolver.Resolve(_detector, "Setup?", header)
            .Should().Be(new AnswerLanguage(expected, AnswerLanguageSource.AcceptLanguage));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pt-BR,ja;q=0.8")]
    [InlineData(";;;q=abc")]
    public void Resolve_AShortQuestion_WithoutAUsableHeader_KeepsTheDetectorDefault(string? header)
    {
        AnswerLanguageResolver.Resolve(_detector, "Setup?", header)
            .Should().Be(new AnswerLanguage("en", AnswerLanguageSource.DetectedUncertain));
    }

    // ─── Prompt ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_UsesThePromptOfTheAnswerLanguage_WhenTheProfileHasIt()
    {
        var prompt = AnswerPromptBuilder.Build(
            Content(prompts: new() { ["en"] = "EN prompt", ["it"] = "IT prompt" }),
            "it", "[Page 3] text", "Domanda?", null, null, hasVerifiedRules: false);

        prompt.PromptLanguage.Should().Be("it");
        prompt.SystemPrompt.Should().StartWith("IT prompt");
        prompt.SystemPrompt.Should().EndWith(AnswerPromptBuilder.AnswerLanguageInstruction("it"));
    }

    [Fact]
    public void Build_FallsBackToTheFallbackLanguagePrompt_AndStillAnswersInTheUserLanguage()
    {
        var prompt = AnswerPromptBuilder.Build(
            Content(prompts: new() { ["en"] = "EN prompt" }),
            "de", "[Page 3] text", "Frage?", null, null, hasVerifiedRules: false);

        prompt.PromptLanguage.Should().Be("en");
        prompt.SystemPrompt.Should().StartWith("EN prompt");
        prompt.SystemPrompt.Should().Contain("Write the answer in German.");
    }

    [Fact]
    public void Build_PutsContextAndQuestionInTheUserPrompt_AndTheContinuationBeforeThem()
    {
        var prompt = AnswerPromptBuilder.Build(
            Content(), "en", "[Page 3] Each player draws five cards.", "How many cards?", "continuation", "Each player", false);

        prompt.UserPrompt.Should().StartWith("PREVIOUS PARTIAL ANSWER (continue from here):\nEach player\n\n");
        prompt.UserPrompt.Should().Contain("CONTEXT FROM RULEBOOK:\n[Page 3] Each player draws five cards.");
        prompt.UserPrompt.Should().EndWith("QUESTION:\nHow many cards?\n\nANSWER:");
        prompt.SystemPrompt.Should().Contain(AnswerPromptBuilder.ResponseStyleInstruction("continuation"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Build_AuthorisesTheVerifiedRules_OnlyWhenThereAreSome(bool hasVerifiedRules)
    {
        var prompt = AnswerPromptBuilder.Build(Content(), "en", "ctx", "q?", null, null, hasVerifiedRules);

        prompt.SystemPrompt.Contains(AnswerPromptBuilder.VerifiedRulesInstruction, StringComparison.Ordinal)
            .Should().Be(hasVerifiedRules);
    }

    [Fact]
    public void ResponseStyle_Concise_DoesNotTalkAboutCitations()
    {
        // Prima della pipeline lo stile «concise» diceva di non citare le pagine, contraddicendo
        // lo stile delle citazioni del profilo: ora le citazioni le decide solo il profilo.
        AnswerPromptBuilder.ResponseStyleInstruction(null).Should().NotContainAny("page", "cite", "citation");
    }

    // ─── Lettura del profilo e controllo all'avvio ──────────────────────────────

    [Fact]
    public async Task Reader_WithoutAProfile_ThrowsTheExplicitError()
    {
        var repository = new Mock<IAgentProfileRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((AgentProfile?)null);

        var act = () => new PublishedAgentProfileReader(repository.Object).GetAsync(CancellationToken.None);

        await act.Should().ThrowAsync<AgentProfileNotPublishedException>();
    }

    [Fact]
    public async Task Reader_ReturnsThePublishedVersion_NotTheDraft()
    {
        var profile = AgentProfile.CreateWithPublishedVersion(Content(temperature: 0.3m));
        profile.CreateDraft(Content(temperature: 0.9m));
        var repository = new Mock<IAgentProfileRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var published = await new PublishedAgentProfileReader(repository.Object).GetAsync(CancellationToken.None);

        published.VersionId.Should().Be(profile.Published.Id);
        published.VersionNumber.Should().Be(1);
        published.Content.Temperature.Should().Be(0.3m);
    }

    [Fact]
    public async Task StartupCheck_WithoutAProfile_LogsAnError()
    {
        var repository = new Mock<IAgentProfileRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((AgentProfile?)null);
        var logger = new Mock<ILogger>();

        var ok = await AgentProfileStartupCheck.LogIfMissingAsync(repository.Object, logger.Object);

        ok.Should().BeFalse();
        logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(AgentProfileNotPublishedException.Code)),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task StartupCheck_WithAProfile_DoesNotLogAnError()
    {
        var repository = new Mock<IAgentProfileRepository>();
        repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentProfile.CreateWithPublishedVersion(Content()));
        var logger = new Mock<ILogger>();

        var ok = await AgentProfileStartupCheck.LogIfMissingAsync(repository.Object, logger.Object);

        ok.Should().BeTrue();
        logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    private static AgentProfileContent Content(
        Dictionary<string, string>? prompts = null, decimal temperature = 0.3m) =>
        AgentProfileContent.Create(
            "MeepleAI",
            "Test profile",
            "a precise board game rules assistant",
            "deepseek-flash",
            "meta-llama/llama-3.3-70b-instruct",
            temperature,
            1500,
            CitationStyle.PageReferencesInText,
            prompts ?? new Dictionary<string, string> { ["en"] = "EN prompt" },
            "en",
            20,
            5,
            0.55m,
            0.7m,
            0.3m,
            true,
            Enum.GetValues<DocumentCategory>());
}
