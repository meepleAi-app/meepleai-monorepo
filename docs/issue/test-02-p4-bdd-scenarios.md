# TEST-02-P4: BDD Test Scenarios Design

## Overview

Comprehensive BDD test scenarios for LlmService, RagService, and Infrastructure layer to achieve 90%+ coverage.

**Coverage Targets**:
- LlmService: 60% → 90% (+30%)
- RagService: 85% → 95% (+10%)
- Infrastructure: 65% → 90% (+25%)

**Total New Tests**: ~27 tests (15 unit, 12 integration)

---

## LlmService Test Scenarios (12 tests)

**Current Coverage**: ~60% | **Target**: 90% | **Gap**: +30%

### Unit Tests (8 tests)

#### 1. GenerateJsonAsync_WithMalformedJson_ThrowsJsonException
- **Description**: Tests JSON parsing when LLM returns invalid JSON (missing braces, trailing commas, etc.)
- **Type**: Unit
- **Coverage Impact**: HIGH - JSON parsing path currently untested
- **Dependencies to Mock**:
  - `HttpMessageHandler` → returns malformed JSON response
- **Assertions**:
  - Throws `JsonException` or `InvalidOperationException`
  - Error message contains "JSON" or "parsing"
  - No cache write occurs

#### 2. GenerateJsonAsync_WithEmptyJsonObject_ReturnsEmptyObject
- **Description**: Tests deserialization when LLM returns valid but empty JSON `{}`
- **Type**: Unit
- **Coverage Impact**: MEDIUM - Edge case for JSON deserialization
- **Dependencies to Mock**:
  - `HttpMessageHandler` → returns `{"choices": [{"message": {"content": "{}"}}]}`
- **Assertions**:
  - Returns empty object of type `T`
  - No exception thrown
  - Cache write succeeds

#### 3. GenerateCompletionAsync_WithLargeContextWindow_TruncatesCorrectly
- **Description**: Tests token counting and context window limits (>4096 tokens)
- **Type**: Unit
- **Coverage Impact**: HIGH - Token counting logic likely uncovered
- **Dependencies to Mock**:
  - `HttpMessageHandler` → simulates API returning token limit error
- **Assertions**:
  - Request includes correct `max_tokens` parameter
  - Handles 400 error with "context_length_exceeded" gracefully
  - Returns truncated response or throws specific exception

#### 4. GenerateCompletionAsync_WithRateLimitError_RetriesAndFails
- **Description**: Tests 429 rate limit handling and retry logic
- **Type**: Unit
- **Coverage Impact**: MEDIUM - Error handling path
- **Dependencies to Mock**:
  - `HttpMessageHandler` → returns 429 on first N calls
- **Assertions**:
  - Retries up to configured max attempts
  - Throws `HttpRequestException` after max retries
  - Logs rate limit warnings

#### 5. GenerateCompletionStreamAsync_WithEmptyStreamResponse_ReturnsEmptyEnumerable
- **Description**: Tests streaming when API returns no tokens (empty SSE stream)
- **Type**: Unit
- **Coverage Impact**: HIGH - Streaming edge case
- **Dependencies to Mock**:
  - `HttpMessageHandler` → returns empty SSE stream
- **Assertions**:
  - `IAsyncEnumerable` completes without yielding tokens
  - No exception thrown
  - Proper stream disposal

#### 6. GenerateCompletionStreamAsync_WithCancellationToken_CancelsGracefully
- **Description**: Tests mid-stream cancellation via `CancellationToken`
- **Type**: Unit
- **Coverage Impact**: HIGH - Cancellation logic likely untested
- **Dependencies to Mock**:
  - `HttpMessageHandler` → returns slow stream
  - `CancellationTokenSource` → triggers after 2 tokens
- **Assertions**:
  - Stops yielding tokens after cancellation
  - Throws `OperationCanceledException`
  - Disposes HttpClient properly

#### 7. GenerateCompletionAsync_WithNullApiKey_ThrowsArgumentException
- **Description**: Tests configuration validation when API key is null/empty
- **Type**: Unit
- **Coverage Impact**: MEDIUM - Input validation
- **Dependencies to Mock**:
  - `IConfiguration` → returns null for OPENROUTER_API_KEY
- **Assertions**:
  - Throws `ArgumentNullException` or `InvalidOperationException` at service construction
  - Error message mentions "API key"

#### 8. GenerateCompletionAsync_WithCacheHit_SkipsApiCall
- **Description**: Tests cache integration when response is already cached
- **Type**: Unit
- **Coverage Impact**: HIGH - Cache integration path
- **Dependencies to Mock**:
  - `IAiResponseCacheService.GetCachedResponseAsync()` → returns cached response
  - `HttpMessageHandler` → should not be called
- **Assertions**:
  - Returns cached response
  - HttpClient never invoked (verify via mock)
  - Logs cache hit

---

### Integration Tests (4 tests)

#### 9. GenerateCompletionStreamAsync_WithRealTimeout_ThrowsTaskCanceledException
- **Description**: Integration test for streaming timeout with real HttpClient timeout
- **Type**: Integration (no external API, local mock server)
- **Coverage Impact**: HIGH - Real timeout behavior
- **Dependencies**:
  - Mock HTTP server (WireMock or similar) that delays indefinitely
- **Assertions**:
  - Throws `TaskCanceledException` or `OperationCanceledException`
  - Timeout matches configured value (e.g., 30s)
  - Logs timeout error

#### 10. GenerateCompletionAsync_WithConcurrentRequests_HandlesThreadSafety
- **Description**: Tests thread safety with 10+ concurrent LLM requests
- **Type**: Integration (Testcontainers Redis for cache)
- **Coverage Impact**: MEDIUM - Concurrency issues
- **Dependencies**:
  - Redis (Testcontainers) for cache
  - Mock HttpMessageHandler for API
- **Assertions**:
  - All 10 requests complete successfully
  - No race conditions in cache writes
  - Correct number of API calls (accounting for cache)

#### 11. GenerateJsonAsync_WithRealLlmResponse_DeserializesComplexObject
- **Description**: End-to-end test with realistic JSON response structure (nested objects, arrays)
- **Type**: Integration (mock server with realistic response)
- **Coverage Impact**: MEDIUM - Real-world JSON complexity
- **Dependencies**:
  - Mock HTTP server with realistic OpenRouter response
- **Assertions**:
  - Deserializes complex nested object correctly
  - All properties populated
  - No data loss

#### 12. GenerateCompletionStreamAsync_WithNetworkInterruption_HandlesGracefully
- **Description**: Tests streaming resilience when network drops mid-stream
- **Type**: Integration (mock server simulates disconnect)
- **Coverage Impact**: HIGH - Real-world failure scenario
- **Dependencies**:
  - Mock HTTP server that closes connection after N tokens
- **Assertions**:
  - Throws appropriate exception (`HttpRequestException` or `IOException`)
  - Partial tokens yielded before disconnect
  - Logs network error

---

## RagService Test Scenarios (7 tests)

**Current Coverage**: ~85% | **Target**: 95% | **Gap**: +10%

### Unit Tests (4 tests)

#### 13. SearchAsync_WithQueryExpansion_GeneratesMultipleQueries
- **Description**: Tests query expansion logic (if implemented) or validates single-query behavior
- **Type**: Unit
- **Coverage Impact**: MEDIUM - Query preprocessing path
- **Dependencies to Mock**:
  - `IEmbeddingService.GenerateEmbeddingAsync()` → returns mock embedding
  - `IQdrantService.SearchAsync()` → returns mock results
- **Assertions**:
  - Generates expected number of query variants (or confirms single query)
  - Each variant embedded separately (or single embedding)
  - Results merged/ranked correctly

#### 14. SearchAsync_WithEmptyKnowledgeBase_ReturnsEmptyResults
- **Description**: Tests behavior when Qdrant collection is empty (no documents indexed)
- **Type**: Unit
- **Coverage Impact**: HIGH - Edge case likely untested
- **Dependencies to Mock**:
  - `IQdrantService.SearchAsync()` → returns empty list
- **Assertions**:
  - Returns empty result set (not null)
  - No exception thrown
  - Logs "no results" warning

#### 15. SearchAsync_WithMultiGameContext_FiltersCorrectly
- **Description**: Tests game-specific filtering when multiple games have similar content
- **Type**: Unit
- **Coverage Impact**: MEDIUM - Filter logic validation
- **Dependencies to Mock**:
  - `IQdrantService.SearchAsync()` → returns results from multiple games
- **Assertions**:
  - Results filtered to specified `gameId`
  - No cross-game contamination
  - Correct number of results

#### 16. SearchAsync_WithCacheHit_SkipsEmbeddingAndSearch
- **Description**: Tests cache integration when query+gameId combination is cached
- **Type**: Unit
- **Coverage Impact**: HIGH - Cache bypass path
- **Dependencies to Mock**:
  - `IAiResponseCacheService.GetCachedResponseAsync()` → returns cached RAG results
  - `IEmbeddingService`, `IQdrantService` → should not be called
- **Assertions**:
  - Returns cached results
  - Embedding/search services never invoked (verify via mock)
  - Logs cache hit

---

### Integration Tests (3 tests)

#### 17. SearchAsync_WithRealQdrant_RanksResultsByRelevance
- **Description**: End-to-end RAG search with real Qdrant, validates ranking algorithm
- **Type**: Integration (Testcontainers Qdrant)
- **Coverage Impact**: HIGH - Real vector search behavior
- **Dependencies**:
  - Qdrant (Testcontainers) with pre-indexed test documents
  - Real `EmbeddingService` (or mock with consistent embeddings)
- **Assertions**:
  - Results ranked by score (descending)
  - Top result matches expected document
  - Minimum score threshold applied

#### 18. SearchAsync_WithConcurrentRequests_HandlesThreadSafety
- **Description**: Tests RAG pipeline thread safety with 10+ concurrent queries
- **Type**: Integration (Testcontainers Qdrant + Redis)
- **Coverage Impact**: MEDIUM - Concurrency validation
- **Dependencies**:
  - Qdrant (Testcontainers) with test data
  - Redis (Testcontainers) for cache
- **Assertions**:
  - All requests complete successfully
  - No race conditions in cache/Qdrant access
  - Results consistent across concurrent requests

#### 19. SearchAsync_WithLongQuery_TruncatesAndSearches
- **Description**: Tests behavior with extremely long queries (>1000 characters)
- **Type**: Integration (Testcontainers Qdrant)
- **Coverage Impact**: MEDIUM - Input validation edge case
- **Dependencies**:
  - Qdrant (Testcontainers)
  - Real `EmbeddingService` (or mock)
- **Assertions**:
  - Query truncated to max length (or handled gracefully)
  - Embedding generated successfully
  - Search completes without error

---

## Infrastructure Layer Test Scenarios (8 tests)

**Current Coverage**: ~65% | **Target**: 90% | **Gap**: +25%

### Unit Tests (3 tests)

#### 20. MeepleAiDbContext_OnModelCreating_ConfiguresAllEntities
- **Description**: Validates entity configuration (indexes, constraints, relationships)
- **Type**: Unit (SQLite in-memory)
- **Coverage Impact**: HIGH - Configuration validation
- **Dependencies**:
  - SQLite in-memory database
- **Assertions**:
  - All 15+ DbSets registered
  - Primary keys configured
  - Foreign key relationships defined
  - Unique constraints applied (e.g., `user_sessions.session_token`)
  - Indexes created (e.g., `users.email`, `games.slug`)

#### 21. EntityConstraints_WithInvalidData_ThrowsDbUpdateException
- **Description**: Tests entity-level constraints (e.g., email format, required fields)
- **Type**: Unit (SQLite in-memory)
- **Coverage Impact**: MEDIUM - Constraint enforcement
- **Dependencies**:
  - SQLite in-memory database
- **Assertions**:
  - Inserting null required field → throws `DbUpdateException`
  - Inserting duplicate unique key → throws `DbUpdateException`
  - Invalid foreign key → throws `DbUpdateException`

#### 22. SeedData_FromMigration_CreatesExpectedEntities
- **Description**: Validates seed data migration (20251009140700_SeedDemoData)
- **Type**: Unit (SQLite in-memory)
- **Coverage Impact**: HIGH - Seed data validation
- **Dependencies**:
  - SQLite in-memory with migrations applied
- **Assertions**:
  - 3 demo users created (admin, editor, user)
  - 2 demo games created (Tic-Tac-Toe, Chess)
  - Rule specs exist for both games
  - Default agents configured

---

### Integration Tests (5 tests)

#### 23. MigrationApplication_FromScratch_AppliesAllMigrations
- **Description**: Tests full migration sequence from empty database to latest schema
- **Type**: Integration (Testcontainers Postgres)
- **Coverage Impact**: HIGH - Migration integrity
- **Dependencies**:
  - PostgreSQL (Testcontainers)
- **Assertions**:
  - All 40+ migrations apply successfully
  - No SQL errors
  - `__EFMigrationsHistory` table populated correctly
  - Final schema matches expected structure

#### 24. TransactionRollback_OnError_RevertsChanges
- **Description**: Tests transaction behavior with SaveChanges failure
- **Type**: Integration (Testcontainers Postgres)
- **Coverage Impact**: MEDIUM - Transaction semantics
- **Dependencies**:
  - PostgreSQL (Testcontainers)
- **Assertions**:
  - Begin transaction → modify entities → force error → rollback
  - Database state unchanged after rollback
  - No partial writes

#### 25. CascadingDelete_OnGameDeletion_DeletesRelatedEntities
- **Description**: Tests cascade delete behavior (e.g., deleting Game deletes RuleSpecs, ChatMessages, etc.)
- **Type**: Integration (Testcontainers Postgres)
- **Coverage Impact**: HIGH - Referential integrity
- **Dependencies**:
  - PostgreSQL (Testcontainers)
- **Assertions**:
  - Delete game → related rule specs deleted
  - Delete game → related chat messages deleted
  - Delete game → related PDF documents deleted
  - Orphan check: no orphaned records remain

#### 26. ConcurrentAccess_WithOptimisticConcurrency_HandlesConflicts
- **Description**: Tests concurrency tokens (if configured) or last-write-wins behavior
- **Type**: Integration (Testcontainers Postgres)
- **Coverage Impact**: MEDIUM - Concurrency control
- **Dependencies**:
  - PostgreSQL (Testcontainers)
- **Assertions**:
  - Two transactions modify same entity concurrently
  - One succeeds, one fails with `DbUpdateConcurrencyException` (or both succeed with last-write-wins)
  - Database state consistent

#### 27. ConnectionResilience_WithTransientFailure_RetriesSuccessfully
- **Description**: Tests connection retry logic with transient network failures
- **Type**: Integration (Testcontainers Postgres with network interruption)
- **Coverage Impact**: LOW - Resilience validation
- **Dependencies**:
  - PostgreSQL (Testcontainers) with simulated network issue
- **Assertions**:
  - Transient failure → automatic retry → success
  - Logs retry attempts
  - No data loss

---

## Test Implementation Priority

### High Priority (Implement First)
1. **LlmService**: #1 (JSON parsing), #3 (token counting), #5 (empty stream), #6 (cancellation), #8 (cache hit)
2. **RagService**: #14 (empty knowledge base), #16 (cache hit), #17 (real Qdrant ranking)
3. **Infrastructure**: #20 (entity config), #23 (migration application), #25 (cascade delete)

**Rationale**: These tests target the largest coverage gaps and most critical failure scenarios.

### Medium Priority (Implement Second)
1. **LlmService**: #2 (empty JSON), #4 (rate limit), #9 (real timeout), #12 (network interruption)
2. **RagService**: #13 (query expansion), #15 (multi-game), #18 (concurrency)
3. **Infrastructure**: #21 (constraints), #24 (transaction rollback), #26 (concurrency)

### Low Priority (Implement If Time Permits)
1. **LlmService**: #7 (null API key), #10 (concurrency), #11 (complex JSON)
2. **RagService**: #19 (long query)
3. **Infrastructure**: #22 (seed data), #27 (connection resilience)

---

## Expected Coverage Outcomes

| Component      | Current | Target | New Tests | Expected Final |
|----------------|---------|--------|-----------|----------------|
| LlmService     | 60%     | 90%    | 12        | 91-93%         |
| RagService     | 85%     | 95%    | 7         | 95-97%         |
| Infrastructure | 65%     | 90%    | 8         | 89-92%         |

**Assumptions**:
- Each high-priority test adds ~3-5% coverage
- Medium-priority tests add ~1-3% coverage
- Low-priority tests add ~0.5-1% coverage
- Some overlap between tests (diminishing returns)

---

## Implementation Notes

### Shared Test Fixtures
- **LlmServiceTestFixture**: Mock HttpClient, IConfiguration, ICacheService
- **RagServiceTestFixture**: Mock IEmbeddingService, IQdrantService, ILlmService, ICacheService
- **InfrastructureTestFixture**: SQLite in-memory DbContext for unit tests, Testcontainers Postgres for integration tests

### Mock Response Examples
```csharp
// LlmService: Malformed JSON response
var malformedJsonResponse = @"{
  ""choices"": [{
    ""message"": {
      ""content"": ""{ invalid json, }""
    }
  }]
}";

// RagService: Empty Qdrant response
var emptyQdrantResponse = new List<VectorSearchResult>();

// Infrastructure: Seed data validation query
var seedUsers = await context.Users
    .Where(u => u.Email.EndsWith("@meepleai.dev"))
    .ToListAsync();
```

### BDD Naming Convention Examples
```
✅ GenerateJsonAsync_WithMalformedJson_ThrowsJsonException
✅ SearchAsync_WithEmptyKnowledgeBase_ReturnsEmptyResults
✅ MigrationApplication_FromScratch_AppliesAllMigrations

❌ TestJsonParsing (too vague)
❌ When_Search_Returns_Empty (incorrect format)
❌ test_migration_apply (wrong case)
```

---

## Success Criteria

**Coverage**:
- [ ] LlmService coverage ≥90%
- [ ] RagService coverage ≥95%
- [ ] Infrastructure coverage ≥90%

**Quality**:
- [ ] All tests follow BDD naming convention
- [ ] All tests use Arrange-Act-Assert structure
- [ ] No flaky tests (>99% pass rate on CI)
- [ ] Integration tests complete in <3 minutes

**Documentation**:
- [ ] All test scenarios documented in this file
- [ ] Coverage report updated in `docs/code-coverage.md`
- [ ] PR description links to this design doc

---

## Next Steps

1. **Review this design document** with team for feedback
2. **Create TEST-02-P4 implementation issue** on GitHub
3. **Implement high-priority tests first** (Phase 1)
4. **Validate coverage gains** after each phase
5. **Adjust priorities** based on actual coverage impact
6. **Complete all tests** within 3-4 day timeline

---

## References

- **TEST-02 Initiative**: Systematic test coverage improvement
- **Existing Patterns**: `EmailServiceTests.cs`, `PasswordResetServiceTests.cs`, `ChatExportServiceTests.cs`
- **Coverage Baseline**: Current coverage report (60%, 85%, 65%)
- **Codebase**: `Services/LlmService.cs`, `Services/RagService.cs`, `Infrastructure/MeepleAiDbContext.cs`
