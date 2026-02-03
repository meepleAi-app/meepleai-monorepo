# SharedGameCatalog Bounded Context

**Database community di giochi con soft-delete e audit trail**

---

## 📋 Responsabilità

- Catalogo condiviso community-driven
- Soft-delete workflow con audit trail
- PostgreSQL Full-Text Search (italiano + inglese)
- Moderazione contenuti utente
- Integrazione con GameManagement context

---

## 🏗️ Domain Model

**SharedGame** (Aggregate Root):
```csharp
public class SharedGame
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public Guid CreatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public void SoftDelete(string userId) { }
    public void Restore() { }
}
```

---

## 📡 Application Layer

### Commands
- `CreateSharedGameCommand`
- `UpdateSharedGameCommand`
- `SoftDeleteSharedGameCommand`
- `RestoreSharedGameCommand`

### Queries
- `GetAllSharedGamesQuery` (with soft-delete filter)
- `SearchSharedGamesQuery` (PostgreSQL FTS)

---

## 🔍 PostgreSQL Full-Text Search (ADR-018)

**Index Configuration**:
```sql
CREATE INDEX idx_sharedgames_fts ON SharedGames
USING gin(
    to_tsvector('italian', Title || ' ' || Description)
);
```

**Search Query**:
```csharp
var results = await _db.SharedGames
    .Where(g => !g.IsDeleted)  // Global query filter
    .Where(g => EF.Functions.ToTsVector("italian", g.Title + " " + g.Description)
        .Matches(EF.Functions.PlainToTsQuery("italian", searchTerm)))
    .OrderByDescending(g => EF.Functions.TsRank(...))
    .ToListAsync();
```

---

## 🗑️ Soft-Delete Workflow (ADR-019)

**Global Query Filter**:
```csharp
modelBuilder.Entity<SharedGame>().HasQueryFilter(g => !g.IsDeleted);
```

**Restore Capability**:
```csharp
public void Restore()
{
    IsDeleted = false;
    DeletedAt = null;
    DeletedBy = null;
}
```

**Audit Trail**: Chi, quando, perché ha eliminato/ripristinato

---

## 📂 Code Location

`apps/api/src/Api/BoundedContexts/SharedGameCatalog/`

---

## 📖 Related Documentation

- [ADR-016: Shared Catalog Bounded Context](../01-architecture/adr/adr-016-shared-catalog-bounded-context.md)
- [ADR-018: PostgreSQL FTS](../01-architecture/adr/adr-018-postgresql-fts-for-shared-catalog.md)
- [ADR-019: Soft-Delete Workflow](../01-architecture/adr/adr-019-shared-catalog-delete-workflow.md)

---

**Status**: ✅ Production
**Last Updated**: 2026-01-18
