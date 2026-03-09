using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Tests for GetGameAnalysisQueryHandler.
/// Issue #5454: Analysis results UI.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GetGameAnalysisQueryHandlerTests
{
    private readonly Mock<IRulebookAnalysisRepository> _repository = new();
    private readonly GetGameAnalysisQueryHandler _sut;
    private static readonly Guid GameId = Guid.NewGuid();

    public GetGameAnalysisQueryHandlerTests()
    {
        _sut = new GetGameAnalysisQueryHandler(_repository.Object);
    }

    [Fact]
    public async Task Handle_WithActiveAnalyses_ReturnsOnlyActive()
    {
        // Arrange
        var active = CreateAnalysis(isActive: true);
        var inactive = CreateAnalysis(isActive: false);
        _repository.Setup(r => r.GetBySharedGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis> { active, inactive });

        // Act
        var result = await _sut.Handle(new GetGameAnalysisQuery(GameId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoAnalyses_ReturnsEmptyList()
    {
        // Arrange
        _repository.Setup(r => r.GetBySharedGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis>());

        // Act
        var result = await _sut.Handle(new GetGameAnalysisQuery(GameId), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_IncludesKeyConcepts()
    {
        // Arrange
        var concepts = new List<KeyConcept>
        {
            new("Settlement", "A building", "Buildings"),
            new("Road", "A path", "Infrastructure")
        };
        var analysis = CreateAnalysis(isActive: true, keyConcepts: concepts);
        _repository.Setup(r => r.GetBySharedGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis> { analysis });

        // Act
        var result = await _sut.Handle(new GetGameAnalysisQuery(GameId), CancellationToken.None);

        // Assert
        result[0].KeyConcepts.Should().HaveCount(2);
        result[0].KeyConcepts[0].Term.Should().Be("Settlement");
    }

    [Fact]
    public async Task Handle_IncludesGeneratedFaqs()
    {
        // Arrange
        var faqs = new List<GeneratedFaq>
        {
            new("Can you trade?", "Yes", "Trading", 0.9m, new List<string> { "trade" })
        };
        var analysis = CreateAnalysis(isActive: true, generatedFaqs: faqs);
        _repository.Setup(r => r.GetBySharedGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis> { analysis });

        // Act
        var result = await _sut.Handle(new GetGameAnalysisQuery(GameId), CancellationToken.None);

        // Assert
        result[0].GeneratedFaqs.Should().HaveCount(1);
        result[0].GeneratedFaqs[0].Question.Should().Be("Can you trade?");
        result[0].GeneratedFaqs[0].Confidence.Should().Be(0.9m);
    }

    [Fact]
    public async Task Handle_IncludesCompletionStatus()
    {
        // Arrange
        var analysis = CreateAnalysis(isActive: true);
        analysis.MarkAsPartiallyComplete(new List<string> { "VictoryConditions" });
        _repository.Setup(r => r.GetBySharedGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis> { analysis });

        // Act
        var result = await _sut.Handle(new GetGameAnalysisQuery(GameId), CancellationToken.None);

        // Assert
        result[0].CompletionStatus.Should().Be("PartiallyComplete");
        result[0].MissingSections.Should().Contain("VictoryConditions");
    }

    [Fact]
    public async Task Handle_IncludesGameStateSchemaJson()
    {
        // Arrange
        var schema = """{"type":"object","properties":{"score":{"type":"number"}}}""";
        var analysis = CreateAnalysis(isActive: true, gameStateSchema: schema);
        _repository.Setup(r => r.GetBySharedGameIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis> { analysis });

        // Act
        var result = await _sut.Handle(new GetGameAnalysisQuery(GameId), CancellationToken.None);

        // Assert
        result[0].GameStateSchemaJson.Should().Be(schema);
    }

    private static RulebookAnalysis CreateAnalysis(
        bool isActive,
        List<KeyConcept>? keyConcepts = null,
        List<GeneratedFaq>? generatedFaqs = null,
        string? gameStateSchema = null)
    {
        var analysis = RulebookAnalysis.CreateFromAI(
            GameId, Guid.NewGuid(), "Test Game", "A test game",
            new List<string> { "Trading" }, null,
            new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.85m, Guid.NewGuid(),
            keyConcepts: keyConcepts,
            generatedFaqs: generatedFaqs,
            gameStateSchemaJson: gameStateSchema);
        if (isActive) analysis.SetAsActive();
        return analysis;
    }
}
