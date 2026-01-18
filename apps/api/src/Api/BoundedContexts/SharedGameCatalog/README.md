# SharedGameCatalog Bounded Context

**Community-shared board game catalog with BGG integration, full-text search, and governance workflows.**

**Issues**: #2370 (Phase 1), #2371 (Phase 2), #2372 (Phase 3), #2373 (Phase 4), #2374 (Phase 5)

---

## Overview

SharedGameCatalog manages a community-curated database of board games separate from personal GameManagement collections. It provides:
- **BoardGameGeek (BGG) Integration**: Import games from BGG API
- **Full-Text Search**: PostgreSQL FTS with Italian language support (P95 < 200ms)
- **Multi-Tiered Caching**: HybridCache (L1: Memory, L2: Redis) with > 80% hit rate target
- **Governance Workflows**: Two-step delete approval (Editor requests → Admin approves)
- **Rich Metadata**: Categories, mechanics, designers, publishers, FAQs, errata
- **PDF Documentation**: Link rulebooks and errata documents to games

---

## Domain Model

### Aggregates
- **SharedGame**: Root aggregate for catalog game entries
  - Entities: GameFaq, GameErrata
  - Value Objects: GameRules, DocumentVersion
  - Relationships: Many-to-many with GameCategory, GameMechanic, GameDesigner, GamePublisher

### Entities
- `GameCategory`: Taxonomy (Strategy, Family, Party, etc.)
- `GameMechanic`: Mechanics (Deck Building, Worker Placement, etc.)
- `GameDesigner`: Game designers (Uwe Rosenberg, Reiner Knizia, etc.)
- `GamePublisher`: Publishers (Z-Man Games, Stonemaier, etc.)
- `GameFaq`: Frequently asked questions with ordering
- `GameErrata`: Official errata with page references and publication dates
- `SharedGameDeleteRequest`: Delete approval workflow entity
- `SharedGameDocument`: PDF document associations (rules, errata, FAQs)

### Enums
- `GameStatus`: Draft (0), Published (2), Archived (3)
- `DeleteRequestStatus`: Pending (0), Approved (1), Rejected (2)
- `SharedGameDocumentType`: Rules (0), Errata (1), FAQ (2), Other (3)

---

## API Surface (20 CQRS Operations)

### Commands (14)

#### Game CRUD
- **CreateSharedGameCommand**: Create new catalog game (Admin/Editor)
- **UpdateSharedGameCommand**: Update game details (Admin/Editor)
- **DeleteSharedGameCommand**: Permanently delete (Admin only, after approval)

#### Workflow
- **PublishSharedGameCommand**: Draft → Published (Admin/Editor)
- **ArchiveSharedGameCommand**: Published → Archived (Admin only)
- **RequestDeleteSharedGameCommand**: Request deletion (Admin/Editor)
- **ApproveDeleteRequestCommand**: Approve delete request (Admin only)
- **RejectDeleteRequestCommand**: Reject delete request (Admin only)

#### BGG Integration
- **ImportGameFromBggCommand**: Import single game from BGG (Admin/Editor)
- **BulkImportGamesCommand**: Import multiple games (Admin only, up to 500 BGG IDs)

#### FAQ/Errata Management
- **AddGameFaqCommand**: Add FAQ to game (Admin/Editor)
- **UpdateGameFaqCommand**: Update FAQ (Admin/Editor)
- **DeleteGameFaqCommand**: Remove FAQ (Admin/Editor)
- **AddGameErrataCommand**: Add errata (Admin/Editor)
- **UpdateGameErrataCommand**: Update errata (Admin/Editor)
- **DeleteGameErrataCommand**: Remove errata (Admin/Editor)

#### Document Management (Issue #2391)
- **AddDocumentToSharedGameCommand**: Link PDF to game (Admin/Editor)
- **SetActiveDocumentVersionCommand**: Activate document version (Admin/Editor)
- **RemoveDocumentFromSharedGameCommand**: Unlink document (Admin/Editor)

### Queries (6)

- **SearchSharedGamesQuery**: Full-text search with filters (Public)
  - Filters: SearchTerm, Categories, Mechanics, Players, PlayingTime, Status
  - Pagination: PageNumber, PageSize (default: 20)
  - Sorting: Title, YearPublished, AverageRating (ascending/descending)
  - Cache: L1 15min, L2 1h

- **GetSharedGameByIdQuery**: Get game details with FAQs/errata (Public)
  - Cache: L1 30min, L2 2h
  - Returns: SharedGameDetailDto with all relationships

- **GetAllSharedGamesQuery**: Admin list view with pagination (Admin/Editor)
  - Filter: Status (Draft, Published, Archived)
  - Pagination: PageNumber, PageSize (default: 50)
  - No cache (admin needs fresh data)

- **GetGameCategoriesQuery**: Taxonomy data (Public)
  - Cache: 24h (categories rarely change)

- **GetGameMechanicsQuery**: Taxonomy data (Public)
  - Cache: 24h (mechanics rarely change)

- **GetPendingDeleteRequestsQuery**: Delete approval queue (Admin only)
  - Pagination: PageNumber, PageSize

- **GetDocumentsBySharedGameQuery**: Get linked PDFs (Public)
  - Filter: DocumentType (optional)
  - Returns: Active documents only

- **GetActiveDocumentsQuery**: Get active document versions (Public)

---

## Performance Characteristics

### Database Indexes (Issue #2374 Phase 5)

**8 Critical Indexes**:
1. **ix_shared_games_fts** (GIN): Full-text search on title + description
2. **ix_shared_games_status_year_title**: Composite for sorting by year
3. **ix_shared_games_status_rating_title**: Composite for sorting by rating
4. **ix_shared_games_players**: Player count range filters
5. **ix_shared_games_playtime**: Playing time filters
6. **ix_shared_game_categories_***: Many-to-many junction indexes (2)
7. **ix_shared_game_mechanics_***: Many-to-many junction indexes (2)
8. **ix_shared_games_getbyid_covering**: Covering index for cache misses

**Impact**: P95 search latency < 200ms (10x improvement over BGG ~2000ms baseline)

### Cache Strategy (Issue #2371 Phase 2, #2374 Phase 5)

| Operation | L1 (Memory) | L2 (Redis) | Hit Rate Target |
|-----------|-------------|------------|-----------------|
| SearchSharedGames | 15min | 1h | > 70% |
| GetSharedGameById | 30min | 2h | > 85% |
| GetGameCategories | - | 24h | > 95% |
| GetGameMechanics | - | 24h | > 95% |

**Metrics**: OpenTelemetry counters `meepleai_cache_hits_total`, `meepleai_cache_misses_total`

### Load Testing Thresholds (Issue #2374 Phase 5)

- **Search**: 100 req/s sustained (P95 < 200ms)
- **Admin**: 50 req/s sustained (P95 < 300ms)
- **Failure rate**: < 1%
- **Cache hit rate**: > 80% overall

**Validation**: `tests/k6/shared-catalog-load-test.js`

---

## Authorization Model

### Policies
- **AdminOnlyPolicy**: Full CRUD + workflow approvals
- **AdminOrEditorPolicy**: Create, edit, publish, request deletion
- **AllowAnonymous**: Search, get details, taxonomy data

### Role Hierarchy
```
Admin (Full Access)
  └─ Can: All operations

Editor (Content Management)
  └─ Can: Create, edit, publish, request deletion
  └─ Cannot: Approve deletions, bulk import, archive

User (Read-Only)
  └─ Can: Search, view published games
  └─ Cannot: Admin operations
```

---

## Monitoring & Observability (Issue #2424)

### Health Checks
- **Endpoint**: GET /health
- **Check**: `shared-catalog-fts` (FTS performance monitoring)
- **Thresholds**:
  - Healthy: FTS latency < 200ms
  - Degraded: 200-500ms (recommend VACUUM ANALYZE)
  - Unhealthy: > 500ms (verify ix_shared_games_fts index)

### Prometheus Metrics
- `meepleai_cache_hits_total{operation="search", cache_type="shared_games"}`
- `meepleai_cache_misses_total{operation="get_by_id", cache_type="shared_games"}`
- `http_server_request_duration_bucket{route="/api/v1/shared-games"}`

### Grafana Dashboard
- **File**: `infra/monitoring/grafana/dashboards/shared-catalog-performance.json`
- **Panels**: Search P95, cache hit rate, request rate, latency percentiles
- **Alerts**: P95 > 200ms, cache < 80%

### Audit Logging (Issue #2374 Phase 5)
- **Event Handlers**: 8 automatic audit trail handlers
- **Query**: GET /api/v1/admin/audit-logs?resource=SharedGame*
- **Events**: Created, Updated, Published, Archived, Deleted, DeleteRequested, FaqAdded, ErrataAdded

---

## API Endpoints

### Public (AllowAnonymous + Rate Limit: 300 req/min)
```
GET  /api/v1/shared-games                    # Search with filters
GET  /api/v1/shared-games/{id}               # Get game details
GET  /api/v1/shared-games/categories         # List categories
GET  /api/v1/shared-games/mechanics          # List mechanics
GET  /api/v1/shared-games/{id}/documents     # Get linked PDFs
```

### Admin (AdminOrEditorPolicy + Rate Limit: 100 req/min)
```
POST   /api/v1/admin/shared-games                      # Create game
PUT    /api/v1/admin/shared-games/{id}                 # Update game
POST   /api/v1/admin/shared-games/import-bgg           # Import from BGG
POST   /api/v1/admin/shared-games/bulk-import          # Bulk import (Admin only)
GET    /api/v1/admin/shared-games                      # List all (with filters)
POST   /api/v1/admin/shared-games/{id}/publish         # Publish game
POST   /api/v1/admin/shared-games/{id}/archive         # Archive (Admin only)
POST   /api/v1/admin/shared-games/{id}/request-delete  # Request deletion
GET    /api/v1/admin/shared-games/delete-requests      # Pending requests (Admin only)
POST   /api/v1/admin/shared-games/delete-requests/{id}/approve  # Approve (Admin only)
POST   /api/v1/admin/shared-games/delete-requests/{id}/reject   # Reject (Admin only)

# FAQ Management
POST   /api/v1/admin/shared-games/{id}/faqs            # Add FAQ
PUT    /api/v1/admin/shared-games/{id}/faqs/{faqId}    # Update FAQ
DELETE /api/v1/admin/shared-games/{id}/faqs/{faqId}    # Delete FAQ

# Errata Management
POST   /api/v1/admin/shared-games/{id}/errata          # Add errata
PUT    /api/v1/admin/shared-games/{id}/errata/{errataId}  # Update errata
DELETE /api/v1/admin/shared-games/{id}/errata/{errataId}  # Delete errata

# Document Management
POST   /api/v1/admin/shared-games/{id}/documents       # Link PDF
POST   /api/v1/admin/shared-games/{id}/documents/{docId}/activate  # Activate version
DELETE /api/v1/admin/shared-games/{id}/documents/{docId}  # Unlink PDF
```

---

## Development Workflow

### Adding a New Game
```csharp
// 1. Create command
var command = new CreateSharedGameCommand(
    Title: "Catan",
    YearPublished: 1995,
    Description: "Build settlements and roads on the island of Catan",
    MinPlayers: 3,
    MaxPlayers: 4,
    PlayingTimeMinutes: 90,
    MinAge: 10,
    ImageUrl: "https://example.com/catan.jpg",
    ThumbnailUrl: "https://example.com/catan-thumb.jpg",
    CreatedBy: adminUserId
);

// 2. Send via MediatR
var gameId = await _mediator.Send(command);

// 3. Publish when ready
await _mediator.Send(new PublishSharedGameCommand(gameId, adminUserId));
```

### Searching Games
```csharp
// Frontend: apps/web/src/components/shared-games/SharedGameSearch.tsx
const results = await api.sharedGames.search({
  searchTerm: "strategia",
  categoryIds: [strategyCategoryId],
  minPlayers: 2,
  maxPlayers: 4,
  pageNumber: 1,
  pageSize: 20,
  sortBy: "AverageRating",
  sortDescending: true
});
```

---

## Testing

### Unit Tests
- **Location**: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/`
- **Coverage**: Domain entities, command handlers, query handlers
- **Pattern**: Arrange-Act-Assert with FluentAssertions

### Integration Tests
- **Testcontainers**: PostgreSQL for real database queries
- **Coverage**: Full-text search, cache behavior, authorization

### E2E Tests (Issue #2374 Phase 6)
- **Playwright**: Admin workflow (Create → Edit → Publish → Archive → Delete)
- **Bulk import**: 100 games from BGG
- **Permission enforcement**: Verify 403 for unauthorized access

### Load Tests (Issue #2374 Phase 5)
- **k6**: `tests/k6/shared-catalog-load-test.js`
- **Scenarios**: 100 req/s search, 50 req/s admin, spike 50-200 req/s

---

## Configuration

### Environment Variables
```bash
# Required
CONNECTIONSTRINGS__POSTGRES=...  # PostgreSQL connection
CONNECTIONSTRINGS__REDIS=...     # Redis for HybridCache L2

# Optional (cache TTLs)
CACHE_EXPIRATION_CATEGORIES=86400      # 24h default
CACHE_EXPIRATION_MECHANICS=86400       # 24h default
CACHE_EXPIRATION_SEARCH=3600           # 1h default
CACHE_EXPIRATION_GETBYID_L1=1800       # 30min L1 default
CACHE_EXPIRATION_GETBYID_L2=7200       # 2h L2 default

# BGG Integration
BGG_API_URL=https://boardgamegeek.com/xmlapi2
BGG_API_TIMEOUT_SECONDS=30
```

### Database Migration
```bash
# Apply performance indexes
cd apps/api/src/Api
dotnet ef database update

# Verify indexes
psql -f ../../../docs/05-testing/shared-catalog-fts-performance-validation.sql
```

---

## Architecture Decisions

- **ADR-016**: Bounded Context Separation (SharedGameCatalog vs GameManagement)
- **ADR-018**: PostgreSQL FTS Technology Choice (vs Elasticsearch/Qdrant)
- **ADR-019**: Two-Step Delete Workflow Governance

See: `docs/01-architecture/adr/`

---

## Performance Benchmarks

### Measured Latencies (Issue #2374 Phase 5)
- **Search P95**: < 200ms (target met with GIN FTS index)
- **GetById P95**: < 100ms (target met with covering index)
- **Categories/Mechanics**: < 50ms (cached 24h)
- **Admin operations**: < 300ms

### Cache Hit Rates (OpenTelemetry)
- **Search**: 70-80% (15min TTL, frequent queries)
- **GetById**: 85-90% (30min L1, popular games hot)
- **Taxonomy**: 95%+ (24h TTL, rarely changes)

### Database Statistics
- **Row count**: ~10,000 games (projected: 50,000 in 2 years)
- **Index size**: ~2MB for GIN FTS (10K games)
- **Query plan**: Bitmap Index Scan using ix_shared_games_fts

---

## Rate Limiting (Issue #2424)

- **Public endpoints**: 300 req/min per IP
- **Admin endpoints**: 100 req/min per user
- **Rejection**: 429 Too Many Requests with Retry-After header

---

## Security

### Input Validation
- **FluentValidation**: All commands have validators
- **MaxLength**: Title (500), URLs (1000)
- **Range validation**: Year (1900-2100), Players (> 0), Ratings (1.0-10.0)
- **SQL injection**: Protected via EF Core parameterization

### Authorization
- **AdminOnlyPolicy**: Delete approval, bulk import, archive
- **AdminOrEditorPolicy**: CRUD, publish, FAQ/errata management
- **AllowAnonymous**: Search, details, taxonomy

---

## Future Enhancements

- **Semantic Search**: "Find similar games" using Qdrant vector similarity
- **Advanced Filters**: Designer, publisher, year range, complexity rating
- **User Contributions**: Allow users to suggest FAQs/errata for admin review
- **Localization**: Multi-language support (EN, IT, DE, FR)
- **BGG Rating Sync**: Periodic background job to update ratings
- **Recommendation Engine**: "If you liked X, try Y" based on mechanics/categories

---

## Troubleshooting

### Slow Search Performance
```sql
-- Check index usage
SELECT * FROM pg_stat_user_indexes
WHERE tablename = 'shared_games' AND indexname LIKE '%fts%';

-- If idx_scan is low, VACUUM ANALYZE
VACUUM ANALYZE shared_games;
```

### Low Cache Hit Rate
```promql
# Query Prometheus
rate(meepleai_cache_hits_total{cache_type="shared_games"}[5m]) /
  (rate(meepleai_cache_hits_total{cache_type="shared_games"}[5m]) +
   rate(meepleai_cache_misses_total{cache_type="shared_games"}[5m]))

# If < 70%, check:
# - Redis connectivity (L2 cache down?)
# - Cache key generation (hash collisions?)
# - TTL configuration (too short?)
```

### BGG Import Failures
```bash
# Check BGG API status
curl https://boardgamegeek.com/xmlapi2/thing?id=13

# If timeout, increase BGG_API_TIMEOUT_SECONDS
# If rate limited, implement exponential backoff in ImportGameFromBggCommandHandler
```

---

**Last Updated**: 2026-01-14
**Maintainer**: MeepleAI Development Team
