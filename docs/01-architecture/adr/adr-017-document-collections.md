# ADR-017: Document Collections for Multi-Document Support

**Status**: Accepted
**Date**: 2025-12-12
**Issue**: #2051
**Decision Makers**: Engineering Team

---

## Context

Players often use multiple PDF documents per game (base rulebook + expansions + errata + house rules). The original system supported only 1:1 Game-to-PDF relationship, forcing users to manually merge documents.

**Problem**: No support for:
- Multiple documents per game (base + expansions)
- Document type classification (base vs expansion vs errata)
- Citation priority (which document to cite when content overlaps)
- Source filtering in chat (ask questions about specific expansions)

---

## Decision

Implement **DocumentCollection** aggregate root following DDD/CQRS patterns.

### Core Design

**DocumentCollection Aggregate:**
```csharp
public class DocumentCollection : AggregateRoot<Guid>
{
    public Guid GameId { get; }
    public CollectionName Name { get; }
    public List<CollectionDocument> Documents { get; } // Max 5

    public void AddDocument(Guid pdfId, DocumentType type, int sortOrder);
    public void RemoveDocument(Guid pdfId);
}
```

**DocumentType Value Object:**
- Types: `base` (0), `expansion` (1), `errata` (2), `homerule` (3)
- Priority for citations: homerule > (errata/expansion by date) > base

**Citation Priority Logic:**
1. **Homerule first** (priority 3) - user customizations trump all
2. **Date-based for errata/expansion** (priority 2/1) - newest wins
3. **Base last** (priority 0) - fallback to core rules

---

## Alternatives Considered

### Option A: Minimal DB Changes (Rejected)
- **Approach**: Just add `document_type` column + junction table
- **Pro**: Faster implementation (3-4 days)
- **Con**: Application layer de-duplication, limited scalability
- **Rejection Reason**: User preferred rich domain model for long-term value

### Option B: DocumentCollection Aggregate (Chosen)
- **Approach**: Full DDD aggregate with collection semantics
- **Pro**: Scalable, business-aligned, domain-driven
- **Con**: More complex (5-7 days implementation)
- **Selection Reason**: Better long-term architecture, supports future features (versioning, comparisons)

---

## Consequences

### Positive
✅ **Rich domain model** - Collection business logic in aggregate
✅ **Scalable** - Easy to add features (versioning, compare rulebook versions)
✅ **Citation control** - Priority service handles overlapping content
✅ **DDD alignment** - Follows project's 100% DDD completion
✅ **Type safety** - DocumentType/CollectionName value objects enforce constraints

### Negative
⚠️ **Migration complexity** - All existing PDFs auto-assigned to default collections
⚠️ **More entities** - Additional testing/maintenance burden
⚠️ **Learning curve** - Developers must understand collection semantics

### Neutral
📊 **Performance**: Minimal impact (indexed FK, JSON for collection docs)
📦 **Storage**: +2 tables, ~200 bytes per collection

---

## Implementation Details

### Database Schema

**document_collections:**
```sql
id UUID PRIMARY KEY
game_id UUID NOT NULL → games(id) CASCADE
name VARCHAR(200) NOT NULL
description VARCHAR(1000)
created_by_user_id UUID NOT NULL → users(id) RESTRICT
documents_json TEXT DEFAULT '[]'  -- Serialized CollectionDocument[]
created_at, updated_at TIMESTAMP
```

**pdf_documents (modified):**
```sql
collection_id UUID → document_collections(id) SET NULL
document_type VARCHAR(50) DEFAULT 'base' CHECK (base|expansion|errata|homerule)
sort_order INT DEFAULT 0
```

**chat_thread_collections (junction):**
```sql
id UUID PRIMARY KEY
chat_thread_id UUID → chat_threads(id) CASCADE
collection_id UUID → document_collections(id) CASCADE
UNIQUE(chat_thread_id, collection_id)
```

### Data Migration Strategy

**Phase 1**: Create default collection for each game with PDFs
**Phase 2**: Assign all existing PDFs to game's collection as `document_type='base'`

```sql
-- Auto-executed in migration
INSERT INTO document_collections (...)
SELECT game_id, name, ... FROM games WHERE has_pdfs;

UPDATE pdf_documents
SET collection_id = (SELECT id FROM document_collections WHERE game_id = ...),
    document_type = 'base';
```

---

## API Surface

**New Endpoints:**
```
POST   /api/v1/document-collections/{gameId}           → Create
GET    /api/v1/document-collections/{id}              → Get by ID
GET    /api/v1/document-collections/by-game/{gameId}  → Get by game
GET    /api/v1/document-collections/by-user/{userId}  → List user's
POST   /api/v1/document-collections/{id}/documents    → Add doc
DELETE /api/v1/document-collections/{id}/documents/:pdfId → Remove
```

**Modified Endpoints:**
```
POST /api/v1/chat  → Accept `selectedDocumentIds: Guid[]` (future)
```

---

## Testing Strategy

**Unit Tests** (56 tests total):
- DocumentType: Validation, priority, equality (18 tests)
- CollectionName: Length validation, trimming (12 tests)
- DocumentCollection: Add/remove, max docs, ordering (16 tests)
- CitationPriorityService: Priority ordering, de-duplication (10 tests)

**Integration Tests** (planned):
- Repository CRUD operations
- CQRS handler workflows
- Migration data integrity

**E2E Tests** (planned):
- Upload base + expansion → verify both in collection
- Filter by expansion only → verify citations only from expansion
- Citation priority → homerule appears first

---

## Rollback Plan

**If issues arise:**
1. **Database**: Migration has `Down()` method - rolls back cleanly
2. **Application**: Feature toggle to disable collection endpoints
3. **Frontend**: Components isolated - easy to remove
4. **Data**: Default collections for all games - no data loss

**Rollback Cost**: ~2 hours (run migration rollback, remove endpoints)

---

## Future Enhancements

**Enabled by this architecture:**
- 📚 **Rulebook versioning** - Track different editions of same document
- 🔄 **Compare versions** - Show rule changes between editions
- 📊 **Collection analytics** - Most-used expansions, popular combinations
- 🎯 **Smart recommendations** - Suggest relevant expansions based on questions
- 🔗 **Cross-references** - Link related rules across documents

---

## References

- Issue #2051: Multi-document upload requirements
- CLAUDE.md: DDD architecture, CQRS patterns
- ADR-001: Hybrid RAG (impacted by multi-doc citations)
- Pattern: Existing PDF upload workflow (UploadPdfCommandHandler)

---

**Approved**: 2025-12-12
**Implemented**: feature/issue-2051-multi-document-upload branch
