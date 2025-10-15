using Api.Models;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Tests for contract/DTO model classes
/// </summary>
public class ContractModelTests
{
    #region IngestPdfResponse Tests

    [Fact]
    public void IngestPdfResponse_Constructor_SetsJobId()
    {
        // Act
        var response = new IngestPdfResponse("job-123");

        // Assert
        Assert.Equal("job-123", response.jobId);
    }

    [Fact]
    public void IngestPdfResponse_Equality_WorksForRecords()
    {
        // Arrange
        var response1 = new IngestPdfResponse("job-123");
        var response2 = new IngestPdfResponse("job-123");
        var response3 = new IngestPdfResponse("job-456");

        // Assert
        Assert.Equal(response1, response2);
        Assert.NotEqual(response1, response3);
    }

    [Fact]
    public void IngestPdfResponse_WithExpression_CreatesNewInstance()
    {
        // Arrange
        var original = new IngestPdfResponse("job-123");

        // Act
        var modified = original with { jobId = "job-456" };

        // Assert
        Assert.Equal("job-456", modified.jobId);
        Assert.Equal("job-123", original.jobId); // Original unchanged
    }

    [Fact]
    public void IngestPdfResponse_ToString_ContainsJobId()
    {
        // Arrange
        var response = new IngestPdfResponse("job-789");

        // Act
        var stringRepresentation = response.ToString();

        // Assert
        Assert.Contains("job-789", stringRepresentation);
    }

    #endregion

    #region Phase 3: Additional Model Tests

    #region QaRequest Tests

    [Fact]
    public void QaRequest_Constructor_SetsAllProperties()
    {
        // Act
        var request = new QaRequest("game-123", "How do I win?", Guid.NewGuid());

        // Assert
        Assert.Equal("game-123", request.gameId);
        Assert.Equal("How do I win?", request.query);
        Assert.NotNull(request.chatId);
    }

    [Fact]
    public void QaRequest_WithoutChatId_DefaultsToNull()
    {
        // Act
        var request = new QaRequest("game-123", "test query");

        // Assert
        Assert.Equal("game-123", request.gameId);
        Assert.Equal("test query", request.query);
        Assert.Null(request.chatId);
    }

    #endregion

    #region QaResponse Tests

    [Fact]
    public void QaResponse_WithFullMetadata_StoresAllFields()
    {
        // Arrange
        var snippets = new List<Snippet>
        {
            new("Text 1", "PDF:123", 5, 10),
            new("Text 2", "PDF:456", 7, 15)
        };
        var metadata = new Dictionary<string, string>
        {
            { "model", "anthropic/claude-3.5-sonnet" },
            { "finish_reason", "stop" }
        };

        // Act
        var response = new QaResponse(
            "The answer is 42.",
            snippets,
            promptTokens: 100,
            completionTokens: 50,
            totalTokens: 150,
            confidence: 0.95,
            metadata: metadata);

        // Assert
        Assert.Equal("The answer is 42.", response.answer);
        Assert.Equal(2, response.snippets.Count);
        Assert.Equal(100, response.promptTokens);
        Assert.Equal(50, response.completionTokens);
        Assert.Equal(150, response.totalTokens);
        Assert.Equal(0.95, response.confidence);
        Assert.NotNull(response.metadata);
        Assert.Equal(2, response.metadata.Count);
        Assert.Equal("anthropic/claude-3.5-sonnet", response.metadata["model"]);
    }

    [Fact]
    public void QaResponse_WithMinimalData_HasDefaultValues()
    {
        // Act
        var response = new QaResponse("Answer", Array.Empty<Snippet>());

        // Assert
        Assert.Equal("Answer", response.answer);
        Assert.Empty(response.snippets);
        Assert.Equal(0, response.promptTokens);
        Assert.Equal(0, response.completionTokens);
        Assert.Equal(0, response.totalTokens);
        Assert.Null(response.confidence);
        Assert.Null(response.metadata);
    }

    #endregion

    #region Snippet Tests

    [Fact]
    public void Snippet_Constructor_SetsAllProperties()
    {
        // Act
        var snippet = new Snippet("Rule text here", "PDF:game-rules-v2.pdf", 15, 42);

        // Assert
        Assert.Equal("Rule text here", snippet.text);
        Assert.Equal("PDF:game-rules-v2.pdf", snippet.source);
        Assert.Equal(15, snippet.page);
        Assert.Equal(42, snippet.line);
    }

    [Fact]
    public void Snippet_Equality_ComparesAllFields()
    {
        // Arrange
        var snippet1 = new Snippet("Text", "Source", 1, 2);
        var snippet2 = new Snippet("Text", "Source", 1, 2);
        var snippet3 = new Snippet("Different", "Source", 1, 2);

        // Assert
        Assert.Equal(snippet1, snippet2);
        Assert.NotEqual(snippet1, snippet3);
    }

    #endregion

    #region ExplainRequest Tests

    [Fact]
    public void ExplainRequest_Constructor_SetsProperties()
    {
        // Act
        var chatId = Guid.NewGuid();
        var request = new ExplainRequest("game-456", "combat mechanics", chatId);

        // Assert
        Assert.Equal("game-456", request.gameId);
        Assert.Equal("combat mechanics", request.topic);
        Assert.Equal(chatId, request.chatId);
    }

    #endregion

    #region ExplainResponse Tests

    [Fact]
    public void ExplainResponse_WithFullData_StoresAllFields()
    {
        // Arrange
        var outline = new ExplainOutline("Combat", new List<string> { "Attack", "Defense", "Resolution" });
        var citations = new List<Snippet> { new("Rule A", "PDF:1", 5, 0) };

        // Act
        var response = new ExplainResponse(
            outline,
            "Full script content here",
            citations,
            estimatedReadingTimeMinutes: 5,
            promptTokens: 120,
            completionTokens: 80,
            totalTokens: 200,
            confidence: 0.88);

        // Assert
        Assert.Equal("Combat", response.outline.mainTopic);
        Assert.Equal(3, response.outline.sections.Count);
        Assert.Equal("Full script content here", response.script);
        Assert.Single(response.citations);
        Assert.Equal(5, response.estimatedReadingTimeMinutes);
        Assert.Equal(120, response.promptTokens);
        Assert.Equal(80, response.completionTokens);
        Assert.Equal(200, response.totalTokens);
        Assert.Equal(0.88, response.confidence);
    }

    #endregion

    #region SetupGuideRequest Tests

    [Fact]
    public void SetupGuideRequest_Constructor_SetsProperties()
    {
        // Act
        var chatId = Guid.NewGuid();
        var request = new SetupGuideRequest("game-789", chatId);

        // Assert
        Assert.Equal("game-789", request.gameId);
        Assert.Equal(chatId, request.chatId);
    }

    #endregion

    #region SetupGuideResponse Tests

    [Fact]
    public void SetupGuideResponse_WithSteps_StoresAllData()
    {
        // Arrange
        var steps = new List<SetupGuideStep>
        {
            new(1, "Unbox", "Remove all components", new List<Snippet>(), false),
            new(2, "Setup Board", "Place board in center", new List<Snippet>(), false)
        };

        // Act
        var response = new SetupGuideResponse(
            "Test Game",
            steps,
            estimatedSetupTimeMinutes: 10,
            promptTokens: 50,
            completionTokens: 30,
            totalTokens: 80,
            confidence: 0.92);

        // Assert
        Assert.Equal("Test Game", response.gameTitle);
        Assert.Equal(2, response.steps.Count);
        Assert.Equal(10, response.estimatedSetupTimeMinutes);
        Assert.Equal(50, response.promptTokens);
        Assert.Equal(30, response.completionTokens);
        Assert.Equal(80, response.totalTokens);
        Assert.Equal(0.92, response.confidence);
    }

    #endregion

    #region SetupGuideStep Tests

    [Fact]
    public void SetupGuideStep_Constructor_SetsAllProperties()
    {
        // Arrange
        var references = new List<Snippet> { new("Reference", "PDF:123", 5, 0) };

        // Act
        var step = new SetupGuideStep(
            stepNumber: 3,
            title: "Distribute Cards",
            instruction: "Give each player 7 cards",
            references: references,
            isOptional: true);

        // Assert
        Assert.Equal(3, step.stepNumber);
        Assert.Equal("Distribute Cards", step.title);
        Assert.Equal("Give each player 7 cards", step.instruction);
        Assert.Single(step.references);
        Assert.True(step.isOptional);
    }

    #endregion

    #region N8nConfigDto Tests

    [Fact]
    public void N8nConfigDto_Constructor_SetsAllProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var dto = new N8nConfigDto(
            Id: "config-123",
            Name: "Production Workflow",
            BaseUrl: "https://n8n.example.com",
            WebhookUrl: "https://n8n.example.com/webhook/abc",
            IsActive: true,
            LastTestedAt: now,
            LastTestResult: "Success",
            CreatedAt: now.AddDays(-30),
            UpdatedAt: now);

        // Assert
        Assert.Equal("config-123", dto.Id);
        Assert.Equal("Production Workflow", dto.Name);
        Assert.Equal("https://n8n.example.com", dto.BaseUrl);
        Assert.Equal("https://n8n.example.com/webhook/abc", dto.WebhookUrl);
        Assert.True(dto.IsActive);
        Assert.Equal(now, dto.LastTestedAt);
        Assert.Equal("Success", dto.LastTestResult);
    }

    #endregion

    #region CreateN8nConfigRequest Tests

    [Fact]
    public void CreateN8nConfigRequest_Constructor_SetsAllProperties()
    {
        // Act
        var request = new CreateN8nConfigRequest(
            Name: "Test Config",
            BaseUrl: "https://test.n8n.io",
            ApiKey: "test-api-key-123",
            WebhookUrl: "https://test.n8n.io/webhook/test");

        // Assert
        Assert.Equal("Test Config", request.Name);
        Assert.Equal("https://test.n8n.io", request.BaseUrl);
        Assert.Equal("test-api-key-123", request.ApiKey);
        Assert.Equal("https://test.n8n.io/webhook/test", request.WebhookUrl);
    }

    #endregion

    #region ChessAgentRequest Tests

    [Fact]
    public void ChessAgentRequest_WithFenPosition_SetsAllProperties()
    {
        // Act
        var chatId = Guid.NewGuid();
        var request = new ChessAgentRequest(
            question: "Is this checkmate?",
            fenPosition: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            chatId: chatId);

        // Assert
        Assert.Equal("Is this checkmate?", request.question);
        Assert.Equal("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", request.fenPosition);
        Assert.Equal(chatId, request.chatId);
    }

    [Fact]
    public void ChessAgentRequest_WithoutFenPosition_DefaultsToNull()
    {
        // Act
        var request = new ChessAgentRequest(question: "What is castling?");

        // Assert
        Assert.Equal("What is castling?", request.question);
        Assert.Null(request.fenPosition);
        Assert.Null(request.chatId);
    }

    #endregion

    #region ChessAgentResponse Tests

    [Fact]
    public void ChessAgentResponse_WithAnalysis_StoresAllData()
    {
        // Arrange
        var analysis = new ChessAnalysis(
            fenPosition: "rnbqkb1r/pppp1ppp/5n2/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 4 3",
            evaluationSummary: "White has slight advantage",
            keyConsiderations: new List<string> { "Control center", "Develop pieces" });
        var sources = new List<Snippet> { new("Chess rules", "PDF:chess", 10, 0) };
        var suggestedMoves = new List<string> { "Nc3", "Bb5", "d4" };
        var metadata = new Dictionary<string, string> { { "model", "gpt-4" } };

        // Act
        var response = new ChessAgentResponse(
            answer: "Consider controlling the center.",
            analysis: analysis,
            suggestedMoves: suggestedMoves,
            sources: sources,
            promptTokens: 200,
            completionTokens: 150,
            totalTokens: 350,
            confidence: 0.90,
            metadata: metadata);

        // Assert
        Assert.Equal("Consider controlling the center.", response.answer);
        Assert.NotNull(response.analysis);
        Assert.Equal("White has slight advantage", response.analysis.evaluationSummary);
        Assert.Equal(3, response.suggestedMoves.Count);
        Assert.Single(response.sources);
        Assert.Equal(200, response.promptTokens);
        Assert.Equal(150, response.completionTokens);
        Assert.Equal(350, response.totalTokens);
        Assert.Equal(0.90, response.confidence);
        Assert.NotNull(response.metadata);
    }

    #endregion

    #region ChatDto Tests

    [Fact]
    public void ChatDto_Constructor_SetsAllProperties()
    {
        // Arrange
        var chatId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow.AddMinutes(-10);
        var lastMessageAt = DateTime.UtcNow;

        // Act
        var dto = new ChatDto(
            Id: chatId,
            GameId: "game-123",
            GameName: "Chess",
            AgentId: "agent-qa",
            AgentName: "Q&A Assistant",
            StartedAt: startedAt,
            LastMessageAt: lastMessageAt);

        // Assert
        Assert.Equal(chatId, dto.Id);
        Assert.Equal("game-123", dto.GameId);
        Assert.Equal("Chess", dto.GameName);
        Assert.Equal("agent-qa", dto.AgentId);
        Assert.Equal("Q&A Assistant", dto.AgentName);
        Assert.Equal(startedAt, dto.StartedAt);
        Assert.Equal(lastMessageAt, dto.LastMessageAt);
    }

    #endregion

    #region AgentFeedbackRequest Tests

    [Fact]
    public void AgentFeedbackRequest_Constructor_SetsAllProperties()
    {
        // Act
        var request = new AgentFeedbackRequest(
            messageId: "msg-123",
            endpoint: "qa",
            outcome: "helpful",
            userId: "user-456",
            gameId: "game-789");

        // Assert
        Assert.Equal("msg-123", request.messageId);
        Assert.Equal("qa", request.endpoint);
        Assert.Equal("helpful", request.outcome);
        Assert.Equal("user-456", request.userId);
        Assert.Equal("game-789", request.gameId);
    }

    [Fact]
    public void AgentFeedbackRequest_WithNullOutcome_AllowsNull()
    {
        // Act
        var request = new AgentFeedbackRequest(
            messageId: "msg-123",
            endpoint: "qa",
            outcome: null,
            userId: "user-456",
            gameId: "game-789");

        // Assert
        Assert.Null(request.outcome);
    }

    #endregion

    #region N8nTestResult Tests

    [Fact]
    public void N8nTestResult_Constructor_SetsAllProperties()
    {
        // Act
        var result = new N8nTestResult(
            Success: true,
            Message: "Connection successful",
            LatencyMs: 125);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Connection successful", result.Message);
        Assert.Equal(125, result.LatencyMs);
    }

    [Fact]
    public void N8nTestResult_WithFailure_HandlesNullLatency()
    {
        // Act
        var result = new N8nTestResult(
            Success: false,
            Message: "Connection timeout",
            LatencyMs: null);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Connection timeout", result.Message);
        Assert.Null(result.LatencyMs);
    }

    #endregion

    #endregion
}
