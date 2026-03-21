# Backend & Test Codebase Improvement — Design Spec

**Date**: 2026-03-18
**Scope**: Full backend codebase + test suite
**Approach**: Bottom-Up Surgical, Aggressive, No Exclusions
**Phases**: 6 (parallelizable: 1+2+3, then 4+5, then 6)

---

## Problem Statement

The MeepleAI backend (4,936 files, ~75K LOC, 17 bounded contexts) and test suite (1,590 files, ~57K LOC, 13,134+ tests) have accumulated structural debt:

- **Giant files**: Routing files up to ~3,000 lines, services up to ~2,300 lines, handlers up to ~1,600 lines
- **Inconsistent test patterns**: Mixed assertion libraries (FluentAssertions vs xUnit Assert), 53 files missing Category traits, 764 files without visible assertions
- **Under-utilized infrastructure**: ~69 of ~127 repositories do not extend the existing `RepositoryBase`; context-specific test helpers with duplicated logic
- **God Object**: `User.cs` at ~900 lines serving 5+ bounded contexts
- **Coverage gaps**: 4 BCs with low test-to-source file ratio (UserLibrary, SessionTracking, SystemConfiguration, AgentMemory)

## Success Criteria

- No file in Routing/ exceeds 500 lines
- No handler exceeds 200 lines (orchestration extracted)
- No service exceeds 500 lines
- 100% of test files have Category trait
- 100% FluentAssertions (zero xUnit Assert.*)
- All Tier 1 BCs reach 0.30+ test file ratio (test files / source files excluding Infrastructure/Configurations)
- All ~69 non-inheriting repositories migrated to existing `RepositoryBase`
- User entity split into BC-owned models (code-level, DB migration deferred)

---

## Phase 1: File Splitting

### 1A. Routing File Splits

**Note**: Line counts are approximate (taken 2026-03-18, may drift ~10-15%). Re-verify with `wc -l` at implementation start.

| File (approx lines) | Split Into |
|---|---|
| `SharedGameCatalogEndpoints.cs` (~2,900) | `SharedGameSearchEndpoints.cs`, `SharedGameAdminEndpoints.cs`, `SharedGameContributorEndpoints.cs`, `SharedGameBadgeEndpoints.cs`, `SharedGameTrendingEndpoints.cs` |
| `UserLibraryEndpoints.cs` (~1,900) | `CollectionEndpoints.cs`, `WishlistEndpoints.cs`, `GameHistoryEndpoints.cs`, `UserLibraryStatsEndpoints.cs` |
| `SessionTrackingEndpoints.cs` (~2,000) | `SessionNotesEndpoints.cs`, `SessionScoringEndpoints.cs`, `SessionActivityEndpoints.cs` |
| `AdminUserEndpoints.cs` (~1,300) | `AdminUserManagementEndpoints.cs`, `AdminRoleEndpoints.cs`, `AdminAuditEndpoints.cs` |
| `PdfEndpoints.cs` (~1,200) | `PdfUploadEndpoints.cs`, `PdfProcessingEndpoints.cs`, `PdfQueryEndpoints.cs` |

**Pattern**: Original file becomes a lightweight orchestrator calling sub-files:

```csharp
internal static class SharedGameCatalogEndpoints
{
    public static RouteGroupBuilder MapSharedGameCatalogEndpoints(this RouteGroupBuilder group)
    {
        SharedGameSearchEndpoints.Map(group);
        SharedGameAdminEndpoints.Map(group);
        SharedGameContributorEndpoints.Map(group);
        SharedGameBadgeEndpoints.Map(group);
        SharedGameTrendingEndpoints.Map(group);
        return group;
    }
}
```

Zero breaking changes: `Program.cs` wiring unchanged.

### 1B. Service Splits

| File (approx lines) | Split Into |
|---|---|
| `EmailService.cs` (~2,300) | `EmailTemplateService.cs`, `EmailSenderService.cs`, `EmailStorageService.cs` |
| `RagService.cs` (~840) | `RagRetrievalService.cs`, `RagRerankingService.cs`, `RagResponseService.cs` |
| `PromptEvaluationService.cs` (~820) | `PromptScoringService.cs`, `PromptValidationService.cs` |

**Note**: `RagService.cs` and `PromptEvaluationService.cs` are close to 500-line threshold post-split. Verify actual split sizes — may need only 2-way split instead of 3.

**Global Services location**: These files live in `Services/` outside any BC. After splitting, they remain in `Services/` with sub-folders by domain (e.g., `Services/Email/`, `Services/Rag/`). Moving them into BCs is out of scope — that would be a separate architectural decision.

DI registration updated to wire new services.

### 1C. Handler Splits

| File (approx lines) | Split Into |
|---|---|
| `UploadPdfCommandHandler.cs` (~1,600) | Thin handler (~200 lines) + `PdfProcessingOrchestrator.cs` + `PdfChunkingService.cs` + `PdfEmbeddingService.cs` |
| `PlaygroundChatCommandHandler.cs` (~1,050) | Thin handler + `ChatOrchestrationService.cs` + `ChatContextBuilder.cs` |
| `SendAgentMessageCommandHandler.cs` (~810) | Thin handler + `AgentMessageOrchestrator.cs` |

**Principle**: Handler = dispatch + validation. Complex logic extracted to injected services.

### 1D. Metrics Split

`MeepleAiMetrics.cs` (~1,400 lines) split via partial classes:

```csharp
// Metrics/AuthenticationMetrics.cs
public static partial class MeepleAiMetrics { public static class Auth { ... } }

// Metrics/GameMetrics.cs
public static partial class MeepleAiMetrics { public static class Games { ... } }
```

Zero breaking changes (same class name, partial).

**Estimated scope**: ~25 files split, zero breaking changes, atomic commits per split.

---

## Phase 2: Handler & Organization Standardization

### 2A. Standard Directory Layout

Every bounded context adopts:

```
BoundedContexts/{Context}/
├── Application/
│   ├── Commands/
│   │   ├── CreateGameCommand.cs
│   │   └── CreateGameCommandHandler.cs    # handler next to command
│   ├── Queries/
│   │   ├── GetGameQuery.cs
│   │   └── GetGameQueryHandler.cs         # handler next to query
│   ├── Validators/
│   ├── DTOs/
│   ├── EventHandlers/
│   ├── Services/
│   └── Mappers/
├── Domain/
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Events/
│   ├── Repositories/          # interfaces only
│   ├── Services/
│   ├── Enums/
│   └── Exceptions/
└── Infrastructure/
    ├── DependencyInjection/
    ├── Persistence/
    └── Services/
```

### 2B. Migration Rules

| Current Pattern | Action |
|---|---|
| Handler in separate `Handlers/` folder | Move to `Commands/` or `Queries/` next to its command/query |
| Separate `QueryHandlers/` | Move to `Queries/` |
| Handler + Command in same file | Split into 2 files, same folder |
| Handler already next to command | Leave as-is |

Executed via `git mv` to preserve history. One commit per BC.

**Complete sweep rule**: When a BC is touched by Phase 2, ALL handlers in that BC are migrated to the standard layout — no partial states. BCs with pre-existing mixed patterns (e.g., Administration with handlers in both `Handlers/` and `Queries/`) get a full sweep in a single commit.

### 2C. Handler Thickness Standard

- < 100 lines: OK
- 100-200 lines: acceptable if linear logic
- > 200 lines: MUST extract orchestration to `{Feature}Orchestrator` or `{Feature}Service`

### 2D. Priority Order

1. KnowledgeBase (916 files, most disorganized)
2. GameManagement (581 files)
3. SharedGameCatalog (492 files)
4. Authentication (285 files)
5. Remaining BCs in batch

**Estimated scope**: ~200-300 `git mv` operations, namespace updates, no logic changes.

---

## Phase 3: Test Standardization

### 3A. Unify on FluentAssertions

Migrate ~805 files from xUnit `Assert.*` to FluentAssertions `.Should()`:

| xUnit Assert | FluentAssertions |
|---|---|
| `Assert.Equal(expected, actual)` | `actual.Should().Be(expected)` |
| `Assert.True(condition)` | `condition.Should().BeTrue()` |
| `Assert.False(condition)` | `condition.Should().BeFalse()` |
| `Assert.Null(obj)` | `obj.Should().BeNull()` |
| `Assert.NotNull(obj)` | `obj.Should().NotBeNull()` |
| `Assert.Contains(item, col)` | `col.Should().Contain(item)` |
| `Assert.Empty(col)` | `col.Should().BeEmpty()` |
| `Assert.Throws<T>(action)` | `action.Should().Throw<T>()` |
| `Assert.IsType<T>(obj)` | `obj.Should().BeOfType<T>()` |

**Two-pass approach**:

**Pass 1 — Quarantine**: Before bulk replace, grep for these patterns and quarantine affected files for manual migration:

| xUnit Pattern | Reason | FluentAssertions Equivalent |
|---|---|---|
| `Assert.Collection(` | Ordered element inspectors, different API | `.SatisfyRespectively()` (manual rewrite) |
| `Assert.All(` | Requires FluentAssertions 6.x+ `.AllSatisfy()` | Verify FA version first |
| `Assert.Equal(expected, actual, comparer)` | Custom IEqualityComparer overload | `.BeEquivalentTo()` with options, or `.Be()` with custom config |
| `Assert.Raises<T>(` / `Assert.RaisesAsync<T>(` | Event assertions | No direct FA equivalent — keep xUnit or use `Monitor` |
| `Assert.*` with custom message parameter | Message string as 3rd arg | `.Should().Be(x, "because ...")` manual rewrite |

**Pass 2 — Bulk replace**: Apply regex transforms on non-quarantined files, one BC at a time. Run `dotnet test --filter "BoundedContext={BC}"` after each BC batch. If failures > 0, revert and investigate before continuing.

**Rollback strategy**: Each BC's assertion migration is a separate commit. If test failures appear, `git revert` the specific BC commit without affecting other BCs.

### 3B. Add Missing Category Traits (53 files)

Every test file must have:

```csharp
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
```

Auto-detection from path:
- Path contains `Integration` -> Integration
- Path contains `E2E` -> E2E
- Path contains `Security` -> Security
- Path contains `Performance` -> Performance
- Default -> Unit

### 3C. Consolidate Test Base Classes

Target hierarchy:

```
TestBase (abstract)
├── UnitTestBase          — AutoFixture, Moq setup
├── SharedDatabaseTestBase — Testcontainers, DB isolation (existing)
└── E2ETestBase           — WebApplicationFactory (existing)
```

Context-specific test bases become static helper/extension methods:

```csharp
public static class GameManagementTestHelpers
{
    public static async Task<Game> SeedTestGame(this MeepleAiDbContext db, Action<Game>? configure = null) { ... }
}
```

### 3D. Audit 764 Files Without Visible Assertions

Classification script:
1. Has `.Should()` or `Assert.*` -> OK
2. Has `Verify(` or `.Verify()` -> OK (mock verification)
3. Has `Should().ThrowAsync` -> OK
4. None of above -> FLAG for manual review

Flagged files receive explicit assertions or `[Fact(Skip = "Missing assertions")]` temporarily.

### 3E. Naming Convention

New/touched tests follow: `{Method}_When{Condition}_Should{ExpectedResult}`

Existing tests not renamed (churn vs value).

---

## Phase 4: Test Coverage Gaps

### 4A. Priority Map

**Tier 1 — Critical** (ratio < 0.20):

| BC | Current Ratio | Target |
|---|---|---|
| UserLibrary | 0.16 | 0.35 |
| SessionTracking | 0.18 | 0.35 |
| SystemConfiguration | 0.10 | 0.30 |
| AgentMemory | 0.10 | 0.30 |

**Tier 2 — Under-tested** (ratio 0.20-0.30):

| BC | Current Ratio | Target |
|---|---|---|
| Administration | 0.25 | 0.35 |
| GameManagement | 0.27 | 0.35 |
| WorkflowIntegration | 0.19 | 0.30 |

**Tier 3 — Small BCs** (low risk): EntityRelationships, GameToolkit, GameToolbox, Gamification

### 4B. Test Generation Template

For each Command/Query in under-tested BCs:

```csharp
public class {Handler}Tests : UnitTestBase
{
    [Fact, Trait("Category", TestCategories.Unit), Trait("BoundedContext", "{BC}")]
    public async Task Handle_WithValidRequest_ShouldSucceed() { }

    [Fact, Trait("Category", TestCategories.Unit), Trait("BoundedContext", "{BC}")]
    public async Task Handle_WhenEntityNotFound_ShouldThrowNotFoundException() { }

    [Fact, Trait("Category", TestCategories.Unit), Trait("BoundedContext", "{BC}")]
    public async Task Handle_WithInvalidRequest_ShouldFailValidation() { }
}
```

Plus validator tests and integration tests for complex queries.

### 4C. Security & Performance Expansion

| Type | Target BC | Tests |
|---|---|---|
| Security | Authentication | Brute force, token tampering, session hijacking |
| Security | Administration | Role escalation, unauthorized access |
| Security | KnowledgeBase | Prompt injection, data leakage |
| Security | DocumentProcessing | Path traversal, malicious PDF |
| Performance | KnowledgeBase | RAG latency < 5s, concurrent search |
| Performance | SharedGameCatalog | FTS < 500ms, pagination under load |
| Performance | DocumentProcessing | Upload throughput, chunking speed |

### 4D. Estimated Scope

| Tier | Test files to generate |
|---|---|
| Tier 1 (4 BCs) | ~200 files |
| Tier 2 (3 BCs) | ~120 files |
| Tier 3 (4 BCs) | ~30 files |
| Security | ~15 files |
| Performance | ~10 files |

---

## Phase 5: Repository Consolidation (Migrate to Existing Infrastructure)

### 5A. Existing Infrastructure (DO NOT recreate)

The SharedKernel already provides:

- `SharedKernel/Infrastructure/RepositoryBase.cs` — abstract base with domain event collection
- `SharedKernel/Infrastructure/Persistence/IRepository.cs` — `IRepository<TEntity, TId>` with CRUD methods
- `SharedKernel/Infrastructure/Persistence/IUnitOfWork.cs` — full UoW with transaction support (begin/commit/rollback)
- `SharedKernel/Domain/Interfaces/IEntity.cs` — `IEntity<out TId>` (generic, two type params)

**~58 of ~127 repositories** already extend `RepositoryBase`. The remaining **~69 repositories** implement their own CRUD logic — these are the migration targets.

### 5B. Migration: Adopt RepositoryBase for non-inheriting repos

For each repository that does NOT extend `RepositoryBase`:

```csharp
// BEFORE: standalone repo with duplicated CRUD
public class GameRepository : IGameRepository
{
    private readonly MeepleAiDbContext _context;
    public async Task<Game?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _context.Games.FindAsync(new object[] { id }, ct);  // duplicated
    public async Task AddAsync(Game game, CancellationToken ct)
        => await _context.Games.AddAsync(game, ct);                  // duplicated
    // ... 10+ boilerplate methods
    public async Task<PagedResult<Game>> SearchAsync(string term, int page, int size, CancellationToken ct)
        => // custom logic
}

// AFTER: inherits RepositoryBase, keeps only custom methods
public class GameRepository : RepositoryBase<Game, Guid>, IGameRepository
{
    public GameRepository(MeepleAiDbContext context) : base(context) { }

    // Only custom methods that aren't in RepositoryBase
    public async Task<PagedResult<Game>> SearchAsync(string term, int page, int size, CancellationToken ct)
        => // custom logic
}
```

### 5C. Specification Pattern (NEW addition to SharedKernel)

Add a lightweight Specification interface for encapsulating query logic in the Domain layer:

```csharp
// SharedKernel/Domain/Interfaces/ISpecification.cs (NEW)
public interface ISpecification<T> { IQueryable<T> Apply(IQueryable<T> query); }

// RepositoryBase gets a new method:
public virtual async Task<PagedResult<TEntity>> QueryAsync(
    ISpecification<TEntity> spec, int page, int pageSize, CancellationToken ct)
{
    var query = spec.Apply(DbSet.AsQueryable());
    var total = await query.CountAsync(ct);
    var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    return new PagedResult<TEntity>(items, total, page, pageSize);
}
```

This is the only NEW infrastructure. Everything else already exists.

### 5D. Migration Strategy

1. Add `ISpecification<T>` to SharedKernel + `QueryAsync` to `RepositoryBase` (small additive change)
2. Migrate Tier 1 BCs (UserLibrary, SessionTracking, SystemConfig, AgentMemory) — ~15 repos
3. Migrate large BCs touched by file splitting (SharedGameCatalog, GameManagement) — ~30 repos
4. Remaining BCs — opportunistic migration when touching a repo

**Per-repo migration**: one commit per repository, verify with existing integration tests before proceeding.

### 5E. Exclusions

- **DO NOT** create new `BaseRepository<T>` or `IRepository<T>` — use existing ones
- **DO NOT** create simplified `IUnitOfWork` — existing full UoW is correct
- No Generic Repository anti-pattern (no exposed `IQueryable` from repos)
- No repository for Value Objects (Aggregate Roots only)
- Read-side queries can use a read-only interface if needed (future work)

---

## Phase 6: Entity Decomposition

### 6A. User Entity Split

Current `User.cs` (~900 lines) decomposed into BC-owned models:

| BC | Entity | Responsibility |
|---|---|---|
| Authentication (OWNER) | `User.cs` (~300 lines) | Id, Email, PasswordHash, 2FA, Sessions, OAuth |
| Administration | `UserProfile.cs` (~200 lines) | DisplayName, Avatar, Bio, Role, Permissions |
| Administration/BusinessSim | `UserBudget.cs` (~150 lines) | Budget, TokenUsage, BillingPlan |
| UserLibrary | `UserPreferences.cs` (~100 lines) | Notification/Theme/Game prefs |
| Gamification | `UserGamificationProfile.cs` (~100 lines) | Points, Level, Badges |

**Rules**:
- Only `Authentication.User` owns the primary Id
- Other BCs reference UserId as FK
- No cross-BC navigation properties — communication via MediatR

### 6B. Migration (Zero Downtime)

| Step | Action | Risk |
|---|---|---|
| 1 | Create new C# entities mapping to SAME `Users` table | Zero |
| 2 | New handlers use new entities, old ones keep using `User` | Zero |
| 3 | DB migration: create `user_profiles`, `user_budgets`, etc. with copied data | Low |
| 4 | Switch new entities to new tables | Medium |
| 5 | Remove migrated columns from `Users` | Medium |
| 6 | Remove old monolithic `User.cs` | Low |

**This plan covers Steps 1-2 only.** Steps 3-6 are a follow-up after validation.

**CRITICAL: Write Isolation during Steps 1-2**

During the dual-entity period (both `User` and `UserProfile`/`UserBudget`/etc. mapping to the same `Users` table), there is a write-conflict hazard. Mitigations:

1. **New entities are READ-ONLY** during Steps 1-2. All write operations continue through the existing `User` entity exclusively.
2. EF Core configuration for new entities uses `.ToTable("Users")` with all shared columns configured as `.HasColumnName("X").IsRequired(false)` and **no setter** (computed columns or `ValueGeneratedNever()`).
3. New entity classes expose only getters — no `Update*()`, `Set*()`, or factory methods that modify state.
4. Only after Step 4 (new entities on new tables) do the new entities gain write capabilities.
5. Concurrency tokens (`RowVersion`) remain exclusively on `Authentication.User` during Steps 1-2.

This ensures zero concurrent-write risk: one writer (`User`), multiple readers (`UserProfile`, `UserBudget`, etc.).

### 6C. MeepleAiMetrics Decomposition

Split via partial classes by domain:

```csharp
// Metrics/AuthenticationMetrics.cs
public static partial class MeepleAiMetrics { public static class Auth { ... } }

// Metrics/GameMetrics.cs
public static partial class MeepleAiMetrics { public static class Games { ... } }
```

Zero breaking changes.

---

## Coverage Metric Definition

**Test-to-source file ratio** = (test files in BC) / (source files in BC, excluding `Infrastructure/Configurations/`, `Infrastructure/Entities/`, and auto-generated files).

Infrastructure configuration files (EF entity configs, DI registrations) have minimal unit-test surface and inflate the denominator. Excluding them gives a more accurate picture of domain/application logic coverage.

**AgentMemory caveat**: This BC is small (49 source files, mostly infrastructure). Tier 1 classification is based on ratio, but actual test generation should focus on domain logic files only — skip tests for pure configuration/entity mapping files.

## Execution Schedule

| Phase | Parallelizable With | Constraint | Estimated PRs |
|---|---|---|---|
| Phase 1 (File Splitting) | Phase 3 (different file types) | Complete before Phase 2 starts same BC | 3-5 PRs |
| Phase 2 (Handler Standardization) | Phase 1 (different BCs only) | Gate: BC must be split first if in Phase 1 scope | 2-3 PRs (per BC batch) |
| Phase 3 (Test Standardization) | Phase 1 | Gate: do NOT start BC's test migration until Phase 2 completes that BC (namespace changes affect test imports) | 2-3 PRs |
| Phase 4 (Coverage Gaps) | Phase 5 | Starts after Phases 1-3 complete | 4-7 PRs (per tier) |
| Phase 5 (Repository Consolidation) | Phase 4 | Independent of test work | 2-3 PRs |
| Phase 6 (Entity Decomposition) | None (last) | Starts after Phase 5 | 2-3 PRs |

**Parallelization rule**: Phases can run in parallel on **different BCs**, but the same BC must complete phases sequentially (Phase 1 → Phase 2 → Phase 3). This prevents namespace conflicts in test files.

**Total estimated**: 15-24 PRs, all targeting `main-dev`.

## Risks & Mitigations

| Risk | Mitigation |
|---|---|
| Merge conflicts between parallel phases | Same-BC work is sequential (1→2→3); only different BCs run in parallel |
| Namespace breaks after git mv | Automated namespace update script + `dotnet build` verification per commit |
| Test regressions from assertion migration | One BC per commit, `dotnet test --filter "BoundedContext={BC}"` gate, `git revert` per BC if failures |
| Quarantined assertions (Collection, All, Raises) | Grep-first pass isolates these files; manual migration after bulk pass |
| Repository migration breaks queries | One repo per commit, verify with existing integration tests |
| User entity dual-mapping write conflicts | New entities are READ-ONLY during Steps 1-2; all writes through existing `User` only |
| Phase 5 conflicts with existing SharedKernel | Phase 5 extends existing `RepositoryBase`, does NOT create new base classes |
