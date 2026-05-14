# Stage 3 — `/toolkits/[id]` mockup-faithful re-implementation

| | |
|---|---|
| **Date** | 2026-05-14 |
| **Status** | Draft (v1, in review) |
| **Authors** | claude-code (Aaron) |
| **Parent issues** | #1026 (Stage 3 conformity), #1023 (De-versioning umbrella) |
| **Tracking issues** | #1144 (BE), #1145 (FE) |
| **Soft-coordinated with** | #822 (Phase 5 own-variant), #819 (ratings), #820 (cover upload, deferred) |
| **Mockup** | `admin-mockups/design_files/sp4-toolkit-detail.jsx` |
| **Sibling spec (pattern source)** | `2026-05-13-player-detail-stage3-cluster-design.md` |

---

## 1. Problem

The `/toolkits/[id]` route is one of six Wave-3 detail surfaces flagged `missing` by the Stage 1 mockup-conformity audit (#1024). Two gaps compound:

- **Backend (`ToolkitDetailDto`)** carries Gate-B stubs documented inline at `ToolkitDetailDto.cs:13-26`: `Description` is derived from `Name + author DisplayName`, `CurrentVersion` is synthesised as `"1.0.{intVersion}"`, `License`/`SizeBytes`/`GameName` columns don't exist, `InstallCount`/`RatingAverage`/`RatingCount` are hardcoded 0/null/0. The mockup hero (`sp4-toolkit-detail.jsx:36-67`) consumes real values for all of these.
- **Frontend** — six placeholder components exist under `apps/web/src/components/features/toolkit-detail/` (`PromptPreviewBlock`, `RatingBreakdown`, `Stars`, `ToolkitIncludesGrid`, `ToolkitSummaryPanel`, `VersionTimeline`) but each is `export function X(_props): null`. The route currently has no `DetailPageLayout` adoption.

Both must converge for the route to reach mockup parity. The two PRs are independent in delivery order (FE can stub on the live wire shape; BE can land first to remove stubs).

---

## 2. AC0 — current state verification (2026-05-14)

Audit run on `main-dev` at `8993813c0`.

### 2.1 Endpoint contract — `GET /api/v1/toolkits/{id}`

Route lives in `apps/api/src/Api/Routing/GameToolkit/ToolkitMarketplaceEndpoints.cs:61-72`, handled by `GetToolkitDetailQueryHandler.cs`. Returns 200 with `ToolkitDetailResponse { Toolkit: ToolkitDetailDto, ViewerContext: ViewerContextDto }` or 404.

### 2.2 `ToolkitDetailDto` — present vs mockup

| Mockup field (`sp4-toolkit-detail.jsx`) | DTO field | Status | Source |
|---|---|---|---|
| `title` | `Name` | ✅ Real | `GameToolkitEntity.Name` |
| `desc` | `Description` | ⚠️ **Derived** (synthetic) | Computed from `Name + AuthorName` |
| `author{name,avatar}` | `AuthorName`, `AuthorAvatarUrl` | ✅ Real | `UserEntity` join via `CreatorId` |
| `author{id}` | `AuthorId` | ✅ Real | `GameToolkitEntity.CreatorId` |
| `cover` (gradient placeholder) | `CoverImageUrl` | ❌ Always null | No column |
| `agent{id,name,promptPreview}` | `Agent` | ✅ Real (preview 500 chars) | JSON `AgentConfig` parse |
| KB count | `KbDocsCount` | ⚠️ **Stub 0** | Phase 5 schema |
| Tools count | `ToolsCount` | ✅ Real | Sum of tool JSON arrays |
| `downloads` | `InstallCount` | ⚠️ **Stub 0** | No install entity |
| `rating`, `ratingCount` | `RatingAverage`, `RatingCount` | ⚠️ **Stub null/0** | No rating entity (#819) |
| `publishedAt` | `PublishedAt` | ✅ Real (= UpdatedAt when published) | Derived |
| `version` (semver e.g. `2.0.0`) | `CurrentVersion` | ⚠️ **Synthetic** (`"1.0.{int}"`) | Cast from int |
| `license` (e.g. `"CC BY-SA 4.0"`) | — | ❌ Missing | No column |
| `size` (e.g. `"124 KB"`) | — | ❌ Missing | No column / no projection |
| `gameId` / game association | — | ❌ Missing in DTO | FK exists on `GameToolkitEntity` |
| `reviews[]`, `ratingHistogram`, `versions[]`, `toolIds[]`, `kbIds[]` | — | ❌ Out of scope (Phase 5) | #819, #822 |

### 2.3 FE stubs

All six files under `apps/web/src/components/features/toolkit-detail/*.tsx` declare typed props but return `null`. The route page (`apps/web/src/app/(authenticated)/toolkits/[id]/page.tsx` if present) does not adopt `DetailPageLayout`.

---

## 3. Decisions taken during brainstorming

**D-1 — Path C consensus (BE + FE in parallel).** BE lands real columns + DTO promotion in a "mini-extension" PR (issue #1144). FE adopts `DetailPageLayout` consuming the live wire shape — when BE fields are stubbed, they render the stub gracefully (e.g. `null` license → meta row hidden, `RatingAverage = null` → "No ratings yet" copy). When BE lands real values, FE picks them up automatically. Rationale: mockup-faithfulness on shell layout doesn't depend on real data; both PRs are independently reviewable.

**D-2 — DTO superset, not parallel new DTO.** Extend the existing `ToolkitDetailDto` rather than introducing a `ToolkitMarketplaceDetailDto`. Reason (Fowler): adding optional fields to a record is non-breaking for existing consumers; introducing a second DTO doubles the projection surface and creates a naming split (`Toolkit` vs `ToolkitMarketplace`) that doesn't match the bounded context vocabulary. The DTO is internal — no external API contract obligation.

**D-3 — Hard-coded Phase 5 stubs stay.** `InstallCount`, `RatingAverage`, `RatingCount`, `KbDocsCount` remain `0` / `null` / `0` / `0` in this iteration. They are out of scope for issue #1144 (rationale documented in §10). The FE renders mockup-faithfully on zero values (showing "Be the first to rate" / "0 installs" copy), avoiding a tight coupling with #819 (ratings) and #822 (Phase 5 own-variant) delivery.

**D-4 — `sizeBytes` projected, not stored.** Compute at projection time: `Encoding.UTF8.GetByteCount(systemPrompt) + serializedToolsJsonLength`. Reason: a stored column would need a trigger / save-time hook to stay correct as tools/agent edit. Projection cost is O(1) per detail call and the value is already a cached 10-min response.

**D-5 — `version_semver` is the source of truth going forward.** Issue an EF migration adding `version_semver text NOT NULL DEFAULT '0.1.0'` (via the 3-step idempotent migration in §5.1). Backfill from existing int `Version`: `$"0.{version}.0"`. Old `Version int` column stays for now (other endpoints may still read it; full deprecation is a follow-up).

**Drift prevention** (Fowler panel CRITICAL): the two columns are an architectural debt accumulator unless writes update both transactionally. This PR enforces drift prevention via:

1. **`[Obsolete]` on `Version int` setter** in `GameToolkitEntity` — read access remains for legacy endpoints, but any new write code path is flagged at compile time.
2. **Domain invariant**: the entity factory / version-bump command writes BOTH columns in the same EF Core change tracker. A unit test (`Version_int_and_semver_stay_in_sync`) enforces that any code path mutating one mutates the other.
3. **CI guard**: a repository-scan unit test (using `Directory.EnumerateFiles` + regex over `apps/api/src/**/*.cs`) fails the build if any production code outside `Infrastructure/Migrations/` matches `\bentity\.Version\s*=` or `\bSet\(.*nameof\(.*\.Version\)\s*,` without an adjacent `VersionSemver` write on the same line or within the same statement block. NetArchTest is not a current project dependency — this grep-based check is sufficient and self-contained.
4. **Deletion milestone**: legacy `Version int` removed in a follow-up PR scheduled for the next minor release after all consumers verified migrated (tracked in §13).

Without these guardrails the two columns will drift within 2-3 PRs and produce silent marketplace-vs-runtime version mismatches.

**D-6 — `description` migrates to optional column.** Add `description text NULL`. The handler picks `entity.Description ?? FallbackFromName(entity.Name)` so existing toolkits without descriptions still render the synthetic value, but new/edited toolkits use the real one. Validation: max 2000 chars (Wiegers SMART: precise upper bound for storage planning).

**D-7 — `license` is a free string with a hint.** Free `string?` column rather than enum. Mockup shows `"CC BY-SA 4.0"` — a SPDX-like identifier — but binding the schema to a fixed enum locks out commercial / custom licenses. FE shows the string verbatim; future work could add SPDX validation.

**D-8 — `gameName` is projected, not stored.** The `GameId` FK already exists on `GameToolkitEntity`. Add a LEFT JOIN to `Games` in `ComputeDetailAsync`; project `Game.Name` as `GameName` (string?, null when toolkit not tied to a game). No new column.

**D-9 — Spec amendment: issue #1144 body says "extend `ToolboxDto`".** This is a terminology error — `ToolboxDto` is the session-scoped runtime aggregate. The marketplace detail is `ToolkitDetailDto`. This spec is the authoritative correction; the BE PR title and acceptance criteria refer to `ToolkitDetailDto`. The issue body should be amended at PR-open time.

---

## 4. Goals & non-goals

**Goals (in scope):**

1. Add 4 new columns to `GameToolkits` table: `description`, `license`, `version_semver`, *(none for size — projected)*. EF migration reversible.
2. Add **3 new fields** to `ToolkitDetailDto`: `License`, `GameName`, `SizeBytes` (all nullable per §5.5.2 versioning policy). Two existing fields change source but keep the same wire shape: `Description` reads from the new column with synthetic fallback; `CurrentVersion` reads from the new `version_semver` column instead of the synthesised `"1.0.{int}"`.
3. FE adopts `DetailPageLayout` for `/toolkits/[id]`, wires the 6 stub components, implements 6-tab navigation, renders mockup-faithfully against the live wire shape.
4. Visual conformity vs `admin-mockups/design_files/sp4-toolkit-detail.jsx` validated by Playwright.

**Non-goals (out of scope — deferred):**

- `InstallCount`, `RatingAverage`, `RatingCount`, `KbDocsCount` real values (#819 / Phase 5).
- `reviews[]`, `ratingHistogram` (#819).
- `versions[]` array with per-version notes / kind (#822 Phase 5).
- `toolIds[]`, `kbIds[]` exposed as structured arrays (currently only counts; #822).
- `coverImageUrl` upload + storage (#820 deferred).
- `useCount`, `visibility` enum, `yankedAt` workflow (#822).
- Removing the legacy `Version int` column (separate cleanup PR after consumers migrate).

---

## 5. API

### 5.1 BE: schema migration

New EF migration `ExtendGameToolkitWithMarketplaceFields`. The semver migration is split into **three idempotent steps** so re-running the migration on a partially-backfilled database does not corrupt legitimate user-assigned `"0.1.0"` values (Adzic panel CRITICAL):

```csharp
// Up()
migrationBuilder.AddColumn<string>(
    name: "description",
    table: "game_toolkits",
    type: "text",
    nullable: true);

migrationBuilder.AddColumn<string>(
    name: "license",
    table: "game_toolkits",
    type: "text",
    nullable: true);

// Step 1 — add VersionSemver as NULLABLE first
migrationBuilder.AddColumn<string>(
    name: "version_semver",
    table: "game_toolkits",
    type: "text",
    nullable: true);

// Step 2 — backfill ONLY rows where the column is still NULL
//          (guards against re-runs overwriting legitimate user values)
migrationBuilder.Sql(@"
    UPDATE game_toolkits
    SET version_semver = '0.' || COALESCE(version, 0)::text || '.0'
    WHERE version_semver IS NULL;
");

// Step 3 — promote to NOT NULL with a default for any future inserts
//          that don't specify the column
migrationBuilder.AlterColumn<string>(
    name: "version_semver",
    table: "game_toolkits",
    type: "text",
    nullable: false,
    defaultValue: "0.1.0",
    oldClrType: typeof(string),
    oldType: "text",
    oldNullable: true);
```

`Down()` drops all three columns (reversible). Forward-only deploy on staging first; the staging DB clone exercise (AC5) catches Postgres FK / type quirks before prod.

**Idempotency Gherkin** (Adzic panel addition, exercised in AC5):

```gherkin
Scenario: migration is idempotent under re-run
  Given a toolkit with version = 5 and version_semver = "0.5.0" from a prior migration
  When the migration runs a second time
  Then version_semver remains "0.5.0"
  And no row's version_semver is overwritten to "0.1.0"

Scenario: backfill handles NULL legacy version
  Given a toolkit with version = NULL
  When the migration runs
  Then version_semver = "0.0.0" (via COALESCE)
```

### 5.2 BE: `GameToolkitEntity` extension

```csharp
public class GameToolkitEntity
{
    // … existing properties …
    public string? Description { get; set; }
    public string? License { get; set; }
    public string VersionSemver { get; set; } = "0.1.0";
}
```

Update `MeepleAiDbContext.OnModelCreating` mapping; `Description` max 2000, `License` max 200, `VersionSemver` max 50 (per Wiegers measurable bounds).

### 5.3 BE: `ToolkitDetailDto` extension

New fields **append at the end** of the positional record (preserves binary compatibility with all existing C# call sites and contract-test snapshots; Fowler/Wiegers panel CRITICAL):

```csharp
internal sealed record ToolkitDetailDto(
    Guid Id,
    string Name,
    string Description,              // unchanged shape; source: entity.Description ?? Synthetic(Name)
    Guid AuthorId,
    string AuthorName,
    string? AuthorAvatarUrl,
    string? CoverImageUrl,
    ToolkitAgentSummaryDto Agent,
    int KbDocsCount,
    int ToolsCount,
    int InstallCount,
    decimal? RatingAverage,
    int RatingCount,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    DateTime? YankedAt,
    string CurrentVersion,           // unchanged shape; source: entity.VersionSemver
    // ── appended below — Stage 3 additions (#1144) ──────────────────────
    string? License,                 // NEW — nullable for forward-compat
    string? GameName,                // NEW — nullable, LEFT JOIN may miss
    long? SizeBytes);                // NEW — nullable for forward-compat;
                                     //       handler computes deterministically
                                     //       but FE may receive null from a
                                     //       BE one release behind (DTO
                                     //       versioning policy §5.5.2).
```

### 5.4 BE: handler projection changes

In `GetToolkitDetailQueryHandler.ComputeDetailAsync`:

- **LEFT JOIN** `Games` on `GameToolkit.GameId`. Project `Game.Name AS GameName`.
- **Description**: `entity.Description ?? $"Toolkit \"{entity.Name}\" by {authorName}."` (existing synthetic kept as fallback).
- **CurrentVersion**: `entity.VersionSemver` (replaces `$"1.0.{entity.Version}"`).
- **SizeBytes** (single canonical formula — Adzic panel IMPORTANT):

```csharp
long sizeBytes =
    Encoding.UTF8.GetByteCount(agentConfig?.SystemPrompt ?? string.Empty) +
    Encoding.UTF8.GetByteCount(entity.ToolsConfig ?? string.Empty);
```

Both terms are **UTF-8 byte counts**, never char counts. `ToolsConfig` MUST be read as the **persisted raw JSON text** (e.g. via `EF.Property<string>(entity, "ToolsConfig")` or a shadow string property mapped to the underlying column). Never re-serialise from a deserialised object graph — `JsonSerializer.Serialize` is non-deterministic on key ordering and whitespace, which would make `SizeBytes` drift between sequential reads of the same row. If the entity exposes `ToolsConfig` only as a typed POCO, add a `[NotMapped]` `RawToolsConfigJson` shadow property that the handler reads.

Exemplary Gherkin (added to §9.1): `Given systemPrompt = "Café ☕" (8 UTF-8 bytes) and ToolsConfig = "[]" (2 UTF-8 bytes), When the handler runs, Then SizeBytes = 10`.
- **License**: pass-through `entity.License`.

Cache key unchanged. Cache invalidation tag unchanged. The 10-min TTL is sufficient — these are slow-moving fields.

### 5.5 FE: wire types

`apps/web/src/lib/api/schemas/toolkit-detail.schemas.ts` (Zod) gains:

```typescript
export const toolkitDetailSchema = z.object({
  // … existing …
  license: z.string().nullable(),
  gameName: z.string().nullable(),
  sizeBytes: z.number().int().nonnegative().nullable(),
});
```

`useToolkitDetail` hook in `apps/web/src/hooks/queries/useToolkitDetail.ts` re-validates.

#### 5.5.1 Schema mode

The schema does **not** use `.strict()` — it tolerates unknown additive keys so a FE one release behind a BE that adds further fields continues to parse. This matches the existing Zod conventions across the codebase (verify via `pnpm exec grep -r ".strict()" apps/web/src/lib/api/schemas/`).

#### 5.5.2 DTO versioning policy

New DTO fields land **nullable** for one full release cycle, then get promoted to non-null in a follow-up PR only after every active FE deploy is verified to send/receive the new shape. This protects against a partial deploy state where the BE produces a richer envelope than the FE knows how to consume — and equally, where a stale FE call (e.g. cached service worker) hits an older BE that emits `null` for the new field.

The named schema (`toolkitDetailSchemaV2` if/when a breaking change ships) replaces the unsuffixed default; readers MUST import the version they target.

### 5.6 FE: route composition (issue #1145)

`/toolkits/[id]/page.tsx` consumes `useToolkitDetail` → routes through `ToolkitDetailView` (orchestrator, mirrors `PlayerDetailView` pattern). Orchestrator returns one of: `loading | error | not-found | default`.

Default render:

```tsx
<div data-slot="toolkit-detail-view">
  <DetailPageLayout
    hero={<ToolkitSummaryPanel detail={detail} viewerContext={ctx} labels={...} />}
    connections={<ToolkitConnectionBar detail={detail} labels={...} />}
    tabs={<ToolkitTabs activeTab={tab} onChange={onTabChange} counts={tabCounts} labels={tabLabels} />}
  >
    {tabPanel}
  </DetailPageLayout>
</div>
```

Shells (`loading`, `error`, `not-found`) emit `data-slot="toolkit-detail-{loading|error|not-found}"` directly per the player-detail pattern (issue #1143 contract).

### 5.7 FE: 6 tabs

| Key | Component | Stub-tolerant? |
|---|---|---|
| `overview` (default) | `OverviewTabPanel` (uses `ToolkitIncludesGrid` + description block) | ✅ |
| `agent` | `AgentTabPanel` (uses `PromptPreviewBlock` with `Agent.SystemPromptPreview`) | ✅ |
| `kb` | `KbTabPanel` (empty state when `KbDocsCount = 0`) | ✅ |
| `tools` | `ToolsTabPanel` (counts only; "structured list coming soon" when v1 carryover) | ✅ |
| `versions` | `VersionsTabPanel` (uses `VersionTimeline` with current version only in v1) | ✅ |
| `ratings` | `RatingsTabPanel` (uses `RatingBreakdown` with all-zero histogram + "No ratings yet") | ✅ |

URL state: `?tab=<key>` (default omitted from URL). Mirrors `PlayerTabs.tsx` pattern.

---

## 6. Routing & state

- **URL**: `/toolkits/[id]` (authenticated route). Query params: `?tab={overview|agent|kb|tools|versions|ratings}`, `?state={loading|error|not-found}` (dev/visual-test override).
- **Data hook**: `useToolkitDetail(toolkitId)` — TanStack Query, 10-min stale time matching server cache. Hits `GET /api/v1/toolkits/{id}`.
- **FSM**: identical 4-state shape used in `PlayerDetailView`: `loading → error | not-found | default`. Derivation logic in a pure helper `derive-toolkit-detail-state.ts`.
- **Auth gating**: 401 from API → redirect to `/login?next=/toolkits/{id}` (mirror player-detail).
- **Visual-test fixture**: short-circuit via `IS_VISUAL_TEST_BUILD` env, loading `toolkit-detail-visual-fixture.ts` for prod-build E2E without backend.

---

## 7. DOM composition

```
<div data-slot="toolkit-detail-view">
  <div data-slot="detail-page-layout">
    <header><!-- ToolkitSummaryPanel: cover, title, desc, author chip, game chip, stats grid, CTAs, meta panel --></header>
    <aside aria-label="related entities"><!-- ToolkitConnectionBar: 6-pip bar (overview, agent, kb, tools, versions, ratings) --></aside>
    <nav aria-label="detail sections"><!-- ToolkitTabs: 6 tabs with counts --></nav>
    <main><!-- active TabPanel --></main>
  </div>
</div>
```

Each `TabPanel` component emits `data-slot="toolkit-detail-tabpanel-{key}"` for visual + a11y targeting. Same pattern as player-detail.

---

## 8. Visual conformity

Mockup reference `admin-mockups/design_files/sp4-toolkit-detail.jsx:159-499`.

### 8.1 Tokens

All colors via semantic tokens (`bg-card`, `text-muted-foreground`, `border-border`) per **CLAUDE.md token canonicalization** §Design System De-versioning. No hardcoded `bg-white`, `bg-slate-*`, etc. (ESLint `local/no-hardcoded-color-utility` enforces).

Entity utility: `bg-entity-toolkit` / `text-entity-toolkit` / `ring-entity-toolkit/30` if defined; otherwise add to `apps/web/src/styles/entity-tokens.css` in this PR (entity color: violet for toolkit per design system).

### 8.2 Spacing / typography

`SummaryPanel`: 1-col grid below `md`, 2-col `[summary | tabs+content]` from `md` up. Hero cover gradient ratio 16:9. Stats grid 3-col fixed.

Typography: title `font-display text-[28px] font-extrabold`, description `text-[14px] text-muted-foreground`, stats labels `text-[11px] uppercase tracking-wide`.

---

## 9. Test plan

### 9.1 BE — unit tests

`apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Queries/GetToolkitDetailQueryHandlerTests.cs`:

```gherkin
Scenario: returns License when entity has it
  Given a toolkit with License = "CC BY-SA 4.0"
  When GetToolkitDetailQuery is handled
  Then response.Toolkit.License = "CC BY-SA 4.0"

Scenario: returns null License when entity doesn't have one
  Given a toolkit with License = null
  When GetToolkitDetailQuery is handled
  Then response.Toolkit.License = null

Scenario: returns GameName via LEFT JOIN
  Given a toolkit with GameId pointing to "Wingspan"
  When GetToolkitDetailQuery is handled
  Then response.Toolkit.GameName = "Wingspan"

Scenario: returns null GameName when toolkit has no game
  Given a toolkit with GameId = null
  When GetToolkitDetailQuery is handled
  Then response.Toolkit.GameName = null

Scenario: SizeBytes computed from systemPrompt + tools
  Given a toolkit with systemPrompt = "hello" (5 bytes) and 2 tools serializing to 100 bytes total
  When GetToolkitDetailQuery is handled
  Then response.Toolkit.SizeBytes = 105

Scenario: SizeBytes is 0 when toolkit has no agent and no tools
  Given a toolkit with no AgentConfig and no tools
  When GetToolkitDetailQuery is handled
  Then response.Toolkit.SizeBytes = 0

Scenario: CurrentVersion now uses semver
  Given a toolkit with VersionSemver = "2.0.0"
  When GetToolkitDetailQuery is handled
  Then response.Toolkit.CurrentVersion = "2.0.0"

Scenario: Description falls back to synthetic when entity description is null
  Given a toolkit with Description = null, Name = "Strategy Lab", author = "Aaron"
  When GetToolkitDetailQuery is handled
  Then response.Toolkit.Description = 'Toolkit "Strategy Lab" by Aaron.'

# ── Cross-aggregate edge cases (Adzic panel IMPORTANT) ─────────────────

Scenario: GameName is null when the referenced Game is soft-deleted
  Given a toolkit with GameId pointing to a Game with IsDeleted = true
  When the handler runs (MeepleAiDbContext applies HasQueryFilter(!IsDeleted))
  Then response.Toolkit.GameName = null
  And response.Toolkit.GameId remains the original Guid (FK not nulled)

Scenario: Description "" (empty string) is preserved, not replaced by synthetic
  Given a toolkit with Description = "" (user-set empty)
  When the handler runs
  Then response.Toolkit.Description = "" (NOT the synthetic fallback)
  # Rationale: empty-string is a meaningful user choice;
  # synthetic only applies on NULL (column unset).

Scenario: VersionSemver max-length boundary
  Given a toolkit with VersionSemver = "{50-char-string}"
  When the handler runs
  Then response is 200 and CurrentVersion equals the input verbatim
  # Anti-scenario: 51-char input is rejected by the DbContext mapping
  # at write time, not read time — this scenario covers the read path only.

Scenario: License at and over the max-length boundary
  Given the License column max = 200
  When a toolkit with License = "{200-char-string}" is fetched
  Then response.Toolkit.License has length 200
  # Write-side rejection at length 201 is covered by entity-level validation tests.

Scenario: SizeBytes unicode encoding correctness
  Given systemPrompt = "Café ☕" (8 UTF-8 bytes; not 6 chars)
   And ToolsConfig = "[]" (2 UTF-8 bytes)
  When the handler runs
  Then SizeBytes = 10
```

### 9.2 BE — integration tests

`apps/api/tests/Api.Tests/Integration/GameToolkit/ToolkitMarketplaceEndpointsTests.cs`:

```gherkin
Scenario: 200 response shape contains all marketplace fields
  Given the API is running with a seeded toolkit
  When GET /api/v1/toolkits/{id}
  Then status = 200
  And response.body.toolkit has fields: id, name, description, authorId, authorName,
      authorAvatarUrl, coverImageUrl, license, gameName, sizeBytes, agent, kbDocsCount,
      toolsCount, installCount, ratingAverage, ratingCount, createdAt, publishedAt,
      yankedAt, currentVersion
  And response.body.viewerContext has fields: isOwner, hasInstalled, canRate

Scenario: 200 response when license is unset
  Given the API is running with a seeded toolkit that has no License column value
  When GET /api/v1/toolkits/{id}
  Then status = 200
  And response.body.toolkit.license = null

Scenario: existing /toolkits/{id} consumers are unaffected
  Given the FE useToolkitDetail hook before this PR
  When GET /api/v1/toolkits/{id} returns the new superset
  Then the old shape is a strict subset
  And Zod parsing of the new shape via the old schema succeeds (no breaking change)
```

### 9.3 FE — Vitest unit tests

`apps/web/src/app/(authenticated)/toolkits/[id]/_components/__tests__/ToolkitDetailView.test.tsx`:

- All 4 FSM states render correct shell (`data-slot` assertions).
- Default render includes `player-detail-view`-style `[data-slot="toolkit-detail-view"]` (mirroring #1143 contract).
- Each tab panel renders its specific `data-slot="toolkit-detail-tabpanel-{key}"`.
- Stub-tolerance: `RatingsTabPanel` renders "No ratings yet" when `RatingCount = 0`.
- Stub-tolerance: meta row hides License entry when `License = null`.

### 9.4 FE — Playwright visual

`apps/web/e2e/visual-migrated/sp4-toolkit-detail.spec.ts`:

- Default render screenshot vs baseline at desktop-chrome viewport.
- Not-found shell screenshot.
- Tab switching: capture each tab panel.

### 9.5 FE — Smoke

`apps/web/e2e/smoke-real-backend/toolkit-detail.smoke.spec.ts`:

```typescript
test('frontend /toolkits/{id} renders shell for deterministic slug', async ({ page }) => {
  await page.goto(`/toolkits/${NEVER_EXISTS_ID}`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector(
    '[data-slot="toolkit-detail-view"], [data-slot="toolkit-detail-not-found"], [data-slot="toolkit-detail-error"]',
    { timeout: 30_000 }
  );
});
```

(Direct port of `player-detail.smoke.spec.ts`. #1143 lesson: keep the OR-selector.)

---

## 10. Files touched

### 10.1 BE PR (issue #1144)

| Path | Change |
|---|---|
| `apps/api/src/Api/Infrastructure/Migrations/2026MMDD_ExtendGameToolkitWithMarketplaceFields.cs` | NEW |
| `apps/api/src/Api/Infrastructure/Entities/GameToolkit/GameToolkitEntity.cs` | +3 props |
| `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` | +property mappings |
| `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Queries/GetToolkitDetail/ToolkitDetailDto.cs` | +3 fields |
| `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Queries/GetToolkitDetail/GetToolkitDetailQueryHandler.cs` | projection update + LEFT JOIN |
| `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Queries/GetToolkitDetailQueryHandlerTests.cs` | +8 scenarios |
| `apps/api/tests/Api.Tests/Integration/GameToolkit/ToolkitMarketplaceEndpointsTests.cs` | +3 scenarios |

### 10.2 FE PR (issue #1145)

| Path | Change |
|---|---|
| `apps/web/src/lib/api/schemas/toolkit-detail.schemas.ts` | +3 zod fields |
| `apps/web/src/hooks/queries/useToolkitDetail.ts` | type re-validation |
| `apps/web/src/app/(authenticated)/toolkits/[id]/page.tsx` | route boundary |
| `apps/web/src/app/(authenticated)/toolkits/[id]/_components/ToolkitDetailView.tsx` | NEW orchestrator |
| `apps/web/src/app/(authenticated)/toolkits/[id]/_components/ToolkitTabs.tsx` | NEW (mirror PlayerTabs) |
| `apps/web/src/app/(authenticated)/toolkits/[id]/_components/ToolkitConnectionBar.tsx` | NEW |
| `apps/web/src/app/(authenticated)/toolkits/[id]/_components/{Overview,Agent,Kb,Tools,Versions,Ratings}TabPanel.tsx` | NEW × 6 |
| `apps/web/src/components/features/toolkit-detail/*.tsx` | Wire 6 existing stubs |
| `apps/web/src/lib/toolkit-detail/derive-toolkit-detail-state.ts` | NEW pure helper |
| `apps/web/src/lib/toolkit-detail/toolkit-detail-visual-test-fixture.ts` | NEW |
| `apps/web/e2e/visual-migrated/sp4-toolkit-detail.spec.ts` | NEW |
| `apps/web/e2e/smoke-real-backend/toolkit-detail.smoke.spec.ts` | NEW |
| Tests under `__tests__/` | NEW for each new component |

---

## 11. Acceptance criteria

### 11.1 BE (issue #1144)

- **AC1 — Endpoint shape**: `GET /api/v1/toolkits/{id}` 200 response includes `license` (string?), `gameName` (string?), `sizeBytes` (long), and `currentVersion` from `VersionSemver`. Verified by integration test.
- **AC2 — Migration reversibility (Testcontainers Postgres)**: an integration test using the existing Testcontainers Postgres fixture
  (a) applies the migration, captures the `information_schema.columns` snapshot for `game_toolkits`,
  (b) runs `Down()`, captures a second snapshot,
  (c) asserts column-set equality with the pre-migration baseline (column names, types, nullability, defaults).
  Data preservation assertion: a probe row inserted before migration MUST still be readable after Up→Down with all original column values intact. Schema diff is unit-testable as a SQL string comparison only as a smoke; the authoritative check is the Testcontainers roundtrip.
- **AC3 — Unit coverage**: 8 new scenarios in `GetToolkitDetailQueryHandlerTests` pass. Coverage on the modified handler ≥ 90% measured via `dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura` against the existing `coverlet.runsettings` config (repo target documented in `apps/api/tests/Api.Tests/coverlet.runsettings`).
- **AC4 — Backwards compatibility**: existing wire-shape consumers (FE `useToolkitDetail` pre-PR) parse the new payload without error. Zod `.passthrough()` not required — additive fields only.
- **AC5 — Staging exercise**: backfill SQL run on a staging DB clone; row counts match; spot-check 5 toolkits show plausible `VersionSemver` values.
- **AC6 — No regression**: `dotnet test --filter "BoundedContext=GameToolkit|GameToolbox"` green.
- **AC7 — Cache invalidation on write** (Fowler panel IMPORTANT): the HybridCache tag `toolkitDetail` (and the per-toolkit tag `toolkit:{id}`) is bumped on every write path that mutates fields surfaced in this DTO — specifically `AgentConfig.SystemPrompt`, `ToolsConfig`, `Description`, `License`, `VersionSemver`. An integration test edits each field via its respective command and asserts the next `GET /api/v1/toolkits/{id}` returns the new value (not the cached stale one).
- **AC8 — Drift prevention enforcement** (Fowler panel CRITICAL): the legacy `Version int` setter carries `[Obsolete]`; an explicit unit test (`Version_int_and_semver_stay_in_sync`) verifies that any command writing one writes the other in the same `SaveChangesAsync` call; a repository-scan test (per D-5.3) fails the build if production code outside `Infrastructure/Migrations/` writes `entity.Version` without an adjacent `VersionSemver` write.

> **Precondition (not AC)**: issue #1144 body is edited at BE-PR-open time to reference `ToolkitDetailDto` (not `ToolboxDto`) — see decision D-9 for rationale. This is a process step, not an artifact-verifiable acceptance criterion.

### 11.2 FE (issue #1145)

- **AC9 — Layout adoption**: `/toolkits/[id]` default render wraps `DetailPageLayout` with `data-slot="toolkit-detail-view"`.
- **AC10 — All FSM shells**: loading / error / not-found emit own `data-slot="toolkit-detail-{loading|error|not-found}"`.
- **AC11 — 6 tabs functional**: each tab renders its panel; URL `?tab={key}` round-trips on navigation; default tab is `overview` (omitted from URL).
- **AC12 — Stub-tolerance** (decomposed per Wiegers panel IMPORTANT): the route renders mockup-faithfully on Phase-5 stub values. Verified by these specific sub-assertions:
  - AC12.1 `RatingsTabPanel` shows "No ratings yet" copy when `ratingCount = 0`.
  - AC12.2 `ToolkitSummaryPanel` hides the License meta row when `license = null`; renders the value verbatim otherwise.
  - AC12.3 `ToolkitSummaryPanel` hides the Game chip when `gameName = null`; renders chip + link otherwise.
  - AC12.4 `ToolkitSummaryPanel` shows the cover gradient placeholder when `coverImageUrl = null`; renders image otherwise.
  - AC12.5 `VersionsTabPanel` shows only the current version (no timeline) when versions array is empty (v1 carryover).
  - AC12.6 `OverviewTabPanel` description block renders the synthetic fallback when `description` is the BE-derived synthetic; renders the real value verbatim when not.
  - AC12.7 No `Suspense` boundary throws; no `kind="error"` shell is reachable from any stub state.
- **AC13 — Visual conformity**: Playwright visual baseline matches mockup within tolerance threshold (1.5% pixel diff per existing config at `apps/web/playwright-visual.config.ts`).
- **AC14 — Smoke**: `toolkit-detail.smoke.spec.ts` passes on `main-dev` after merge using the OR-selector pattern from #1143.
- **AC15 — Unit coverage**: ≥ 85% on new components measured via `pnpm test:coverage` (Vitest v8 coverage) against the threshold gate in `apps/web/vitest.config.ts` (per CLAUDE.md frontend target).
- **AC16 — A11y**: WAI-ARIA landmarks via `DetailPageLayout` (`<header>`, `<aside>`, `<nav>`, `<main>`). No new `axe-core` violations beyond baseline.
- **AC17 — No hardcoded colors**: ESLint `local/no-hardcoded-color-utility` clean.

---

## 12. Rollback

### BE
- Revert PR via GitHub UI (squash-merge enables atomic revert).
- Run `dotnet ef migrations remove` on the new migration locally; produce a follow-up "RevertExtendGameToolkitWithMarketplaceFields" migration that drops the three columns + restores defaults. Apply on staging then prod.
- Cache invalidation: HybridCache tag `toolkitDetail` bumped automatically on entity write — no manual flush.

### FE
- Revert PR. Route falls back to legacy stub render (or the pre-#1145 render if any).
- Visual baselines: keep both old + new in `e2e/visual-migrated/` so revert reuses the old baseline.

No downstream consumer of the new DTO fields outside the FE PR — revert is safe in either order.

---

## 13. Follow-ups (out of scope)

- **#819** — real ratings + reviews entities, histogram, `RatingAverage` / `RatingCount` real population.
- **#822** — Phase 5 own-variant: real `versions[]` array, `useCount`, `visibility`, yank workflow, `publishedAt` separate from `UpdatedAt`.
- **#820** — cover image upload + storage (deferred from this iteration).
- **Cleanup PR** — drop legacy `Version int` column from `GameToolkitEntity` once all consumers read `VersionSemver`.
- **#1147** — `/discover` 7-row catalog (sibling Stage 3 issue).
- **#1126** — DetailPageLayout primitive spec-hardening (5 deferred items from `/sc:spec-panel` #1112).

---

## 14. References

- Mockup: `admin-mockups/design_files/sp4-toolkit-detail.jsx`
- Sibling spec: `docs/superpowers/specs/2026-05-13-player-detail-stage3-cluster-design.md`
- Stage 3 umbrella: issue #1023
- Stage 3 cluster: issue #1026
- DetailPageLayout primitive: PR #1112, spec `2026-05-13-detail-page-layout-design.md`
- AuthLogin rate-limit fix (CI smoke unblock): PR #1142, issue #1099
- player-detail data-slot wrapper precedent: PR #1146, issue #1143
- Token canonicalization (color rules): CLAUDE.md "Token Canonicalization" + `docs/for-developers/specs/2026-05-12-token-canonicalization.md`
- Design system de-versioning umbrella: `docs/for-developers/specs/2026-05-11-design-system-deversioning.md`
