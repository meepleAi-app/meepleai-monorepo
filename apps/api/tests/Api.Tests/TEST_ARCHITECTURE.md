# Test Architecture Documentation

## Overview

This document explains the test architecture for the MeepleAI API and categorizes tests based on their dependencies.

## Test Categories

### 1. Unit Tests
- **Dependencies**: None (pure logic)
- **Execution**: Always run in CI/CD
- **Examples**:
  - `RuleSpecV0ModelTests`
  - `ContractModelTests`
  - `RateLimitServiceTests` (with Redis mock)

### 2. Integration Tests with Mocks
- **Dependencies**: In-memory SQLite, mocked external services
- **Execution**: Always run in CI/CD
- **Mock Strategy**:
  - SQLite in-memory database for `MeepleAiDbContext`
  - Mocked Redis via `Mock<IConnectionMultiplexer>`
  - Mocked Qdrant via `Mock<IQdrantClientAdapter>` (**returns empty results**)
  - Mocked Embedding Service (**returns dummy 1536-dim zero vectors**)
  - Stubbed HttpClient (**returns empty JSON `{}`**)

**Current Limitation**: The current mock setup does NOT support:
- Vector search (Qdrant mock returns empty lists)
- LLM interactions (HttpClient stub returns `{}`)
- Real AI/RAG workflows

### 3. E2E Tests (Require External Services)
- **Dependencies**: Real Qdrant instance + OpenRouter API key
- **Execution**: Skip in CI/CD, run manually for validation
- **Examples**:
  - `PdfIndexingIntegrationTests.SearchIndexedPdf_FilteredByGame_ReturnsOnlyGameResults`
  - `ExplainEndpointTests.PostAgentsExplain_WhenAuthenticated_ReturnsExplanation`
  - `ExplainEndpointTests.PostAgentsExplain_TracksTokenUsage`
  - `ChessKnowledgeRetrievalPrecisionTests.*`

## Test Failures Explained

### Currently Failing Tests

| Test | Reason | Solution |
|------|--------|----------|
| `SearchIndexedPdf_FilteredByGame_ReturnsOnlyGameResults` | Qdrant mock returns empty list | Requires real Qdrant + OpenRouter |
| `PostAgentsExplain_WhenAuthenticated_ReturnsExplanation` | HttpClient stub returns `{}` | Requires real OpenRouter API |
| `PostAgentsExplain_TracksTokenUsage` | No real LLM call | Requires real OpenRouter API |
| `PostAgentsExplain_WithoutIndexedContent_ReturnsNoResults` | HttpClient stub returns `{}` | Requires real OpenRouter API |
| `IndexPdf_WithLargeText_CreatesMultipleChunks` | Chunking calculation off | Expected ≤110, got 119 (math issue) |
| `PostIngestPdf_WhenAdminUploadsValidPdf_ReturnsSuccessWithDocumentId` | Status mismatch | Expected "pending", got "processing" (timing issue) |
| RLS/Audit tests | Depend on vector search | Require real Qdrant |

## Running E2E Tests

To run E2E tests that require external services:

### Prerequisites

1. **Qdrant**: Running instance (Docker or cloud)
   ```bash
   docker run -p 6333:6333 qdrant/qdrant
   ```

2. **OpenRouter API Key**: Set environment variable
   ```bash
   export OPENROUTER_API_KEY="your-key-here"
   ```

3. **Update Test Configuration**: Create a test configuration that uses real services instead of mocks

### Running Specific E2E Tests

```bash
# Skip all tests that require external services (default)
dotnet test

# Run only E2E tests (requires infrastructure)
# TODO: Implement category-based filtering
dotnet test --filter "Category=E2E"
```

## Docnet.Core Thread Safety

**Issue Resolved** (2025-10-10): Docnet.Core is not thread-safe. When tests run in parallel, concurrent access to PDF extraction causes `AccessViolationException` crashes.

**Solution**: Added a static `SemaphoreSlim` in `PdfTextExtractionService` to serialize access to Docnet.Core:

```csharp
private static readonly SemaphoreSlim DocnetSemaphore = new(1, 1);

await DocnetSemaphore.WaitAsync(ct);
try
{
    var (rawText, pageCount) = await Task.Run(() => ExtractRawText(filePath), ct);
    // ... extraction logic
}
finally
{
    DocnetSemaphore.Release();
}
```

## Recommendations

### Short Term
1. ✅ **DONE**: Fix Docnet.Core thread safety issue
2. ✅ **DONE**: Document test architecture and limitations
3. Skip E2E tests in CI until proper infrastructure is available

### Medium Term
1. Create separate test projects:
   - `Api.UnitTests` - Pure unit tests
   - `Api.IntegrationTests` - Tests with mocks
   - `Api.E2ETests` - Tests requiring real services
2. Implement proper mock strategies for:
   - Qdrant search results (return configurable test data)
   - LLM responses (return configurable test responses)
3. Add category attributes to tests for selective execution

### Long Term
1. Set up test Qdrant instance in CI
2. Use OpenRouter test API key in CI (if available)
3. Implement contract testing for external dependencies
4. Add performance benchmarks

## CI/CD Strategy

### Current CI Pipeline

```yaml
- name: Run Tests
  run: cd apps/api && dotnet test
```

**Behavior**: Runs all tests except those marked with `Skip` attribute.

### Recommended CI Pipeline

```yaml
- name: Run Unit and Integration Tests
  run: |
    cd apps/api
    dotnet test --filter "Category!=E2E"

- name: Run E2E Tests (optional, gated)
  if: github.event_name == 'workflow_dispatch'
  env:
    OPENROUTER_API_KEY: ${{ secrets.OPENROUTER_API_KEY }}
    QDRANT_URL: ${{ secrets.TEST_QDRANT_URL }}
  run: |
    cd apps/api
    dotnet test --filter "Category=E2E"
```

## Test Data Management

### Demo Seed Data

The test suite uses the same seed data as production (see `DB-02` in CLAUDE.md):
- **Users**: admin@meepleai.dev, editor@meepleai.dev, user@meepleai.dev (password: `Demo123!`)
- **Games**: tic-tac-toe, chess
- **Agents**: Q&A and Explain agents for each game

### Test Data Cleanup

Tests that create data track created entities in `IntegrationTestBase` and clean up in `DisposeAsync()`:
- PDF documents
- Vector documents
- Users (test-specific)
- Games (test-specific)
- Chats

## References

- Main documentation: `CLAUDE.md`
- Security scanning: `docs/security-scanning.md`
- Code coverage: `docs/code-coverage.md`
