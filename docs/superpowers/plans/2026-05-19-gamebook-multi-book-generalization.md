# Gamebook Multi-Book Generalization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generalize the gamebook companion to support any board game with 1..N books of any role combination (tutorial, rules-reference, narrative, encounter, lore, setup), eliminating the hardcoded "Press Start + Rules" + "Storybook + Encounter" assumption inherited from the Nanolith-specific demo.

**Architecture:** Introduce `GameBook` aggregate in `GameManagement` BC with multi-valued `GameBookRole` flag enum. Refactor `SessionTracking` to track per-book progress via new `SessionBookProgress` aggregate. Replace `GamebookPageType` enum with `GameBookId` FK on `TranslatedParagraph` + `GamebookPhotoArtifact`. Add chunk-level `role_tags` to `text_chunks` + hybrid `RoleClassifierService` (rule + LLM fallback) at ingestion + `IntentClassifierService` at query time + `HybridSearchService` re-ranker boost. Frontend: conditional `BookPicker` (only N>1 narrative books) + per-book resume UI.

**Tech Stack:** .NET 9 (ASP.NET Minimal APIs + MediatR + FluentValidation + EF Core + pgvector), C# 12, xUnit + Testcontainers, Next.js 16 + React 19 + TypeScript + Vitest + Playwright.

**Depends on:** [#1320 Game Entity Reset](../../for-developers/specs/2026-05-19-game-entity-reset.md) Phase 2 mergiato (provides `GameRef` discriminator + reset schema). This plan opens a feature branch from `main-dev` POST-#1320 Phase 2 land.

**Blocked by:** [#4228 AgentDefinition redesign](../../for-developers/specs/2026-05-19-game-entity-reset.md#out-of-scope-deferred) for FM-23 invariant enforcement (deferred to follow-up plan).

**Codebase post-#1320 reconnaissance** (2026-05-19):
- `GameRef` value object location: `apps/api/src/Api/SharedKernel/Domain/ValueObjects/GameRef.cs`
- `MeepleAiDbContext` path: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` (NOT `Infrastructure/Persistence/`)
- Chunk entity: `TextChunkEntity` at `apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/TextChunkEntity.cs` (NOT `TextChunkEntity`)
- Chunk EF config: `apps/api/src/Api/Infrastructure/EntityEntityConfigurations/KnowledgeBase/TextChunkEntityConfiguration.cs`
- Chunk table: `text_chunks` (NOT `text_chunks`)
- **Gamebook BC NOT refactored to GameRef by #1320** — `GamebookCampaignSession.GameId` is still `Guid` with no FK constraint. Must refactor in **Phase A0** below.
- Gamebook entities have NO dedicated EF config files (convention-based mapping). Phase A0 introduces explicit config files for clarity.

---

## File Structure

### Files to create

**`GameManagement` BC**:
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameBook.cs` — aggregate root
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/ValueObjects/GameBookRole.cs` — flag enum (Tutorial=1, RulesReference=2, Narrative=4, Encounter=8, Lore=16, Setup=32)
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/ValueObjects/ParagraphScheme.cs` — enum (None=0, ParagraphNumber=1, PageNumber=2, Section=3)
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/IGameBookRepository.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/GameBookNotFoundException.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/GameBookKbSourceConflictException.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/GameBookPhysicalCoherenceException.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Repositories/GameBookRepository.cs`
- `apps/api/src/Api/Infrastructure/EntityConfigurations/GameManagement/GameBookEntityConfiguration.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CreateGameBookCommand.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CreateGameBookCommandHandler.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CreateGameBookCommandValidator.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/UpdateGameBookCommand.cs` (+ Handler + Validator)
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/AttachKbSourceCommand.cs` (+ Handler + Validator)
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/DetachKbSourceCommand.cs` (+ Handler + Validator)
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/SoftDeleteGameBookCommand.cs` (+ Handler + Validator)
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/GetGameBookByIdQuery.cs` (+ Handler)
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/ListGameBooksByGameQuery.cs` (+ Handler)
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/GameBookDto.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Mappers/GameBookMapper.cs`

**`SessionTracking` BC**:
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/SessionBookProgress.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Repositories/ISessionBookProgressRepository.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Repositories/SessionBookProgressRepository.cs`
- `apps/api/src/Api/Infrastructure/EntityConfigurations/SessionTracking/SessionBookProgressEntityConfiguration.cs`

**`KnowledgeBase` BC**:
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRoleClassifierService.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RoleClassifierService.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IIntentClassifierService.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IntentClassifierService.cs`

**Migrations**:
- `apps/api/src/Api/Infrastructure/Migrations/<TS>_AddGameBooks.cs` (single consolidated migration: `game_books` + `gamebook_session_book_progress` + `gamebook_translated_paragraphs.game_book_id` + `gamebook_photo_artifacts.game_book_id` + `text_chunks.role_tags`)

**Frontend**:
- `apps/web/src/components/gamebook/BookPicker.tsx`
- `apps/web/src/components/gamebook/ResumeBooksList.tsx`
- `apps/web/src/lib/api/gamebook.ts` (already exists, extend with `listGameBooks(gameRef)`)
- `apps/web/src/hooks/useGameBooks.ts`

**Seed**:
- `apps/api/src/Api/Infrastructure/Seeders/GameBookSeeder.cs` (extends existing seed orchestrator)

**Tests**:
- `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/GameBookTests.cs`
- `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/CreateGameBookCommandHandlerTests.cs` (+ similar for Update, AttachKb, DetachKb, SoftDelete)
- `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/GetGameBookByIdQueryHandlerTests.cs` (+ similar for ListGameBooksByGame)
- `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Infrastructure/GameBookRepositoryTests.cs`
- `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/SessionBookProgressTests.cs`
- `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/RoleClassifierServiceTests.cs`
- `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/IntentClassifierServiceTests.cs`
- `apps/web/__tests__/components/gamebook/BookPicker.test.tsx`
- `apps/web/__tests__/components/gamebook/ResumeBooksList.test.tsx`
- E2E (Playwright): `apps/web/tests/e2e/gamebook-multi-config.spec.ts`

### Files to modify

- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/GamebookCampaignSession.cs` — remove `Progress` VO usage, expose `BookProgress` collection via repository
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/TranslatedParagraph.cs` — `Create()` factory adds `gameBookId` parameter; existing field `PageType` removed
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/GamebookPhotoArtifact.cs` — add `GameBookId` field + factory
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/GamebookGlossaryEntry.cs` — add nullable `FirstSeenBookId` field
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/HybridSearchService.cs` — re-ranker includes `role_match` signal
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Pipeline/PdfIngestionPipeline.cs` — invoke `RoleClassifierService` post-chunk-extraction
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/SearchQuery.cs` — add `QueryRoleHint` optional field
- All `TranslatedParagraph.Create()` callers (~3-5 sites in `SessionTracking` command handlers) — add `gameBookId` argument
- All `GamebookPhotoArtifact.Create()` callers — add `gameBookId` argument
- `apps/api/src/Api/Infrastructure/EntityConfigurations/SessionTracking/GamebookCampaignSessionEntityConfiguration.cs` — remove `Progress` value object mapping; configure `BookProgress` collection navigation
- `apps/api/src/Api/Infrastructure/EntityConfigurations/SessionTracking/TranslatedParagraphEntityConfiguration.cs` — add `GameBookId` FK
- `apps/api/src/Api/Infrastructure/EntityConfigurations/SessionTracking/GamebookPhotoArtifactEntityConfiguration.cs` — add `GameBookId` FK
- `apps/api/src/Api/Infrastructure/Configurations/KnowledgeBase/TextChunkEntityEntityConfiguration.cs` — add `RoleTags` column mapping
- `apps/web/src/components/gamebook/PhotoTranslateForm.tsx` (or similar existing photo upload component) — integrate `BookPicker` conditional
- `apps/web/src/app/library/games/[gameId]/play/page.tsx` (resume entry point) — use `ResumeBooksList` instead of single last paragraph display
- `apps/web/tests/e2e/libro-game-nanolith.spec.ts` (existing E2E) — update tags from hardcoded to `@gamebook-multi-config`
- `docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md` — cross-link to generalization spec
- `CLAUDE.md` — add `GameBook` to bounded context table

### Files to delete

- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/GamebookPageType.cs`
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ValueObjects/GamebookProgress.cs`

---

## Task 0: Setup feature branch + prereqs verification

**Files:** none (branch + tooling only)

- [ ] **Step 1: Verify #1320 Phase 2 prerequisite is mergiato**

```bash
git checkout main-dev
git pull
git log --oneline --grep="#1320" | head -5
```

Expected: at least one commit referencing #1320 Phase 2 with `feat:` or `refactor:` prefix. If not present, abort plan execution and wait for #1320 Phase 2 land.

- [ ] **Step 2: Verify `GameRef` discriminator exists in codebase**

```bash
grep -r "public sealed record GameRef" apps/api/src/Api/ | head -3
```

Expected: at least one match showing the `GameRef` discriminator definition. If missing → #1320 Phase 2 incomplete, abort.

- [ ] **Step 3: Create branch from main-dev**

```bash
git branch --show-current  # MUST print main-dev
git status                 # MUST show clean tree
git pull --ff-only         # MUST succeed
git checkout -b feature/issue-TBD-gamebook-multi-book-generalization
git config branch.feature/issue-TBD-gamebook-multi-book-generalization.parent main-dev
```

Replace `TBD` with actual issue number once opened.

- [ ] **Step 4: Verify legacy gamebook tables were dropped by #1320 reset**

```bash
docker compose -f infra/docker-compose.yml exec postgres psql -U meepleai meepleai_dev -c "\dt gamebook_*"
```

Expected: empty result (no rows). If tables exist → #1320 reset incomplete or schema needs manual cleanup. Investigate before proceeding.

- [ ] **Step 5: Run baseline test suite to confirm green starting state**

```bash
cd apps/api/src/Api && dotnet test --filter "Category=Unit" --logger "console;verbosity=minimal"
```

Expected: all tests PASS. Note baseline test count for end-of-plan comparison.

---

## Phase A0: Gamebook BC alignment to GameRef (Tasks A0.1-A0.4)

> **Rationale**: #1320 Phase 2c refactored ~25 consumers to use `GameRef` discriminator but did NOT touch `GamebookCampaignSession.GameId` (still `Guid` with no FK). Phase A0 closes that gap before introducing `GameBook` entity (which assumes `GameRef`). Also introduces explicit EF config files for 4 gamebook entities (currently convention-based mapping).

### Task A0.1: Create explicit EF config files for 4 gamebook entities (convention → explicit)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/SessionTracking/GamebookCampaignSessionEntityConfiguration.cs`
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/SessionTracking/TranslatedParagraphEntityConfiguration.cs`
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/SessionTracking/GamebookPhotoArtifactEntityConfiguration.cs`
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/SessionTracking/GamebookGlossaryEntryEntityConfiguration.cs`

- [ ] **Step 1: Inspect current convention-based mappings to capture all column names + indices**

```bash
grep -n "gamebook_\|translated_paragraphs" apps/api/src/Api/Infrastructure/Migrations/MeepleAiDbContextModelSnapshot.cs | head -40
```

Record actual column names + indices (snake_case mappings).

- [ ] **Step 2: Implement `GamebookCampaignSessionEntityConfiguration`**

```csharp
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SessionTracking;

internal class GamebookCampaignSessionEntityConfiguration : IEntityTypeConfiguration<GamebookCampaignSession>
{
    public void Configure(EntityTypeBuilder<GamebookCampaignSession> builder)
    {
        builder.ToTable("gamebook_campaign_sessions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.GameId).HasColumnName("game_id").IsRequired();   // refactored in Task A0.2
        builder.Property(e => e.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(120).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at");

        builder.OwnsOne(e => e.Progress, p =>
        {
            p.Property(x => x.CurrentParagraph).HasColumnName("progress_current_paragraph");
            p.Property(x => x.History).HasColumnName("progress_history").HasColumnType("integer[]");
            p.Property(x => x.LastReadAt).HasColumnName("progress_last_read_at");
        });

        builder.HasIndex(e => new { e.OwnerUserId, e.GameId, e.IsDeleted })
               .HasDatabaseName("ix_gamebook_campaign_sessions_owner_game");
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
```

> **Note**: do NOT modify `OnModelCreating` in `MeepleAiDbContext.cs` if it already uses `ApplyConfigurationsFromAssembly()` (verified via recon). The new files will be picked up automatically.

- [ ] **Step 3: Repeat pattern for the other 3 entities** (TranslatedParagraph, GamebookPhotoArtifact, GamebookGlossaryEntry)

Use snake_case column names matching `MeepleAiDbContextModelSnapshot.cs` content.

- [ ] **Step 4: Run full test suite to verify zero schema regression**

```bash
cd apps/api/src/Api && dotnet test --filter "Category=Unit|Category=Integration" -v minimal 2>&1 | tail -10
```

Expected: all PASS. If migrations differ now that config is explicit, generate a no-op migration (empty Up/Down methods) to align ModelSnapshot.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/EntityConfigurations/SessionTracking/*.cs
git commit -m "refactor(gamebook): make 4 gamebook EF configs explicit (convention → file-based)"
```

### Task A0.2: Refactor `GamebookCampaignSession.GameId Guid` → `GameRef` discriminator

**Files to modify:**
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/GamebookCampaignSession.cs`
- `apps/api/src/Api/Infrastructure/EntityConfigurations/SessionTracking/GamebookCampaignSessionEntityConfiguration.cs`
- All consumers of `GamebookCampaignSession.GameId` (~3-5 handlers + tests)

- [ ] **Step 1: Identify all consumers**

```bash
grep -rn "GamebookCampaignSession\b" apps/api/src/Api/ apps/api/tests/Api.Tests/ \
  | grep "GameId\|\.Create(" | head -20
```

Record list — these need parameter signature change.

- [ ] **Step 2: Write failing test**

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/GamebookCampaignSessionTests.cs
[Fact]
public void Create_WithGameRef_StoresAsDiscriminator()
{
    var sharedId = Guid.NewGuid();
    var session = GamebookCampaignSession.Create(
        gameRef: GameRef.Shared(sharedId),
        ownerUserId: Guid.NewGuid(),
        title: "Campagna 1");

    Assert.Equal(GameRefKind.Shared, session.GameRef.Kind);
    Assert.Equal(sharedId, session.GameRef.Id);
}

[Fact]
public void Create_WithPrivateGameRef_StoresAsDiscriminator()
{
    var privateId = Guid.NewGuid();
    var session = GamebookCampaignSession.Create(
        gameRef: GameRef.Private(privateId),
        ownerUserId: Guid.NewGuid(),
        title: "Campagna privata");

    Assert.Equal(GameRefKind.Private, session.GameRef.Kind);
}
```

- [ ] **Step 3: Run test → COMPILE FAIL (`GameRef` param not accepted)**

- [ ] **Step 4: Update entity**

```csharp
// In GamebookCampaignSession.cs:
public sealed class GamebookCampaignSession
{
    public Guid Id { get; private set; }
    public GameRef GameRef { get; private set; } = default!;  // REPLACES Guid GameId
    // ... existing fields ...

    public static GamebookCampaignSession Create(GameRef gameRef, Guid ownerUserId, string title)
    {
        if (gameRef is null) throw new ArgumentNullException(nameof(gameRef));
        if (ownerUserId == Guid.Empty) throw new ArgumentException("required", nameof(ownerUserId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("required", nameof(title));

        var now = DateTimeOffset.UtcNow;
        return new GamebookCampaignSession
        {
            Id = Guid.NewGuid(),
            GameRef = gameRef,
            OwnerUserId = ownerUserId,
            Title = title.Trim(),
            Progress = GamebookProgress.Empty(),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = ownerUserId,
        };
    }
}
```

- [ ] **Step 5: Update EF config**

Replace:
```csharp
builder.Property(e => e.GameId).HasColumnName("game_id").IsRequired();
```

With:
```csharp
builder.OwnsOne(e => e.GameRef, gr =>
{
    gr.Property(p => p.Id).HasColumnName("game_ref_id").IsRequired();
    gr.Property(p => p.Kind).HasColumnName("game_ref_kind").HasConversion<short>().IsRequired();
});
```

Update index:
```csharp
builder.HasIndex("owner_user_id", "game_ref_kind", "game_ref_id", "is_deleted")
       .HasDatabaseName("ix_gamebook_campaign_sessions_owner_game_ref");
// Drop old "ix_gamebook_campaign_sessions_owner_game" index name in migration
```

- [ ] **Step 6: Generate migration `RefactorGamebookGameIdToGameRef`**

```bash
cd apps/api/src/Api
dotnet ef migrations add RefactorGamebookGameIdToGameRef
```

Review the generated SQL: must include
1. `ALTER TABLE gamebook_campaign_sessions ADD COLUMN game_ref_id UUID, ADD COLUMN game_ref_kind SMALLINT;`
2. `UPDATE gamebook_campaign_sessions SET game_ref_id = game_id, game_ref_kind = 0;` (data backfill, assume all existing rows pointed to SharedGame)
3. `ALTER TABLE gamebook_campaign_sessions ALTER COLUMN game_ref_id SET NOT NULL, ALTER COLUMN game_ref_kind SET NOT NULL, DROP COLUMN game_id;`
4. Drop old index + add new one

If EF doesn't generate the UPDATE statement automatically, add it manually in the migration's `Up()` method.

- [ ] **Step 7: Update all consumers from Step 1**

For each handler/query consuming `session.GameId`:
- Replace `session.GameId` with `session.GameRef.Id` (when treating it as plain guid)
- Or `session.GameRef` (when passing forward)
- For factory calls: `GamebookCampaignSession.Create(gameId, ...)` → `GamebookCampaignSession.Create(GameRef.Shared(gameId), ...)`

- [ ] **Step 8: Apply migration + run tests**

```bash
dotnet ef database update
dotnet test --filter "FullyQualifiedName~SessionTracking" -v minimal 2>&1 | tail -10
```

Expected: all PASS.

- [ ] **Step 9: Commit**

```bash
git add -u
git commit -m "refactor(gamebook): refactor GamebookCampaignSession.GameId Guid -> GameRef discriminator (aligns to #1320 Phase 2c)"
```

### Task A0.3: Verify all gamebook tests + smoke check

- [ ] **Step 1: Run full test suite**

```bash
cd apps/api/src/Api && dotnet test 2>&1 | tail -10
```

Expected: all PASS. Note baseline count for end-of-plan comparison.

- [ ] **Step 2: Manual smoke**

```bash
cd infra && make dev-core
# Wait API startup
curl -s http://localhost:8080/health | jq
# Expected: {"status":"healthy"}
```

- [ ] **Step 3: Commit only if Step 1+2 found+fixed issues; otherwise skip commit**

---

## Phase A: GameBook Domain (Tasks A1-A6)

### Task A1: Create `GameBookRole` flag enum + `ParagraphScheme` enum

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/ValueObjects/GameBookRole.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/ValueObjects/ParagraphScheme.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/GameBookRoleTests.cs`

- [ ] **Step 1: Write the failing test for `GameBookRole` flag combinations**

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/GameBookRoleTests.cs
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

public class GameBookRoleTests
{
    [Fact]
    public void GameBookRole_SupportsMultiValuedFlags()
    {
        var allInOne = GameBookRole.Tutorial | GameBookRole.RulesReference
                     | GameBookRole.Narrative | GameBookRole.Encounter;

        Assert.True(allInOne.HasFlag(GameBookRole.Tutorial));
        Assert.True(allInOne.HasFlag(GameBookRole.RulesReference));
        Assert.True(allInOne.HasFlag(GameBookRole.Narrative));
        Assert.True(allInOne.HasFlag(GameBookRole.Encounter));
        Assert.False(allInOne.HasFlag(GameBookRole.Lore));
    }

    [Fact]
    public void GameBookRole_NoneIsZero()
    {
        Assert.Equal(0, (int)GameBookRole.None);
    }

    [Fact]
    public void ParagraphScheme_HasAllExpectedValues()
    {
        Assert.Equal(0, (int)ParagraphScheme.None);
        Assert.Equal(1, (int)ParagraphScheme.ParagraphNumber);
        Assert.Equal(2, (int)ParagraphScheme.PageNumber);
        Assert.Equal(3, (int)ParagraphScheme.Section);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~GameBookRoleTests" -v minimal
```

Expected: FAIL with "type or namespace name 'GameBookRole' could not be found".

- [ ] **Step 3: Implement `GameBookRole` and `ParagraphScheme` enums**

```csharp
// File: apps/api/src/Api/BoundedContexts/GameManagement/Domain/ValueObjects/GameBookRole.cs
namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

[Flags]
public enum GameBookRole
{
    None = 0,
    Tutorial = 1,
    RulesReference = 2,
    Narrative = 4,
    Encounter = 8,
    Lore = 16,
    Setup = 32,
}
```

```csharp
// File: apps/api/src/Api/BoundedContexts/GameManagement/Domain/ValueObjects/ParagraphScheme.cs
namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

public enum ParagraphScheme
{
    None = 0,
    ParagraphNumber = 1,
    PageNumber = 2,
    Section = 3,
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~GameBookRoleTests" -v minimal
```

Expected: 3 PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/ValueObjects/GameBookRole.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Domain/ValueObjects/ParagraphScheme.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/GameBookRoleTests.cs
git commit -m "feat(gamebook): add GameBookRole flag enum + ParagraphScheme (refs gamebook-multi-book-generalization)"
```

### Task A2: Create `GameBook` entity with factory + invariants

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameBook.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/GameBookPhysicalCoherenceException.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/GameBookTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/GameBookTests.cs
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;  // GameRef post-#1320
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

public class GameBookTests
{
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GameId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid PdfId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly GameRef SharedRef = GameRef.Shared(GameId);

    [Fact]
    public void CreateCommunity_WithValidInputs_SetsOwnerUserIdNull()
    {
        var book = GameBook.CreateCommunity(
            SharedRef, "Press Start",
            GameBookRole.Tutorial | GameBookRole.Setup,
            ParagraphScheme.None, "en",
            sequentialRead: false,
            kbSourceDocId: PdfId,
            physicalOnly: false,
            createdBy: AdminId);

        Assert.Null(book.OwnerUserId);
        Assert.Equal("Press Start", book.DisplayName);
        Assert.Equal(GameBookRole.Tutorial | GameBookRole.Setup, book.Roles);
        Assert.Equal(PdfId, book.KbSourceDocId);
        Assert.False(book.PhysicalOnly);
    }

    [Fact]
    public void CreatePersonal_WithValidInputs_SetsOwnerUserId()
    {
        var book = GameBook.CreatePersonal(
            SharedRef, UserId, "My House Rules",
            GameBookRole.RulesReference,
            ParagraphScheme.None, "it",
            sequentialRead: false,
            kbSourceDocId: PdfId,
            physicalOnly: false);

        Assert.Equal(UserId, book.OwnerUserId);
    }

    [Fact]
    public void Create_PhysicalOnlyTrueWithKbSource_ThrowsCoherenceException()
    {
        Assert.Throws<GameBookPhysicalCoherenceException>(() =>
            GameBook.CreateCommunity(
                SharedRef, "Storybook",
                GameBookRole.Narrative,
                ParagraphScheme.ParagraphNumber, "en",
                sequentialRead: true,
                kbSourceDocId: PdfId,         // INCOHERENT
                physicalOnly: true,           // INCOHERENT
                createdBy: AdminId));
    }

    [Fact]
    public void Create_EmptyRoles_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            GameBook.CreateCommunity(
                SharedRef, "Empty roles",
                GameBookRole.None,            // INVALID
                ParagraphScheme.None, "en",
                sequentialRead: false,
                kbSourceDocId: null,
                physicalOnly: true,
                createdBy: AdminId));
    }

    [Fact]
    public void AttachKbSource_OnPhysicalOnlyBook_ThrowsCoherenceException()
    {
        var book = GameBook.CreateCommunity(
            SharedRef, "Cartaceo", GameBookRole.Narrative,
            ParagraphScheme.ParagraphNumber, "en", true, null, true, AdminId);

        Assert.Throws<GameBookPhysicalCoherenceException>(() =>
            book.AttachKbSource(PdfId, AdminId));
    }

    [Fact]
    public void SoftDelete_SetsIsDeletedAndDeletedAt()
    {
        var book = GameBook.CreateCommunity(
            SharedRef, "X", GameBookRole.Tutorial, ParagraphScheme.None,
            "en", false, null, true, AdminId);

        book.SoftDelete(AdminId);

        Assert.True(book.IsDeleted);
        Assert.NotNull(book.DeletedAt);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~GameBookTests" -v minimal
```

Expected: COMPILE FAIL (`GameBook` not defined, `GameBookPhysicalCoherenceException` not defined).

- [ ] **Step 3: Implement exception + `GameBook` entity**

```csharp
// File: apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/GameBookPhysicalCoherenceException.cs
namespace Api.BoundedContexts.GameManagement.Domain.Exceptions;

public sealed class GameBookPhysicalCoherenceException : Exception
{
    public GameBookPhysicalCoherenceException(string message) : base(message) { }
}
```

```csharp
// File: apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameBook.cs
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

public sealed class GameBook
{
    public Guid Id { get; private set; }
    public GameRef GameRef { get; private set; } = default!;
    public Guid? OwnerUserId { get; private set; }
    public string DisplayName { get; private set; } = default!;
    public GameBookRole Roles { get; private set; }
    public ParagraphScheme ParagraphScheme { get; private set; }
    public string Language { get; private set; } = default!;
    public bool SequentialRead { get; private set; }
    public Guid? KbSourceDocId { get; private set; }
    public bool PhysicalOnly { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private GameBook() { }

    public static GameBook CreateCommunity(
        GameRef gameRef, string displayName, GameBookRole roles,
        ParagraphScheme paragraphScheme, string language, bool sequentialRead,
        Guid? kbSourceDocId, bool physicalOnly, Guid createdBy)
    {
        return CreateInternal(gameRef, ownerUserId: null, displayName, roles,
            paragraphScheme, language, sequentialRead, kbSourceDocId, physicalOnly, createdBy);
    }

    public static GameBook CreatePersonal(
        GameRef gameRef, Guid ownerUserId, string displayName, GameBookRole roles,
        ParagraphScheme paragraphScheme, string language, bool sequentialRead,
        Guid? kbSourceDocId, bool physicalOnly)
    {
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("ownerUserId required", nameof(ownerUserId));

        return CreateInternal(gameRef, ownerUserId, displayName, roles,
            paragraphScheme, language, sequentialRead, kbSourceDocId, physicalOnly, ownerUserId);
    }

    private static GameBook CreateInternal(
        GameRef gameRef, Guid? ownerUserId, string displayName, GameBookRole roles,
        ParagraphScheme paragraphScheme, string language, bool sequentialRead,
        Guid? kbSourceDocId, bool physicalOnly, Guid createdBy)
    {
        if (gameRef is null) throw new ArgumentNullException(nameof(gameRef));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("displayName required", nameof(displayName));
        if (displayName.Length > 120)
            throw new ArgumentException("displayName max length 120", nameof(displayName));
        if (roles == GameBookRole.None)
            throw new ArgumentException("at least one role required", nameof(roles));
        if (string.IsNullOrWhiteSpace(language) || language.Length != 2)
            throw new ArgumentException("language must be ISO 639-1 (2 chars)", nameof(language));
        if (physicalOnly && kbSourceDocId.HasValue)
            throw new GameBookPhysicalCoherenceException(
                "physicalOnly=true implies kbSourceDocId must be null");

        var now = DateTimeOffset.UtcNow;
        return new GameBook
        {
            Id = Guid.NewGuid(),
            GameRef = gameRef,
            OwnerUserId = ownerUserId,
            DisplayName = displayName.Trim(),
            Roles = roles,
            ParagraphScheme = paragraphScheme,
            Language = language.ToLowerInvariant(),
            SequentialRead = sequentialRead,
            KbSourceDocId = kbSourceDocId,
            PhysicalOnly = physicalOnly,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = createdBy,
        };
    }

    public void AttachKbSource(Guid pdfDocId, Guid updatedBy)
    {
        if (PhysicalOnly)
            throw new GameBookPhysicalCoherenceException(
                "Cannot attach KB source to physical-only book. Call MarkAsIndexable first.");
        KbSourceDocId = pdfDocId;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void DetachKbSource(Guid updatedBy)
    {
        KbSourceDocId = null;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void UpdateRoles(GameBookRole newRoles, Guid updatedBy)
    {
        if (newRoles == GameBookRole.None)
            throw new ArgumentException("at least one role required", nameof(newRoles));
        Roles = newRoles;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Rename(string newName, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(newName) || newName.Length > 120)
            throw new ArgumentException("invalid displayName", nameof(newName));
        DisplayName = newName.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void SoftDelete(Guid deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DeletedAt.Value;
        UpdatedBy = deletedBy;
    }
}
```

- [ ] **Step 4: Run test to verify all 6 tests pass**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~GameBookTests" -v minimal
```

Expected: 6 PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameBook.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/GameBookPhysicalCoherenceException.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/GameBookTests.cs
git commit -m "feat(gamebook): add GameBook aggregate with factory + invariants"
```

### Task A3: Create `IGameBookRepository` interface + 2 exceptions

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/IGameBookRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/GameBookNotFoundException.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/GameBookKbSourceConflictException.cs`

- [ ] **Step 1: Implement the 2 exception types**

```csharp
// File: apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/GameBookNotFoundException.cs
namespace Api.BoundedContexts.GameManagement.Domain.Exceptions;

public sealed class GameBookNotFoundException : Exception
{
    public GameBookNotFoundException(Guid id)
        : base($"GameBook with id '{id}' not found.") { }
}
```

```csharp
// File: apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/GameBookKbSourceConflictException.cs
namespace Api.BoundedContexts.GameManagement.Domain.Exceptions;

public sealed class GameBookKbSourceConflictException : Exception
{
    public GameBookKbSourceConflictException(Guid pdfDocId, Guid conflictingBookId)
        : base($"PDF document {pdfDocId} is already kbSource of community GameBook {conflictingBookId}.") { }
}
```

- [ ] **Step 2: Implement `IGameBookRepository` interface**

```csharp
// File: apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/IGameBookRepository.cs
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

public interface IGameBookRepository
{
    Task<GameBook?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<GameBook>> ListByGameRefAsync(GameRef gameRef, Guid? ownerUserId, CancellationToken ct);
    Task<GameBook?> FindCommunityByKbSourceAsync(Guid pdfDocId, CancellationToken ct);
    Task AddAsync(GameBook book, CancellationToken ct);
    Task UpdateAsync(GameBook book, CancellationToken ct);
}
```

- [ ] **Step 3: Verify compilation**

```bash
cd apps/api/src/Api && dotnet build --no-restore 2>&1 | grep -i "error\|fail" | head -5
```

Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/IGameBookRepository.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/GameBookNotFoundException.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Domain/Exceptions/GameBookKbSourceConflictException.cs
git commit -m "feat(gamebook): add IGameBookRepository interface + domain exceptions"
```

### Task A4: Create `GameBookEntityConfiguration` (EF mapping)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/GameManagement/GameBookEntityConfiguration.cs`
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` (add `DbSet<GameBook> GameBooks`)

- [ ] **Step 1: Write the EF configuration**

```csharp
// File: apps/api/src/Api/Infrastructure/EntityConfigurations/GameManagement/GameBookEntityConfiguration.cs
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

internal class GameBookEntityConfiguration : IEntityTypeConfiguration<GameBook>
{
    public void Configure(EntityTypeBuilder<GameBook> builder)
    {
        builder.ToTable("game_books");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.OwnsOne(e => e.GameRef, gr =>
        {
            gr.Property(p => p.Id).HasColumnName("game_ref_id").IsRequired();
            gr.Property(p => p.Kind).HasColumnName("game_ref_kind")
              .HasConversion<short>().IsRequired();
        });

        builder.Property(e => e.OwnerUserId).HasColumnName("owner_user_id");
        builder.Property(e => e.DisplayName).HasColumnName("display_name")
               .HasMaxLength(120).IsRequired();
        builder.Property(e => e.Roles).HasColumnName("roles")
               .HasConversion<int>().IsRequired();
        builder.Property(e => e.ParagraphScheme).HasColumnName("paragraph_scheme")
               .HasConversion<short>().IsRequired();
        builder.Property(e => e.Language).HasColumnName("language")
               .HasMaxLength(2).IsRequired();
        builder.Property(e => e.SequentialRead).HasColumnName("sequential_read")
               .HasDefaultValue(false).IsRequired();
        builder.Property(e => e.KbSourceDocId).HasColumnName("kb_source_doc_id");
        builder.Property(e => e.PhysicalOnly).HasColumnName("physical_only")
               .HasDefaultValue(false).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted")
               .HasDefaultValue(false).IsRequired();
        builder.Property(e => e.RowVersion).HasColumnName("row_version")
               .IsRowVersion();

        builder.HasIndex("game_ref_kind", "game_ref_id", "deleted_at")
               .HasDatabaseName("ix_game_books_game_ref");
        builder.HasIndex(e => new { e.OwnerUserId, e.DeletedAt })
               .HasDatabaseName("ix_game_books_owner_user_id")
               .HasFilter("owner_user_id IS NOT NULL");

        // Unique kb_source for community books only
        builder.HasIndex(e => e.KbSourceDocId)
               .IsUnique()
               .HasDatabaseName("ux_game_books_kb_source_community")
               .HasFilter("kb_source_doc_id IS NOT NULL AND owner_user_id IS NULL AND deleted_at IS NULL");

        // Soft-delete query filter
        builder.HasQueryFilter(e => !e.IsDeleted);

        // CHECK constraint: physical_only=true ⇒ kb_source_doc_id IS NULL
        builder.ToTable(t => t.HasCheckConstraint(
            "chk_game_books_physical_kb_coherence",
            "(physical_only = true AND kb_source_doc_id IS NULL) OR (physical_only = false)"));
    }
}
```

- [ ] **Step 2: Add `DbSet<GameBook>` to DbContext**

Modify `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`:

```csharp
// Find the existing DbSet declarations for GameManagement BC and add:
public DbSet<GameBook> GameBooks => Set<GameBook>();
```

Add the `using Api.BoundedContexts.GameManagement.Domain.Entities;` at top of file if not present.

- [ ] **Step 3: Verify compilation**

```bash
cd apps/api/src/Api && dotnet build --no-restore 2>&1 | grep -E "error|fail" | head -5
```

Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/EntityConfigurations/GameManagement/GameBookEntityConfiguration.cs \
        apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
git commit -m "feat(gamebook): add GameBookEntityConfiguration + DbSet mapping"
```

### Task A5: Implement `GameBookRepository`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Repositories/GameBookRepository.cs`
- Modify: DI registration (typically `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/GameManagementServiceExtensions.cs` or equivalent)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Infrastructure/GameBookRepositoryTests.cs`

- [ ] **Step 1: Write the failing integration test (Testcontainers)**

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Infrastructure/GameBookRepositoryTests.cs
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Infrastructure;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure;

public class GameBookRepositoryTests : IntegrationTestBase
{
    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTrip()
    {
        await using var fixture = await CreateFixtureAsync();
        var repo = fixture.Services.GetRequiredService<IGameBookRepository>();
        var uow  = fixture.Services.GetRequiredService<IUnitOfWork>();

        var book = GameBook.CreateCommunity(
            GameRef.Shared(Guid.NewGuid()), "Press Start",
            GameBookRole.Tutorial | GameBookRole.Setup, ParagraphScheme.None,
            "en", false, null, true, fixture.AdminUserId);

        await repo.AddAsync(book, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        var loaded = await repo.GetByIdAsync(book.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("Press Start", loaded!.DisplayName);
        Assert.Equal(GameBookRole.Tutorial | GameBookRole.Setup, loaded.Roles);
    }

    [Fact]
    public async Task FindCommunityByKbSourceAsync_ReturnsBookWhenExists()
    {
        await using var fixture = await CreateFixtureAsync();
        var repo = fixture.Services.GetRequiredService<IGameBookRepository>();
        var uow  = fixture.Services.GetRequiredService<IUnitOfWork>();

        var pdfId = await fixture.SeedPdfDocumentAsync();
        var book = GameBook.CreateCommunity(
            GameRef.Shared(Guid.NewGuid()), "Rules",
            GameBookRole.RulesReference, ParagraphScheme.None,
            "en", false, pdfId, false, fixture.AdminUserId);

        await repo.AddAsync(book, CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);

        var found = await repo.FindCommunityByKbSourceAsync(pdfId, CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal(book.Id, found!.Id);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~GameBookRepositoryTests" -v minimal
```

Expected: COMPILE FAIL (`IGameBookRepository` impl missing or DI not registered).

- [ ] **Step 3: Implement `GameBookRepository`**

```csharp
// File: apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Repositories/GameBookRepository.cs
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Repositories;

internal class GameBookRepository : IGameBookRepository
{
    private readonly MeepleAiDbContext _db;

    public GameBookRepository(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<GameBook?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.GameBooks
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GameBook>> ListByGameRefAsync(
        GameRef gameRef, Guid? ownerUserId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(gameRef);
        var query = _db.GameBooks
            .Where(b => b.GameRef.Id == gameRef.Id && b.GameRef.Kind == gameRef.Kind);

        // Community books always visible. Personal books only to owner.
        if (ownerUserId.HasValue)
            query = query.Where(b => b.OwnerUserId == null || b.OwnerUserId == ownerUserId.Value);
        else
            query = query.Where(b => b.OwnerUserId == null);

        return await query.ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<GameBook?> FindCommunityByKbSourceAsync(Guid pdfDocId, CancellationToken ct)
    {
        return await _db.GameBooks
            .FirstOrDefaultAsync(b => b.KbSourceDocId == pdfDocId && b.OwnerUserId == null, ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(GameBook book, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(book);
        await _db.GameBooks.AddAsync(book, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(GameBook book, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(book);
        _db.GameBooks.Update(book);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Register repository in DI**

Modify the GameManagement DI extension (locate via `grep -r "AddScoped<IGameRepository" apps/api/src/Api/`):

```csharp
services.AddScoped<IGameBookRepository, GameBookRepository>();
```

- [ ] **Step 5: Run test to verify it passes (Testcontainers spins up Postgres)**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~GameBookRepositoryTests" -v minimal
```

Expected: 2 PASS. (May take 30-60s for Testcontainers cold start.)

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Repositories/GameBookRepository.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Infrastructure/GameBookRepositoryTests.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/GameManagementServiceExtensions.cs
git commit -m "feat(gamebook): add GameBookRepository implementation + DI registration"
```

### Task A6: Generate EF migration `AddGameBooks`

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddGameBooks.cs` (auto-generated)

- [ ] **Step 1: Generate migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddGameBooks
```

Expected: 2 new files in `Infrastructure/Migrations/` (Migration + Designer).

- [ ] **Step 2: Review generated SQL**

```bash
cat apps/api/src/Api/Infrastructure/Migrations/*_AddGameBooks.cs | head -80
```

Verify migration creates `game_books` table with all columns + indexes + check constraint. If anything missing, adjust `GameBookEntityConfiguration` and re-generate.

- [ ] **Step 3: Apply migration locally**

```bash
cd apps/api/src/Api
dotnet ef database update
```

Expected: "Done." message, no errors.

- [ ] **Step 4: Verify table created**

```bash
docker compose -f infra/docker-compose.yml exec postgres psql -U meepleai meepleai_dev -c "\d game_books"
```

Expected: table description with all columns listed.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/*_AddGameBooks.cs \
        apps/api/src/Api/Infrastructure/Migrations/*_AddGameBooks.Designer.cs
git commit -m "feat(gamebook): add EF migration for game_books table"
```

---

## Phase B: GameBook Application (Tasks B1-B7)

### Task B1: `CreateGameBookCommand` + Validator + Handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CreateGameBookCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CreateGameBookCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CreateGameBookCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/GameBookDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Mappers/GameBookMapper.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/CreateGameBookCommandHandlerTests.cs`

- [ ] **Step 1: Write the failing handler test**

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/CreateGameBookCommandHandlerTests.cs
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

public class CreateGameBookCommandHandlerTests
{
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Handle_ValidCommunityCommand_CreatesBookAndReturnsDto()
    {
        var repo = new Mock<IGameBookRepository>();
        var uow = new Mock<IUnitOfWork>();
        var handler = new CreateGameBookCommandHandler(repo.Object, uow.Object);

        var cmd = new CreateGameBookCommand(
            GameRef: GameRef.Shared(Guid.NewGuid()),
            OwnerUserId: null,
            DisplayName: "Press Start",
            Roles: (int)(GameBookRole.Tutorial | GameBookRole.Setup),
            ParagraphScheme: (int)ParagraphScheme.None,
            Language: "en",
            SequentialRead: false,
            KbSourceDocId: null,
            PhysicalOnly: true,
            RequestedBy: AdminId);

        var dto = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("Press Start", dto.DisplayName);
        repo.Verify(r => r.AddAsync(It.IsAny<GameBook>(), It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_KbSourceConflict_ThrowsConflictException()
    {
        var pdfId = Guid.NewGuid();
        var existing = GameBook.CreateCommunity(
            GameRef.Shared(Guid.NewGuid()), "Existing", GameBookRole.RulesReference,
            ParagraphScheme.None, "en", false, pdfId, false, AdminId);

        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.FindCommunityByKbSourceAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var uow = new Mock<IUnitOfWork>();
        var handler = new CreateGameBookCommandHandler(repo.Object, uow.Object);

        var cmd = new CreateGameBookCommand(
            GameRef.Shared(Guid.NewGuid()), null, "Conflicting",
            (int)GameBookRole.RulesReference, (int)ParagraphScheme.None, "en", false,
            pdfId, false, AdminId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(cmd, CancellationToken.None));
    }
}
```

- [ ] **Step 2: Run test to verify it fails (COMPILE FAIL)**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~CreateGameBookCommandHandlerTests" -v minimal
```

Expected: COMPILE FAIL on `CreateGameBookCommand` and handler types.

- [ ] **Step 3: Implement DTO + Mapper + Command + Validator + Handler**

```csharp
// File: apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/GameBookDto.cs
namespace Api.BoundedContexts.GameManagement.Application.DTOs;

public sealed record GameBookDto(
    Guid Id,
    Guid GameRefId,
    int GameRefKind,
    Guid? OwnerUserId,
    string DisplayName,
    int Roles,
    int ParagraphScheme,
    string Language,
    bool SequentialRead,
    Guid? KbSourceDocId,
    bool PhysicalOnly,
    DateTimeOffset CreatedAt);
```

```csharp
// File: apps/api/src/Api/BoundedContexts/GameManagement/Application/Mappers/GameBookMapper.cs
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Application.Mappers;

internal static class GameBookMapper
{
    public static GameBookDto ToDto(this GameBook book) => new(
        book.Id,
        book.GameRef.Id,
        (int)book.GameRef.Kind,
        book.OwnerUserId,
        book.DisplayName,
        (int)book.Roles,
        (int)book.ParagraphScheme,
        book.Language,
        book.SequentialRead,
        book.KbSourceDocId,
        book.PhysicalOnly,
        book.CreatedAt);
}
```

```csharp
// File: apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CreateGameBookCommand.cs
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal record CreateGameBookCommand(
    GameRef GameRef,
    Guid? OwnerUserId,
    string DisplayName,
    int Roles,
    int ParagraphScheme,
    string Language,
    bool SequentialRead,
    Guid? KbSourceDocId,
    bool PhysicalOnly,
    Guid RequestedBy
) : ICommand<GameBookDto>;
```

```csharp
// File: apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CreateGameBookCommandValidator.cs
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal class CreateGameBookCommandValidator : AbstractValidator<CreateGameBookCommand>
{
    public CreateGameBookCommandValidator()
    {
        RuleFor(x => x.GameRef).NotNull();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Roles).GreaterThan(0).WithMessage("at least one role required");
        RuleFor(x => x.Language).NotEmpty().Length(2);
        RuleFor(x => x.RequestedBy).NotEqual(Guid.Empty);
        RuleFor(x => x).Must(c => !(c.PhysicalOnly && c.KbSourceDocId.HasValue))
                       .WithMessage("physicalOnly=true is incompatible with kbSourceDocId");
    }
}
```

```csharp
// File: apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CreateGameBookCommandHandler.cs
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal class CreateGameBookCommandHandler : ICommandHandler<CreateGameBookCommand, GameBookDto>
{
    private readonly IGameBookRepository _repo;
    private readonly IUnitOfWork _uow;

    public CreateGameBookCommandHandler(IGameBookRepository repo, IUnitOfWork uow)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    public async Task<GameBookDto> Handle(CreateGameBookCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // Check kb_source conflict only for community books
        if (cmd.OwnerUserId is null && cmd.KbSourceDocId.HasValue)
        {
            var existing = await _repo.FindCommunityByKbSourceAsync(cmd.KbSourceDocId.Value, ct)
                .ConfigureAwait(false);
            if (existing is not null)
                throw new ConflictException("GameBook",
                    $"PDF {cmd.KbSourceDocId} already linked to community book {existing.Id}");
        }

        var book = cmd.OwnerUserId.HasValue
            ? GameBook.CreatePersonal(
                cmd.GameRef, cmd.OwnerUserId.Value, cmd.DisplayName,
                (GameBookRole)cmd.Roles, (ParagraphScheme)cmd.ParagraphScheme,
                cmd.Language, cmd.SequentialRead, cmd.KbSourceDocId, cmd.PhysicalOnly)
            : GameBook.CreateCommunity(
                cmd.GameRef, cmd.DisplayName,
                (GameBookRole)cmd.Roles, (ParagraphScheme)cmd.ParagraphScheme,
                cmd.Language, cmd.SequentialRead, cmd.KbSourceDocId, cmd.PhysicalOnly,
                cmd.RequestedBy);

        await _repo.AddAsync(book, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return book.ToDto();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~CreateGameBookCommandHandlerTests" -v minimal
```

Expected: 2 PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CreateGameBook*.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/GameBookDto.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Application/Mappers/GameBookMapper.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/CreateGameBookCommandHandlerTests.cs
git commit -m "feat(gamebook): add CreateGameBookCommand + handler + validator + DTO + mapper"
```

### Task B2: `UpdateGameBookCommand` + Validator + Handler

**Files:**
- Create: 3 files matching pattern in B1 (Command/Validator/Handler)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/UpdateGameBookCommandHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/UpdateGameBookCommandHandlerTests.cs
[Fact]
public async Task Handle_RenameAndUpdateRoles_PersistsChanges()
{
    var book = GameBook.CreateCommunity(
        GameRef.Shared(Guid.NewGuid()), "Old Name",
        GameBookRole.Tutorial, ParagraphScheme.None, "en", false, null, true, AdminId);

    var repo = new Mock<IGameBookRepository>();
    repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);

    var uow = new Mock<IUnitOfWork>();
    var handler = new UpdateGameBookCommandHandler(repo.Object, uow.Object);

    var cmd = new UpdateGameBookCommand(
        book.Id, "New Name",
        Roles: (int)(GameBookRole.Tutorial | GameBookRole.RulesReference),
        RequestedBy: AdminId);

    var dto = await handler.Handle(cmd, CancellationToken.None);

    Assert.Equal("New Name", dto.DisplayName);
    Assert.Equal((int)(GameBookRole.Tutorial | GameBookRole.RulesReference), dto.Roles);
    repo.Verify(r => r.UpdateAsync(book, It.IsAny<CancellationToken>()), Times.Once);
    uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task Handle_BookNotFound_ThrowsNotFoundException()
{
    var repo = new Mock<IGameBookRepository>();
    repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((GameBook?)null);

    var handler = new UpdateGameBookCommandHandler(repo.Object, new Mock<IUnitOfWork>().Object);
    var cmd = new UpdateGameBookCommand(Guid.NewGuid(), "X", (int)GameBookRole.Tutorial, AdminId);

    await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));
}
```

- [ ] **Step 2: Run test to verify it fails**

Expected: COMPILE FAIL.

- [ ] **Step 3: Implement UpdateGameBookCommand + Validator + Handler**

```csharp
// File: apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/UpdateGameBookCommand.cs
internal record UpdateGameBookCommand(
    Guid BookId, string DisplayName, int Roles, Guid RequestedBy
) : ICommand<GameBookDto>;

// File: apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/UpdateGameBookCommandValidator.cs
internal class UpdateGameBookCommandValidator : AbstractValidator<UpdateGameBookCommand>
{
    public UpdateGameBookCommandValidator()
    {
        RuleFor(x => x.BookId).NotEqual(Guid.Empty);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Roles).GreaterThan(0);
        RuleFor(x => x.RequestedBy).NotEqual(Guid.Empty);
    }
}

// File: apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/UpdateGameBookCommandHandler.cs
internal class UpdateGameBookCommandHandler : ICommandHandler<UpdateGameBookCommand, GameBookDto>
{
    private readonly IGameBookRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateGameBookCommandHandler(IGameBookRepository repo, IUnitOfWork uow)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    public async Task<GameBookDto> Handle(UpdateGameBookCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var book = await _repo.GetByIdAsync(cmd.BookId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException("GameBook", cmd.BookId.ToString());

        book.Rename(cmd.DisplayName, cmd.RequestedBy);
        book.UpdateRoles((GameBookRole)cmd.Roles, cmd.RequestedBy);

        await _repo.UpdateAsync(book, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return book.ToDto();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~UpdateGameBookCommandHandlerTests" -v minimal
```

Expected: 2 PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/UpdateGameBook*.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/UpdateGameBookCommandHandlerTests.cs
git commit -m "feat(gamebook): add UpdateGameBookCommand + handler + validator"
```

### Task B3: `AttachKbSourceCommand` + Validator + Handler

**Files:** matching pattern (Command/Validator/Handler/Test).

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public async Task Handle_AttachKbToCommunityBook_PersistsKbSource()
{
    var book = GameBook.CreateCommunity(
        GameRef.Shared(Guid.NewGuid()), "Rules",
        GameBookRole.RulesReference, ParagraphScheme.None, "en", false,
        kbSourceDocId: null, physicalOnly: false, AdminId);
    var pdfId = Guid.NewGuid();

    var repo = new Mock<IGameBookRepository>();
    repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);
    repo.Setup(r => r.FindCommunityByKbSourceAsync(pdfId, It.IsAny<CancellationToken>()))
        .ReturnsAsync((GameBook?)null);

    var uow = new Mock<IUnitOfWork>();
    var handler = new AttachKbSourceCommandHandler(repo.Object, uow.Object);
    var cmd = new AttachKbSourceCommand(book.Id, pdfId, AdminId);

    var dto = await handler.Handle(cmd, CancellationToken.None);

    Assert.Equal(pdfId, dto.KbSourceDocId);
}

[Fact]
public async Task Handle_AttachKbConflict_ThrowsConflictException()
{
    var book = GameBook.CreateCommunity(/* ... */);
    var pdfId = Guid.NewGuid();
    var existing = GameBook.CreateCommunity(/* ... */, kbSourceDocId: pdfId, /* ... */);

    var repo = new Mock<IGameBookRepository>();
    repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);
    repo.Setup(r => r.FindCommunityByKbSourceAsync(pdfId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(existing);

    var handler = new AttachKbSourceCommandHandler(repo.Object, new Mock<IUnitOfWork>().Object);
    var cmd = new AttachKbSourceCommand(book.Id, pdfId, AdminId);

    await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
}
```

- [ ] **Step 2: Run test → FAIL (COMPILE)**

- [ ] **Step 3: Implement**

```csharp
internal record AttachKbSourceCommand(Guid BookId, Guid PdfDocId, Guid RequestedBy)
    : ICommand<GameBookDto>;

internal class AttachKbSourceCommandValidator : AbstractValidator<AttachKbSourceCommand>
{
    public AttachKbSourceCommandValidator()
    {
        RuleFor(x => x.BookId).NotEqual(Guid.Empty);
        RuleFor(x => x.PdfDocId).NotEqual(Guid.Empty);
        RuleFor(x => x.RequestedBy).NotEqual(Guid.Empty);
    }
}

internal class AttachKbSourceCommandHandler : ICommandHandler<AttachKbSourceCommand, GameBookDto>
{
    private readonly IGameBookRepository _repo;
    private readonly IUnitOfWork _uow;

    public AttachKbSourceCommandHandler(IGameBookRepository repo, IUnitOfWork uow)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    public async Task<GameBookDto> Handle(AttachKbSourceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var book = await _repo.GetByIdAsync(cmd.BookId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException("GameBook", cmd.BookId.ToString());

        // Conflict check only for community books
        if (book.OwnerUserId is null)
        {
            var conflict = await _repo.FindCommunityByKbSourceAsync(cmd.PdfDocId, ct)
                .ConfigureAwait(false);
            if (conflict is not null && conflict.Id != book.Id)
                throw new ConflictException("GameBook",
                    $"PDF {cmd.PdfDocId} already kbSource of community book {conflict.Id}");
        }

        book.AttachKbSource(cmd.PdfDocId, cmd.RequestedBy);

        await _repo.UpdateAsync(book, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return book.ToDto();
    }
}
```

- [ ] **Step 4: Run test → PASS**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/AttachKbSource*.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/AttachKbSourceCommandHandlerTests.cs
git commit -m "feat(gamebook): add AttachKbSourceCommand + handler"
```

### Task B4: `DetachKbSourceCommand` (mirror of B3, simpler — no conflict check)

**Files:** matching pattern.

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public async Task Handle_DetachKbSource_ClearsKbSourceDocId()
{
    var pdfId = Guid.NewGuid();
    var book = GameBook.CreateCommunity(/* ... */, kbSourceDocId: pdfId, physicalOnly: false, AdminId);

    var repo = new Mock<IGameBookRepository>();
    repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);
    var uow = new Mock<IUnitOfWork>();
    var handler = new DetachKbSourceCommandHandler(repo.Object, uow.Object);

    var dto = await handler.Handle(new DetachKbSourceCommand(book.Id, AdminId), CancellationToken.None);

    Assert.Null(dto.KbSourceDocId);
}
```

- [ ] **Step 2: Run test → FAIL**

- [ ] **Step 3: Implement DetachKbSourceCommand + Validator + Handler**

```csharp
internal record DetachKbSourceCommand(Guid BookId, Guid RequestedBy) : ICommand<GameBookDto>;

internal class DetachKbSourceCommandValidator : AbstractValidator<DetachKbSourceCommand>
{
    public DetachKbSourceCommandValidator()
    {
        RuleFor(x => x.BookId).NotEqual(Guid.Empty);
        RuleFor(x => x.RequestedBy).NotEqual(Guid.Empty);
    }
}

internal class DetachKbSourceCommandHandler : ICommandHandler<DetachKbSourceCommand, GameBookDto>
{
    private readonly IGameBookRepository _repo;
    private readonly IUnitOfWork _uow;

    public DetachKbSourceCommandHandler(IGameBookRepository repo, IUnitOfWork uow)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    public async Task<GameBookDto> Handle(DetachKbSourceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var book = await _repo.GetByIdAsync(cmd.BookId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException("GameBook", cmd.BookId.ToString());

        book.DetachKbSource(cmd.RequestedBy);
        await _repo.UpdateAsync(book, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return book.ToDto();
    }
}
```

- [ ] **Step 4: Run test → PASS**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/DetachKbSource*.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/DetachKbSourceCommandHandlerTests.cs
git commit -m "feat(gamebook): add DetachKbSourceCommand + handler"
```

### Task B5: `SoftDeleteGameBookCommand`

**Files:** matching pattern.

- [ ] **Step 1-5: Mirror Task B4 but call `book.SoftDelete(cmd.RequestedBy)` instead.**

Implementation:

```csharp
internal record SoftDeleteGameBookCommand(Guid BookId, Guid RequestedBy) : ICommand<Unit>;

internal class SoftDeleteGameBookCommandHandler : ICommandHandler<SoftDeleteGameBookCommand, Unit>
{
    private readonly IGameBookRepository _repo;
    private readonly IUnitOfWork _uow;

    public SoftDeleteGameBookCommandHandler(IGameBookRepository repo, IUnitOfWork uow)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    public async Task<Unit> Handle(SoftDeleteGameBookCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var book = await _repo.GetByIdAsync(cmd.BookId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException("GameBook", cmd.BookId.ToString());

        book.SoftDelete(cmd.RequestedBy);
        await _repo.UpdateAsync(book, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return Unit.Value;
    }
}
```

Test:
```csharp
[Fact]
public async Task Handle_SoftDelete_MarksBookAsDeleted()
{
    var book = GameBook.CreateCommunity(/* ... */);
    var repo = new Mock<IGameBookRepository>();
    repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);
    var handler = new SoftDeleteGameBookCommandHandler(repo.Object, new Mock<IUnitOfWork>().Object);

    await handler.Handle(new SoftDeleteGameBookCommand(book.Id, AdminId), CancellationToken.None);

    Assert.True(book.IsDeleted);
}
```

Commit:
```bash
git commit -m "feat(gamebook): add SoftDeleteGameBookCommand + handler"
```

### Task B6: `GetGameBookByIdQuery` + Handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/GetGameBookByIdQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/GetGameBookByIdQueryHandler.cs`
- Test: matching file

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public async Task Handle_ExistingBook_ReturnsDto()
{
    var book = GameBook.CreateCommunity(/* ... */);
    var repo = new Mock<IGameBookRepository>();
    repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);

    var handler = new GetGameBookByIdQueryHandler(repo.Object);
    var dto = await handler.Handle(new GetGameBookByIdQuery(book.Id), CancellationToken.None);

    Assert.Equal(book.Id, dto.Id);
}

[Fact]
public async Task Handle_BookNotFound_ThrowsNotFoundException()
{
    var repo = new Mock<IGameBookRepository>();
    repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((GameBook?)null);

    var handler = new GetGameBookByIdQueryHandler(repo.Object);
    await Assert.ThrowsAsync<NotFoundException>(() =>
        handler.Handle(new GetGameBookByIdQuery(Guid.NewGuid()), CancellationToken.None));
}
```

- [ ] **Step 2: Run test → FAIL**

- [ ] **Step 3: Implement query + handler**

```csharp
internal record GetGameBookByIdQuery(Guid BookId) : IQuery<GameBookDto>;

internal class GetGameBookByIdQueryHandler : IQueryHandler<GetGameBookByIdQuery, GameBookDto>
{
    private readonly IGameBookRepository _repo;

    public GetGameBookByIdQueryHandler(IGameBookRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task<GameBookDto> Handle(GetGameBookByIdQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var book = await _repo.GetByIdAsync(query.BookId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException("GameBook", query.BookId.ToString());
        return book.ToDto();
    }
}
```

- [ ] **Step 4: Run test → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(gamebook): add GetGameBookByIdQuery + handler"
```

### Task B7: `ListGameBooksByGameQuery` + Handler

**Files:** matching pattern.

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public async Task Handle_ListByGameRef_ReturnsCommunityBooks()
{
    var gameRef = GameRef.Shared(Guid.NewGuid());
    var books = new[] {
        GameBook.CreateCommunity(gameRef, "Press Start", GameBookRole.Tutorial, /* ... */),
        GameBook.CreateCommunity(gameRef, "Rules", GameBookRole.RulesReference, /* ... */),
    };

    var repo = new Mock<IGameBookRepository>();
    repo.Setup(r => r.ListByGameRefAsync(gameRef, null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(books);

    var handler = new ListGameBooksByGameQueryHandler(repo.Object);
    var result = await handler.Handle(
        new ListGameBooksByGameQuery(gameRef, null), CancellationToken.None);

    Assert.Equal(2, result.Count);
}

[Fact]
public async Task Handle_ListIncludesPersonalForOwner()
{
    var gameRef = GameRef.Shared(Guid.NewGuid());
    var ownerId = Guid.NewGuid();
    var books = new[] {
        GameBook.CreateCommunity(gameRef, "Community", GameBookRole.Tutorial, /* ... */),
        GameBook.CreatePersonal(gameRef, ownerId, "Personal", GameBookRole.Narrative, /* ... */),
    };

    var repo = new Mock<IGameBookRepository>();
    repo.Setup(r => r.ListByGameRefAsync(gameRef, ownerId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(books);

    var handler = new ListGameBooksByGameQueryHandler(repo.Object);
    var result = await handler.Handle(
        new ListGameBooksByGameQuery(gameRef, ownerId), CancellationToken.None);

    Assert.Equal(2, result.Count);
    Assert.Contains(result, b => b.OwnerUserId == ownerId);
}
```

- [ ] **Step 2: Run test → FAIL**

- [ ] **Step 3: Implement query + handler**

```csharp
internal record ListGameBooksByGameQuery(GameRef GameRef, Guid? OwnerUserId)
    : IQuery<IReadOnlyList<GameBookDto>>;

internal class ListGameBooksByGameQueryHandler
    : IQueryHandler<ListGameBooksByGameQuery, IReadOnlyList<GameBookDto>>
{
    private readonly IGameBookRepository _repo;

    public ListGameBooksByGameQueryHandler(IGameBookRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task<IReadOnlyList<GameBookDto>> Handle(
        ListGameBooksByGameQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var books = await _repo.ListByGameRefAsync(query.GameRef, query.OwnerUserId, ct)
            .ConfigureAwait(false);
        return books.Select(b => b.ToDto()).ToList();
    }
}
```

- [ ] **Step 4: Run test → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(gamebook): add ListGameBooksByGameQuery + handler"
```

---

## Phase C: SessionTracking Refactor (Tasks C1-C6)

### Task C1: Create `SessionBookProgress` entity + repository + EF config + migration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/SessionBookProgress.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Repositories/ISessionBookProgressRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Repositories/SessionBookProgressRepository.cs`
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/SessionTracking/SessionBookProgressEntityConfiguration.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/SessionBookProgressTests.cs`

- [ ] **Step 1: Write failing test for entity**

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/SessionBookProgressTests.cs
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Xunit;

public class SessionBookProgressTests
{
    [Fact]
    public void Create_WithValidInputs_SetsLastLocationAndVisitedAt()
    {
        var sessionId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var progress = SessionBookProgress.Create(sessionId, bookId, "§289");

        Assert.Equal("§289", progress.LastLocation);
        Assert.NotEqual(default(DateTimeOffset), progress.LastVisitedAt);
        Assert.Empty(progress.HistoryJson);
    }

    [Fact]
    public void UpdateLocation_AppendsToHistory()
    {
        var progress = SessionBookProgress.Create(Guid.NewGuid(), Guid.NewGuid(), "§147");
        progress.UpdateLocation("§148");
        progress.UpdateLocation("§149");

        Assert.Equal("§149", progress.LastLocation);
        Assert.Equal(2, progress.HistoryJson.Count(c => c == ','));  // primitive check on JSON array
    }
}
```

- [ ] **Step 2: Run test → COMPILE FAIL**

- [ ] **Step 3: Implement entity**

```csharp
// File: apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/SessionBookProgress.cs
using System.Text.Json;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

public sealed class SessionBookProgress
{
    public Guid Id { get; private set; }
    public Guid CampaignSessionId { get; private set; }
    public Guid GameBookId { get; private set; }
    public string LastLocation { get; private set; } = default!;
    public string HistoryJson { get; private set; } = "[]";
    public DateTimeOffset LastVisitedAt { get; private set; }
    public string? NotesJson { get; private set; }

    private SessionBookProgress() { }

    public static SessionBookProgress Create(Guid campaignSessionId, Guid gameBookId, string initialLocation)
    {
        if (campaignSessionId == Guid.Empty) throw new ArgumentException("required", nameof(campaignSessionId));
        if (gameBookId == Guid.Empty) throw new ArgumentException("required", nameof(gameBookId));
        if (string.IsNullOrWhiteSpace(initialLocation)) throw new ArgumentException("required", nameof(initialLocation));

        return new SessionBookProgress
        {
            Id = Guid.NewGuid(),
            CampaignSessionId = campaignSessionId,
            GameBookId = gameBookId,
            LastLocation = initialLocation.Trim(),
            HistoryJson = JsonSerializer.Serialize(new[] { initialLocation.Trim() }),
            LastVisitedAt = DateTimeOffset.UtcNow,
        };
    }

    public void UpdateLocation(string newLocation)
    {
        if (string.IsNullOrWhiteSpace(newLocation))
            throw new ArgumentException("required", nameof(newLocation));

        var history = JsonSerializer.Deserialize<List<string>>(HistoryJson) ?? new List<string>();
        if (history.Count == 0 || history[^1] != newLocation)
            history.Add(newLocation);

        LastLocation = newLocation.Trim();
        HistoryJson = JsonSerializer.Serialize(history);
        LastVisitedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateNotes(string? notesJson)
    {
        NotesJson = notesJson;
        LastVisitedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 4: Run test → PASS**

- [ ] **Step 5: Implement repository interface + impl + EF config + add DbSet + generate migration**

```csharp
// Repository interface
public interface ISessionBookProgressRepository
{
    Task<SessionBookProgress?> GetByCampaignAndBookAsync(Guid campaignSessionId, Guid gameBookId, CancellationToken ct);
    Task<IReadOnlyList<SessionBookProgress>> ListByCampaignAsync(Guid campaignSessionId, CancellationToken ct);
    Task AddAsync(SessionBookProgress progress, CancellationToken ct);
    Task UpdateAsync(SessionBookProgress progress, CancellationToken ct);
}

// Repository impl: similar pattern to GameBookRepository

// Entity config:
internal class SessionBookProgressEntityConfiguration : IEntityTypeConfiguration<SessionBookProgress>
{
    public void Configure(EntityTypeBuilder<SessionBookProgress> builder)
    {
        builder.ToTable("gamebook_session_book_progress");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CampaignSessionId).HasColumnName("campaign_session_id").IsRequired();
        builder.Property(e => e.GameBookId).HasColumnName("game_book_id").IsRequired();
        builder.Property(e => e.LastLocation).HasColumnName("last_location").HasMaxLength(40).IsRequired();
        builder.Property(e => e.HistoryJson).HasColumnName("history_json").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.LastVisitedAt).HasColumnName("last_visited_at").IsRequired();
        builder.Property(e => e.NotesJson).HasColumnName("notes_json").HasColumnType("jsonb");

        builder.HasIndex(e => new { e.CampaignSessionId, e.GameBookId })
               .IsUnique()
               .HasDatabaseName("ux_session_book_progress_campaign_book");
    }
}
```

Then DbSet + DI + migration:
```bash
dotnet ef migrations add AddSessionBookProgress
dotnet ef database update
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/SessionBookProgress.cs \
        apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Repositories/ISessionBookProgressRepository.cs \
        apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Repositories/SessionBookProgressRepository.cs \
        apps/api/src/Api/Infrastructure/EntityConfigurations/SessionTracking/SessionBookProgressEntityConfiguration.cs \
        apps/api/src/Api/Infrastructure/Migrations/*_AddSessionBookProgress*.cs \
        apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/SessionBookProgressTests.cs
git commit -m "feat(gamebook): add SessionBookProgress entity + repository + EF migration"
```

### Task C2: Refactor `GamebookCampaignSession` (remove Progress VO)

**Files to modify:**
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/GamebookCampaignSession.cs`
- `apps/api/src/Api/Infrastructure/EntityConfigurations/SessionTracking/GamebookCampaignSessionEntityConfiguration.cs`

**Files to delete:**
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ValueObjects/GamebookProgress.cs`

- [ ] **Step 1: Identify all callers of `GamebookCampaignSession.UpdateProgress()` and `.Progress` getter**

```bash
grep -rn "\.UpdateProgress(\|\.Progress\b" apps/api/src/Api/BoundedContexts/SessionTracking/ \
                                            apps/api/src/Api/BoundedContexts/KnowledgeBase/ \
  | grep -v "\.Designer\." | head -20
```

Record the list — these will all need updating in Task C3+.

- [ ] **Step 2: Update entity — remove `Progress` field and `UpdateProgress()` method**

```csharp
// File: apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/GamebookCampaignSession.cs
public sealed class GamebookCampaignSession
{
    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }                     // pre-#1320 — see Task C5 for GameRef refactor
    public Guid OwnerUserId { get; private set; }
    public string Title { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private GamebookCampaignSession() { }

    public static GamebookCampaignSession Create(Guid gameId, Guid ownerUserId, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("title required", nameof(title));
        if (gameId == Guid.Empty)
            throw new ArgumentException("gameId required", nameof(gameId));
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("ownerUserId required", nameof(ownerUserId));

        var now = DateTimeOffset.UtcNow;
        return new GamebookCampaignSession
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            OwnerUserId = ownerUserId,
            Title = title.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = ownerUserId,
        };
    }

    // UpdateProgress() REMOVED. Progress is now per-book in SessionBookProgress entity.

    public void Rename(string newTitle, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            throw new ArgumentException("title required", nameof(newTitle));
        Title = newTitle.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void SoftDelete(Guid deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DeletedAt.Value;
        UpdatedBy = deletedBy;
    }
}
```

- [ ] **Step 3: Update EF config to remove `Progress` mapping**

Remove the `OwnsOne(e => e.Progress, ...)` block from `GamebookCampaignSessionEntityConfiguration.cs`.

- [ ] **Step 4: Generate migration to drop progress columns**

```bash
cd apps/api/src/Api
dotnet ef migrations add RemoveGamebookProgressVO
```

Review migration — it should DROP columns `progress_current_paragraph`, `progress_history`, `progress_last_read_at` from `gamebook_campaign_sessions` (or whatever the OwnsOne block named them).

- [ ] **Step 5: Update all callers of `.UpdateProgress()` and `.Progress`**

From the grep in Step 1, for each caller:
- If the call was `session.UpdateProgress(123)` → replace with `await sessionBookProgressRepo.UpsertAsync(sessionId, bookId, "§123", ct)` (you'll need to thread `bookId` through — see Task C3)
- If reading `.Progress.CurrentParagraph` → replace with `sessionBookProgressRepo.GetByCampaignAndBookAsync(sessionId, bookId).LastLocation`

**This step touches multiple files** (typically 3-5 handlers in SessionTracking + a few in KnowledgeBase). Update each, ensuring tests still compile.

- [ ] **Step 6: Run full backend test suite**

```bash
cd apps/api/src/Api && dotnet test --filter "Category=Unit" -v minimal 2>&1 | tail -10
```

Expected: all PASS. If any test fails, fix call sites identified in Step 1.

- [ ] **Step 7: Delete `GamebookProgress` VO**

```bash
rm apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ValueObjects/GamebookProgress.cs
```

Verify no remaining references:

```bash
grep -rn "GamebookProgress" apps/api/src/Api/ | head -5
```

Expected: empty.

- [ ] **Step 8: Commit**

```bash
git add -u  # stages deletions + modifications
git commit -m "refactor(gamebook): replace GamebookProgress VO with per-book SessionBookProgress entity"
```

### Task C3: Refactor `TranslatedParagraph` (add `GameBookId` parameter)

**Files to modify:**
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/TranslatedParagraph.cs`
- `apps/api/src/Api/Infrastructure/EntityConfigurations/SessionTracking/TranslatedParagraphEntityConfiguration.cs`
- All `TranslatedParagraph.Create()` call sites

- [ ] **Step 1: Identify all `TranslatedParagraph.Create()` callers**

```bash
grep -rn "TranslatedParagraph\.Create(" apps/api/src/Api/ | head -10
```

Record the list.

- [ ] **Step 2: Write failing test for new signature**

Add to `TranslatedParagraphTests.cs` (or create):

```csharp
[Fact]
public void Create_WithGameBookId_SetsField()
{
    var bookId = Guid.NewGuid();
    var tp = TranslatedParagraph.Create(
        campaignId: Guid.NewGuid(),
        gameBookId: bookId,
        photoArtifactId: Guid.NewGuid(),
        paragraphNumber: 147,
        sourceEn: "Source",
        translatedIt: "Tradotto",
        appliedTerms: new[] { "term1" },
        createdBy: Guid.NewGuid());

    Assert.Equal(bookId, tp.GameBookId);
}

[Fact]
public void Create_WithEmptyGameBookId_ThrowsArgumentException()
{
    Assert.Throws<ArgumentException>(() =>
        TranslatedParagraph.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), 1,
            "src", "trad", Array.Empty<string>(), Guid.NewGuid()));
}
```

- [ ] **Step 3: Run test → COMPILE FAIL**

- [ ] **Step 4: Refactor `TranslatedParagraph` entity**

```csharp
// Replace existing TranslatedParagraph.cs
public sealed class TranslatedParagraph
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid GameBookId { get; private set; }                 // NEW (replaces PageType)
    public Guid PhotoArtifactId { get; private set; }
    public int ParagraphNumber { get; private set; }
    public string SourceTextEn { get; private set; } = default!;
    public string TranslatedTextIt { get; private set; } = default!;
    public string[] AppliedGlossaryTerms { get; private set; } = Array.Empty<string>();
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    private TranslatedParagraph() { }

    public static TranslatedParagraph Create(
        Guid campaignId, Guid gameBookId, Guid photoArtifactId,
        int paragraphNumber, string sourceEn, string translatedIt,
        IEnumerable<string> appliedTerms, Guid createdBy)
    {
        if (campaignId == Guid.Empty) throw new ArgumentException("required", nameof(campaignId));
        if (gameBookId == Guid.Empty) throw new ArgumentException("required", nameof(gameBookId));
        if (photoArtifactId == Guid.Empty) throw new ArgumentException("required", nameof(photoArtifactId));
        if (paragraphNumber < 0) throw new ArgumentException("must be >= 0", nameof(paragraphNumber));
        if (string.IsNullOrWhiteSpace(sourceEn)) throw new ArgumentException("required", nameof(sourceEn));
        if (string.IsNullOrWhiteSpace(translatedIt)) throw new ArgumentException("required", nameof(translatedIt));
        if (createdBy == Guid.Empty) throw new ArgumentException("required", nameof(createdBy));

        return new TranslatedParagraph
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            GameBookId = gameBookId,
            PhotoArtifactId = photoArtifactId,
            ParagraphNumber = paragraphNumber,
            SourceTextEn = sourceEn.Trim(),
            TranslatedTextIt = translatedIt.Trim(),
            AppliedGlossaryTerms = appliedTerms?.ToArray() ?? Array.Empty<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
        };
    }
}
```

Update EF config:

```csharp
// Add to TranslatedParagraphEntityConfiguration.cs (replace any PageType mapping):
builder.Property(e => e.GameBookId).HasColumnName("game_book_id").IsRequired();
builder.HasIndex(e => new { e.CampaignSessionId, e.GameBookId, e.ParagraphNumber })
       .IsUnique()
       .HasDatabaseName("ux_translated_paragraphs_campaign_book_paragraph");
// Remove existing UNIQUE on (campaign_session_id, paragraph_number) — superseded
// Remove PageType column mapping
```

- [ ] **Step 5: Update all callers identified in Step 1**

For each caller (typically `TranslateParagraphCommandHandler` etc.):
- Add `gameBookId` parameter to command + validator + handler
- Pass it through to `TranslatedParagraph.Create()`
- Update test fixtures

This may touch 3-5 files. Be exhaustive.

- [ ] **Step 6: Generate migration**

```bash
dotnet ef migrations add AddGameBookIdToTranslatedParagraph
dotnet ef database update
```

- [ ] **Step 7: Run all SessionTracking tests**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~SessionTracking" -v minimal 2>&1 | tail -10
```

Expected: all PASS.

- [ ] **Step 8: Commit**

```bash
git add -u
git add apps/api/src/Api/Infrastructure/Migrations/*_AddGameBookIdToTranslatedParagraph*.cs
git commit -m "refactor(gamebook): add GameBookId FK to TranslatedParagraph (replaces PageType enum)"
```

### Task C4: Refactor `GamebookPhotoArtifact` (add `GameBookId`)

**Files to modify:**
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/GamebookPhotoArtifact.cs`
- EF config + all callers

- [ ] **Step 1-5: Mirror Task C3 pattern for `GamebookPhotoArtifact`**

- Add `GameBookId Guid { get; private set; }` field
- Update factory to require `gameBookId`
- Update EF config: `builder.Property(e => e.GameBookId).HasColumnName("game_book_id").IsRequired();`
- Update all callers to pass `gameBookId` (typically inferred from book selection in UI)
- Generate migration `AddGameBookIdToPhotoArtifact`
- Run tests
- Commit

```bash
git commit -m "refactor(gamebook): add GameBookId to GamebookPhotoArtifact"
```

### Task C5: Delete `GamebookPageType` enum + update all call sites

**Files to delete:**
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/GamebookPageType.cs`

- [ ] **Step 1: Identify all references**

```bash
grep -rn "GamebookPageType" apps/api/src/Api/ apps/api/tests/Api.Tests/ apps/web/ \
  | grep -v "\.Designer\." | grep -v "/bin/" | head -30
```

Expected: multiple references in handlers, DTOs, and frontend types.

- [ ] **Step 2: For each reference, replace with `GameBookId` lookup**

Patterns:
- DTO field `PageType: GamebookPageType` → `BookId: Guid` (consumer fetches `GameBook` for role classification)
- API endpoint param `pageType=storybook` → `bookId=<guid>`
- Frontend `pageType: 'storybook' | 'encounter'` enum → use book picker UI (Task E1)

- [ ] **Step 3: Delete enum file**

```bash
rm apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/GamebookPageType.cs
```

- [ ] **Step 4: Verify no references remain**

```bash
grep -rn "GamebookPageType" apps/api/src/Api/ apps/api/tests/Api.Tests/ | grep -v "/bin/"
```

Expected: empty.

- [ ] **Step 5: Run full test suite**

```bash
cd apps/api/src/Api && dotnet test -v minimal 2>&1 | tail -10
```

Expected: all PASS.

- [ ] **Step 6: Commit**

```bash
git add -u
git commit -m "refactor(gamebook): delete GamebookPageType enum + update all call sites to use GameBookId"
```

### Task C6: Add `FirstSeenBookId` to `GamebookGlossaryEntry`

**Files to modify:**
- `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/GamebookGlossaryEntry.cs`
- EF config + migration

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public void Create_WithFirstSeenBookId_SetsField()
{
    var bookId = Guid.NewGuid();
    var entry = GamebookGlossaryEntry.Create(
        campaignSessionId: Guid.NewGuid(),
        termEn: "Voidstone", termIt: "Pietra del Vuoto",
        firstSeenParagraph: 147, firstSeenBookId: bookId);

    Assert.Equal(bookId, entry.FirstSeenBookId);
}
```

- [ ] **Step 2: Run test → FAIL**

- [ ] **Step 3: Add field + factory parameter + EF mapping**

```csharp
// In GamebookGlossaryEntry:
public Guid? FirstSeenBookId { get; private set; }

public static GamebookGlossaryEntry Create(
    Guid campaignSessionId, string termEn, string termIt,
    int? firstSeenParagraph, Guid? firstSeenBookId)
{
    // existing validation + new field
}
```

EF config:
```csharp
builder.Property(e => e.FirstSeenBookId).HasColumnName("first_seen_book_id");
```

- [ ] **Step 4: Migration**

```bash
dotnet ef migrations add AddFirstSeenBookIdToGlossary
dotnet ef database update
```

- [ ] **Step 5: Run test → PASS**

- [ ] **Step 6: Commit**

```bash
git commit -m "refactor(gamebook): add nullable FirstSeenBookId to glossary entry"
```

---

## Phase D: RAG Role-aware (Tasks D1-D7)

### Task D1: Add `role_tags` column to `text_chunks`

**Files to modify:**
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/TextChunkEntity.cs` (or equivalent)
- `apps/api/src/Api/Infrastructure/EntityConfigurations/KnowledgeBase/TextChunkEntityEntityConfiguration.cs`

- [ ] **Step 1: Add `RoleTags` property to entity**

```csharp
// In TextChunkEntity:
public GameBookRole RoleTags { get; private set; } = GameBookRole.None;

public void AssignRoleTags(GameBookRole tags)
{
    RoleTags = tags;
}
```

- [ ] **Step 2: Update EF config**

```csharp
// In TextChunkEntityEntityConfiguration:
builder.Property(e => e.RoleTags)
       .HasColumnName("role_tags")
       .HasConversion<int>()
       .HasDefaultValue(GameBookRole.None)
       .IsRequired();
builder.HasIndex(e => e.RoleTags)
       .HasDatabaseName("ix_text_chunks_role_tags")
       .HasFilter("role_tags != 0");
```

- [ ] **Step 3: Generate migration**

```bash
dotnet ef migrations add AddRoleTagsToTextChunkEntitys
dotnet ef database update
```

- [ ] **Step 4: Verify column added**

```bash
docker compose -f infra/docker-compose.yml exec postgres psql -U meepleai meepleai_dev \
  -c "SELECT column_name, data_type FROM information_schema.columns WHERE table_name='text_chunks' AND column_name='role_tags';"
```

Expected: 1 row with `role_tags integer`.

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(rag): add role_tags column to text_chunks for role-aware retrieval"
```

### Task D2: Implement `RoleClassifierService` (rule-based)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRoleClassifierService.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RoleClassifierService.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/RoleClassifierServiceTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
public class RoleClassifierServiceTests
{
    private readonly RoleClassifierService _classifier = new RoleClassifierService(
        llmClient: new Mock<ILlmClient>().Object,
        logger: NullLogger<RoleClassifierService>.Instance);

    [Fact]
    public async Task Classify_ChunkWithSetupHeading_AssignsTutorialSetupTags()
    {
        var chunk = new ChunkInput(
            HeadingPath: "Setup > Number of Players",
            BodyText: "For 4 players, lay out the board...");

        var result = await _classifier.ClassifyAsync(new[] { chunk }, CancellationToken.None);

        Assert.True(result[0].HasFlag(GameBookRole.Tutorial));
        Assert.True(result[0].HasFlag(GameBookRole.Setup));
    }

    [Fact]
    public async Task Classify_ChunkWithRulesHeading_AssignsRulesReferenceTag()
    {
        var chunk = new ChunkInput(
            HeadingPath: "Combat > Magic Combat",
            BodyText: "The wizard rolls d6 for fire damage...");

        var result = await _classifier.ClassifyAsync(new[] { chunk }, CancellationToken.None);

        Assert.True(result[0].HasFlag(GameBookRole.RulesReference));
    }

    [Fact]
    public async Task Classify_ChunkWithParagraphNumber_AssignsNarrativeTag()
    {
        var chunk = new ChunkInput(
            HeadingPath: "Adventures > Paragraph 147",
            BodyText: "You enter the dark cave...");

        var result = await _classifier.ClassifyAsync(new[] { chunk }, CancellationToken.None);

        Assert.True(result[0].HasFlag(GameBookRole.Narrative));
    }
}
```

- [ ] **Step 2: Run test → COMPILE FAIL**

- [ ] **Step 3: Implement interface + rule-based classifier**

```csharp
// File: apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRoleClassifierService.cs
public interface IRoleClassifierService
{
    Task<IReadOnlyList<GameBookRole>> ClassifyAsync(
        IReadOnlyList<ChunkInput> chunks, CancellationToken ct);
}

public sealed record ChunkInput(string HeadingPath, string BodyText);

// File: apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RoleClassifierService.cs
internal class RoleClassifierService : IRoleClassifierService
{
    private readonly ILlmClient _llm;
    private readonly ILogger<RoleClassifierService> _logger;

    private static readonly (Regex Pattern, GameBookRole Role)[] HeadingRules = new[]
    {
        (new Regex(@"\b(setup|quick\s*start|learn\s*to\s*play|getting\s*started|components|preparazione)\b", RegexOptions.IgnoreCase), GameBookRole.Tutorial | GameBookRole.Setup),
        (new Regex(@"\b(rules?|reference|combat|magic|phases?|actions?|turn|regole|combattimento)\b", RegexOptions.IgnoreCase), GameBookRole.RulesReference),
        (new Regex(@"\b(adventures?|chapters?|paragrap?h\s*\d+|§\s*\d+|avventur)", RegexOptions.IgnoreCase), GameBookRole.Narrative),
        (new Regex(@"\b(encounters?|scenarios?|combat\s*sheet|incontro|scontri)\b", RegexOptions.IgnoreCase), GameBookRole.Encounter),
        (new Regex(@"\b(lore|background|history|codex|storia)\b", RegexOptions.IgnoreCase), GameBookRole.Lore),
    };

    public RoleClassifierService(ILlmClient llm, ILogger<RoleClassifierService> logger)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<GameBookRole>> ClassifyAsync(
        IReadOnlyList<ChunkInput> chunks, CancellationToken ct)
    {
        var results = new GameBookRole[chunks.Count];
        var ambiguous = new List<(int Index, ChunkInput Chunk)>();

        for (var i = 0; i < chunks.Count; i++)
        {
            var role = ApplyRules(chunks[i].HeadingPath);
            if (role != GameBookRole.None)
                results[i] = role;
            else
                ambiguous.Add((i, chunks[i]));
        }

        // LLM fallback for ambiguous (Task D3 implements this)
        if (ambiguous.Count > 0)
        {
            var llmResults = await ClassifyViaLlmAsync(
                ambiguous.Select(a => a.Chunk).ToList(), ct).ConfigureAwait(false);
            for (var i = 0; i < ambiguous.Count; i++)
                results[ambiguous[i].Index] = llmResults[i];
        }

        return results;
    }

    private static GameBookRole ApplyRules(string headingPath)
    {
        if (string.IsNullOrWhiteSpace(headingPath)) return GameBookRole.None;
        var result = GameBookRole.None;
        foreach (var (pattern, role) in HeadingRules)
        {
            if (pattern.IsMatch(headingPath)) result |= role;
        }
        return result;
    }

    private Task<IReadOnlyList<GameBookRole>> ClassifyViaLlmAsync(
        IReadOnlyList<ChunkInput> chunks, CancellationToken ct)
    {
        // Stub for Task D3 — return empty for now
        return Task.FromResult<IReadOnlyList<GameBookRole>>(
            chunks.Select(_ => GameBookRole.RulesReference).ToList());
    }
}
```

- [ ] **Step 4: Register in DI** + Run test → PASS

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~RoleClassifierServiceTests" -v minimal
```

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(rag): add RoleClassifierService with rule-based heading detection"
```

### Task D3: `RoleClassifierService` LLM fallback

**Files to modify:**
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RoleClassifierService.cs`
- Test file extended

- [ ] **Step 1: Write failing test for LLM fallback**

```csharp
[Fact]
public async Task ClassifyAsync_AmbiguousChunk_FallsBackToLlm()
{
    var llm = new Mock<ILlmClient>();
    llm.Setup(l => l.CompleteAsync(
            It.IsAny<string>(), It.IsAny<LlmRequestOptions>(), It.IsAny<CancellationToken>()))
       .ReturnsAsync(new LlmResponse(
           Content: "[\"tutorial\",\"setup\"]",
           Provider: "deepseek", PromptTokens: 50, CompletionTokens: 10));

    var classifier = new RoleClassifierService(llm.Object, NullLogger<RoleClassifierService>.Instance);
    var chunk = new ChunkInput(HeadingPath: "Random Notes", BodyText: "To start the game, ...");

    var result = await classifier.ClassifyAsync(new[] { chunk }, CancellationToken.None);

    Assert.True(result[0].HasFlag(GameBookRole.Tutorial));
    Assert.True(result[0].HasFlag(GameBookRole.Setup));
    llm.Verify(l => l.CompleteAsync(It.IsAny<string>(), It.IsAny<LlmRequestOptions>(),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

- [ ] **Step 2: Run test → likely FAIL (LLM fallback stub returns hardcoded RulesReference)**

- [ ] **Step 3: Implement real LLM fallback**

```csharp
private async Task<IReadOnlyList<GameBookRole>> ClassifyViaLlmAsync(
    IReadOnlyList<ChunkInput> chunks, CancellationToken ct)
{
    if (chunks.Count == 0) return Array.Empty<GameBookRole>();

    var prompt = BuildClassificationPrompt(chunks);
    var response = await _llm.CompleteAsync(prompt,
        new LlmRequestOptions(MaxTokens: 200, Temperature: 0.0), ct)
        .ConfigureAwait(false);

    return ParseLlmResponse(response.Content, chunks.Count);
}

private static string BuildClassificationPrompt(IReadOnlyList<ChunkInput> chunks)
{
    var sb = new StringBuilder();
    sb.AppendLine("Classify each board game manual chunk by role. Valid labels:");
    sb.AppendLine("tutorial, rulesreference, narrative, encounter, lore, setup");
    sb.AppendLine("Multi-label allowed. Output JSON array of arrays.");
    sb.AppendLine();
    for (var i = 0; i < chunks.Count; i++)
    {
        sb.AppendLine($"Chunk {i}: heading='{chunks[i].HeadingPath}' body='{chunks[i].BodyText[..Math.Min(200, chunks[i].BodyText.Length)]}'");
    }
    return sb.ToString();
}

private static IReadOnlyList<GameBookRole> ParseLlmResponse(string content, int expectedCount)
{
    try
    {
        var parsed = JsonSerializer.Deserialize<string[][]>(content) ?? Array.Empty<string[]>();
        var result = new GameBookRole[expectedCount];
        for (var i = 0; i < expectedCount; i++)
        {
            if (i >= parsed.Length) { result[i] = GameBookRole.RulesReference; continue; }
            var roles = GameBookRole.None;
            foreach (var label in parsed[i])
                roles |= ParseLabel(label);
            result[i] = roles == GameBookRole.None ? GameBookRole.RulesReference : roles;
        }
        return result;
    }
    catch
    {
        // Fail-soft: default to RulesReference if LLM output unparseable
        return Enumerable.Repeat(GameBookRole.RulesReference, expectedCount).ToList();
    }
}

private static GameBookRole ParseLabel(string label) => label.ToLowerInvariant() switch
{
    "tutorial" => GameBookRole.Tutorial,
    "rulesreference" or "rules" or "reference" => GameBookRole.RulesReference,
    "narrative" => GameBookRole.Narrative,
    "encounter" => GameBookRole.Encounter,
    "lore" => GameBookRole.Lore,
    "setup" => GameBookRole.Setup,
    _ => GameBookRole.None
};
```

- [ ] **Step 4: Run test → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(rag): add LLM fallback (DeepSeek default) for RoleClassifierService"
```

### Task D4: Integrate `RoleClassifierService` into ingestion pipeline

**Files to modify:**
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Pipeline/PdfIngestionPipeline.cs`

- [ ] **Step 1: Identify where chunks are persisted post-extraction**

```bash
grep -rn "TextChunkEntity" apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Pipeline/ | head -10
```

- [ ] **Step 2: Inject `IRoleClassifierService` into pipeline + invoke classification**

In the pipeline's chunk processing step:

```csharp
// After chunks are extracted (smoldocling output) and BEFORE persistence:
var chunkInputs = chunks.Select(c => new ChunkInput(
    HeadingPath: c.HeadingPath ?? string.Empty,
    BodyText: c.Text)).ToList();
var roleTags = await _roleClassifier.ClassifyAsync(chunkInputs, ct).ConfigureAwait(false);

for (var i = 0; i < chunks.Count; i++)
    chunks[i].AssignRoleTags(roleTags[i]);

// Now persist as before
```

- [ ] **Step 3: Register `IRoleClassifierService` in DI**

```csharp
services.AddScoped<IRoleClassifierService, RoleClassifierService>();
```

- [ ] **Step 4: Add integration test seeding 2 chunks through pipeline**

```csharp
[Fact]
public async Task IngestionPipeline_ClassifiesChunkRolesEndToEnd()
{
    // Setup pipeline with mock smoldocling returning 2 chunks
    // Run pipeline
    // Verify text_chunks rows have role_tags != 0
}
```

- [ ] **Step 5: Run integration test → PASS**

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(rag): integrate RoleClassifierService into PDF ingestion pipeline"
```

### Task D5: Implement `IntentClassifierService` (regex-based)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IIntentClassifierService.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IntentClassifierService.cs`
- Test: matching file

- [ ] **Step 1: Write failing test**

```csharp
public class IntentClassifierServiceTests
{
    private readonly IntentClassifierService _svc = new();

    [Theory]
    [InlineData("come si setupa per 4 giocatori", GameBookRole.Tutorial | GameBookRole.Setup)]
    [InlineData("setup 4 players", GameBookRole.Tutorial | GameBookRole.Setup)]
    [InlineData("qual è la regola del fuoco", GameBookRole.RulesReference)]
    [InlineData("posso fare l'attacco doppio?", GameBookRole.RulesReference)]
    [InlineData("traduci paragrafo 147", GameBookRole.Narrative)]
    [InlineData("§289 cosa dice", GameBookRole.Narrative)]
    [InlineData("incontro con il drago", GameBookRole.Encounter)]
    [InlineData("totally random gibberish abcxyz", GameBookRole.RulesReference)]  // default fallback
    public void ClassifyIntent_ReturnsExpectedRole(string query, GameBookRole expected)
    {
        var result = _svc.ClassifyIntent(query);
        Assert.Equal(expected, result);
    }
}
```

- [ ] **Step 2: Run test → COMPILE FAIL**

- [ ] **Step 3: Implement service**

```csharp
public interface IIntentClassifierService
{
    GameBookRole ClassifyIntent(string query);
}

internal class IntentClassifierService : IIntentClassifierService
{
    private static readonly (Regex Pattern, GameBookRole Role)[] IntentRules = new[]
    {
        (new Regex(@"\b(setup|set\s*up|setupa|preparare|come\s+si\s+(comincia|inizia)|prima\s+partita|primo\s+turno)\b", RegexOptions.IgnoreCase), GameBookRole.Tutorial | GameBookRole.Setup),
        (new Regex(@"§\s*\d+|\bparagrap?h\s*\d+|\bparagrafo\s*\d+|\btraduci\b|\bnext\s*paragraph\b", RegexOptions.IgnoreCase), GameBookRole.Narrative),
        (new Regex(@"\b(incontro|scontro|combatti(mento)?|encounter)\b", RegexOptions.IgnoreCase), GameBookRole.Encounter),
        (new Regex(@"\b(qual\s+(è|e)\s+la\s+regola|come\s+funziona|posso\s+fare|when\s+can\s+i|rule\s+about|regola\s+(del|della))\b", RegexOptions.IgnoreCase), GameBookRole.RulesReference),
    };

    public GameBookRole ClassifyIntent(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return GameBookRole.RulesReference;

        var result = GameBookRole.None;
        foreach (var (pattern, role) in IntentRules)
        {
            if (pattern.IsMatch(query)) result |= role;
        }

        return result == GameBookRole.None ? GameBookRole.RulesReference : result;
    }
}
```

- [ ] **Step 4: Run test → PASS (8 cases)**

- [ ] **Step 5: Register in DI + Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IIntentClassifierService.cs \
        apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IntentClassifierService.cs \
        apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/IntentClassifierServiceTests.cs
git commit -m "feat(rag): add IntentClassifierService with regex patterns (IT + EN)"
```

### Task D6: `HybridSearchService` re-ranker `role_match` signal

**Files to modify:**
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/HybridSearchService.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/SearchQuery.cs`

- [ ] **Step 1: Identify re-ranker location in `HybridSearchService`**

```bash
grep -n "score\|rank\|Boost" apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/HybridSearchService.cs | head -10
```

- [ ] **Step 2: Add `QueryRoleHint` to `SearchQuery` record**

```csharp
// SearchQuery.cs — add field
public sealed record SearchQuery(
    string Text,
    Guid GameId,
    /* existing fields */,
    GameBookRole QueryRoleHint = GameBookRole.None  // NEW
);
```

- [ ] **Step 3: Write failing integration test**

```csharp
[Fact]
public async Task HybridSearch_WithRoleHint_BoostsMatchingChunks()
{
    // Seed 2 chunks: one with role_tags=Tutorial, one with role_tags=RulesReference
    // Run search with QueryRoleHint=Tutorial
    // Expect chunk with Tutorial tag ranked higher
}
```

- [ ] **Step 4: Run test → FAIL (re-ranker doesn't yet boost on role match)**

- [ ] **Step 5: Modify re-ranker to add `role_match` signal**

```csharp
private double ComputeChunkScore(TextChunkEntity chunk, SearchQuery query)
{
    var baseScore = ComputeSemanticScore(chunk, query)
                  + ComputeBm25Score(chunk, query)
                  + ComputeRecencyScore(chunk);

    // NEW: role match boost
    if (query.QueryRoleHint != GameBookRole.None
        && (chunk.RoleTags & query.QueryRoleHint) != GameBookRole.None)
    {
        baseScore += 0.15;
    }

    return baseScore;
}
```

- [ ] **Step 6: Run test → PASS**

- [ ] **Step 7: Commit**

```bash
git commit -m "feat(rag): add role_match signal to HybridSearchService re-ranker"
```

### Task D7: Wire `QueryRoleHint` through chat query flow

**Files to modify:**
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQueryHandler.cs` (or equivalent)

- [ ] **Step 1: Inject `IIntentClassifierService` into chat query handler**

```csharp
public AskQuestionQueryHandler(
    /* existing deps */,
    IIntentClassifierService intentClassifier)
{
    _intentClassifier = intentClassifier;
}
```

- [ ] **Step 2: Compute role hint and pass to `SearchQuery`**

```csharp
public async Task<AskAnswerDto> Handle(AskQuestionQuery query, CancellationToken ct)
{
    var roleHint = _intentClassifier.ClassifyIntent(query.UserMessage);
    var searchQuery = new SearchQuery(
        Text: query.UserMessage,
        GameId: query.GameId,
        /* existing fields */,
        QueryRoleHint: roleHint);
    // proceed with existing pipeline
}
```

- [ ] **Step 3: Write integration test verifying role hint reaches search**

```csharp
[Fact]
public async Task AskQuestion_WithSetupQuery_RoutesWithTutorialHint()
{
    // Setup seed: 2 chunks (Tutorial-tagged + RulesReference-tagged)
    // Ask "come si setupa per 4 giocatori"
    // Verify response is sourced from Tutorial chunk
}
```

- [ ] **Step 4: Run test → PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(rag): wire QueryRoleHint through chat query flow via IntentClassifierService"
```

---

## Phase E: Frontend Minimal Refactor (Tasks E1-E3)

### Task E1: Create `BookPicker` component

**Files:**
- Create: `apps/web/src/components/gamebook/BookPicker.tsx`
- Create: `apps/web/src/hooks/useGameBooks.ts`
- Modify: `apps/web/src/lib/api/gamebook.ts` (add `listGameBooks(gameRef)` function)
- Test: `apps/web/__tests__/components/gamebook/BookPicker.test.tsx`

- [ ] **Step 1: Write failing Vitest test**

```typescript
// File: apps/web/__tests__/components/gamebook/BookPicker.test.tsx
import { render, screen } from '@testing-library/react';
import { BookPicker } from '@/components/gamebook/BookPicker';

describe('BookPicker', () => {
  const narrativeBooks = [
    { id: 'b1', displayName: 'Storybook', roles: 4 /* Narrative */ },
    { id: 'b2', displayName: 'Encounter Book', roles: 8 /* Encounter */ },
  ];

  it('renders nothing when only 1 narrative book', () => {
    const { container } = render(
      <BookPicker books={[narrativeBooks[0]]} value="b1" onChange={() => {}} />
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('renders book options when 2+ narrative books', () => {
    render(
      <BookPicker books={narrativeBooks} value="b1" onChange={() => {}} />
    );
    expect(screen.getByText('Storybook')).toBeInTheDocument();
    expect(screen.getByText('Encounter Book')).toBeInTheDocument();
  });

  it('calls onChange when user selects a book', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(
      <BookPicker books={narrativeBooks} value="b1" onChange={onChange} />
    );
    await user.click(screen.getByText('Encounter Book'));
    expect(onChange).toHaveBeenCalledWith('b2');
  });
});
```

- [ ] **Step 2: Run test → FAIL**

- [ ] **Step 3: Implement component**

```typescript
// File: apps/web/src/components/gamebook/BookPicker.tsx
import { type FC } from 'react';

export interface BookPickerOption {
  id: string;
  displayName: string;
  roles: number;
}

export interface BookPickerProps {
  books: BookPickerOption[];
  value: string;
  onChange: (bookId: string) => void;
}

export const BookPicker: FC<BookPickerProps> = ({ books, value, onChange }) => {
  if (books.length <= 1) return null;

  return (
    <div role="radiogroup" aria-label="Seleziona libro" className="flex gap-2">
      {books.map((book) => (
        <button
          key={book.id}
          role="radio"
          aria-checked={value === book.id}
          onClick={() => onChange(book.id)}
          className={value === book.id ? 'bg-primary text-primary-foreground' : 'bg-muted'}
        >
          {book.displayName}
        </button>
      ))}
    </div>
  );
};
```

- [ ] **Step 4: Run test → PASS**

- [ ] **Step 5: Create `useGameBooks` hook + API client**

```typescript
// File: apps/web/src/lib/api/gamebook.ts (extend existing)
export async function listGameBooks(gameRef: GameRef): Promise<GameBookDto[]> {
  const params = new URLSearchParams({
    gameRefId: gameRef.id,
    gameRefKind: String(gameRef.kind),
  });
  const res = await fetch(`/api/v1/gamebook/books?${params}`);
  if (!res.ok) throw new Error(`Failed to list game books: ${res.statusText}`);
  return res.json();
}
```

```typescript
// File: apps/web/src/hooks/useGameBooks.ts
import { useQuery } from '@tanstack/react-query';
import { listGameBooks, type GameRef } from '@/lib/api/gamebook';

export function useGameBooks(gameRef: GameRef | null) {
  return useQuery({
    queryKey: ['gameBooks', gameRef?.id, gameRef?.kind],
    queryFn: () => listGameBooks(gameRef!),
    enabled: gameRef !== null,
  });
}
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/gamebook/BookPicker.tsx \
        apps/web/src/hooks/useGameBooks.ts \
        apps/web/src/lib/api/gamebook.ts \
        apps/web/__tests__/components/gamebook/BookPicker.test.tsx
git commit -m "feat(gamebook): add BookPicker component + useGameBooks hook"
```

### Task E2: `ResumeBooksList` component for per-book progress

**Files:**
- Create: `apps/web/src/components/gamebook/ResumeBooksList.tsx`
- Test: `apps/web/__tests__/components/gamebook/ResumeBooksList.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
describe('ResumeBooksList', () => {
  it('renders a row per book with last location', () => {
    const progress = [
      { bookId: 'b1', bookName: 'Storybook', lastLocation: '§289', lastVisitedAt: '2026-05-19T10:00:00Z' },
      { bookId: 'b2', bookName: 'Encounter Book', lastLocation: 'E7', lastVisitedAt: '2026-05-19T11:00:00Z' },
    ];
    render(<ResumeBooksList progress={progress} onResume={() => {}} />);
    expect(screen.getByText('Storybook')).toBeInTheDocument();
    expect(screen.getByText('§289')).toBeInTheDocument();
    expect(screen.getByText('Encounter Book')).toBeInTheDocument();
    expect(screen.getByText('E7')).toBeInTheDocument();
  });

  it('renders empty state when no progress', () => {
    render(<ResumeBooksList progress={[]} onResume={() => {}} />);
    expect(screen.getByText(/nessun libro/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test → FAIL**

- [ ] **Step 3: Implement**

```typescript
// File: apps/web/src/components/gamebook/ResumeBooksList.tsx
export interface BookProgress {
  bookId: string;
  bookName: string;
  lastLocation: string;
  lastVisitedAt: string;
}

export interface ResumeBooksListProps {
  progress: BookProgress[];
  onResume: (bookId: string) => void;
}

export const ResumeBooksList: FC<ResumeBooksListProps> = ({ progress, onResume }) => {
  if (progress.length === 0) {
    return <p className="text-muted-foreground">Nessun libro in corso.</p>;
  }
  return (
    <ul className="flex flex-col gap-2">
      {progress.map((p) => (
        <li key={p.bookId} className="flex items-center justify-between p-3 rounded-md bg-card">
          <div>
            <p className="font-medium">{p.bookName}</p>
            <p className="text-sm text-muted-foreground">{p.lastLocation}</p>
          </div>
          <button onClick={() => onResume(p.bookId)} className="btn btn-primary">
            Riprendi
          </button>
        </li>
      ))}
    </ul>
  );
};
```

- [ ] **Step 4: Run test → PASS**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/gamebook/ResumeBooksList.tsx \
        apps/web/__tests__/components/gamebook/ResumeBooksList.test.tsx
git commit -m "feat(gamebook): add ResumeBooksList component for per-book progress display"
```

### Task E3: Wire `BookPicker` + `ResumeBooksList` into existing pages

**Files to modify:**
- `apps/web/src/components/gamebook/PhotoTranslateForm.tsx` (or equivalent photo upload form)
- `apps/web/src/app/library/games/[gameId]/play/page.tsx` (resume entry point)

- [ ] **Step 1: Locate existing photo-translate form**

```bash
find apps/web/src -name "*PhotoTranslate*" -o -name "*GamebookForm*" 2>&1 | head -5
```

- [ ] **Step 2: Add `BookPicker` conditional in photo-translate form**

```typescript
// Inside the existing form component:
const { data: books } = useGameBooks(gameRef);
const narrativeBooks = books?.filter(b => (b.roles & 4 /* Narrative */) !== 0) ?? [];
const [selectedBookId, setSelectedBookId] = useState<string | undefined>(
  narrativeBooks.length === 1 ? narrativeBooks[0].id : undefined
);

// In JSX:
{narrativeBooks.length > 1 && (
  <BookPicker
    books={narrativeBooks}
    value={selectedBookId ?? ''}
    onChange={setSelectedBookId}
  />
)}
{narrativeBooks.length === 0 && (
  <p className="text-destructive">Questo gioco non ha libri narrativi disponibili per photo-translate.</p>
)}

// Submit logic: include selectedBookId in payload
```

- [ ] **Step 3: Add `ResumeBooksList` to play page**

```typescript
// In play/page.tsx:
const { data: progress } = useSessionBookProgress(sessionId);
return (
  <>
    <ResumeBooksList
      progress={progress?.map(p => ({
        bookId: p.gameBookId,
        bookName: p.bookName,
        lastLocation: p.lastLocation,
        lastVisitedAt: p.lastVisitedAt,
      })) ?? []}
      onResume={(bookId) => router.push(`/library/games/${gameId}/play/${bookId}`)}
    />
    {/* existing content */}
  </>
);
```

- [ ] **Step 4: Run web tests**

```bash
cd apps/web && pnpm test
```

Expected: all PASS.

- [ ] **Step 5: Manual smoke test**

```bash
cd apps/web && pnpm dev
# Open http://localhost:3000/library/games/<nanolithId>/play
# Verify:
# 1. ResumeBooksList shows 2 rows (Storybook + Encounter Book) if progress exists
# 2. PhotoTranslateForm shows BookPicker with 2 buttons (Storybook + Encounter)
```

- [ ] **Step 6: Commit**

```bash
git add -u
git commit -m "feat(gamebook): integrate BookPicker into photo-translate form + ResumeBooksList in play page"
```

---

## Phase F: Seed + E2E + Docs (Tasks F1-F5)

### Task F1: Nanolith re-seed admin script

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Seeders/GameBookSeeder.cs`
- Modify: seed orchestrator to include GameBook seeding step

- [ ] **Step 1: Write seeder**

```csharp
// File: apps/api/src/Api/Infrastructure/Seeders/GameBookSeeder.cs
internal class GameBookSeeder
{
    private readonly IGameBookRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<GameBookSeeder> _logger;

    public GameBookSeeder(IGameBookRepository repo, IUnitOfWork uow, ILogger<GameBookSeeder> logger)
    {
        _repo = repo;
        _uow = uow;
        _logger = logger;
    }

    public async Task SeedNanolithAsync(
        Guid nanolithSharedGameId, Guid pressStartPdfId, Guid rulesPdfId,
        Guid adminUserId, CancellationToken ct)
    {
        var nanolithRef = GameRef.Shared(nanolithSharedGameId);
        var books = new[]
        {
            GameBook.CreateCommunity(nanolithRef, "Press Start",
                GameBookRole.Tutorial | GameBookRole.Setup, ParagraphScheme.None, "en",
                sequentialRead: false, kbSourceDocId: pressStartPdfId,
                physicalOnly: false, createdBy: adminUserId),

            GameBook.CreateCommunity(nanolithRef, "Rules",
                GameBookRole.RulesReference, ParagraphScheme.None, "en",
                sequentialRead: false, kbSourceDocId: rulesPdfId,
                physicalOnly: false, createdBy: adminUserId),

            GameBook.CreateCommunity(nanolithRef, "Storybook",
                GameBookRole.Narrative, ParagraphScheme.ParagraphNumber, "en",
                sequentialRead: true, kbSourceDocId: null,
                physicalOnly: true, createdBy: adminUserId),

            GameBook.CreateCommunity(nanolithRef, "Encounter Book",
                GameBookRole.Encounter, ParagraphScheme.Section, "en",
                sequentialRead: false, kbSourceDocId: null,
                physicalOnly: true, createdBy: adminUserId),
        };

        foreach (var book in books)
            await _repo.AddAsync(book, ct).ConfigureAwait(false);

        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Seeded {Count} Nanolith GameBooks", books.Length);
    }
}
```

- [ ] **Step 2: Wire seeder into orchestrator**

Edit the seed orchestrator to call `GameBookSeeder.SeedNanolithAsync` after `SharedGameSeeder` runs.

- [ ] **Step 3: Run seeder via Make**

```bash
cd infra && make seed-index
```

Expected: log lines confirming 4 books seeded.

- [ ] **Step 4: Verify in DB**

```bash
docker compose -f infra/docker-compose.yml exec postgres psql -U meepleai meepleai_dev \
  -c "SELECT display_name, roles, physical_only FROM game_books WHERE deleted_at IS NULL;"
```

Expected: 4 rows.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/GameBookSeeder.cs
git commit -m "feat(gamebook): add Nanolith GameBook seeder (4 community books)"
```

### Task F2: Fighting Fantasy seed (all-in-one example)

**Files to modify:**
- `apps/api/src/Api/Infrastructure/Seeders/GameBookSeeder.cs` (add `SeedFightingFantasyAsync`)

- [ ] **Step 1: Add seeder method**

```csharp
public async Task SeedFightingFantasyAsync(
    Guid ffSharedGameId, Guid pdfId, Guid adminUserId, CancellationToken ct)
{
    var ffRef = GameRef.Shared(ffSharedGameId);
    var book = GameBook.CreateCommunity(
        ffRef, "City of Thieves",
        GameBookRole.Tutorial | GameBookRole.RulesReference
            | GameBookRole.Narrative | GameBookRole.Encounter,
        ParagraphScheme.ParagraphNumber, "en",
        sequentialRead: false,
        kbSourceDocId: pdfId,
        physicalOnly: false,
        createdBy: adminUserId);
    await _repo.AddAsync(book, ct).ConfigureAwait(false);
    await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
}
```

- [ ] **Step 2: Use in E2E test fixture (will be created in Task F5)**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(gamebook): add Fighting Fantasy all-in-one seeder for multi-role validation"
```

### Task F3: Maracaibo seed (2 libri example — tutorial+rules in 1, narrative in altro)

**Files to modify:**
- `apps/api/src/Api/Infrastructure/Seeders/GameBookSeeder.cs` (add `SeedMaracaiboAsync`)

- [ ] **Step 1: Add seeder method**

```csharp
public async Task SeedMaracaiboAsync(
    Guid maracaiboSharedGameId, Guid rulebookPdfId, Guid adminUserId, CancellationToken ct)
{
    var ref_ = GameRef.Shared(maracaiboSharedGameId);
    var books = new[] {
        GameBook.CreateCommunity(ref_, "Rulebook",
            GameBookRole.Tutorial | GameBookRole.RulesReference,
            ParagraphScheme.None, "en", false, rulebookPdfId, false, adminUserId),
        GameBook.CreateCommunity(ref_, "Story Book",
            GameBookRole.Narrative,
            ParagraphScheme.ParagraphNumber, "en", true, null, true, adminUserId),
    };
    foreach (var b in books) await _repo.AddAsync(b, ct).ConfigureAwait(false);
    await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
}
```

- [ ] **Step 2: Commit**

```bash
git commit -m "feat(gamebook): add Maracaibo 2-libri seeder (rulebook+tutorial fused, narrative separate)"
```

### Task F4: Update existing Gherkin scenarios + add new generalization scenarios

**Files to modify:**
- `apps/web/tests/e2e/libro-game-nanolith.spec.ts` (existing) — update tags
- Create: `apps/web/tests/e2e/gamebook-multi-config.spec.ts` (new)

- [ ] **Step 1: Rename tags in existing E2E spec**

```bash
sed -i 's/@dogfood-nanolith-press-start-rules/@gamebook-multi-config/g' \
    apps/web/tests/e2e/libro-game-nanolith.spec.ts
```

- [ ] **Step 2: Create new generalization E2E spec**

```typescript
// File: apps/web/tests/e2e/gamebook-multi-config.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Gamebook multi-config @gamebook-multi-config', () => {
  test('Case A: Fighting Fantasy all-in-one — Q&A setup works on single multi-role book', async ({ page }) => {
    await page.goto('/library/games/fighting-fantasy/play');
    await page.getByTestId('chat-toggle').click();
    await page.getByPlaceholder('Chiedi...').fill('come si crea il personaggio?');
    await page.getByRole('button', { name: 'Invia' }).click();
    await expect(page.getByTestId('chat-response')).toBeVisible({ timeout: 10000 });
    // No "Press Start vs Rules" dropdown — single book
    await expect(page.getByText(/Press Start/i)).not.toBeVisible();
  });

  test('Case B: Maracaibo 2-libri — setup query routes via Tutorial role on Rulebook', async ({ page }) => {
    await page.goto('/library/games/maracaibo/play');
    await page.getByTestId('chat-toggle').click();
    await page.getByPlaceholder('Chiedi...').fill('setup per 3 giocatori');
    await page.getByRole('button', { name: 'Invia' }).click();
    await expect(page.getByTestId('chat-response')).toBeVisible({ timeout: 10000 });
  });

  test('Case C: 7th Continent — companion disabled when no GameBook', async ({ page }) => {
    await page.goto('/library/games/7th-continent/play');
    await expect(page.getByText(/Modalità companion non disponibile/i)).toBeVisible();
    await expect(page.getByTestId('chat-toggle')).toBeDisabled();
  });

  test('BookPicker visible only when N>1 narrative books (Nanolith)', async ({ page }) => {
    await page.goto('/library/games/nanolith/play');
    await page.getByTestId('photo-translate-cta').click();
    await expect(page.getByRole('radiogroup', { name: 'Seleziona libro' })).toBeVisible();
    await expect(page.getByRole('radio', { name: 'Storybook' })).toBeVisible();
    await expect(page.getByRole('radio', { name: 'Encounter Book' })).toBeVisible();
  });

  test('BookPicker hidden when 1 narrative book (Maracaibo)', async ({ page }) => {
    await page.goto('/library/games/maracaibo/play');
    await page.getByTestId('photo-translate-cta').click();
    await expect(page.getByRole('radiogroup', { name: 'Seleziona libro' })).not.toBeVisible();
  });
});
```

- [ ] **Step 3: Run E2E**

```bash
cd apps/web && pnpm test:e2e --grep "@gamebook-multi-config"
```

Expected: 5 tests PASS (requires seeded data from F1+F2+F3).

- [ ] **Step 4: Commit**

```bash
git add apps/web/tests/e2e/gamebook-multi-config.spec.ts \
        apps/web/tests/e2e/libro-game-nanolith.spec.ts
git commit -m "test(gamebook): add multi-config E2E scenarios + retag existing spec"
```

### Task F5: Documentation updates

**Files to modify:**
- `docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md` — add cross-link
- `CLAUDE.md` — add `GameBook` row to bounded context table

- [ ] **Step 1: Cross-link original Nanolith spec**

Add at top of `2026-05-07-libro-game-nanolith-demo-design.md`:

```markdown
> ⚠️ **GENERALIZED 2026-05-19**: This spec assumed Press Start + Rules + Storybook + Encounter as fixed model. The generalized multi-book schema is documented in [2026-05-19 Gamebook Multi-Book Generalization](../../for-developers/specs/2026-05-19-gamebook-multi-book-generalization-design.md). This spec is retained as the Nanolith-specific case study but its hardcoded assumptions are superseded.
```

- [ ] **Step 2: Update CLAUDE.md bounded context table**

In `CLAUDE.md`, find the table "DDD Bounded Contexts (18)" and update the `GameManagement` row:

```markdown
| GameManagement | Catalog, sessions, FAQs, specs, **game books (multi-role 1..N per game)** |
```

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md CLAUDE.md
git commit -m "docs(gamebook): cross-link generalization spec + update bounded context table"
```

---

## Task FINAL: Open PR

- [ ] **Step 1: Push branch**

```bash
git push -u origin feature/issue-TBD-gamebook-multi-book-generalization
```

- [ ] **Step 2: Detect parent branch**

```bash
git config branch.feature/issue-TBD-gamebook-multi-book-generalization.parent
```

Expected output: `main-dev`.

- [ ] **Step 3: Open PR**

```bash
gh pr create --base main-dev --title "feat(gamebook): multi-book generalization (any board game with N libri)" --body "$(cat <<'EOF'
## Summary

Generalizes the gamebook companion from the Nanolith-specific "2 KB + 2 physical books" assumption to any board game with 1..N books of any role combination.

- Introduces `GameBook` aggregate in `GameManagement` BC with multi-valued `GameBookRole` flag enum
- Replaces `GamebookPageType` enum with `GameBookId` FK on `TranslatedParagraph` + `GamebookPhotoArtifact`
- Refactors `SessionTracking` to track per-book progress via `SessionBookProgress` entity
- Adds chunk-level `role_tags` to `text_chunks` + hybrid `RoleClassifierService` at ingestion + `IntentClassifierService` at query time + `HybridSearchService` re-ranker boost
- Frontend: conditional `BookPicker` (only when N>1 narrative books) + per-book resume UI
- Validates model with seed data for Nanolith (4 books), Fighting Fantasy (1 all-in-one), Maracaibo (2 books), 7th Continent (0 books FM-19)

Depends on #1320 Phase 2 (Game Entity Reset providing `GameRef` discriminator).

Spec: [docs/for-developers/specs/2026-05-19-gamebook-multi-book-generalization-design.md](docs/for-developers/specs/2026-05-19-gamebook-multi-book-generalization-design.md)

## Test plan

- [ ] Backend unit tests pass: `cd apps/api/src/Api && dotnet test --filter "Category=Unit"`
- [ ] Backend integration tests pass: `dotnet test --filter "Category=Integration"`
- [ ] Frontend unit tests pass: `cd apps/web && pnpm test`
- [ ] E2E tests pass: `pnpm test:e2e --grep "@gamebook-multi-config"`
- [ ] Migration applies cleanly to fresh DB
- [ ] Seed creates 4 Nanolith books + 1 Fighting Fantasy + 2 Maracaibo
- [ ] Manual: visit /library/games/nanolith/play, verify BookPicker shows 2 narrative books
- [ ] Manual: visit /library/games/maracaibo/play, verify BookPicker hidden (only 1 narrative)
- [ ] Manual: visit /library/games/fighting-fantasy/play, verify Q&A works with single all-in-one KB
- [ ] CLAUDE.md bounded context table updated

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4: Wait for CI** + address any review comments + merge

---

## Self-Review

**Spec coverage check** (each spec section → task that covers it):

| Spec section | Covered by |
|---|---|
| §1 Context — assumption hardcoded | Task 0 + Phase A overview |
| §2 Problem statement | All phases |
| §3.1 GameBook aggregate | Task A2 |
| §3.2 game_books table | Task A4 + A6 |
| §3.3 SessionTracking refactor schema | Tasks C1-C6 |
| §3.4 text_chunks.role_tags | Task D1 |
| §4.1 N1+N2 merged | Frontend chat panel (Task E3, implicit — no UI distinction) + Backend retrieval (Tasks D5-D7 query routing) |
| §4.2 Pre-condizioni generalizzate | Task F1 (seeder enforces new pre-conditions) |
| §4.3 Seed Nanolith | Task F1 |
| §4.4 Esempi cross-gioco | Tasks F2 + F3 |
| §5 RAG pipeline (RoleClassifier + Intent + re-ranker) | Tasks D2, D3, D4, D5, D6, D7 |
| §6 Failure modes FM-19..FM-24 | Implicit in E2E Case C (FM-19) + backend validation (FM-21 in CreateGameBookCommandValidator) — **GAP**: FM-22, FM-23, FM-24 lack explicit task |
| §7.2 Code change inventory | All phases |
| §7.4 Effort estimate 9-13gg | Phases A-F |
| §8-9 Decisions log + OQ | Documented in spec, no implementation task needed |
| §10 DoD | Task FINAL |

**Gap fixes inline**:

Add to **Task F4** an additional E2E scenario covering FM-22 (multi-narrative-book `bookId` required), FM-23 (skip — blocked by #4228), FM-24 (personal book ownership check):

```typescript
test('FM-22: multi-narrative books require explicit bookId in photo-translate', async ({ page }) => {
  await page.goto('/library/games/nanolith/play');
  await page.getByTestId('photo-translate-cta').click();
  // Don't select book — submit anyway
  await page.getByTestId('photo-upload-submit').click();
  await expect(page.getByText(/Specifica quale libro/i)).toBeVisible();
});
```

Add to **Task B1** validator check for FM-21 (already implicit in `GameBookRole > 0` rule — verified).

**Placeholder scan**: searching for "TBD" — only `feature/issue-TBD-...` placeholder remains (intentional, fills at issue open time). No other red flags.

**Type consistency**: verified `GameBookRole` flag values consistent across all tasks (Tutorial=1, RulesReference=2, Narrative=4, Encounter=8, Lore=16, Setup=32). `ParagraphScheme` values consistent (None=0, ParagraphNumber=1, PageNumber=2, Section=3). `IGameBookRepository` signature consistent across producer (A3, A5) and consumers (B1-B7).

Plan complete.
