# Mock Implementation Documentation

## Overview

This document describes the intelligent mock implementations for Qdrant and OpenRouter LLM services in the test suite. These mocks allow integration tests to run without requiring real external services while still providing realistic behavior.

## Architecture

### In-Memory Qdrant Storage

**Location**: `WebApplicationFactoryFixture.cs:332-333`

```csharp
private static readonly Dictionary<string, List<Qdrant.Client.Grpc.PointStruct>> _mockQdrantStorage = new();
private static readonly object _storageLock = new object();
```

**Behavior**:
- Static storage persists across test runs (cleared between test sessions)
- Thread-safe with lock synchronization
- Organized by collection name as key

### Mock Operations

#### 1. Upsert (Lines 116-139)

**Functionality**:
- Stores points in the in-memory dictionary keyed by collection name
- Implements true upsert behavior: removes existing points with same IDs before adding new ones
- Thread-safe using lock

**Usage in Tests**:
```csharp
// When a test calls the indexing endpoint
var response = await _client.PostAsync($"/ingest/pdf/{pdfId}/index", null);

// The mock stores the points internally
// Later searches will return these stored points
```

#### 2. Search (Lines 141-195)

**Functionality**:
- Returns filtered results from in-memory storage
- Supports Qdrant `Filter` with `Must` conditions
- Filters by `game_id`, `pdf_id`, or `category` fields
- Returns scored points with decreasing scores (0.95, 0.94, 0.93...)
- Respects limit parameter

**Filter Matching**:
```csharp
// Extracts filter conditions
if (condition.Field != null)
{
    var fieldKey = condition.Field.Key;        // e.g., "game_id"
    var matchValue = condition.Field.Match?.Keyword;  // e.g., "tic-tac-toe"

    filteredPoints = filteredPoints.Where(p =>
        p.Payload.ContainsKey(fieldKey) &&
        p.Payload[fieldKey].StringValue == matchValue);
}
```

**Scoring**:
- First result: score = 0.95
- Second result: score = 0.94
- Third result: score = 0.93
- And so on...

This simulates relevance ranking without actual vector similarity computation.

#### 3. Delete (Lines 197-233)

**Functionality**:
- Removes points matching the filter from in-memory storage
- Supports same filter matching logic as search
- Thread-safe using lock

### Smart LLM Handler

**Location**: `WebApplicationFactoryFixture.cs:431-637`

**Class**: `SmartHttpClientFactory.SmartLlmHandler`

#### Request Detection

The handler analyzes the incoming HTTP request to determine the operation type:

```csharp
// Detects "explain" requests
bool isExplain = userContent.Contains("CONTEXT FROM RULEBOOK") &&
                !userContent.Contains("QUESTION:");

// Detects Q&A requests
bool isQa = userContent.Contains("CONTEXT FROM RULEBOOK") &&
           userContent.Contains("QUESTION:");

// Detects empty context (no indexed content)
bool hasNoContext = userContent.Contains("CONTEXT FROM RULEBOOK") &&
                   userContent.Contains("[Page") == false;
```

#### Response Generation

**For Explain Requests**:
```csharp
responseText = $@"# Explanation: {topic}

## Overview

{firstSnippet}

## Details

{remainingSnippets}

Based on the rulebook, {topic} involves the mechanics...";
```

**For Q&A Requests**:
```csharp
responseText = $"{firstSnippet} (see page 1 for details)";
```

**For Empty Context**:
- Explain: `"No relevant information found about this topic in the rulebook."`
- Q&A: `"Not specified"`

#### Context Snippet Extraction

The handler extracts text between `[Page N]` markers from the LLM prompt:

```csharp
// Input prompt from RagService:
[Page 1]
Players alternate marking X or O. Three in a row wins.

---

[Page 2]
If all squares are filled, the game is a draw.

// Extracted snippets:
["Players alternate marking X or O. Three in a row wins.",
 "If all squares are filled, the game is a draw."]
```

#### Token Usage Tracking

The mock estimates token usage for ADM-01 compliance:

```csharp
private static int EstimateTokens(string text)
{
    return Math.Max(1, text.Length / 4);  // 1 token ≈ 4 characters
}
```

Returns OpenRouter-compatible response:
```json
{
  "usage": {
    "prompt_tokens": 250,
    "completion_tokens": 100,
    "total_tokens": 350
  }
}
```

## Test Compatibility

### PDF Indexing Tests

**Test**: `PdfIndexingIntegrationTests.SearchIndexedPdf_FilteredByGame_ReturnsOnlyGameResults`

**Flow**:
1. Test creates two PDFs for different games:
   - `tic-tac-toe`: "Players alternate marking X or O. Three in a row wins."
   - `chess`: "The knight moves in an L-shape. Checkmate ends the game."

2. Test calls `/ingest/pdf/{pdfId}/index` for each PDF
   - Mock Qdrant stores points with `game_id` in payload

3. Test calls `/agents/qa` with `gameId = "tic-tac-toe"`
   - Mock Qdrant filters by `game_id` and returns only tic-tac-toe chunks
   - Mock LLM generates response containing "three in a row"

4. Test verifies response contains tic-tac-toe content, not chess content

### Explain Endpoint Tests

**Test**: `ExplainEndpointTests.PostAgentsExplain_WhenAuthenticated_ReturnsExplanation`

**Flow**:
1. Test seeds indexed content via `SeedIndexedContentAsync`:
   ```csharp
   var pdf = new PdfDocumentEntity {
       ExtractedText = "Winning Conditions: Three marks in a row..."
   };
   ```

2. **IMPORTANT**: Test must call the indexing endpoint to populate Qdrant storage:
   ```csharp
   // Currently missing in ExplainEndpointTests.SeedIndexedContentAsync
   await _client.PostAsync($"/ingest/pdf/{pdf.Id}/index", null);
   ```

3. Test calls `/agents/explain` with `topic = "winning conditions"`
   - RagService searches Qdrant (returns stored chunks)
   - RagService calls LLM with context
   - Mock LLM extracts snippets and builds explanation

4. Test verifies response contains:
   - `outline` with `mainTopic` and `sections`
   - `script` with explanation text
   - `citations` array with source references
   - `estimatedReadingTimeMinutes`

**Test**: `ExplainEndpointTests.PostAgentsExplain_WithoutIndexedContent_ReturnsNoResults`

**Flow**:
1. Test creates game WITHOUT calling indexing endpoint
2. Mock Qdrant returns empty results (no points stored)
3. RagService detects empty results and returns error response
4. Test verifies `script` contains "No relevant information found"

**Test**: `ExplainEndpointTests.PostAgentsExplain_TracksTokenUsage`

**Flow**:
1. Test calls `/agents/explain`
2. Mock LLM returns token usage in response
3. LlmService extracts token counts
4. RagService logs to `AiRequestLog` table
5. Test verifies log entry with correct token counts

## Known Issues and Limitations

### Issue 1: ExplainEndpointTests Needs Indexing Call

**Problem**: `SeedIndexedContentAsync` only creates database entities but doesn't populate Qdrant storage.

**Current Code** (lines 360-405 in ExplainEndpointTests.cs):
```csharp
private async Task SeedIndexedContentAsync(string gameId, string userId)
{
    // Creates PDF entity
    var pdf = new PdfDocumentEntity { ... };
    db.PdfDocuments.Add(pdf);

    // Creates vector document entity
    var vectorDoc = new VectorDocumentEntity { ... };
    db.Set<VectorDocumentEntity>().Add(vectorDoc);

    await db.SaveChangesAsync();

    // MISSING: Actual indexing call to populate Qdrant
}
```

**Solution**: Add indexing call after creating PDF:
```csharp
private async Task SeedIndexedContentAsync(string gameId, string userId)
{
    using var scope = Factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

    // Create PDF document
    var pdf = new PdfDocumentEntity
    {
        Id = $"pdf-explain-{gameId}-{TestRunId}",
        GameId = gameId,
        ExtractedText = "Winning Conditions: Three marks in a row...",
        ProcessingStatus = "completed",
        // ... other properties
    };
    db.PdfDocuments.Add(pdf);
    await db.SaveChangesAsync();

    // Index the PDF to populate Qdrant storage
    var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        HandleCookies = false
    });

    // Get admin session for authorization
    var adminCookies = await AuthenticateUserAsync("admin@meepleai.dev", "Demo123!");
    var request = new HttpRequestMessage(HttpMethod.Post, $"/ingest/pdf/{pdf.Id}/index");
    AddCookies(request, adminCookies);

    var indexResponse = await client.SendAsync(request);
    indexResponse.EnsureSuccessStatusCode();

    TrackPdfDocumentId(pdf.Id);
}
```

### Issue 2: Static Storage Persistence

**Behavior**: Qdrant storage is static, so points persist across tests in the same test session.

**Impact**: Tests may see data from previous tests if they don't use unique gameIds.

**Mitigation**: Tests use `TestRunId` suffix for game IDs to ensure isolation:
```csharp
var game = await CreateTestGameAsync($"Tic-Tac-Toe-{TestRunId}");
```

### Issue 3: No Real Vector Similarity

**Limitation**: Mock returns all points matching the filter, not the most similar ones.

**Impact**: Tests cannot verify semantic similarity accuracy.

**Acceptable Because**: These are integration tests focused on:
- Data flow (PDF → indexing → search → LLM → response)
- Authorization and validation
- Error handling
- Not vector search quality

## Testing the Implementation

### Quick Test

Run specific tests to verify mock behavior:

```powershell
# Test PDF indexing with filtering
dotnet test --filter "FullyQualifiedName~SearchIndexedPdf_FilteredByGame_ReturnsOnlyGameResults"

# Test explain endpoint
dotnet test --filter "FullyQualifiedName~ExplainEndpointTests.PostAgentsExplain_WhenAuthenticated"

# Test token tracking
dotnet test --filter "FullyQualifiedName~ExplainEndpointTests.PostAgentsExplain_TracksTokenUsage"
```

### Full Test Suite

```powershell
# Run all PDF indexing tests
dotnet test --filter "FullyQualifiedName~PdfIndexingIntegrationTests"

# Run all explain endpoint tests
dotnet test --filter "FullyQualifiedName~ExplainEndpointTests"
```

### Debugging

Enable detailed logging to see mock interactions:

```csharp
// In test file
[Fact]
public async Task MyTest()
{
    // Verify Qdrant storage state
    var storage = WebApplicationFactoryFixture._mockQdrantStorage;
    Assert.True(storage.ContainsKey("meepleai_documents"));
    Assert.NotEmpty(storage["meepleai_documents"]);

    // Verify points have correct payload
    var firstPoint = storage["meepleai_documents"][0];
    Assert.Equal("tic-tac-toe", firstPoint.Payload["game_id"].StringValue);
}
```

## Future Improvements

### 1. Automatic Cleanup Between Tests

Add cleanup to `WebApplicationFactoryFixture.Dispose`:

```csharp
protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        // Clear Qdrant storage
        lock (_storageLock)
        {
            _mockQdrantStorage.Clear();
        }

        _connection?.Close();
        _connection?.Dispose();
    }
    base.Dispose(disposing);
}
```

### 2. Vector Similarity Simulation

Implement basic cosine similarity for more realistic search:

```csharp
private static float CalculateSimilarity(float[] queryVector, float[] pointVector)
{
    // Simple dot product for all-zero vectors returns 0
    // Could implement basic pattern matching on text instead

    // For now, return decreasing scores based on order
    return 0.95f;
}
```

### 3. LLM Response Caching

The mock could cache responses for identical prompts to improve test performance:

```csharp
private static readonly Dictionary<string, string> _responseCache = new();

// In SendAsync:
var cacheKey = ComputeHash(userContent);
if (_responseCache.TryGetValue(cacheKey, out var cached))
{
    return CreateResponse(cached);
}
```

### 4. Configurable Mock Behavior

Allow tests to configure mock behavior:

```csharp
public class QdrantMockOptions
{
    public bool ReturnEmptyResults { get; set; }
    public float BaseScore { get; set; } = 0.95f;
    public int MaxResults { get; set; } = 10;
}

// Usage in test:
var factory = _factory.WithTestServices(services => {
    services.Configure<QdrantMockOptions>(opts => {
        opts.ReturnEmptyResults = true;  // Test error handling
    });
});
```

## Summary

The mock implementation provides:

✅ In-memory Qdrant storage with filtering by gameId/category
✅ Realistic LLM responses based on request type
✅ Token usage tracking for ADM-01 compliance
✅ Support for explain and Q&A operations
✅ "No results" handling for empty context
✅ Thread-safe concurrent access

This allows integration tests to run quickly without external dependencies while maintaining realistic behavior for test scenarios.
