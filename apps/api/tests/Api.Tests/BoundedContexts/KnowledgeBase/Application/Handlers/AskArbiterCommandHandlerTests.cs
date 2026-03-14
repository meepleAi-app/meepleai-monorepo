using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for AskArbiterCommandHandler.
/// Issue #5585: Arbiter Mode — dispute arbitration with citations and verdict.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5585")]
public sealed class AskArbiterCommandHandlerTests : IDisposable
{
    private readonly Mock<IAgentRepository> _mockAgentRepository;
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<ILogger<AskArbiterCommandHandler>> _mockLogger;
    private readonly MeepleAiDbContext _dbContext;
    private readonly AskArbiterCommandHandler _handler;
    private readonly Guid _agentId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public AskArbiterCommandHandlerTests()
    {
        _mockAgentRepository = new Mock<IAgentRepository>();
        _mockLlmService = new Mock<ILlmService>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockLogger = new Mock<ILogger<AskArbiterCommandHandler>>();

        var dbOptions = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ArbiterTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(
            dbOptions,
            Mock.Of<MediatR.IMediator>(),
            Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>());

        // Default mocks for RAG pipeline
        _mockEmbeddingService
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[384] }));

        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Verdetto: Posizione A ha ragione."));

        _handler = new AskArbiterCommandHandler(
            _mockAgentRepository.Object,
            _mockLlmService.Object,
            _mockEmbeddingService.Object,
            _dbContext,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private AskArbiterCommand CreateCommand(
        Guid? agentId = null,
        string situation = "Player disagrees on resource placement",
        string positionA = "Resources can be placed on any empty space",
        string positionB = "Resources must be placed adjacent to existing ones")
    {
        return new AskArbiterCommand
        {
            AgentId = agentId ?? _agentId,
            SessionId = _sessionId,
            Situation = situation,
            PositionA = positionA,
            PositionB = positionB,
            UserId = _userId
        };
    }

    private Agent CreateTestAgent(Guid? gameId = null)
    {
        return new Agent(
            _agentId,
            "TestArbitro",
            AgentType.RulesInterpreter,
            AgentStrategy.VectorOnly(),
            isActive: true,
            gameId: gameId ?? _gameId);
    }

    private void SetupAgentWithConfig(Agent? agent = null, List<Guid>? documentIds = null)
    {
        var testAgent = agent ?? CreateTestAgent();
        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(_agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testAgent);

        if (documentIds != null && documentIds.Count > 0)
        {
            var config = new AgentConfigurationEntity
            {
                Id = Guid.NewGuid(),
                AgentId = _agentId,
                LlmProvider = 0,
                LlmModel = "test-model",
                AgentMode = 0,
                SelectedDocumentIdsJson = JsonSerializer.Serialize(documentIds),
                Temperature = 0.3m,
                MaxTokens = 1024,
                IsCurrent = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _userId
            };
            _dbContext.AgentConfigurations.Add(config);
            _dbContext.SaveChanges();
        }
    }

    #region Confidence Calculation Tests

    [Fact]
    public void CalculateConfidence_WithChunksAndCitations_ReturnsAvgScore()
    {
        // Arrange
        var chunks = new List<SearchResultItem>
        {
            new() { Score = 0.9f, Text = "Rule text 1", PdfId = "abc", Page = 1 },
            new() { Score = 0.8f, Text = "Rule text 2", PdfId = "abc", Page = 2 }
        };
        var citations = new List<ArbiterCitationDto>
        {
            new() { DocumentTitle = "Rules", Section = "Page 1", Text = "text", RelevanceScore = 0.9 }
        };

        // Act
        var confidence = AskArbiterCommandHandler.CalculateConfidence(chunks, citations);

        // Assert — avg(0.9, 0.8) * 1.0 = 0.85
        confidence.Should().BeApproximately(0.85, 0.001);
    }

    [Fact]
    public void CalculateConfidence_WithChunksNoCitations_ReturnsHalvedAvg()
    {
        // Arrange
        var chunks = new List<SearchResultItem>
        {
            new() { Score = 0.9f, Text = "Rule text 1", PdfId = "abc", Page = 1 },
            new() { Score = 0.8f, Text = "Rule text 2", PdfId = "abc", Page = 2 }
        };
        var citations = new List<ArbiterCitationDto>();

        // Act
        var confidence = AskArbiterCommandHandler.CalculateConfidence(chunks, citations);

        // Assert — avg(0.9, 0.8) * 0.5 = 0.425
        confidence.Should().BeApproximately(0.425, 0.001);
    }

    [Fact]
    public void CalculateConfidence_NoChunks_ReturnsZero()
    {
        // Arrange
        var chunks = new List<SearchResultItem>();
        var citations = new List<ArbiterCitationDto>();

        // Act
        var confidence = AskArbiterCommandHandler.CalculateConfidence(chunks, citations);

        // Assert
        confidence.Should().Be(0);
    }

    [Fact]
    public void CalculateConfidence_HighScoresWithCitations_IsConclusive()
    {
        // Arrange
        var chunks = new List<SearchResultItem>
        {
            new() { Score = 0.95f, Text = "text", PdfId = "a", Page = 1 },
            new() { Score = 0.90f, Text = "text", PdfId = "a", Page = 2 }
        };
        var citations = new List<ArbiterCitationDto>
        {
            new() { DocumentTitle = "R", Section = "P1", Text = "t", RelevanceScore = 0.95 }
        };

        // Act
        var confidence = AskArbiterCommandHandler.CalculateConfidence(chunks, citations);

        // Assert — avg(0.95, 0.90) * 1.0 = 0.925 >= 0.85
        confidence.Should().BeGreaterThanOrEqualTo(AskArbiterCommandHandler.ConclusiveThreshold);
    }

    #endregion

    #region System Prompt Tests

    [Fact]
    public void BuildArbiterSystemPrompt_WithGameName_IncludesGameName()
    {
        // Act
        var prompt = AskArbiterCommandHandler.BuildArbiterSystemPrompt("Catan");

        // Assert
        prompt.Should().Contain("Catan");
        prompt.Should().Contain("arbitro ufficiale");
        prompt.Should().Contain("regolamento");
        prompt.Should().Contain("VERDETTO");
    }

    [Fact]
    public void BuildArbiterSystemPrompt_WithoutGameName_UsesFallback()
    {
        // Act
        var prompt = AskArbiterCommandHandler.BuildArbiterSystemPrompt(null);

        // Assert
        prompt.Should().Contain("giochi da tavolo");
    }

    [Fact]
    public void BuildArbiterSystemPrompt_ContainsRequiredInstructions()
    {
        // Act
        var prompt = AskArbiterCommandHandler.BuildArbiterSystemPrompt("Chess");

        // Assert
        prompt.Should().Contain("Cita sempre la fonte esatta");
        prompt.Should().Contain("Posizione A");
        prompt.Should().Contain("Posizione B");
        prompt.Should().Contain("italiano");
    }

    #endregion

    #region User Prompt Tests

    [Fact]
    public void BuildArbiterUserPrompt_WithChunks_IncludesRegolamento()
    {
        // Arrange
        var chunks = new List<SearchResultItem>
        {
            new() { Score = 0.9f, Text = "Rule: adjacent placement required", PdfId = "abc", Page = 5 }
        };

        // Act
        var prompt = AskArbiterCommandHandler.BuildArbiterUserPrompt(
            "Placement dispute", "Position A text", "Position B text", chunks);

        // Assert
        prompt.Should().Contain("=== Regolamento ===");
        prompt.Should().Contain("adjacent placement required");
        prompt.Should().Contain("SITUAZIONE: Placement dispute");
        prompt.Should().Contain("POSIZIONE A: Position A text");
        prompt.Should().Contain("POSIZIONE B: Position B text");
    }

    [Fact]
    public void BuildArbiterUserPrompt_NoChunks_IncludesWarning()
    {
        // Arrange
        var chunks = new List<SearchResultItem>();

        // Act
        var prompt = AskArbiterCommandHandler.BuildArbiterUserPrompt(
            "Situation", "A", "B", chunks);

        // Assert
        prompt.Should().NotContain("=== Regolamento ===");
        prompt.Should().Contain("Nessun passaggio rilevante trovato");
    }

    #endregion

    #region Search Query Tests

    [Fact]
    public void BuildSearchQuery_CombinesAllParts()
    {
        // Act
        var query = AskArbiterCommandHandler.BuildSearchQuery(
            "Placement dispute", "Can place anywhere", "Must place adjacent");

        // Assert
        query.Should().Contain("Placement dispute");
        query.Should().Contain("Can place anywhere");
        query.Should().Contain("Must place adjacent");
    }

    #endregion

    #region Handler Integration Tests

    [Fact]
    public async Task Handle_AgentNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _mockAgentRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var command = CreateCommand();

        // Act & Assert
        await Assert.ThrowsAsync<Api.Middleware.Exceptions.NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_EmbeddingFails_ReturnsNoContextVerdict()
    {
        // Arrange
        SetupAgentWithConfig();
        _mockEmbeddingService
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Embedding service down"));

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Confidence.Should().Be(0);
        result.IsConclusive.Should().BeFalse();
        result.ExpansionWarning.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_NoRelevantChunks_ReturnsLowConfidenceVerdict()
    {
        // Arrange
        SetupAgentWithConfig();

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Verdict.Should().NotBeNullOrEmpty();
        result.Confidence.Should().Be(0);
        result.IsConclusive.Should().BeFalse();
        result.Citations.Should().BeEmpty();
        result.ExpansionWarning.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_WithNoVectorSearch_ReturnsVerdictWithNoCitations()
    {
        // Arrange — Qdrant removed; handler always returns empty search results
        SetupAgentWithConfig();

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Verdict.Should().NotBeNullOrEmpty();
        result.Citations.Should().BeEmpty();
        result.ExpansionWarning.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_LlmFails_ReturnsUnavailableVerdict()
    {
        // Arrange
        SetupAgentWithConfig();

        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("LLM unavailable"));

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Verdict.Should().Contain("Impossibile generare il verdetto");
        result.Confidence.Should().Be(0);
        result.IsConclusive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoVectorResults_MarksNotConclusive()
    {
        // Arrange — Qdrant removed; handler always returns empty search results
        SetupAgentWithConfig();

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — no chunks → confidence 0, not conclusive
        result.IsConclusive.Should().BeFalse();
        result.Confidence.Should().Be(0);
        result.ExpansionWarning.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Verdict DTO Construction Tests

    [Fact]
    public async Task Handle_ReturnsProperlyStructuredDto()
    {
        // Arrange
        SetupAgentWithConfig();

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — all required fields are present
        result.Should().NotBeNull();
        result.Verdict.Should().NotBeNull();
        result.Confidence.Should().BeInRange(0, 1);
        result.Citations.Should().NotBeNull();
    }

    #endregion
}
