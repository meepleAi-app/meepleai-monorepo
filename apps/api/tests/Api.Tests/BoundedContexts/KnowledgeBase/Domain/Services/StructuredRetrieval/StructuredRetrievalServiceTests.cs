using Api.BoundedContexts.KnowledgeBase.Domain.Services.StructuredRetrieval;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.StructuredRetrieval;

/// <summary>
/// Tests for StructuredRetrievalService.
/// Issue #5453: Structured RAG fusion.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StructuredRetrievalServiceTests
{
    private readonly Mock<IRulebookAnalysisRepository> _repository = new();
    private readonly StructuredRetrievalService _sut;
    private static readonly Guid GameId = Guid.NewGuid();

    public StructuredRetrievalServiceTests()
    {
        _sut = new StructuredRetrievalService(
            _repository.Object,
            new StructuredQueryIntentClassifier(),
            NullLogger<StructuredRetrievalService>.Instance);
    }

    #region Victory Conditions Retrieval

    [Fact]
    public async Task RetrieveAsync_VictoryQuery_ReturnsVictoryConditions()
    {
        // Arrange
        var analysis = CreateAnalysisWithVictoryConditions();
        _repository.Setup(r => r.GetBySharedGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis> { analysis });

        // Act
        var result = await _sut.RetrieveAsync("How do you win Catan?", GameId);

        // Assert
        result.Classification.Intent.Should().Be(StructuredQueryIntent.VictoryConditions);
        result.Results.Should().HaveCount(1);
        result.Results[0].Content.Should().Contain("10 victory points");
        result.Results[0].SourceField.Should().Be("VictoryConditions");
    }

    #endregion

    #region Glossary Retrieval

    [Fact]
    public async Task RetrieveAsync_GlossaryQuery_ReturnsMatchingConcept()
    {
        // Arrange
        var analysis = CreateAnalysisWithConcepts();
        _repository.Setup(r => r.GetBySharedGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis> { analysis });

        // Act
        var result = await _sut.RetrieveAsync("What is a settlement?", GameId);

        // Assert
        result.Classification.Intent.Should().Be(StructuredQueryIntent.Glossary);
        result.Results.Should().ContainSingle(r => r.Content.Contains("Settlement"));
    }

    [Fact]
    public async Task RetrieveAsync_GlossaryQuery_NoMatch_ReturnsEmpty()
    {
        // Arrange
        var analysis = CreateAnalysisWithConcepts();
        _repository.Setup(r => r.GetBySharedGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis> { analysis });

        // Act
        var result = await _sut.RetrieveAsync("What is a zeppelin?", GameId);

        // Assert
        result.Results.Should().BeEmpty();
    }

    #endregion

    #region FAQ Retrieval

    [Fact]
    public async Task RetrieveAsync_FaqQuery_ReturnsMatchingFaq()
    {
        // Arrange
        var analysis = CreateAnalysisWithFaqs();
        _repository.Setup(r => r.GetBySharedGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis> { analysis });

        // Act
        var result = await _sut.RetrieveAsync("Can you trade resources with other players?", GameId);

        // Assert
        result.Classification.Intent.Should().Be(StructuredQueryIntent.Faq);
        result.Results.Should().NotBeEmpty();
    }

    #endregion

    #region General Fallback

    [Fact]
    public async Task RetrieveAsync_GeneralQuery_ReturnsEmptyWithNoBypass()
    {
        // Act
        var result = await _sut.RetrieveAsync("Tell me about this game", GameId);

        // Assert
        result.Classification.Intent.Should().Be(StructuredQueryIntent.General);
        result.Results.Should().BeEmpty();
        result.ShouldBypassVector.Should().BeFalse();
        result.StructuredContributionPercent.Should().Be(0.0);
    }

    #endregion

    #region No Active Analysis

    [Fact]
    public async Task RetrieveAsync_NoActiveAnalysis_ReturnsEmpty()
    {
        // Arrange - analysis exists but is NOT active
        var analysis = CreateAnalysisWithVictoryConditions(isActive: false);
        _repository.Setup(r => r.GetBySharedGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis> { analysis });

        // Act
        var result = await _sut.RetrieveAsync("How do you win?", GameId);

        // Assert
        result.Results.Should().BeEmpty();
        result.ShouldBypassVector.Should().BeFalse();
    }

    #endregion

    #region Bypass Logic

    [Fact]
    public async Task RetrieveAsync_HighConfidenceMatch_SetsShouldBypassTrue()
    {
        // Arrange - Exact glossary match → 0.95 confidence → should bypass
        var analysis = CreateAnalysisWithConcepts();
        _repository.Setup(r => r.GetBySharedGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis> { analysis });

        // Act
        var result = await _sut.RetrieveAsync("What is a settlement?", GameId);

        // Assert
        result.ShouldBypassVector.Should().BeTrue();
        result.StructuredContributionPercent.Should().BeGreaterThan(85.0);
    }

    #endregion

    #region Mechanics Retrieval

    [Fact]
    public async Task RetrieveAsync_MechanicsQuery_ReturnsMechanicsAndPhases()
    {
        // Arrange
        var analysis = CreateAnalysisWithMechanics();
        _repository.Setup(r => r.GetBySharedGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis> { analysis });

        // Act
        var result = await _sut.RetrieveAsync("What mechanics does the game use?", GameId);

        // Assert
        result.Classification.Intent.Should().Be(StructuredQueryIntent.Mechanics);
        result.Results.Should().HaveCount(1);
        result.Results[0].Content.Should().Contain("Worker Placement");
        result.Results[0].Content.Should().Contain("Game Phases");
    }

    #endregion

    #region Helpers

    private static RulebookAnalysis CreateAnalysisWithVictoryConditions(bool isActive = true)
    {
        var vc = VictoryConditions.Create("Reach 10 victory points", new List<string> { "Longest road bonus" }, true, 10);
        var analysis = RulebookAnalysis.CreateFromAI(
            GameId, Guid.NewGuid(), "Catan", "Trading and building game",
            new List<string> { "Trading", "Building" }, vc,
            new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.90m, Guid.NewGuid());
        if (isActive) analysis.SetAsActive();
        return analysis;
    }

    private static RulebookAnalysis CreateAnalysisWithConcepts()
    {
        var concepts = new List<KeyConcept>
        {
            new("Settlement", "A basic building placed on intersections", "Buildings"),
            new("Road", "A connection between two intersections", "Infrastructure"),
            new("Development Card", "A card bought with specific resources", "Cards")
        };
        var analysis = RulebookAnalysis.CreateFromAI(
            GameId, Guid.NewGuid(), "Catan", "Trading game",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.85m, Guid.NewGuid(), keyConcepts: concepts);
        analysis.SetAsActive();
        return analysis;
    }

    private static RulebookAnalysis CreateAnalysisWithFaqs()
    {
        var faqs = new List<GeneratedFaq>
        {
            new("Can you trade resources with other players?", "Yes, you can trade on your turn.",
                "Trading", 0.90m, new List<string> { "trading", "resources" }),
            new("How many roads can you build per turn?", "You can build as many as you can afford.",
                "Building", 0.85m, new List<string> { "building", "roads" })
        };
        var analysis = RulebookAnalysis.CreateFromAI(
            GameId, Guid.NewGuid(), "Catan", "Trading game",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.85m, Guid.NewGuid(), generatedFaqs: faqs);
        analysis.SetAsActive();
        return analysis;
    }

    private static RulebookAnalysis CreateAnalysisWithMechanics()
    {
        var phases = new List<GamePhase>
        {
            GamePhase.Create("Resource Production", "Roll dice for resources", 1, false),
            GamePhase.Create("Trading", "Trade with players or bank", 2, true),
            GamePhase.Create("Building", "Spend resources to build", 3, false)
        };
        var analysis = RulebookAnalysis.CreateFromAI(
            GameId, Guid.NewGuid(), "Catan", "Trading game",
            new List<string> { "Worker Placement", "Dice Rolling", "Trading" }, null,
            new List<Resource>(), phases, new List<string>(), 0.85m, Guid.NewGuid());
        analysis.SetAsActive();
        return analysis;
    }

    #endregion
}
