using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Rules;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Rules;

/// <summary>#4411 (A5): cerca_regole con l'accesso e il tier della chat sulle regole (spec §4.2).</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4411")]
public sealed class GameRuleSourceTests
{
    private static readonly Guid Game = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
    private static readonly Guid Host = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

    private readonly Mock<IAnswerPreconditions> _preconditions = new();
    private readonly Mock<ITieredPassageSearch> _passages = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IAssistantRuleSource Source(string? role = "User", string language = "it") =>
        new AssistantRuleSourceFactory(_preconditions.Object, _passages.Object)
            .Create(new AssistantRuleScope(Game, Host, role, AnswerPipelineTestDoubles.SeedContent(), language));

    [Fact]
    public async Task Search_WithoutAccess_IsNotAccessible_AndDoesNotRetrieve()
    {
        _preconditions.Setup(p => p.EnsureCanAskAboutGameAsync(Host, "User", Game, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RagOwnershipRequiredException(Game));

        var search = await Source().SearchAsync("quante tessere?", Ct);

        search.Should().Be(AssistantRuleSearch.NotAccessible);
        _passages.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Search_GivesThePassagesFromTheCitations_InTheLanguageOfTheTurn()
    {
        var citation = new ChunkCitation("pdf-1", 3, 0.9f, "Si pescano 4 tessere.", CopyrightTier.Protected);
        _passages.Setup(p => p.SearchAsync(
                It.Is<AnswerRetrievalRequest>(r => r.GameId == Game && r.Question == "quante tessere?"),
                It.IsAny<AgentProfileContent>(), "en", Host, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TieredPassages([], [citation]));

        var search = await Source(language: "en").SearchAsync("quante tessere?", Ct);

        search.Passages.Should().Equal(new AssistantRulePassage("Si pescano 4 tessere.", 3));
        search.Citations.Should().Equal(citation);
        search.Accessible.Should().BeTrue();
    }

    // Review Focus 4 del piano 3: un host senza claim di ruolo vale come utente, e la decisione è del passo condiviso.
    [Fact]
    public async Task Search_PassesTheRoleAsItCame_EvenWhenAbsent()
    {
        _passages.Setup(p => p.SearchAsync(It.IsAny<AnswerRetrievalRequest>(),
                It.IsAny<AgentProfileContent>(), It.IsAny<string>(), Host, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TieredPassages.None);

        await Source(role: null).SearchAsync("quante tessere?", Ct);

        _preconditions.Verify(p => p.EnsureCanAskAboutGameAsync(Host, null, Game, It.IsAny<CancellationToken>()), Times.Once);
    }
}
