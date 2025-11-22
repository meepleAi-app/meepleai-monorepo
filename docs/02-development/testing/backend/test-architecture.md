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

## Performance Optimization

### Current Performance Issues

1. **Container per test class**: Each test class creates new PostgreSQL container (~2-3s)
2. **Manual database reset**: TRUNCATE-based cleanup is slow (~50-100ms per test)
3. **No shared fixtures**: No resource sharing across test classes
4. **EF Core tracking conflicts**: 5/15 cross-context tests fail due to tracking issues

**Current Performance**: ~58 seconds for 15 test classes, 162 tests

### Recommended Optimizations (See: `docs/02-development/testing/integration-tests-performance-guide.md`)

**Priority 1: Shared Database Fixture** (HIGH IMPACT)
- Use xUnit `ICollectionFixture<T>` to share containers across test classes
- **Impact**: 50-70% faster test suite
- **Effort**: 2-3 hours

**Priority 2: Respawn Integration** (HIGH IMPACT)
- Replace manual TRUNCATE with Respawn.Postgres library
- **Impact**: 3-13x faster database reset
- **Effort**: 1-2 hours

**Priority 3: AsNoTracking Pattern** (MEDIUM IMPACT)
- Apply `AsNoTracking()` to read-only repository queries
- Fix `UpdateAsync()` tracking conflicts
- **Impact**: 30% faster queries, resolves 5/15 failing tests
- **Effort**: 2-4 hours

**Priority 4: Container Reuse** (LOCAL DEV ONLY)
- Enable `.WithReuse(true)` for local development
- **Impact**: 50x faster iterations (10s → 200ms)
- **Effort**: 30 minutes

**Expected Results**:
- CI/CD: 58s → 13s (4.5x faster)
- Local Dev: 58s → 3-4s with reuse (15-20x faster)
- Test Success Rate: 67% → 100% (fix tracking conflicts)

For detailed implementation patterns, see **Integration Tests Performance Guide**.

## Recommendations

### Short Term (Completed ✅)
1. ✅ **DONE**: Fix Docnet.Core thread safety issue
2. ✅ **DONE**: Document test architecture and limitations
3. ✅ **DONE**: Create comprehensive performance optimization guide
4. Skip E2E tests in CI until proper infrastructure is available

### Medium Term (Recommended)
1. **Performance Optimizations** (2-4 weeks):
   - Implement shared database fixture with xUnit collections
   - Integrate Respawn.Postgres for fast database reset
   - Apply AsNoTracking pattern to repository queries
   - Enable container reuse for local development
2. Create separate test projects:
   - `Api.UnitTests` - Pure unit tests
   - `Api.IntegrationTests` - Tests with mocks
   - `Api.E2ETests` - Tests requiring real services
3. Implement proper mock strategies for:
   - Qdrant search results (return configurable test data)
   - LLM responses (return configurable test responses)
4. Add category attributes to tests for selective execution

### Long Term
1. Set up test Qdrant instance in CI
2. Use OpenRouter test API key in CI (if available)
3. Implement contract testing for external dependencies
4. Add performance benchmarks
5. Parallel test execution with multiple collections (2-4x faster)

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

- **Integration Tests Performance Guide**: `docs/02-development/testing/integration-tests-performance-guide.md` ⭐ NEW
- **Testing Guide**: `docs/02-development/testing/testing-guide.md`
- **Known Issues**: `docs/07-project-management/tracking/integration-tests-known-issues.md`
- Main documentation: `CLAUDE.md`
- Security scanning: `docs/06-security/code-scanning-remediation-summary.md`
