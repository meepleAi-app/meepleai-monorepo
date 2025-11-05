using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Tests.Fixtures;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// End-to-End integration tests for complete RAG pipeline
/// Covers FLUSSI.md Flow 3 + Flow 6:
/// 1. Upload PDF
/// 2. Extract text
/// 3. Generate RuleSpec
/// 4. Index in Qdrant
/// 5. Query with RAG
/// 6. Receive answer with citations
/// </summary>
[Collection("Admin Endpoints")]
public class RagEndToEndFlowIntegrationTests : AdminTestFixture
{
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _client;

    public RagEndToEndFlowIntegrationTests(
        PostgresCollectionFixture postgresFixture,
        WebApplicationFactoryFixture factory,
        ITestOutputHelper output)
        : base(postgresFixture, factory)
    {
        _output = output;
        _client = Factory.CreateClient();
    }

    [Fact]
    public async Task CompleteRagFlow_UploadPdf_ExtractText_Index_Query_ReturnsAnswerWithCitations()
    {
        // STEP 1: Authenticate as Editor
        _output.WriteLine("🔐 STEP 1: Authenticating as Editor...");
        var cookies = await RegisterAndAuthenticateAsync(_client, "rag-e2e-editor@test.com", "Editor");
        var authenticatedClient = CreateClientWithoutCookies();
        _output.WriteLine("✅ Authenticated successfully");

        // STEP 2: Create a new game
        _output.WriteLine("\n🎮 STEP 2: Creating game...");
        var gameRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/games")
        {
            Content = JsonContent.Create(new
            {
                title = "Tic-Tac-Toe E2E Test",
                publisher = "Test Publisher",
                yearPublished = 2024,
                minPlayers = 2,
                maxPlayers = 2,
                playtimeMinutes = 5,
                minAge = 5,
                description = "Simple test game for RAG E2E flow"
            })
        };
        AddCookies(gameRequest, cookies);
        var gameResponse = await authenticatedClient.SendAsync(gameRequest);
        gameResponse.EnsureSuccessStatusCode();
        var gameData = await gameResponse.Content.ReadFromJsonAsync<JsonElement>();
        var gameId = gameData.GetProperty("id").GetGuid();
        _output.WriteLine($"✅ Game created: {gameId}");

        // STEP 3: Upload PDF
        _output.WriteLine("\n📄 STEP 3: Uploading PDF...");
        var pdfContent = CreateSimplePdfContent();
        using var formData = new MultipartFormDataContent();
        formData.Add(new ByteArrayContent(pdfContent), "file", "test-rules.pdf");
        formData.Add(new StringContent(gameId.ToString()), "gameId");

        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf")
        {
            Content = formData
        };
        AddCookies(uploadRequest, cookies);
        var uploadResponse = await authenticatedClient.SendAsync(uploadRequest);
        uploadResponse.EnsureSuccessStatusCode();
        var uploadData = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var pdfId = uploadData.GetProperty("pdfId").GetGuid();
        _output.WriteLine($"✅ PDF uploaded: {pdfId}");

        // STEP 4: Wait for text extraction (polling)
        _output.WriteLine("\n⏳ STEP 4: Waiting for text extraction...");
        var textReady = false;
        var retries = 0;
        const int maxRetries = 10;

        while (!textReady && retries < maxRetries)
        {
            await Task.Delay(1000); // Wait 1 second
            var textRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/pdfs/{pdfId}/text");
            AddCookies(textRequest, cookies);
            var textResponse = await authenticatedClient.SendAsync(textRequest);

            if (textResponse.StatusCode == HttpStatusCode.OK)
            {
                var textData = await textResponse.Content.ReadFromJsonAsync<JsonElement>();
                var status = textData.GetProperty("status").GetString();
                _output.WriteLine($"⏳ Status: {status}");

                if (status == "completed")
                {
                    textReady = true;
                    _output.WriteLine("✅ Text extraction completed");
                }
                else if (status == "failed")
                {
                    throw new Exception("PDF text extraction failed");
                }
            }
            retries++;
        }

        textReady.Should().BeTrue("Text extraction should complete within 10 seconds");

        // STEP 5: Verify RuleSpec generation
        _output.WriteLine("\n📋 STEP 5: Verifying RuleSpec generation...");
        var ruleSpecRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/games/{gameId}/rulespec");
        AddCookies(ruleSpecRequest, cookies);
        var ruleSpecResponse = await authenticatedClient.SendAsync(ruleSpecRequest);
        ruleSpecResponse.EnsureSuccessStatusCode();
        var ruleSpecData = await ruleSpecResponse.Content.ReadFromJsonAsync<JsonElement>();
        ruleSpecData.ValueKind.Should().NotBe(JsonValueKind.Null);
        _output.WriteLine("✅ RuleSpec generated successfully");

        // STEP 6: Index PDF in Qdrant
        _output.WriteLine("\n🔍 STEP 6: Indexing PDF in Qdrant...");
        var indexRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ingest/pdf/{pdfId}/index");
        AddCookies(indexRequest, cookies);
        var indexResponse = await authenticatedClient.SendAsync(indexRequest);
        indexResponse.EnsureSuccessStatusCode();
        _output.WriteLine("✅ PDF indexed in Qdrant");

        // STEP 7: Query RAG agent
        _output.WriteLine("\n💬 STEP 7: Querying RAG agent...");
        var qaRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa")
        {
            Content = JsonContent.Create(new
            {
                question = "What is the objective of the game?",
                gameId
            })
        };
        AddCookies(qaRequest, cookies);
        var qaResponse = await authenticatedClient.SendAsync(qaRequest);
        qaResponse.EnsureSuccessStatusCode();
        var qaData = await qaResponse.Content.ReadFromJsonAsync<JsonElement>();

        // STEP 8: Verify answer quality
        _output.WriteLine("\n✅ STEP 8: Verifying answer quality...");
        qaData.TryGetProperty("answer", out var answerProp).Should().BeTrue();
        var answer = answerProp.GetString();
        answer.Should().NotBeNullOrWhiteSpace();
        _output.WriteLine($"📝 Answer: {answer}");

        qaData.TryGetProperty("snippets", out var snippetsProp).Should().BeTrue();
        var snippets = snippetsProp.EnumerateArray().ToList();
        snippets.Should().NotBeEmpty("RAG should return citations");
        _output.WriteLine($"📚 Citations: {snippets.Count} snippets found");

        // Verify snippets structure
        foreach (var snippet in snippets)
        {
            snippet.TryGetProperty("text", out _).Should().BeTrue();
            snippet.TryGetProperty("pageNumber", out var pageProp).Should().BeTrue();
            snippet.TryGetProperty("score", out var scoreProp).Should().BeTrue();

            var score = scoreProp.GetDouble();
            score.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
            _output.WriteLine($"  - Page {pageProp.GetInt32()}: Score {score:F2}");
        }

        // STEP 9: Submit feedback
        _output.WriteLine("\n⭐ STEP 9: Submitting feedback...");
        var messageId = qaData.GetProperty("messageId").GetGuid();
        var feedbackRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/feedback")
        {
            Content = JsonContent.Create(new
            {
                messageId,
                isHelpful = true,
                comment = "E2E test: RAG pipeline working correctly!"
            })
        };
        AddCookies(feedbackRequest, cookies);
        var feedbackResponse = await authenticatedClient.SendAsync(feedbackRequest);
        feedbackResponse.EnsureSuccessStatusCode();
        _output.WriteLine("✅ Feedback submitted");

        _output.WriteLine("\n🎉 COMPLETE RAG FLOW SUCCESSFUL!");
    }

    [Fact]
    public async Task RagFlow_WithStreamingQA_ReturnsSSEStream()
    {
        // Arrange - Authenticate as Editor
        _output.WriteLine("🔐 Authenticating as Editor...");
        var cookies = await RegisterAndAuthenticateAsync(_client, "rag-streaming@test.com", "Editor");
        var authenticatedClient = CreateClientWithoutCookies();

        // Create game
        var gameRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/games")
        {
            Content = JsonContent.Create(new
            {
                title = "Streaming Test Game",
                publisher = "Test",
                yearPublished = 2024,
                minPlayers = 2,
                maxPlayers = 2
            })
        };
        AddCookies(gameRequest, cookies);
        var gameResponse = await authenticatedClient.SendAsync(gameRequest);
        gameResponse.EnsureSuccessStatusCode();
        var gameData = await gameResponse.Content.ReadFromJsonAsync<JsonElement>();
        var gameId = gameData.GetProperty("id").GetGuid();

        // Act - Query streaming endpoint
        _output.WriteLine("\n📡 Querying streaming QA endpoint...");
        var streamRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa/stream")
        {
            Content = JsonContent.Create(new
            {
                question = "What is the objective?",
                gameId
            })
        };
        AddCookies(streamRequest, cookies);
        var streamResponse = await authenticatedClient.SendAsync(streamRequest, HttpCompletionOption.ResponseHeadersRead);

        // Assert
        streamResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        streamResponse.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");

        _output.WriteLine("✅ SSE stream established");

        // Read first few SSE events
        await using var stream = await streamResponse.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var eventCount = 0;
        var maxEvents = 5;

        while (eventCount < maxEvents && !reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(line))
            {
                _output.WriteLine($"📨 SSE Event: {line}");
                eventCount++;
            }
        }

        eventCount.Should().BeGreaterThan(0, "Should receive SSE events");
    }

    /// <summary>
    /// Creates a minimal valid PDF content for testing
    /// </summary>
    private static byte[] CreateSimplePdfContent()
    {
        // Minimal PDF structure (simplified for testing)
        var pdfContent = @"%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /Resources 4 0 R /MediaBox [0 0 612 792] /Contents 5 0 R >>
endobj
4 0 obj
<< /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >>
endobj
5 0 obj
<< /Length 85 >>
stream
BT
/F1 12 Tf
100 700 Td
(Objective: Be the first player to get three in a row.) Tj
ET
endstream
endobj
xref
0 6
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000214 00000 n
0000000303 00000 n
trailer
<< /Size 6 /Root 1 0 R >>
startxref
437
%%EOF";

        return Encoding.ASCII.GetBytes(pdfContent);
    }
}
