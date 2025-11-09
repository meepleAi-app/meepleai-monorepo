using Api.Models;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Tests for contract/DTO model classes
/// </summary>
public class ContractModelTests
{
    private readonly ITestOutputHelper _output;

    #region IngestPdfResponse Tests

    [Fact]
    public void IngestPdfResponse_Constructor_SetsJobId()
    {
        // Act
        var response = new IngestPdfResponse("job-123");

        // Assert
        response.jobId.Should().Be("job-123");
    }

    [Fact]
    public void IngestPdfResponse_Equality_WorksForRecords()
    {
        // Arrange
        var response1 = new IngestPdfResponse("job-123");
        var response2 = new IngestPdfResponse("job-123");
        var response3 = new IngestPdfResponse("job-456");

        // Assert
        response2.Should().Be(response1);
        response3.Should().NotBe(response1);
    }

    [Fact]
    public void IngestPdfResponse_WithExpression_CreatesNewInstance()
    {
        // Arrange
        var original = new IngestPdfResponse("job-123");

        // Act
        var modified = original with { jobId = "job-456" };

        // Assert
        modified.jobId.Should().Be("job-456");
        original.jobId.Should().Be("job-123"); // Original unchanged
    }

    [Fact]
    public void IngestPdfResponse_ToString_ContainsJobId()
    {
        // Arrange
        var response = new IngestPdfResponse("job-789");

        // Act
        var stringRepresentation = response.ToString();

        // Assert
        stringRepresentation.Should().Contain("job-789");
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
        request.gameId.Should().Be("game-123");
        request.query.Should().Be("How do I win?");
        request.chatId.Should().NotBeNull();
    }

    [Fact]
    public void QaRequest_WithoutChatId_DefaultsToNull()
    {
        // Act
        var request = new QaRequest("game-123", "test query");

        // Assert
        request.gameId.Should().Be("game-123");
        request.query.Should().Be("test query");
        request.chatId.Should().BeNull();
    }

    #endregion

    #region QaResponse Tests

    [Fact]
    public void QaResponse_WithFullMetadata_StoresAllFields()
    {
        // Arrange
        var snippets = new List<Snippet>
        {
            new("Text 1", "PDF:123", 5, 10, 0.85f),
            new("Text 2", "PDF:456", 7, 15, 0.85f)
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
        response.answer.Should().Be("The answer is 42.");
        response.snippets.Count.Should().Be(2);
        response.promptTokens.Should().Be(100);
        response.completionTokens.Should().Be(50);
        response.totalTokens.Should().Be(150);
        response.confidence.Should().Be(0.95);
        response.metadata.Should().NotBeNull();
        response.metadata.Count.Should().Be(2);
        response.metadata["model"].Should().Be("anthropic/claude-3.5-sonnet");
    }

    [Fact]
    public void QaResponse_WithMinimalData_HasDefaultValues()
    {
        // Act
        var response = new QaResponse("Answer", Array.Empty<Snippet>());

        // Assert
        response.answer.Should().Be("Answer");
        response.snippets.Should().BeEmpty();
        response.promptTokens.Should().Be(0);
        response.completionTokens.Should().Be(0);
        response.totalTokens.Should().Be(0);
        response.confidence.Should().BeNull();
        response.metadata.Should().BeNull();
    }

    #endregion

    #region Snippet Tests

    [Fact]
    public void Snippet_Constructor_SetsAllProperties()
    {
        // Act
        var snippet = new Snippet("Rule text here", "PDF:game-rules-v2.pdf", 15, 42, 0.85f);

        // Assert
        snippet.text.Should().Be("Rule text here");
        snippet.source.Should().Be("PDF:game-rules-v2.pdf");
        snippet.page.Should().Be(15);
        snippet.line.Should().Be(42);
    }

    [Fact]
    public void Snippet_Equality_ComparesAllFields()
    {
        // Arrange
        var snippet1 = new Snippet("Text", "Source", 1, 2, 0.85f);
        var snippet2 = new Snippet("Text", "Source", 1, 2, 0.85f);
        var snippet3 = new Snippet("Different", "Source", 1, 2, 0.85f);

        // Assert
        snippet2.Should().Be(snippet1);
        snippet3.Should().NotBe(snippet1);
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
        request.gameId.Should().Be("game-456");
        request.topic.Should().Be("combat mechanics");
        request.chatId.Should().Be(chatId);
    }

    #endregion

    #region ExplainResponse Tests

    [Fact]
    public void ExplainResponse_WithFullData_StoresAllFields()
    {
        // Arrange
        var outline = new ExplainOutline("Combat", new List<string> { "Attack", "Defense", "Resolution" });
        var citations = new List<Snippet> { new("Rule A", "PDF:1", 5, 0, 0.85f) };

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
        response.outline.mainTopic.Should().Be("Combat");
        response.outline.sections.Count.Should().Be(3);
        response.script.Should().Be("Full script content here");
        response.citations.Should().ContainSingle();
        response.estimatedReadingTimeMinutes.Should().Be(5);
        response.promptTokens.Should().Be(120);
        response.completionTokens.Should().Be(80);
        response.totalTokens.Should().Be(200);
        response.confidence.Should().Be(0.88);
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
        request.gameId.Should().Be("game-789");
        request.chatId.Should().Be(chatId);
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
        response.gameTitle.Should().Be("Test Game");
        response.steps.Count.Should().Be(2);
        response.estimatedSetupTimeMinutes.Should().Be(10);
        response.promptTokens.Should().Be(50);
        response.completionTokens.Should().Be(30);
        response.totalTokens.Should().Be(80);
        response.confidence.Should().Be(0.92);
    }

    #endregion

    #region SetupGuideStep Tests

    [Fact]
    public void SetupGuideStep_Constructor_SetsAllProperties()
    {
        // Arrange
        var references = new List<Snippet> { new("Reference", "PDF:123", 5, 0, 0.85f) };

        // Act
        var step = new SetupGuideStep(
            stepNumber: 3,
            title: "Distribute Cards",
            instruction: "Give each player 7 cards",
            references: references,
            isOptional: true);

        // Assert
        step.stepNumber.Should().Be(3);
        step.title.Should().Be("Distribute Cards");
        step.instruction.Should().Be("Give each player 7 cards");
        step.references.Should().ContainSingle();
        step.isOptional.Should().BeTrue();
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
        dto.Id.Should().Be("config-123");
        dto.Name.Should().Be("Production Workflow");
        dto.BaseUrl.Should().Be("https://n8n.example.com");
        dto.WebhookUrl.Should().Be("https://n8n.example.com/webhook/abc");
        dto.IsActive.Should().BeTrue();
        dto.LastTestedAt.Should().Be(now);
        dto.LastTestResult.Should().Be("Success");
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
        request.Name.Should().Be("Test Config");
        request.BaseUrl.Should().Be("https://test.n8n.io");
        request.ApiKey.Should().Be("test-api-key-123");
        request.WebhookUrl.Should().Be("https://test.n8n.io/webhook/test");
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
        request.question.Should().Be("Is this checkmate?");
        request.fenPosition.Should().Be("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        request.chatId.Should().Be(chatId);
    }

    [Fact]
    public void ChessAgentRequest_WithoutFenPosition_DefaultsToNull()
    {
        // Act
        var request = new ChessAgentRequest(question: "What is castling?");

        // Assert
        request.question.Should().Be("What is castling?");
        request.fenPosition.Should().BeNull();
        request.chatId.Should().BeNull();
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
        var sources = new List<Snippet> { new("Chess rules", "PDF:chess", 10, 0, 0.85f) };
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
        response.answer.Should().Be("Consider controlling the center.");
        response.analysis.Should().NotBeNull();
        response.analysis.evaluationSummary.Should().Be("White has slight advantage");
        response.suggestedMoves.Count.Should().Be(3);
        response.sources.Should().ContainSingle();
        response.promptTokens.Should().Be(200);
        response.completionTokens.Should().Be(150);
        response.totalTokens.Should().Be(350);
        response.confidence.Should().Be(0.90);
        response.metadata.Should().NotBeNull();
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
        dto.Id.Should().Be(chatId);
        dto.GameId.Should().Be("game-123");
        dto.GameName.Should().Be("Chess");
        dto.AgentId.Should().Be("agent-qa");
        dto.AgentName.Should().Be("Q&A Assistant");
        dto.StartedAt.Should().Be(startedAt);
        dto.LastMessageAt.Should().Be(lastMessageAt);
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
        request.messageId.Should().Be("msg-123");
        request.endpoint.Should().Be("qa");
        request.outcome.Should().Be("helpful");
        request.userId.Should().Be("user-456");
        request.gameId.Should().Be("game-789");
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
        request.outcome.Should().BeNull();
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
        result.Success.Should().BeTrue();
        result.Message.Should().BeEquivalentTo("Connection successful");
        result.LatencyMs.Should().Be(125);
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
        result.Success.Should().BeFalse();
        result.Message.Should().BeEquivalentTo("Connection timeout");
        result.LatencyMs.Should().BeNull();
    }

    #endregion

    #endregion
}
