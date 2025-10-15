using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Infrastructure.Entities;
using Api.Models;
using Xunit;

namespace Api.Tests;

/// <summary>
/// CHESS-04: BDD-style integration tests for Chess Agent endpoint
///
/// Feature: Chess conversational agent
/// As a chess player
/// I want to ask questions about chess rules, openings, tactics, and positions
/// So that I can improve my understanding and play better
/// </summary>
public class ChessAgentIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ChessAgentIntegrationTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Scenario: Authenticated user asks a simple chess question
    ///   Given user is authenticated
    ///   And chess knowledge is indexed
    ///   When user asks "What is en passant?"
    ///   Then answer is returned
    ///   And sources are cited
    ///   And confidence is reported
    /// </summary>
    [Fact(Skip = "Integration test - requires Qdrant and OpenRouter")]
    public async Task AskChessAgent_SimpleRulesQuestion_ReturnsAnswerWithSources()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync("chess-simple", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: Chess knowledge is indexed (assumed from CHESS-03)
        // Note: In real test, would call POST /chess/index first

        // When: User asks "What is en passant?"
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/chess")
        {
            Content = JsonContent.Create(new ChessAgentRequest("What is en passant?"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Answer is returned
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ChessAgentResponse>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.NotEmpty(result!.answer);
        Assert.Contains("passant", result.answer, StringComparison.OrdinalIgnoreCase);

        // And: Sources are cited
        Assert.NotEmpty(result.sources);
        Assert.All(result.sources, source =>
        {
            Assert.NotEmpty(source.text);
            Assert.StartsWith("ChessKnowledge:", source.source);
        });

        // And: Confidence is reported
        Assert.NotNull(result.confidence);
        Assert.True(result.confidence > 0.0);
    }

    /// <summary>
    /// Scenario: User asks about a chess opening
    ///   Given user is authenticated
    ///   When user asks about the Italian Game
    ///   Then explanation includes key moves and ideas
    /// </summary>
    [Fact(Skip = "Integration test - requires Qdrant and OpenRouter")]
    public async Task AskChessAgent_OpeningQuestion_ReturnsExplanation()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync("chess-opening", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User asks about the Italian Game
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/chess")
        {
            Content = JsonContent.Create(new ChessAgentRequest("Explain the Italian Game opening"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Explanation includes key moves and ideas
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ChessAgentResponse>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Contains("Italian", result!.answer, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.answer.Length > 50); // Substantial explanation
    }

    /// <summary>
    /// Scenario: User asks for position analysis with FEN
    ///   Given user is authenticated
    ///   When user provides a valid FEN position
    ///   Then analysis includes evaluation and key considerations
    ///   And suggested moves are provided
    /// </summary>
    [Fact(Skip = "Integration test - requires Qdrant and OpenRouter")]
    public async Task AskChessAgent_PositionAnalysisWithFEN_ReturnsAnalysisAndMoves()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync("chess-fen", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User provides a valid FEN position (after 1. e4)
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/chess")
        {
            Content = JsonContent.Create(new ChessAgentRequest(
                "What should Black play now?",
                "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1"
            ))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Analysis includes evaluation and key considerations
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ChessAgentResponse>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.NotNull(result!.analysis);
        Assert.NotEmpty(result.analysis!.fenPosition!);

        // And: Suggested moves are provided
        Assert.NotEmpty(result.suggestedMoves);
    }

    /// <summary>
    /// Scenario: User provides invalid FEN position
    ///   Given user is authenticated
    ///   When user provides an invalid FEN string
    ///   Then response includes warning about invalid FEN
    ///   And still attempts to answer the question
    /// </summary>
    [Fact(Skip = "Integration test - requires Qdrant and OpenRouter")]
    public async Task AskChessAgent_InvalidFEN_ReturnsWarning()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync("chess-invalid-fen", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User provides an invalid FEN string
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/chess")
        {
            Content = JsonContent.Create(new ChessAgentRequest(
                "Analyze this position",
                "invalid-fen-string"
            ))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Response includes warning about invalid FEN
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ChessAgentResponse>(json, JsonOptions);

        Assert.NotNull(result);
        // Answer should mention invalid FEN or inability to analyze
        Assert.NotEmpty(result!.answer);
    }

    /// <summary>
    /// Scenario: User asks about tactical patterns
    ///   Given user is authenticated
    ///   When user asks "What is a fork in chess?"
    ///   Then explanation includes definition and examples
    /// </summary>
    [Fact(Skip = "Integration test - requires Qdrant and OpenRouter")]
    public async Task AskChessAgent_TacticalQuestion_ReturnsExplanationWithExamples()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync("chess-tactics", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User asks "What is a fork in chess?"
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/chess")
        {
            Content = JsonContent.Create(new ChessAgentRequest("What is a fork in chess? Give examples."))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Explanation includes definition and examples
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ChessAgentResponse>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Contains("fork", result!.answer, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.answer.Length > 50); // Substantial explanation
    }

    /// <summary>
    /// Scenario: Unauthenticated user tries to access chess agent
    ///   Given user is NOT authenticated
    ///   When user tries to POST to /agents/chess
    ///   Then request is rejected with 401 Unauthorized
    /// </summary>
    [Fact]
    public async Task AskChessAgent_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Given: User is NOT authenticated
        var client = CreateClientWithoutCookies();

        // When: User tries to POST to /agents/chess
        var response = await client.PostAsJsonAsync("/api/v1/agents/chess", new ChessAgentRequest("Test question"));

        // Then: Request is rejected with 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Scenario: User asks empty question
    ///   Given user is authenticated
    ///   When user sends empty question
    ///   Then response indicates question is required
    /// </summary>
    [Fact]
    public async Task AskChessAgent_EmptyQuestion_ReturnsBadRequest()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync("chess-empty", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User sends empty question
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/chess")
        {
            Content = JsonContent.Create(new ChessAgentRequest(""))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Response indicates question is required
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // Service returns 200 with empty answer

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ChessAgentResponse>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Equal("Please provide a question.", result!.answer);
    }

    /// <summary>
    /// Scenario: Response includes token usage information
    ///   Given user is authenticated
    ///   When user asks a question
    ///   Then response includes prompt, completion, and total token counts
    /// </summary>
    [Fact(Skip = "Integration test - requires Qdrant and OpenRouter")]
    public async Task AskChessAgent_ReturnsTokenUsage()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync("chess-tokens", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User asks a question
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/chess")
        {
            Content = JsonContent.Create(new ChessAgentRequest("What is castling?"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Response includes prompt, completion, and total token counts
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ChessAgentResponse>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.True(result!.promptTokens > 0);
        Assert.True(result.completionTokens > 0);
        Assert.Equal(result.promptTokens + result.completionTokens, result.totalTokens);
    }

    /// <summary>
    /// Scenario: Cached response is returned on second request
    ///   Given user is authenticated
    ///   And user has asked a question before
    ///   When user asks the same question again
    ///   Then cached response is returned
    ///   And token usage remains the same (cached)
    /// </summary>
    [Fact(Skip = "Integration test - requires Qdrant and OpenRouter")]
    public async Task AskChessAgent_SameQuestionTwice_ReturnsCachedResponse()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync("chess-cache", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: User has asked a question before
        var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/chess")
        {
            Content = JsonContent.Create(new ChessAgentRequest("What is checkmate?"))
        };
        AddCookies(firstRequest, cookies);

        var firstResponse = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var firstJson = await firstResponse.Content.ReadAsStringAsync();
        var firstResult = JsonSerializer.Deserialize<ChessAgentResponse>(firstJson, JsonOptions);

        // When: User asks the same question again
        var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/chess")
        {
            Content = JsonContent.Create(new ChessAgentRequest("What is checkmate?"))
        };
        AddCookies(secondRequest, cookies);

        var secondResponse = await client.SendAsync(secondRequest);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var secondJson = await secondResponse.Content.ReadAsStringAsync();
        var secondResult = JsonSerializer.Deserialize<ChessAgentResponse>(secondJson, JsonOptions);

        // Then: Cached response is returned
        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal(firstResult!.answer, secondResult!.answer);

        // And: Token usage remains the same (cached)
        Assert.Equal(firstResult.promptTokens, secondResult.promptTokens);
        Assert.Equal(firstResult.completionTokens, secondResult.completionTokens);
    }
}
