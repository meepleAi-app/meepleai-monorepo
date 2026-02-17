# Vector Embedding Query Performance Validation

**Issue**: #3987
**Parent**: #3493 (PostgreSQL Schema Extensions)

## Overview

Performance tests validate that pgvector similarity queries across Context Engineering tables meet the <100ms P95 latency target. Tests use Testcontainers with PostgreSQL + pgvector and realistic data volumes.

## Performance Targets

| Table | Query Type | Target P95 | Target P99 |
|-------|-----------|-----------|-----------|
| `conversation_memory` | Vector similarity (top-10) | <100ms | <200ms |
| `agent_game_state_snapshots` | Vector similarity (top-5) | <100ms | <200ms |
| `strategy_patterns` | Vector similarity (top-20) | <100ms | <200ms |
| `conversation_memory` | Temporal + vector hybrid | <150ms | <300ms |
| All tables | Cold query (first execution) | <200ms | - |
| All tables | 10 concurrent queries | <1000ms total | - |

## Test Data Volumes

| Table | Record Count | Embedding Dimensions | Notes |
|-------|-------------|---------------------|-------|
| `conversation_memory` | 10,000 | 1536 | Random users/games, 80% with game_id |
| `agent_game_state_snapshots` | 5,000 | 1536 | Random game/session distribution |
| `strategy_patterns` | 1,000 | 1536 | 4 phases, 3 sources, score 0.2-1.0 |
| `users` | 100 | - | Parent entities for FK |
| `games` | 50 | - | Parent entities for FK |

All embeddings are normalized unit vectors (1536 dimensions) generated with fixed random seeds for reproducibility.

## Test Scenarios

### 1. Pure Vector Similarity (3 tests)

Tests raw cosine distance queries using the `<=>` operator against each table.

| Test | Table | Top-K | Iterations | Target |
|------|-------|-------|-----------|--------|
| `ConversationMemory_VectorSimilaritySearch_Top10` | conversation_memory | 10 | 100 | P95 <100ms |
| `GameStateSnapshot_VectorSimilaritySearch_Top5` | agent_game_state_snapshots | 5 | 100 | P95 <100ms |
| `StrategyPattern_VectorSimilaritySearch_Top20` | strategy_patterns | 20 | 100 | P95 <100ms |

**Query pattern** (raw SQL):
```sql
SELECT * FROM conversation_memory
ORDER BY embedding <=> $1::vector
LIMIT 10;
```

### 2. Hybrid Queries (3 tests)

Tests vector similarity combined with scalar filters using LINQ `.CosineDistance()`.

| Test | Filters | Top-K | Iterations | Target |
|------|---------|-------|-----------|--------|
| `ConversationMemory_VectorSearchWithFilter` | user_id + game_id IS NOT NULL | 10 | 50 | P95 <150ms |
| `GameStateSnapshot_VectorSearchByGame` | game_id | 5 | 50 | P95 <100ms |
| `StrategyPattern_VectorSearchByGameAndPhase` | game_id + applicable_phase | 10 | 50 | P95 <100ms |

**Query pattern** (LINQ):
```csharp
dbContext.ConversationMemories
    .Where(m => m.UserId == targetUserId && m.GameId != null)
    .OrderBy(m => m.Embedding!.CosineDistance(queryEmbedding))
    .Take(10)
    .AsNoTracking()
    .ToListAsync();
```

### 3. Cold Query Performance (1 test)

Tests first-execution latency on a fresh connection (no query plan cache).

| Test | Connection | Target |
|------|-----------|--------|
| `ColdQuery_FirstExecution_ShouldStillMeetTarget` | New NpgsqlConnection | <200ms |

### 4. Concurrent Query Performance (1 test)

Tests throughput under parallel load using `Task.WhenAll`.

| Test | Parallelism | Target |
|------|------------|--------|
| `ConcurrentVectorQueries_10Parallel` | 10 queries | <1000ms total, P95 <200ms |

### 5. Index Usage Validation (1 test)

Verifies query execution plans via `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)`.

| Test | Output |
|------|--------|
| `VectorQueries_ShouldUseIndexes_VerifyWithExplainAnalyze` | JSON query plan logged to console |

## Running the Tests

```bash
# Run all vector performance tests
dotnet test --filter "Issue=3987"

# Run specific scenario
dotnet test --filter "FullyQualifiedName~VectorEmbeddingPerformanceTests.ConversationMemory"
dotnet test --filter "FullyQualifiedName~VectorEmbeddingPerformanceTests.GameStateSnapshot"
dotnet test --filter "FullyQualifiedName~VectorEmbeddingPerformanceTests.StrategyPattern"
dotnet test --filter "FullyQualifiedName~VectorEmbeddingPerformanceTests.ConcurrentVector"
dotnet test --filter "FullyQualifiedName~VectorEmbeddingPerformanceTests.ColdQuery"
dotnet test --filter "FullyQualifiedName~VectorEmbeddingPerformanceTests.VectorQueries_ShouldUseIndexes"

# Run all performance tests (includes other BC performance tests)
dotnet test --filter "Category=Performance"
```

**Prerequisites**: Docker must be running (Testcontainers starts PostgreSQL with pgvector automatically).

**Note**: Performance tests seed 16K+ records with embeddings. Initial setup takes 30-60 seconds.

## CI/CD Integration

### Performance Regression Detection

Performance tests run as part of the `Category=Performance` test filter. To add to CI:

```yaml
# .github/workflows/performance-tests.yml
- name: Run Performance Tests
  run: |
    dotnet test --filter "Category=Performance" \
      --logger "trx;LogFileName=performance-results.trx" \
      --results-directory ./TestResults
  timeout-minutes: 15
```

### Performance Gate

Tests use hard assertions (P95 <100ms) that fail the build if performance regresses:
- `p95.Should().BeLessThan(100.0)` for pure vector queries
- `p95.Should().BeLessThan(150.0)` for hybrid queries
- `sw.ElapsedMilliseconds.Should().BeLessThan(200)` for cold queries

## Query Optimization Notes

### Vector Distance Operators

pgvector supports three distance metrics:
- `<=>` Cosine distance (used in our tests)
- `<->` L2 (Euclidean) distance
- `<#>` Inner product (negative)

We use **cosine distance** as it's standard for text embeddings (OpenAI, sentence-transformers).

### Index Types

| Index Type | Best For | Trade-offs |
|-----------|---------|------------|
| **Sequential scan** | <10K rows | No index overhead, exact results |
| **IVFFlat** | 10K-1M rows | Fast build, approximate, needs `lists` tuning |
| **HNSW** | >100K rows | Best recall, slower build, more memory |

Current dataset (10K-16K rows) may use sequential scan. For production with larger datasets, create IVFFlat or HNSW indexes:

```sql
-- IVFFlat index (good for 10K-1M rows)
CREATE INDEX idx_conversation_memory_embedding
ON conversation_memory USING ivfflat (embedding vector_cosine_ops)
WITH (lists = 100);

-- HNSW index (better recall, slower build)
CREATE INDEX idx_conversation_memory_embedding_hnsw
ON conversation_memory USING hnsw (embedding vector_cosine_ops)
WITH (m = 16, ef_construction = 64);
```

### Monitoring Queries

```sql
-- Check index usage for vector queries
EXPLAIN (ANALYZE, BUFFERS)
SELECT id, content, embedding <=> '[0.1, 0.2, ...]'::vector AS distance
FROM conversation_memory
ORDER BY embedding <=> '[0.1, 0.2, ...]'::vector
LIMIT 10;

-- Check table sizes
SELECT
    relname AS table_name,
    pg_size_pretty(pg_total_relation_size(oid)) AS total_size,
    pg_size_pretty(pg_relation_size(oid)) AS table_size,
    pg_size_pretty(pg_total_relation_size(oid) - pg_relation_size(oid)) AS index_size
FROM pg_class
WHERE relname IN ('conversation_memory', 'agent_game_state_snapshots', 'strategy_patterns');

-- Check embedding column storage
SELECT
    table_name,
    column_name,
    data_type,
    udt_name
FROM information_schema.columns
WHERE table_schema = 'public'
AND column_name = 'embedding';
```

## Architecture Notes

- **Isolated databases**: Each test class creates its own database via `SharedTestcontainersFixture.CreateIsolatedDatabaseAsync()` to prevent interference
- **Batch seeding**: Data inserted in batches of 1000 to avoid memory pressure
- **Fixed random seeds**: Reproducible embeddings (seed 42/43/44 for data, 100-108 for queries)
- **Normalized vectors**: All embeddings are L2-normalized unit vectors for consistent cosine distance behavior
- **Warm-up queries**: First query excluded from measurements to separate JIT/plan compilation from steady-state performance
- **EF Core + raw SQL**: Raw SQL for exact operator control, LINQ for hybrid query testing
