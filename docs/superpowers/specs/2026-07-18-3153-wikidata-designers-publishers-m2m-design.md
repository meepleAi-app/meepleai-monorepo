# Design — #3153: Persist Wikidata designers/publishers at draft→game (M2M get-or-create)

**Issue**: [#3153](https://github.com/meepleAi-app/meepleai-monorepo/issues/3153) · **Branch**: `feature/issue-3153-wikidata-designers-publishers-m2m` · **Parent**: `main-dev` · **Date**: 2026-07-18

Follow-up of #3147 (`feat: persist Wikidata properties at draft→game`) + #3154 (`fix: trim EnrichFromProvenance to persisting scalars`).

---

## 1. Problem

Approving a Wikidata-seeded draft promotes it to a `SharedGame` skeleton. Scalars (year, min/max players, playtime) are persisted (#3147), but **designers/publishers are silently dropped** — no exception, the names never reach the DB. #3154 deliberately trimmed `SharedGame.EnrichFromProvenance` to scalars-only precisely because the persistence layer could not write the M2M, deferring the real fix to this issue.

Discovered via **live verification** of #3147 (approving a Wikidata-seeded draft): scalars persisted, designers/publishers did not.

## 2. Root cause (verified against code)

`SharedGameRepository` write path (`.../SharedGameCatalog/Infrastructure/Repositories/SharedGameRepository.cs`):

- `MapToEntity` (L183-222) is `private static`, synchronous, and maps **scalar columns only** onto a **fresh DETACHED** `SharedGameEntity`. It never touches the `Designers`/`Publishers` M2M navigations. So every `AddAsync`/`Update` round-trips the aggregate **without** persisting join rows.
- `MapToDomain` (L119-181) hydrates `Designers` on read (only when `GetByIdAsync` eager-loads via `.Include(g => g.Designers)`), but **never** hydrates `Publishers` and no repo method `.Include`s `Publishers`.

The M2M is a classic EF Core skip-navigation (implicit `Dictionary<string,object>` join, tables `shared_game_designers`/`shared_game_publishers`, both FKs `ON DELETE CASCADE`). Both lookup tables carry a **UNIQUE index on `Name`** (`ix_game_designers_name`, `ix_game_publishers_name`, case-sensitive, maxlen 200). Naively assigning `new GameDesignerEntity{Name=…}` to the detached entity and saving would attempt to INSERT designers and violate the unique index for any pre-existing name. So a **find-or-create by name** is mandatory.

## 3. Key facts that reframe the issue

The issue text says "make `MapToEntity` DB-aware (async)". Discovery showed a cleaner path:

1. **There is no `UpdateAsync`.** The interface has `Task AddAsync(…)` (async, has `CancellationToken`) and `void Update(SharedGame)` (sync, no `ct`). The issue's premise that both are "already async" is half-wrong.
2. **The promotion scenario uses `AddAsync` only.** `CatalogSeedApprovedEventHandler` new-skeleton branch (L154-186): `CreateSkeleton → EnrichFromProvenance → AddAsync → single SaveChanges`. The existing-game branch (BggId collision, L130-153) does **not** call `EnrichFromProvenance` — it only backfills `WikidataQid` via `Update`. It is out of scope.
3. **The names are already in provenance.** `CatalogSeedProvenance` carries `FieldProvenance.Value` typed `List<string>` under keys `"designers"`/`"publishers"` (`WikidataCatalogProvider` L349-359, one label each today), retrievable via `provenance.GetValue<List<string>>("designers")`. The handler simply never reads them.
4. **A working get-or-create M2M pattern already exists** (`UpdateSharedGameCommandHandler.ReplaceDesignersAsync`, L203-236) but on a separately-loaded TRACKED entity — a different shape from the repo's detached-map path. The canonical pattern the issue points at is `RelationshipSeeder.GetOrCreateDesignerAsync` (L149-182): trim → cache → `EF.Functions.ILike` DB lookup → insert.

## 4. Decisions (locked)

| # | Decision | Choice | Rationale |
|---|---|---|---|
| D1 | Where to resolve get-or-create | **Inside `AddAsync`** (private async helpers); `MapToEntity` stays a pure scalar mapper; `Update` untouched | Zero interface change vs. the ~28 production call-sites + ~10 Moq test files an `Update→UpdateAsync` rename would break. Covers the issue's scenario exactly. Diverges from the issue's literal text but is technically superior. |
| D2 | Concurrency (two drafts approved concurrently with the same NEW designer name → UNIQUE violation) | **Mirror the seeder** — no concurrency handling, single-writer assumption; document the race | The issue asks to mirror `RelationshipSeeder` (which has no concurrency defense). Promotion is a low-frequency admin action and is already non-atomic with approval, so this matches the current risk posture. |
| D3 | Read-side symmetry | **Add `Publishers` hydration** (`MapToDomain` loop + `.Include(g => g.Publishers)` in `GetByIdAsync`), mirroring the existing `Designers` handling | Cheap; avoids persisting data nothing can read back; lets the integration test assert via the repo round-trip too. |
| D4 | Matching semantics | **`EF.Functions.ILike`** case-insensitive (mirror seeder), not exact match | The issue explicitly points at `GetOrCreateDesignerAsync`. |
| D5 | Scope | **New-skeleton (`AddAsync`) branch only** | Matches the current scalar-enrichment scoping; the existing-game branch does not enrich. |
| D6 | `EnrichFromProvenance` signature | **Extend the single method with two TRAILING OPTIONAL params** (`IReadOnlyList<string>? designers = null, publishers = null` after `modifiedBy`) | Non-breaking: the 7 existing `EnrichFromProvenance` unit-test call sites and the handler's current call compile unchanged (no compile-break TDD hazard). The single production caller (promotion handler) passes them explicitly. Revised from "required params before modifiedBy" after adversarial plan review flagged the compile-ordering break. |

## 4a. Adversarial plan-review findings incorporated

Two adversarial reviewers (EF/persistence correctness + test-design/DoD) reviewed the plan against the real code. Verified findings, all incorporated:

- **`AddAsync` blast radius is NOT confined to the promotion path** (EF reviewer, verified). `SharedGameRepository.AddAsync` has ~7 `SharedGame`-aggregate callers, but only **`CreateSharedGameCommandHandler`** (`:80-88`) builds a designer/publisher-carrying aggregate before `AddAsync` — every other caller (`ImportGameFromBgg`, `ImportGamesFromExcel`, `ConfirmExcelImport`, `CreateSharedGameFromPdf`, `ApproveGameProposal`) passes an **empty**-designer aggregate → the resolver is a no-op → unaffected. `ImportGameFromBgg`/`UpdateSharedGame` persist designers via a **separate tracked-entity path** on an empty-designer aggregate → no double-persist. For `CreateSharedGameCommandHandler`, designers/publishers are **currently silently dropped** (the same latent bug class as #3153, different entry point) — this change **fixes** that too. Acknowledged, and pinned by a regression test (a duplicate-name-in-one-call test, which also guards `CreateSharedGameCommand`'s no-dedup 20-name payload).
- **Duplicate-insert / UNIQUE-violation** (EF reviewer, verified MAJOR): the same new name supplied twice in ONE `AddAsync` (reachable from `CreateSharedGameCommand`, which caps count at 20 but does not de-dup) would insert two rows → `ix_game_designers_name` violation. **Fix**: the resolver de-dups its input case-insensitively (`.Distinct(StringComparer.OrdinalIgnoreCase)` over trimmed names). The `.Local` fallback additionally reuses a still-pending insert from an earlier `AddAsync` in the same `DbContext` (cross-call, tracked via the graph add).
- **Existing test flips** (test reviewer, verified M1): `Handle_ProvenanceWithProperties_EnrichesNewSkeleton` currently asserts `Designers/Publishers.Should().BeEmpty()` — must be flipped to expect them populated (and its `#3154` comment updated); the existing `RichProvenance()` helper is reused. `Handle_ExistingGameCollision_...` keeps its `BeEmpty` assertion (existing-game branch never enriches — a valid D5 scope guard).
- **Publisher reuse untested** (test reviewer, verified M3): `ResolvePublishersAsync` is a copy of `ResolveDesignersAsync`; a publisher-reuse integration test is added (publishers are more shared in practice — Kosmos, Hasbro — so the copy-paste reuse path is high-value to guard).
- **MINOR**: `ILIKE` treats `%`/`_` in a name as wildcards (accepted, mirrors the seeder, documented in a code comment). `GetByIdAsync` gains a second collection `.Include` → add `.AsSplitQuery()` to avoid the cartesian product.

## 5. Design — changes per layer

### 5.1 Domain aggregate — `SharedGame.EnrichFromProvenance`

Extend the signature (D6) with two **trailing optional** params (`= null`) so all existing call sites compile unchanged. A null/empty list is a lenient no-op, consistent with the method's skip-don't-throw contract for scalars:

```csharp
public void EnrichFromProvenance(
    int? yearPublished,
    int? minPlayers,
    int? maxPlayers,
    int? playingTimeMinutes,
    Guid modifiedBy,
    IReadOnlyList<string>? designers = null,
    IReadOnlyList<string>? publishers = null)
```

Body: after the scalar blocks, before the `if (changed)` audit stamp, add lenient de-duplicated ingestion for each collection:

```csharp
if (designers is not null)
{
    foreach (var name in designers)
    {
        if (string.IsNullOrWhiteSpace(name)) continue;
        var trimmed = name.Trim();
        if (trimmed.Length > 200) continue;                 // lenient skip (mirror scalar leniency; avoid GameDesigner.Create throw)
        if (_designers.Any(d => string.Equals(d.Name, trimmed, StringComparison.OrdinalIgnoreCase))) continue; // intra-aggregate dedup
        _designers.Add(GameDesigner.Create(trimmed));
        changed = true;
    }
}
// symmetric block for publishers → _publishers / GamePublisher.Create
```

Update the XML remarks (currently L609-614) that state designers/publishers are excluded — they are now ingested; persistence is handled by the repo (this issue).

**Leniency note**: names >200 chars or blank are skipped, not thrown — `EnrichFromProvenance` must remain non-throwing (except the pre-existing `modifiedBy == Guid.Empty` guard) so a malformed Wikidata label can't abort the whole promotion.

### 5.2 Application handler — `CatalogSeedApprovedEventHandler`

Read the two name lists from provenance (near L109-119, alongside the scalars) and pass them into the new-skeleton `EnrichFromProvenance` call (L178-183):

```csharp
var provDesigners  = provenance.GetValue<List<string>>("designers");
var provPublishers = provenance.GetValue<List<string>>("publishers");
// …
skeleton.EnrichFromProvenance(
    yearPublished: provYear, minPlayers: provMinPlayers,
    maxPlayers: provMaxPlayers, playingTimeMinutes: provPlayingTime,
    designers: provDesigners, publishers: provPublishers,
    modifiedBy: notification.ApprovedByUserId);
```

Update the L113-119 comment that says designers/publishers are deliberately skipped. The existing-game branch is unchanged (D5).

### 5.3 Infrastructure repo — `SharedGameRepository`

`AddAsync` resolves the aggregate's names and attaches resolved entities to the graph before the in-memory add (no `SaveChanges` here — the handler owns the single flush for atomicity):

```csharp
public async Task AddAsync(SharedGame sharedGame, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(sharedGame);
    var entity = MapToEntity(sharedGame);
    await ResolveDesignersAsync(entity, sharedGame.Designers, cancellationToken).ConfigureAwait(false);
    await ResolvePublishersAsync(entity, sharedGame.Publishers, cancellationToken).ConfigureAwait(false);
    await DbContext.Set<SharedGameEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
}
```

Resolver (designers; publishers symmetric against `DbContext.GamePublishers`). Input is **de-duped case-insensitively** (fixes the duplicate-insert UNIQUE violation for a caller supplying the same name twice, e.g. `CreateSharedGameCommand`):

```csharp
private async Task ResolveDesignersAsync(
    SharedGameEntity entity, IReadOnlyCollection<GameDesigner> designers, CancellationToken ct)
{
    foreach (var trimmed in designers
        .Select(d => d.Name.Trim())
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.OrdinalIgnoreCase))                              // intra-call de-dup
    {
        // Get-or-create by name. ILIKE mirrors RelationshipSeeder (case-insensitive
        // equality; a name literally containing % or _ would over-match — accepted).
        var existing = await DbContext.GameDesigners
            .FirstOrDefaultAsync(d => EF.Functions.ILike(d.Name, trimmed), ct)
            .ConfigureAwait(false);

        var resolved = existing
            ?? DbContext.GameDesigners.Local.FirstOrDefault(                       // reuse a pending add (cross-call, same context)
                   d => string.Equals(d.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            ?? new GameDesignerEntity { Id = Guid.NewGuid(), Name = trimmed, CreatedAt = DateTime.UtcNow };

        if (!entity.Designers.Any(d => d.Id == resolved.Id))
            entity.Designers.Add(resolved);
    }
}
```

- **`existing` found** → tracked `Unchanged`; adding to `entity.Designers` + `AddAsync(entity)` inserts only the join row.
- **new** → inline `GameDesignerEntity`; becomes `Added` when `AddAsync` traverses the graph; both the designer row and the join row are inserted by the handler's single `SaveChanges`. Cross-call reuse works because a prior game's `AddAsync(entity)` already tracked its new designers into `.Local`.
- Input `.Distinct(OrdinalIgnoreCase)` guarantees no two identical names in one call (→ no duplicate insert). The `.Local` check + link-row guard are belts for the cross-call and existing-row cases.

Read-side symmetry (D3): add a `Publishers` hydration loop in `MapToDomain` (mirror the existing `Designers` loop, guard non-blank `Name` → `sharedGame.AddPublisher(name)`) and add `.Include(g => g.Publishers)` next to the existing `.Include(g => g.Designers)` in `GetByIdAsync`, with `.AsSplitQuery()` (two collection includes → avoid the cartesian product).

## 6. No migration

The schema already has `game_designers`, `game_publishers`, the two join tables, and the unique indexes. This is a pure code change — **no EF migration**.

## 7. Testing strategy

Backend integration tests (Testcontainers Postgres). Fixture `SharedTestcontainersFixture`, `[Collection("Integration-GroupC")]`, `[Trait("Category", TestCategories.Integration)]`, `[Trait("BoundedContext", "SharedGameCatalog")]`, `IAsyncLifetime`, per-class isolated DB, `Database.MigrateAsync()`. Assertions via FluentAssertions.

**Repo-level** (`SharedGameRepository` get-or-create mechanics — the class of bug a mocked-repo unit test cannot catch, per DoD):

1. **New names created** — `CreateSkeleton` + `AddDesigner("Klaus Teuber")` + `AddPublisher("Kosmos")` → `AddAsync` + `SaveChanges` + `ChangeTracker.Clear()` → reload → assert both persisted; assert exactly one row in `game_designers` and `game_publishers`.
2. **Existing name reused (no duplicate insert)** — pre-seed `GameDesignerEntity "Klaus Teuber"` linked to another game; promote a new game with the same designer → assert **one** `game_designers` row for that name, both games linked. Core get-or-create correctness (would surface a unique-violation if broken).
3. **Case-insensitive reuse** — pre-seed `"Kosmos"`; promote with `"kosmos"` → assert reused (still one row), proving `ILIKE`.
4. **Publishers read-side** — after persistence, `GetByIdAsync` returns an aggregate with `Publishers` hydrated (proves D3).

**Handler-driven** (mandatory ≥1 real-pipeline test — a fixture-only DTO test masks the wiring gap that shipped #3147's bug):

5. **End-to-end promotion** — seed a `CatalogSeedDraft` entity whose `ProvenanceJson` contains `designers`/`publishers`; run `CatalogSeedApprovedEventHandler.Handle(new CatalogSeedApprovedEvent(...))` with real repos + `DbContext`; assert the resulting `SharedGame` has the join rows persisted. Exercises provenance-read → `EnrichFromProvenance` → `AddAsync` → resolve → join rows end-to-end.

**Aggregate unit tests** (fast, no DB) for `EnrichFromProvenance`: names trimmed, blank skipped, >200-char skipped (no throw), case-insensitive intra-aggregate dedup, `changed`/audit-stamp flips when a name is added, null lists are a no-op. Existing scalar-only `EnrichFromProvenance` unit tests get the two new params (pass `null`) — mechanical update (D6).

## 8. Out of scope

- Existing-game (BggId collision) designer/publisher backfill — the branch doesn't enrich (D5).
- Any `ISharedGameRepository` interface change / `Update`→`UpdateAsync` (D1).
- Concurrency/unique-violation retry (D2).
- Multi-value Wikidata designers/publishers (SPARQL projection emits a single label each) — unchanged.
- Consolidating the get-or-create duplication across `ReplaceDesignersAsync` / `RelationshipSeeder` / this repo — YAGNI; noted as future cleanup.

## 9. Files touched

| File | Change |
|---|---|
| `…/Domain/Aggregates/SharedGame.cs` | `EnrichFromProvenance` signature + body + XML remarks |
| `…/Application/EventHandlers/CatalogSeedApprovedEventHandler.cs` | Read `designers`/`publishers` from provenance + pass through + comment |
| `…/Infrastructure/Repositories/SharedGameRepository.cs` | `AddAsync` + `ResolveDesignersAsync`/`ResolvePublishersAsync`; `MapToDomain` Publishers hydration; `GetByIdAsync` `.Include(Publishers)` |
| `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/…` | Integration tests (repo-level + handler-driven) + `EnrichFromProvenance` unit tests |

## 10. Definition of Done (from the issue)

- [ ] Approving a Wikidata-seeded draft with designers/publishers persists the M2M join rows (existing names reused, new names created).
- [ ] Integration test (Testcontainers Postgres) — including ≥1 handler-driven test.
- [ ] Re-enable designers/publishers in `EnrichFromProvenance` + wire them back in `CatalogSeedApprovedEventHandler`.
