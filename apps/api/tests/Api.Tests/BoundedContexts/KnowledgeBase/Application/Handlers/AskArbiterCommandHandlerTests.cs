using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using AgentDef = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
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
    private readonly Mock<IAgentDefinitionRepository> _mockDefinitionRepository;
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly Mock<IHybridSearchService> _mockHybridSearchService;
    private readonly Mock<ILogger<AskArbiterCommandHandler>> _mockLogger;
    private readonly MeepleAiDbContext _dbContext;
    private readonly AskArbiterCommandHandler _handler;
    private readonly Guid _agentDefinitionId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();

    public AskArbiterCommandHandlerTests()
    {
        _mockDefinitionRepository = new Mock<IAgentDefinitionRepository>();
        _mockLlmService = new Mock<ILlmService>();
        _mockHybridSearchService = new Mock<IHybridSearchService>();
        _mockLogger = new Mock<ILogger<AskArbiterCommandHandler>>();

        var dbOptions = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ArbiterTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(
            dbOptions,
            Mock.Of<MediatR.IMediator>(),
            Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>());

        _mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Verdetto: Posizione A ha ragione."));

        _mockHybridSearchService
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>());

        _handler = new AskArbiterCommandHandler(
            _mockDefinitionRepository.Object,
            _mockLlmService.Object,
            _mockHybridSearchService.Object,
            _dbContext,
            CreatePermissiveRagAccessServiceMock(),
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private AskArbiterCommand CreateCommand(
        Guid? agentDefinitionId = null,
        string situation = "Player disagrees on resource placement",
        string positionA = "Resources can be placed on any empty space",
        string positionB = "Resources must be placed adjacent to existing ones")
    {
        return new AskArbiterCommand
        {
            AgentDefinitionId = agentDefinitionId ?? _agentDefinitionId,
            SessionId = _sessionId,
            Situation = situation,
            PositionA = positionA,
            PositionB = positionB,
            UserId = _userId
        };
    }

    private AgentDef CreateTestDefinition()
    {
        return AgentDef.Create(
            "TestArbitro",
            "Test arbiter definition",
            AgentType.RulesInterpreter,
            AgentDefinitionConfig.Default());
    }

    private void SetupDefinitionInRepo(AgentDef? definition = null)
    {
        var testDefinition = definition ?? CreateTestDefinition();
        _mockDefinitionRepository
            .Setup(r => r.GetByIdAsync(_agentDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testDefinition);
    }

    #region Confidence Calculation Tests

    [Fact]
    public void CalculateConfidence_WithChunksAndCitations_ReturnsAvgScore()
    {
        // Arrange
        var chunks = new List<HybridSearchResult>
        {
            new() { ChunkId = "1", Content = "Rule text 1", PdfDocumentId = "abc", ChunkIndex = 1, PageNumber = 1, HybridScore = 0.9f, GameId = Guid.NewGuid(), Mode = SearchMode.Hybrid },
            new() { ChunkId = "2", Content = "Rule text 2", PdfDocumentId = "abc", ChunkIndex = 2, PageNumber = 2, HybridScore = 0.8f, GameId = Guid.NewGuid(), Mode = SearchMode.Hybrid }
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
        var chunks = new List<HybridSearchResult>
        {
            new() { ChunkId = "1", Content = "Rule text 1", PdfDocumentId = "abc", ChunkIndex = 1, PageNumber = 1, HybridScore = 0.9f, GameId = Guid.NewGuid(), Mode = SearchMode.Hybrid },
            new() { ChunkId = "2", Content = "Rule text 2", PdfDocumentId = "abc", ChunkIndex = 2, PageNumber = 2, HybridScore = 0.8f, GameId = Guid.NewGuid(), Mode = SearchMode.Hybrid }
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
        var chunks = new List<HybridSearchResult>();
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
        var gid = Guid.NewGuid();
        var chunks = new List<HybridSearchResult>
        {
            new() { ChunkId = "1", Content = "text", PdfDocumentId = "a", ChunkIndex = 1, PageNumber = 1, HybridScore = 0.95f, GameId = gid, Mode = SearchMode.Hybrid },
            new() { ChunkId = "2", Content = "text", PdfDocumentId = "a", ChunkIndex = 2, PageNumber = 2, HybridScore = 0.90f, GameId = gid, Mode = SearchMode.Hybrid }
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
        var chunks = new List<HybridSearchResult>
        {
            new() { ChunkId = "1", Content = "Rule: adjacent placement required", PdfDocumentId = "abc", ChunkIndex = 1, PageNumber = 5, HybridScore = 0.9f, GameId = Guid.NewGuid(), Mode = SearchMode.Hybrid }
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
        var chunks = new List<HybridSearchResult>();

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
    public async Task Handle_DefinitionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _mockDefinitionRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDef?)null);

        var command = CreateCommand();

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<Api.Middleware.Exceptions.NotFoundException>();
    }

    [Fact]
    public async Task Handle_DefinitionWithNoGameId_ReturnsVerdictWithoutSearch()
    {
        // Arrange — definition has no GameId, so search is skipped
        SetupDefinitionInRepo();

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Confidence.Should().Be(0);
        result.IsConclusive.Should().BeFalse();
        result.ExpansionWarning.Should().NotBeNullOrEmpty();
        _mockHybridSearchService.Verify(
            s => s.SearchAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<SearchMode>(),
                It.IsAny<int>(), It.IsAny<List<Guid>?>(), It.IsAny<float>(), It.IsAny<float>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NoRelevantChunks_ReturnsLowConfidenceVerdict()
    {
        // Arrange
        SetupDefinitionInRepo();

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
        // Arrange — hybrid search returns empty results
        SetupDefinitionInRepo();

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
        SetupDefinitionInRepo();

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
        // Arrange — hybrid search returns empty results
        SetupDefinitionInRepo();

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
        SetupDefinitionInRepo();

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
    private static IRagAccessService CreatePermissiveRagAccessServiceMock()
    {
        var mock = new Mock<IRagAccessService>();
        mock.Setup(s => s.CanAccessRagAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock.Object;
    }
}
