# ADR-018: PostgreSQL Full-Text Search for SharedGameCatalog

**Status**: Accepted
**Date**: 2026-01-14
**Deciders**: Development Team
**Issue**: #2425 (Parent: #2374 Phase 5)

---

## Context

SharedGameCatalog requires full-text search for Italian board game catalog with:
- **Multilingual support**: Italian stemming, stop words, diacritics
- **Performance target**: P95 latency < 200ms (10x improvement over BGG ~2000ms baseline)
- **Scale**: ~10,000 games initially, growth to 50,000+ over time
- **Query patterns**: Search by title/description, filter by category/mechanic/players/time

We evaluated multiple search technologies to determine the optimal solution.

---

## Decision

**We use PostgreSQL native Full-Text Search with:**
- **Language configuration**: `'italian'` for proper stemming and stop words
- **GIN index**: `ix_shared_games_fts` on `to_tsvector('italian', title || ' ' || description)`
- **Query function**: `EF.Functions.PlainToTsQuery('italian', searchTerm)` for parameterized queries
- **Filtering**: Composite indexes for category/mechanic/players/playtime filters

---

## Alternatives Considered

### Alternative 1: Elasticsearch
**Pros**:
- Industry-standard full-text search
- Advanced features (fuzzy matching, boosting, aggregations)
- Horizontal scalability

**Cons**:
- ❌ Infrastructure complexity (additional service to manage)
- ❌ Overkill for < 50K documents
- ❌ Operational overhead (heap sizing, cluster management, backups)
- ❌ Cost (memory requirements 2-4GB minimum)
- ❌ Data synchronization (PostgreSQL → Elasticsearch latency)

**Verdict**: Rejected - unnecessary complexity for our scale.

### Alternative 2: Qdrant Vector Search
**Pros**:
- Already used for RAG vector search
- Semantic search capability
- No additional infrastructure

**Cons**:
- ❌ Not optimized for keyword-based search
- ❌ No Italian language-specific features (stemming, stop words)
- ❌ Hybrid search (keyword + vector) adds latency
- ❌ Less mature for traditional FTS compared to PostgreSQL

**Verdict**: Rejected - wrong tool for keyword search use case.

### Alternative 3: Simple LIKE Queries
**Pros**:
- Trivial implementation
- No indexes needed
- Zero learning curve

**Cons**:
- ❌ Poor performance (sequential scans, O(n) complexity)
- ❌ No ranking or relevance scoring
- ❌ No Italian stemming ("gioco" vs "giochi" are separate)
- ❌ Case-insensitive requires LOWER() function (index-unfriendly)

**Verdict**: Rejected - fails performance requirements.

---

## Rationale

### 1. Native Italian Language Support
PostgreSQL `'italian'` configuration provides:
- **Stemming**: "strategia" matches "strategie", "strategico"
- **Stop words**: Filters "il", "la", "di", "da" for cleaner matching
- **Diacritics**: Handles "città", "perché" correctly
- **Case-insensitive**: Built into `to_tsvector`

This is **critical** for Italian board game catalog (e.g., "Il Gioco della Città" matches "gioco città").

### 2. Proven Performance
**Measured Results** (Issue #2374 Phase 5):
- **GIN index creation**: One-time ~500ms for 10K games
- **Search latency P95**: < 200ms (target met)
- **Index size**: ~2MB for 10K games (minimal storage overhead)
- **Maintenance**: Auto-updated on INSERT/UPDATE (no manual reindexing)

Compared to:
- BGG baseline: ~2000ms (10x slower)
- Elasticsearch: Similar performance but with operational complexity

### 3. Zero Additional Infrastructure
PostgreSQL is **already required** for:
- User authentication (sessions, API keys)
- Game management (personal collections)
- Document processing (PDF metadata)
- RAG system (text chunks, though vectors in Qdrant)

Adding Elasticsearch would mean:
- Docker Compose service (+ monitoring)
- Backup strategy
- Data synchronization logic
- Version compatibility management

### 4. Integrated Transaction Support
PostgreSQL FTS allows:
- **ACID transactions**: Game creation + FTS indexing in single transaction
- **Referential integrity**: FK constraints between SharedGame and Categories/Mechanics
- **Composite queries**: JOIN categories + full-text search in single query

Elasticsearch requires eventual consistency (async indexing).

### 5. Cost-Effective Scaling
PostgreSQL FTS scales to **millions of documents** before needing alternatives:
- **10K games**: < 200ms P95 ✅
- **100K games**: < 500ms P95 (estimated, with partitioning)
- **1M+ games**: Consider Elasticsearch (but unlikely for board game domain)

For our projected scale (50K games), PostgreSQL is sufficient for years.

---

## Implementation Details

### GIN Index Strategy
```sql
CREATE INDEX ix_shared_games_fts
ON shared_games
USING GIN (to_tsvector('italian', title || ' ' || COALESCE(description, '')))
WHERE is_deleted = false;
```

**Why GIN?**
- Optimized for full-text search (vs GiST which is for geometric data)
- Faster queries at cost of slower inserts (acceptable: catalog updates are infrequent)
- Supports `@@` (match) operator efficiently

### Query Pattern
```csharp
dbQuery = dbQuery.Where(g =>
    EF.Functions.ToTsVector("italian", g.Title + " " + g.Description)
        .Matches(EF.Functions.PlainToTsQuery("italian", searchTerm)));
```

**Why PlainToTsQuery?**
- User-friendly (no syntax required)
- SQL injection safe (parameterized by EF Core)
- AND semantics ("gioco strategia" = both terms required)

---

## Consequences

### Positive
- ✅ Native Italian language support (stemming, stop words)
- ✅ P95 < 200ms performance (10x improvement validated)
- ✅ Zero additional infrastructure
- ✅ ACID transactions for data consistency
- ✅ Cost-effective scaling to 100K+ games
- ✅ Integrated with existing PostgreSQL expertise

### Negative
- ❌ Locked into PostgreSQL (acceptable - already core dependency)
- ❌ Index maintenance overhead (mitigated by filtered indexes)
- ❌ Limited to keyword search (no semantic/vector search)
- ❌ Manual language configuration (must specify 'italian')

### Mitigation
- **Lock-in**: PostgreSQL is industry-standard, migration paths exist if needed
- **Maintenance**: VACUUM ANALYZE automated via cron (standard PostgreSQL ops)
- **Semantic search**: Use Qdrant for "similar games" feature if needed (complementary, not replacement)

---

## Monitoring & Validation

### Performance Metrics
- **Health check**: `SharedGameCatalogHealthCheck` monitors FTS latency
- **Prometheus**: `http_server_request_duration_bucket{route="/api/v1/shared-games"}`
- **Grafana**: Dashboard with P95 alert (> 200ms triggers warning)

### Index Usage Validation
```sql
-- Verify GIN index usage
EXPLAIN ANALYZE
SELECT * FROM shared_games
WHERE to_tsvector('italian', title || ' ' || description)
  @@ plainto_tsquery('italian', 'strategia');

-- Expected: "Bitmap Index Scan using ix_shared_games_fts"
```

### Load Testing
- **k6 script**: `tests/k6/shared-catalog-load-test.js`
- **Scenario**: 100 req/s sustained for 3 minutes
- **Threshold**: P95 < 200ms (enforced in k6)

---

## Future Considerations

### When to Reconsider
- **Scale**: If catalog exceeds 500K games AND P95 degrades > 500ms
- **Features**: If semantic search ("find similar games") becomes critical
- **Multi-language**: If catalog expands beyond Italian (need language detection)

### Migration Path (if needed)
1. Keep PostgreSQL for transactional data
2. Add Elasticsearch/Typesense for search only
3. Sync via Change Data Capture (CDC) or domain events
4. Maintain PostgreSQL FTS as fallback

---

## Compliance

This decision aligns with:
- **YAGNI**: Use simplest solution that meets requirements
- **Boring Technology**: Prefer proven, well-understood technologies
- **Operational Excellence**: Minimize moving parts and complexity

---

## References

- Issue #2371: Phase 2 (FTS implementation)
- Issue #2374: Phase 5 (Performance optimization with GIN indexes)
- Issue #1996: Italian FTS configuration fix
- PostgreSQL Documentation: [Full-Text Search](https://www.postgresql.org/docs/16/textsearch.html)
- Migration: `20260114121520_AddSharedGameCatalogPerformanceIndexes.cs`
- Performance validation: `docs/05-testing/shared-catalog-fts-performance-validation.sql`
