# Phase 4: GameManagement Context - Implementation Plan

**Status**: READY TO START
**Target**: Refactor 3 services (~885 lines) into DDD bounded context
**Estimated Time**: 6-8 hours

---

## Current Services Analysis

### Services to Refactor (3 total, ~885 lines)

**1. GameService** (~117 lines):
- `CreateGameAsync()`: Create new game entity
- `GetGamesAsync()`: Query all games
- `GenerateIdFromName()`: Normalize game names to IDs
- `NormalizeId()`: Slug generation from title

**2. RuleSpecService** (~576 lines):
- `GenerateRuleSpecFromPdfAsync()`: AI-powered RuleSpec generation
- `GetOrCreateDemoAsync()`: Demo RuleSpec for testing
- `GetRuleSpecAsync()`, `UpdateRuleSpecAsync()`: CRUD operations
- `GenerateNextVersionAsync()`: Version management
- `GetVersionHistoryAsync()`, `GetVersionTimelineAsync()`: Version queries
- `CreateZipArchiveAsync()`: Bulk export (EDIT-07)
- Parser methods: `ParseAtomicRules()`, `ParseExtractedText()`

**3. RuleSpecDiffService** (~192 lines - estimated):
- Version comparison and diff generation

**Total**: ~885 lines to organize into DDD structure

---

## Phase 4 Strategy: Pragmatic DDD

### Learning from Phase 3

**What Worked**:
- ✅ Domain services for algorithms
- ✅ Value objects for type safety
- ✅ Clean separation of concerns
- ✅ Mapper pattern for conversions

**Apply to GameManagement**:
- Focus on core domain logic
- Keep integration simple
- Reuse Phase 3 patterns
- Target: 6-8 hours (vs 10 hours Phase 3)

---

## Domain Model Design

### Aggregate Roots (2)

**1. Game** (Aggregate Root):
```csharp
public sealed class Game : AggregateRoot<Guid>
{
    public GameTitle Title { get; private set; }
    public Publisher? Publisher { get; private set; }
    public YearPublished? YearPublished { get; private set; }
    public PlayerCount PlayerCount { get; private set; }
    public PlayTime PlayTime { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Methods
    public void UpdateDetails(GameTitle title, Publisher? publisher, ...)
    public bool SupportsPlayerCount(int players)
}
```

**2. RuleSpec** (Aggregate Root):
```csharp
public sealed class RuleSpec : AggregateRoot<Guid>
{
    public Guid GameId { get; private set; }
    public Version Version { get; private set; }
    public RuleSpecContent Content { get; private set; } // JSON
    public RuleSpecStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    // Methods
    public void UpdateContent(RuleSpecContent content, Guid userId)
    public void Publish()
    public void Archive()
    public Version GenerateNextVersion()
}
```

### Value Objects (6)

**1. GameTitle**: Title with normalization and ID generation
**2. Version**: Semantic versioning (major.minor.patch)
**3. Publisher**: Publisher name validation
**4. PlayerCount**: Min/max player range validation
**5. PlayTime**: Min/max duration in minutes
**6. RuleSpecContent**: JSON content with validation

### Domain Services (3)

**1. RuleSpecGenerationDomainService**:
- `GenerateFromPdf()`: AI-powered RuleSpec generation logic
- `ParseExtractedText()`: Text parsing logic
- `CreateRuleAtoms()`: Rule structure creation

**2. RuleSpecVersioningDomainService**:
- `GenerateNextVersion()`: Version increment logic
- `CompareVersions()`: Version diff algorithm
- `ValidateVersion()`: Version rules enforcement

**3. GameNormalizationDomainService**:
- `NormalizeTitle()`: Slug generation
- `GenerateIdFromTitle()`: Deterministic ID creation
- `ValidateGameData()`: Business rules validation

---

## Phase 4 Implementation Plan

### Step 1: Domain Layer (2-3 hours)

**1.1 Value Objects** (30 min):
- GameTitle (with normalization)
- Version (semantic versioning)
- Publisher, PlayerCount, PlayTime
- RuleSpecContent

**1.2 Entities** (1 hour):
- Game (aggregate root)
- RuleSpec (aggregate root)
- RuleSpecVersion (entity)

**1.3 Domain Services** (1 hour):
- RuleSpecGenerationDomainService
- RuleSpecVersioningDomainService
- GameNormalizationDomainService

**1.4 Repository Interfaces** (30 min):
- IGameRepository
- IRuleSpecRepository

### Step 2: Application Layer (1.5-2 hours)

**2.1 Commands** (30 min):
- CreateGameCommand
- UpdateGameCommand
- CreateRuleSpecCommand
- UpdateRuleSpecCommand
- GenerateRuleSpecFromPdfCommand

**2.2 Queries** (30 min):
- GetGameQuery, GetGamesQuery
- GetRuleSpecQuery
- GetVersionHistoryQuery
- GetVersionTimelineQuery

**2.3 DTOs** (15 min):
- GameDto, RuleSpecDto, VersionDto

**2.4 Handlers** (1 hour):
- Game handlers (Create, Update, Get, GetAll)
- RuleSpec handlers (Create, Update, Get, GetHistory)

### Step 3: Infrastructure Layer (1.5-2 hours)

**3.1 Mappers** (30 min):
- GameManagementMappers (Game ↔ GameEntity, RuleSpec ↔ RuleSpecEntity)

**3.2 Repositories** (1 hour):
- GameRepository (EF Core + mappers)
- RuleSpecRepository (EF Core + mappers + version queries)

**3.3 External Adapters** (30 min - optional):
- BggApiAdapter (if extracting BGG integration)

### Step 4: Integration (1-1.5 hours)

**4.1 DI Registration** (15 min):
- GameManagementServiceExtensions
- Register in ApplicationServiceExtensions

**4.2 API Endpoints** (45 min):
- GameManagementEndpoints
- Routes: `/games/*`, `/rule-specs/*`

**4.3 Testing** (30 min):
- Build verification
- Smoke tests

---

## Simplified Approach for Alpha

Given alpha constraints and Phase 3 experience:

### Option A: Full DDD (6-8 hours) - Comprehensive

**Pros**:
- Complete bounded context
- Pattern reinforcement
- Production-quality architecture

**Cons**:
- Time investment
- Mapping complexity

### Option B: Pragmatic Hybrid (3-4 hours) - Recommended ⭐

**Focus on**:
- Domain services only (extract algorithms)
- Keep existing entities (no aggregate roots yet)
- Thin handlers (delegate to existing services where possible)
- Minimal mapping

**What to extract**:
- ✅ RuleSpecGenerationDomainService (PDF parsing logic)
- ✅ RuleSpecVersioningDomainService (version management)
- ✅ GameNormalizationDomainService (slug generation)
- ❌ Skip: New entities, value objects (use existing)
- ❌ Skip: Full repositories (use DbContext directly in handlers)

**Result**:
- Cleaner code organization
- Business logic separated
- Easier to test algorithms
- Less time investment

---

## Recommended Approach for Phase 4

**Hybrid Approach** (4-5 hours):

1. **Domain Services** (2 hours):
   - Extract 3 domain services with pure algorithms
   - No dependencies on infrastructure

2. **CQRS Handlers** (1.5 hours):
   - Create handlers for key operations
   - Use existing entities (GameEntity, RuleSpecEntity)
   - Delegate to domain services for business logic

3. **Integration** (1 hour):
   - Register services and handlers
   - Update or create endpoints
   - Build and smoke test

4. **Documentation** (30 min):
   - Document pattern and decisions
   - Update memory

**Skip for Now**:
- Custom value objects (use primitives)
- Aggregate roots (use existing entities)
- Full repository pattern (use DbContext)
- Complex mapping layer

**Benefits**:
- ✅ Achieves 70% of DDD value with 50% of time
- ✅ Can add full DDD later if needed
- ✅ Focus on algorithm extraction (main benefit)
- ✅ Keeps momentum for alpha delivery

---

## Timeline Comparison

| Approach | Domain | Application | Infrastructure | Integration | Total |
|----------|--------|-------------|----------------|-------------|-------|
| **Full DDD** | 3h | 2h | 2h | 1.5h | 8.5h |
| **Pragmatic** | 2h | 1.5h | 0.5h | 1h | 5h |

**Savings**: 3.5 hours with pragmatic approach
**Trade-off**: Less "pure" DDD, but cleaner than current state

---

## Decision Point

### Choose Approach

**Option A: Full DDD** (8 hours):
- Complete implementation like Phase 3
- Aggregate roots, value objects, repositories
- Production-quality architecture

**Option B: Pragmatic Hybrid** (5 hours):
- Domain services + handlers
- Keep existing entities
- Focus on algorithm extraction

**Option C: Pause GameManagement**:
- Complete Phase 2 (Authentication) instead
- Or add tests to Phase 3
- Return to GameManagement later

### Recommendation

**For Alpha**: **Option B (Pragmatic Hybrid)**

**Rationale**:
- Main value is algorithm extraction
- Can complete in single session
- Keeps alpha momentum
- Can enhance to full DDD post-alpha

---

## Next Steps

**If Option B (Pragmatic)**:
1. Create 3 domain services (~2 hours)
2. Create CQRS handlers (~1.5 hours)
3. Register and integrate (~1 hour)
4. Document and commit (~30 min)

**Total**: 5 hours = One focused session

Ready to start Phase 4 with pragmatic approach?

---

**Created**: 2025-11-11
**Status**: Ready to start
**Recommended**: Option B (Pragmatic Hybrid, 5 hours)
