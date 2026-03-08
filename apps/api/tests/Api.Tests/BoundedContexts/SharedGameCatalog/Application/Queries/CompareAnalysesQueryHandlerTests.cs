using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Unit tests for CompareAnalysesQueryHandler.
/// Issue #5461: Analysis comparison tool.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CompareAnalysesQueryHandlerTests
{
    private readonly Mock<IRulebookAnalysisRepository> _repositoryMock;
    private readonly CompareAnalysesQueryHandler _handler;

    private static readonly Guid SharedGameId = Guid.NewGuid();
    private static readonly Guid PdfDocumentId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    public CompareAnalysesQueryHandlerTests()
    {
        _repositoryMock = new Mock<IRulebookAnalysisRepository>();
        _handler = new CompareAnalysesQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_LeftAnalysisNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var leftId = Guid.NewGuid();
        var rightId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(leftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RulebookAnalysis?)null);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(rightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAnalysis(mechanics: new List<string> { "A" }));

        var query = new CompareAnalysesQuery(leftId, rightId);

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{leftId}*");
    }

    [Fact]
    public async Task Handle_RightAnalysisNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var leftId = Guid.NewGuid();
        var rightId = Guid.NewGuid();

        var leftAnalysis = CreateAnalysis(mechanics: new List<string> { "A" });

        _repositoryMock
            .Setup(r => r.GetByIdAsync(leftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(leftAnalysis);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(rightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RulebookAnalysis?)null);

        var query = new CompareAnalysesQuery(leftId, rightId);

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{rightId}*");
    }

    [Fact]
    public async Task Handle_IdenticalAnalyses_ReturnsNoDiffs()
    {
        // Arrange
        var leftId = Guid.NewGuid();
        var rightId = Guid.NewGuid();

        var mechanics = new List<string> { "Worker Placement", "Deck Building" };
        var questions = new List<string> { "How to win?", "How many players?" };
        var concepts = new List<KeyConcept>
        {
            new("Meeple", "A wooden figure representing a worker", "Components"),
            new("Victory Points", "Points used to determine the winner", "Scoring")
        };
        var faqs = new List<GeneratedFaq>
        {
            new("What is the goal?", "Score the most points", "Overview", 0.9m, new List<string> { "basics" }),
            new("How many rounds?", "5 rounds per game", "Setup", 0.85m, new List<string> { "setup" })
        };

        var leftAnalysis = CreateAnalysis(
            mechanics: mechanics,
            questions: questions,
            keyConcepts: concepts,
            generatedFaqs: faqs,
            summary: "A strategy game",
            confidenceScore: 0.8m);
        var rightAnalysis = CreateAnalysis(
            mechanics: new List<string>(mechanics),
            questions: new List<string>(questions),
            keyConcepts: new List<KeyConcept>(concepts),
            generatedFaqs: new List<GeneratedFaq>(faqs),
            summary: "A strategy game",
            confidenceScore: 0.8m);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(leftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(leftAnalysis);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(rightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rightAnalysis);

        var query = new CompareAnalysesQuery(leftId, rightId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.MechanicsDiff.Added.Should().BeEmpty();
        result.MechanicsDiff.Removed.Should().BeEmpty();
        result.MechanicsDiff.Unchanged.Should().HaveCount(2);

        result.CommonQuestionsDiff.Added.Should().BeEmpty();
        result.CommonQuestionsDiff.Removed.Should().BeEmpty();
        result.CommonQuestionsDiff.Unchanged.Should().HaveCount(2);

        result.KeyConceptsDiff.Added.Should().BeEmpty();
        result.KeyConceptsDiff.Removed.Should().BeEmpty();
        result.KeyConceptsDiff.Unchanged.Should().HaveCount(2);

        result.FaqDiff.Added.Should().BeEmpty();
        result.FaqDiff.Removed.Should().BeEmpty();
        result.FaqDiff.Modified.Should().BeEmpty();
        result.FaqDiff.Unchanged.Should().HaveCount(2);

        result.SummaryChanged.Should().BeFalse();
        result.ConfidenceScoreDelta.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_DifferentMechanics_ReturnsCorrectDiff()
    {
        // Arrange
        var leftId = Guid.NewGuid();
        var rightId = Guid.NewGuid();

        var leftAnalysis = CreateAnalysis(mechanics: new List<string> { "A", "B" });
        var rightAnalysis = CreateAnalysis(mechanics: new List<string> { "B", "C" });

        _repositoryMock
            .Setup(r => r.GetByIdAsync(leftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(leftAnalysis);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(rightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rightAnalysis);

        var query = new CompareAnalysesQuery(leftId, rightId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.MechanicsDiff.Added.Should().ContainSingle().Which.Should().Be("C");
        result.MechanicsDiff.Removed.Should().ContainSingle().Which.Should().Be("A");
        result.MechanicsDiff.Unchanged.Should().ContainSingle().Which.Should().Be("B");
    }

    [Fact]
    public async Task Handle_DifferentFaqs_ReturnsAddedRemovedModified()
    {
        // Arrange
        var leftId = Guid.NewGuid();
        var rightId = Guid.NewGuid();

        var leftFaqs = new List<GeneratedFaq>
        {
            new("Shared question", "Old answer", "Section1", 0.8m, new List<string> { "tag1" }),
            new("Removed question", "Some answer", "Section2", 0.7m, new List<string> { "tag2" }),
            new("Unchanged question", "Same answer", "Section3", 0.9m, new List<string> { "tag3" })
        };

        var rightFaqs = new List<GeneratedFaq>
        {
            new("Shared question", "New answer", "Section1", 0.85m, new List<string> { "tag1" }),
            new("Added question", "Brand new answer", "Section4", 0.75m, new List<string> { "tag4" }),
            new("Unchanged question", "Same answer", "Section3", 0.9m, new List<string> { "tag3" })
        };

        var leftAnalysis = CreateAnalysis(generatedFaqs: leftFaqs);
        var rightAnalysis = CreateAnalysis(generatedFaqs: rightFaqs);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(leftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(leftAnalysis);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(rightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rightAnalysis);

        var query = new CompareAnalysesQuery(leftId, rightId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.FaqDiff.Added.Should().ContainSingle()
            .Which.Question.Should().Be("Added question");

        result.FaqDiff.Removed.Should().ContainSingle()
            .Which.Question.Should().Be("Removed question");

        result.FaqDiff.Modified.Should().ContainSingle();
        var modified = result.FaqDiff.Modified[0];
        modified.Question.Should().Be("Shared question");
        modified.LeftAnswer.Should().Be("Old answer");
        modified.RightAnswer.Should().Be("New answer");
        modified.LeftConfidence.Should().Be(0.8m);
        modified.RightConfidence.Should().Be(0.85m);

        result.FaqDiff.Unchanged.Should().ContainSingle()
            .Which.Question.Should().Be("Unchanged question");
    }

    [Fact]
    public async Task Handle_SummaryChanged_FlagsCorrectly()
    {
        // Arrange
        var leftId = Guid.NewGuid();
        var rightId = Guid.NewGuid();

        var leftAnalysis = CreateAnalysis(summary: "Original summary of the game");
        var rightAnalysis = CreateAnalysis(summary: "Updated summary with new details");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(leftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(leftAnalysis);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(rightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rightAnalysis);

        var query = new CompareAnalysesQuery(leftId, rightId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.SummaryChanged.Should().BeTrue();
        result.LeftSummary.Should().Be("Original summary of the game");
        result.RightSummary.Should().Be("Updated summary with new details");
    }

    [Fact]
    public async Task Handle_ConfidenceScoreDelta_CalculatedCorrectly()
    {
        // Arrange
        var leftId = Guid.NewGuid();
        var rightId = Guid.NewGuid();

        var leftAnalysis = CreateAnalysis(confidenceScore: 0.7m);
        var rightAnalysis = CreateAnalysis(confidenceScore: 0.9m);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(leftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(leftAnalysis);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(rightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rightAnalysis);

        var query = new CompareAnalysesQuery(leftId, rightId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ConfidenceScoreDelta.Should().Be(0.2m);
    }

    [Theory]
    [InlineData(0.9, 0.3, -0.6)]
    [InlineData(0.5, 0.5, 0.0)]
    [InlineData(0.0, 1.0, 1.0)]
    public async Task Handle_ConfidenceScoreDelta_VariousScenarios(
        double leftScore, double rightScore, double expectedDelta)
    {
        // Arrange
        var leftId = Guid.NewGuid();
        var rightId = Guid.NewGuid();

        var leftAnalysis = CreateAnalysis(confidenceScore: (decimal)leftScore);
        var rightAnalysis = CreateAnalysis(confidenceScore: (decimal)rightScore);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(leftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(leftAnalysis);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(rightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rightAnalysis);

        var query = new CompareAnalysesQuery(leftId, rightId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ConfidenceScoreDelta.Should().Be((decimal)expectedDelta);
    }

    [Fact]
    public async Task Handle_KeyConceptsDiff_ComparesByTerm()
    {
        // Arrange
        var leftId = Guid.NewGuid();
        var rightId = Guid.NewGuid();

        var leftConcepts = new List<KeyConcept>
        {
            new("Meeple", "A wooden worker piece", "Components"),
            new("Victory Points", "Points to win", "Scoring")
        };

        var rightConcepts = new List<KeyConcept>
        {
            new("Victory Points", "Points to win", "Scoring"),
            new("Resource", "Materials used for building", "Economy")
        };

        var leftAnalysis = CreateAnalysis(keyConcepts: leftConcepts);
        var rightAnalysis = CreateAnalysis(keyConcepts: rightConcepts);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(leftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(leftAnalysis);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(rightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rightAnalysis);

        var query = new CompareAnalysesQuery(leftId, rightId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        // KeyConcepts are compared as "Term: Definition" strings
        result.KeyConceptsDiff.Added.Should().ContainSingle()
            .Which.Should().Contain("Resource");
        result.KeyConceptsDiff.Removed.Should().ContainSingle()
            .Which.Should().Contain("Meeple");
        result.KeyConceptsDiff.Unchanged.Should().ContainSingle()
            .Which.Should().Contain("Victory Points");
    }

    /// <summary>
    /// Helper to create a RulebookAnalysis using the factory method with sensible defaults.
    /// </summary>
    private static RulebookAnalysis CreateAnalysis(
        List<string>? mechanics = null,
        List<string>? questions = null,
        List<KeyConcept>? keyConcepts = null,
        List<GeneratedFaq>? generatedFaqs = null,
        string summary = "Default game summary",
        decimal confidenceScore = 0.8m)
    {
        return RulebookAnalysis.CreateFromAI(
            sharedGameId: SharedGameId,
            pdfDocumentId: PdfDocumentId,
            gameTitle: "Test Game",
            summary: summary,
            keyMechanics: mechanics ?? new List<string>(),
            victoryConditions: null,
            resources: new List<Resource>(),
            gamePhases: new List<GamePhase>(),
            commonQuestions: questions ?? new List<string>(),
            confidenceScore: confidenceScore,
            createdBy: CreatedBy,
            keyConcepts: keyConcepts,
            generatedFaqs: generatedFaqs);
    }
}
