using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Constants;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure.BackgroundTasks;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Unit tests for AnalyzeRulebookCommandHandler.
/// Issue #2402: Rulebook Analysis Service
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class AnalyzeRulebookCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _gameRepositoryMock;
    private readonly Mock<IRulebookAnalysisRepository> _analysisRepositoryMock;
    private readonly Mock<IRulebookAnalyzer> _rulebookAnalyzerMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<AnalyzeRulebookCommandHandler>> _loggerMock;
    private readonly AnalyzeRulebookCommandHandler _handler;

    public AnalyzeRulebookCommandHandlerTests()
    {
        _gameRepositoryMock = new Mock<ISharedGameRepository>();
        _analysisRepositoryMock = new Mock<IRulebookAnalysisRepository>();
        _rulebookAnalyzerMock = new Mock<IRulebookAnalyzer>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<AnalyzeRulebookCommandHandler>>();

        var mockBackgroundOrchestrator = new Mock<IBackgroundRulebookAnalysisOrchestrator>();
        var mockTaskOrchestrator = new Mock<IBackgroundTaskOrchestrator>();
        var mockHybridCache = new Mock<IHybridCacheService>();
        var mockOptions = Microsoft.Extensions.Options.Options.Create(new BackgroundAnalysisOptions());

        _handler = new AnalyzeRulebookCommandHandler(
            _gameRepositoryMock.Object,
            _analysisRepositoryMock.Object,
            _rulebookAnalyzerMock.Object,
            mockBackgroundOrchestrator.Object,
            mockTaskOrchestrator.Object,
            _unitOfWorkMock.Object,
            mockHybridCache.Object,
            mockOptions,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesAnalysisSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var game = SharedGame.Create(
            title: "Catan",
            yearPublished: 1995,
            description: "Trading game",
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 7.8m,
            imageUrl: "https://example.com/catan.jpg",
            thumbnailUrl: "https://example.com/catan_thumb.jpg",
            rules: null,
            createdBy: userId,
            bggId: 13);

        var pdfDocumentId = Guid.NewGuid();
        var command = new AnalyzeRulebookCommand(pdfDocumentId, game.Id, userId);

        var analysisResult = new RulebookAnalysisResult(
            GameTitle: "Catan",
            Summary: "Resource management game",
            KeyMechanics: new List<string> { "Trading", "Dice Rolling" },
            VictoryConditions: VictoryConditions.Create("First to 10 points", isPointBased: true, targetPoints: 10),
            Resources: new List<Resource> { Resource.Create("Wood", "Material") },
            GamePhases: new List<GamePhase> { GamePhase.Create("Roll", "Roll dice", 1) },
            CommonQuestions: new List<string> { "How do I trade?" },
            ConfidenceScore: 0.92m);

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _analysisRepositoryMock
            .Setup(r => r.GetPdfTextAsync(pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Rulebook content here");

        _rulebookAnalyzerMock
            .Setup(a => a.AnalyzeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysisResult);

        _analysisRepositoryMock
            .Setup(r => r.GetByPdfDocumentIdAsync(pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis>());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Analysis.Should().NotBeNull();
        result.Analysis!.GameTitle.Should().Be("Catan");
        result.Analysis.ConfidenceScore.Should().Be(0.92m);
        result.Analysis.IsActive.Should().BeTrue();
        result.Analysis.Version.Should().Be("1.0");

        _analysisRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<RulebookAnalysis>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new AnalyzeRulebookCommand(pdfDocumentId, gameId, userId);

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken));

        _analysisRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<RulebookAnalysis>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithExistingAnalyses_IncrementsVersionCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var game = SharedGame.Create(
            title: "Catan",
            yearPublished: 1995,
            description: "Trading game",
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 7.8m,
            imageUrl: "https://example.com/catan.jpg",
            thumbnailUrl: "https://example.com/catan_thumb.jpg",
            rules: null,
            createdBy: userId,
            bggId: 13);

        var pdfDocumentId = Guid.NewGuid();

        // Existing analysis version 1.0
        var existingAnalysis = RulebookAnalysis.CreateFromAI(
            game.Id,
            pdfDocumentId,
            "Catan",
            "Old summary",
            new List<string>(),
            null,
            new List<Resource>(),
            new List<GamePhase>(),
            new List<string>(),
            0.8m,
            userId,
            "1.0");

        var analysisResult = new RulebookAnalysisResult(
            GameTitle: "Catan",
            Summary: "Updated summary",
            KeyMechanics: new List<string>(),
            VictoryConditions: null,
            Resources: new List<Resource>(),
            GamePhases: new List<GamePhase>(),
            CommonQuestions: new List<string>(),
            ConfidenceScore: 0.95m);

        var command = new AnalyzeRulebookCommand(pdfDocumentId, game.Id, userId);

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _analysisRepositoryMock
            .Setup(r => r.GetByPdfDocumentIdAsync(pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis> { existingAnalysis });

        _analysisRepositoryMock
            .Setup(r => r.GetPdfTextAsync(pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Updated rulebook content");

        _rulebookAnalyzerMock
            .Setup(a => a.AnalyzeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysisResult);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Analysis.Should().NotBeNull();
        result.Analysis!.Version.Should().Be("1.1"); // Incremented from 1.0
        result.Analysis.IsActive.Should().BeTrue();

        _analysisRepositoryMock.Verify(
            r => r.DeactivateAllAsync(game.Id, pdfDocumentId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #region Sync/Async Routing (Issue #2525)

    [Fact]
    public async Task Handle_WithSmallRulebook_UsesSynchronousPath()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var game = CreateTestGame(userId);
        var pdfDocumentId = Guid.NewGuid();
        var smallRulebookContent = new string('A', 25000); // < 30k threshold

        var command = new AnalyzeRulebookCommand(pdfDocumentId, game.Id, userId);
        var analysisResult = CreateTestAnalysisResult();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _analysisRepositoryMock
            .Setup(r => r.GetPdfTextAsync(pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(smallRulebookContent);

        _analysisRepositoryMock
            .Setup(r => r.GetByPdfDocumentIdAsync(pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis>());

        _rulebookAnalyzerMock
            .Setup(a => a.AnalyzeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysisResult);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Synchronous response
        result.Should().NotBeNull();
        result.IsBackgroundTask.Should().BeFalse();
        result.Analysis.Should().NotBeNull();
        result.TaskId.Should().BeNull();

        // Verify synchronous analyzer was called
        _rulebookAnalyzerMock.Verify(
            a => a.AnalyzeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Document Category Gate (Issue #5443)

    [Theory]
    [InlineData("QuickStart")]
    [InlineData("Reference")]
    [InlineData("PlayerAid")]
    [InlineData("Other")]
    public async Task Handle_WithNonAnalyzableCategory_ThrowsConflictException(string category)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var game = CreateTestGame(userId);
        var pdfDocumentId = Guid.NewGuid();
        var command = new AnalyzeRulebookCommand(pdfDocumentId, game.Id, userId);

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _analysisRepositoryMock
            .Setup(r => r.GetPdfDocumentCategoryAsync(pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken));

        ex.Message.Should().Contain("not eligible for rulebook analysis");
        ex.Message.Should().Contain(category);

        // Verify analysis was never started
        _rulebookAnalyzerMock.Verify(
            a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("Rulebook")]
    [InlineData("Expansion")]
    [InlineData("Errata")]
    public async Task Handle_WithAnalyzableCategory_ProceedsToAnalysis(string category)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var game = CreateTestGame(userId);
        var pdfDocumentId = Guid.NewGuid();
        var command = new AnalyzeRulebookCommand(pdfDocumentId, game.Id, userId);
        var analysisResult = CreateTestAnalysisResult();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _analysisRepositoryMock
            .Setup(r => r.GetPdfDocumentCategoryAsync(pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        _analysisRepositoryMock
            .Setup(r => r.GetPdfTextAsync(pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Rulebook content");

        _analysisRepositoryMock
            .Setup(r => r.GetByPdfDocumentIdAsync(pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis>());

        _rulebookAnalyzerMock
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysisResult);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - analysis proceeded
        result.Should().NotBeNull();
        result.Analysis.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithNullCategory_ProceedsToAnalysis()
    {
        // Arrange — legacy documents without category should still be analyzed
        var userId = Guid.NewGuid();
        var game = CreateTestGame(userId);
        var pdfDocumentId = Guid.NewGuid();
        var command = new AnalyzeRulebookCommand(pdfDocumentId, game.Id, userId);
        var analysisResult = CreateTestAnalysisResult();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _analysisRepositoryMock
            .Setup(r => r.GetPdfDocumentCategoryAsync(pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _analysisRepositoryMock
            .Setup(r => r.GetPdfTextAsync(pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Rulebook content");

        _analysisRepositoryMock
            .Setup(r => r.GetByPdfDocumentIdAsync(pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RulebookAnalysis>());

        _rulebookAnalyzerMock
            .Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysisResult);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - analysis proceeded (backward compat)
        result.Should().NotBeNull();
        result.Analysis.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private static SharedGame CreateTestGame(Guid userId)
    {
        return SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test description",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.0m,
            averageRating: 7.0m,
            imageUrl: "https://example.com/game.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: userId,
            bggId: 1);
    }

    private static RulebookAnalysisResult CreateTestAnalysisResult()
    {
        return new RulebookAnalysisResult(
            GameTitle: "Test Game",
            Summary: "Test summary",
            KeyMechanics: ["Strategy"],
            VictoryConditions: VictoryConditions.Create("First to win", alternatives: null, isPointBased: true, targetPoints: 10),
            Resources: [Resource.Create("Gold", "Currency")],
            GamePhases: [GamePhase.Create("Action", "Take action", 1)],
            CommonQuestions: ["How to play?"],
            ConfidenceScore: 0.85m);
    }

    #endregion
}
