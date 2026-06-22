# #2437-3 — Play Records version history + restore — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Tracciare la cronologia delle modifiche ai dettagli di un PlayRecord ("who changed what") e permettere al creator di ripristinare una versione precedente (last 5) — decisione **M1**. Questo è l'ultimo sub-PR di **#2437**: al suo merge, #2437 si chiude.

**Architecture:** Una singola tabella `play_record_versions` (decisione utente) cattura uno snapshot dei 3 campi editabili (`SessionDate`, `Notes`, `Location`) + chi/quando, **prima** di ogni update (così ogni versione è un punto di ripristino, incluso lo stato iniziale catturato al primo update). `GET /versions` espone la cronologia (audit-trail dei dettagli) + i restore points; `POST /restore/{n}` riapplica i valori di una versione via `UpdateDetails` (lo stesso percorso d'update, quindi il restore è esso stesso versionato → undo-able). Cap a 5 versioni per record. Pattern clonato da `ToolkitVersion` (GameToolkit). Restore + history sono **creator-only** (coerente con l'edit).

**Tech Stack:** .NET 9 (MediatR, EF Core, migration, Moq/xUnit/Testcontainers) · Next.js/React 19 (TanStack Query, Zod, Vitest).

**Spec:** `docs/superpowers/specs/2026-06-20-issue-2436-prc-2437-spec-panel.md` (#2437 sub-PR 3). **Decisioni utente 2026-06-21**: tabella unificata `play_record_versions`; versionare solo i 3 campi editabili.

## Reuse map (verificata, file:line)
- **Template version**: `ToolkitVersion.cs` + `ToolkitVersionEntity.cs` + `ToolkitVersionEntityConfiguration.cs` + `GetToolkitVersionsQueryHandler.cs` (GameToolkit) — tabella dedicata, snapshot-per-update, query DESC.
- **Migration pattern**: `20260620145331_AddPlayRecordPhotos.cs` (CreateTable + FK cascade + index).
- **PlayRecord innesto**: `PlayRecord.cs` (`UpdateDetails(sessionDate, notes, location, timeProvider)` @367-404 — già accetta i 3 campi); `UpdatePlayRecordCommandHandler.cs:33-54` (dove inserire lo snapshot pre-update); `PlayRecordRepository.cs`; `IPlayRecordRepository.cs`; `PlayRecordEndpoints.cs` (auth per-endpoint); `PlayRecordPermissionChecker.CanEditAsync` (creator check).
- **FE**: `PlayRecordDetailBody.tsx` (mount history button creator-only), `play-records.api.ts`, `usePlayRecords.ts` (`playRecordsKeys`), `@/components/ui/overlays/dialog`.

---

## Task 1: BE — `play_record_versions` persistence + repository + migration

**Files (create):** `Domain/Entities/PlayRecordVersion.cs`, `Infrastructure/Entities/GameManagement/PlayRecordVersionEntity.cs`, `Infrastructure/EntityConfigurations/GameManagement/PlayRecordVersionEntityConfiguration.cs`, `Domain/Repositories/IPlayRecordVersionRepository.cs`, `Infrastructure/Persistence/PlayRecordVersionRepository.cs`. **Modify:** `MeepleAiDbContext` (DbSet). Migration.

- [ ] **Step 1: Domain entity** (lightweight immutable snapshot, mirror `ToolkitVersion` style):
```csharp
namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>Immutable snapshot of a PlayRecord's editable details for version history + restore (#2437-3).</summary>
internal sealed class PlayRecordVersion : Entity<Guid>
{
    public Guid PlayRecordId { get; private set; }
    public int VersionNumber { get; private set; }
    public DateTime SessionDate { get; private set; }
    public string? Notes { get; private set; }
    public string? Location { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

#pragma warning disable CS8618
    private PlayRecordVersion() : base() { }
#pragma warning restore CS8618

    internal PlayRecordVersion(Guid id, Guid playRecordId, int versionNumber, DateTime sessionDate,
        string? notes, string? location, DateTime createdAt, Guid createdByUserId) : base(id)
    {
        if (playRecordId == Guid.Empty) throw new ArgumentException("PlayRecordId cannot be empty", nameof(playRecordId));
        PlayRecordId = playRecordId; VersionNumber = versionNumber; SessionDate = sessionDate;
        Notes = notes; Location = location; CreatedAt = createdAt; CreatedByUserId = createdByUserId;
    }
}
```

- [ ] **Step 2: Infra entity + config** — `PlayRecordVersionEntity` (Id, PlayRecordId, VersionNumber, SessionDate, Notes, Location, CreatedAt, CreatedByUserId + nav `PlayRecordEntity? PlayRecord`). Config: `ToTable("play_record_versions")`, `Notes` maxLen 2000, `Location` maxLen 255, FK to `play_records` cascade-delete, indexes:
```csharp
        builder.HasIndex(e => new { e.PlayRecordId, e.VersionNumber }).IsUnique().HasDatabaseName("UX_play_record_versions_record_version");
        builder.HasIndex(e => new { e.PlayRecordId, e.CreatedAt }).HasDatabaseName("IX_play_record_versions_record_createdat");
```
Add `public DbSet<PlayRecordVersionEntity> PlayRecordVersions => Set<PlayRecordVersionEntity>();` to `MeepleAiDbContext` (find the existing PlayRecord DbSets and add alongside).

- [ ] **Step 3: Repository** — `IPlayRecordVersionRepository`:
```csharp
    Task<int> GetNextVersionNumberAsync(Guid playRecordId, CancellationToken ct = default);
    Task AddAsync(PlayRecordVersion version, CancellationToken ct = default);
    Task<IReadOnlyList<PlayRecordVersion>> GetRecentAsync(Guid playRecordId, int limit, CancellationToken ct = default);
    Task<PlayRecordVersion?> GetByVersionNumberAsync(Guid playRecordId, int versionNumber, CancellationToken ct = default);
    Task PruneOldestAsync(Guid playRecordId, int keep, CancellationToken ct = default);
```
Impl (`RepositoryBase`): `GetNextVersionNumberAsync` = `(max VersionNumber for record) + 1` (or 1 if none); `AddAsync` maps domain→entity + `DbContext.PlayRecordVersions.AddAsync`; `GetRecentAsync` = `AsNoTracking().Where(PlayRecordId).OrderByDescending(VersionNumber).Take(limit)` → map; `GetByVersionNumberAsync` = single; `PruneOldestAsync` = delete rows beyond the `keep` most-recent (`OrderByDescending(VersionNumber).Skip(keep)` → RemoveRange). Register `IPlayRecordVersionRepository` in `GameManagementServiceExtensions` (mirror `IPlayRecordRepository` registration).

- [ ] **Step 4: Migration** — from `apps/api/src/Api`: `dotnet ef migrations add AddPlayRecordVersions`. Verify CreateTable `play_record_versions` + FK cascade + the 2 indexes (unique + createdat). Don't edit old migrations.

- [ ] **Step 5: Build + commit**
```bash
dotnet build .../Api.csproj 2>&1 | grep -E "error" || echo BUILD_OK
git add -A && git commit -m "feat(play-records): #2437-3 BE play_record_versions table + repository"
```

---

## Task 2: BE — snapshot pre-update + last-5 cap

**Files:** modify `UpdatePlayRecordCommandHandler.cs`; create a small `PlayRecordVersioning` helper (optional) for reuse by the restore handler. Test: extend handler test.

- [ ] **Step 1: Snapshot in the update handler** — in `UpdatePlayRecordCommandHandler.Handle`, inject `IPlayRecordVersionRepository`. BEFORE `record.UpdateDetails(...)`, capture the CURRENT (pre-edit) state as a new version:
```csharp
        // #2437-3: snapshot the pre-edit state so this update is restorable. Captured BEFORE
        // mutating, so each version is a prior state (the first update captures the initial state).
        var versionNumber = await _versionRepository.GetNextVersionNumberAsync(command.RecordId, cancellationToken).ConfigureAwait(false);
        await _versionRepository.AddAsync(new PlayRecordVersion(
            Guid.NewGuid(), record.Id, versionNumber, record.SessionDate, record.Notes, record.Location,
            _timeProvider.GetUtcNow().UtcDateTime, command.UserId), cancellationToken).ConfigureAwait(false);

        record.UpdateDetails(command.SessionDate, command.Notes, command.Location, _timeProvider);
        if (command.Xmin.HasValue) record.SetXmin(command.Xmin.Value);

        await _recordRepository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Keep only the 5 most-recent versions per record.
        await _versionRepository.PruneOldestAsync(command.RecordId, 5, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
```
(Confirm `record.SessionDate`/`Notes`/`Location` getters are public — they are.)

- [ ] **Step 2: Test** — extend/add a handler unit test (Moq `IPlayRecordVersionRepository`): updating a record calls `AddAsync` with the PRE-edit values + `PruneOldestAsync(_, 5)`. Run → PASS. Build green.

- [ ] **Step 3: Commit**
```bash
git add -A && git commit -m "feat(play-records): #2437-3 snapshot pre-update version + last-5 cap"
```

---

## Task 3: BE — get versions query + endpoint

**Files (create):** `Application/DTOs/PlayRecords/PlayRecordVersionDto.cs`, `Application/Queries/PlayRecords/GetPlayRecordVersionsQuery.cs` + `Handler`. **Modify:** `PlayRecordEndpoints.cs`. Test.

- [ ] **Step 1: DTO** — `public record PlayRecordVersionDto(int VersionNumber, DateTime SessionDate, string? Notes, string? Location, DateTime CreatedAt, Guid CreatedByUserId);`

- [ ] **Step 2: Query + handler (creator-only)** — `GetPlayRecordVersionsQuery(Guid RecordId, Guid UserId) : IQuery<IReadOnlyList<PlayRecordVersionDto>>`. Handler: inject `IPlayRecordVersionRepository` + `PlayRecordPermissionChecker`. Check `CanEditAsync(UserId, RecordId)` → else `ForbiddenException`. Return `GetRecentAsync(RecordId, 5)` mapped to DTOs (DESC). If the record doesn't exist, `CanEditAsync` returns false → 403 (acceptable; or add an existence check → 404).

- [ ] **Step 3: Endpoint** — `PlayRecordEndpoints.cs` Queries region:
```csharp
        group.MapGet("/play-records/{recordId:guid}/versions", HandleGetVersions)
            .RequireAuthenticatedUser()
            .Produces<IReadOnlyList<PlayRecordVersionDto>>(200).Produces(401).Produces(StatusCodes.Status403Forbidden)
            .WithTags("PlayRecords").WithSummary("Get a play record's version history (creator-only, last 5)");
```
```csharp
    private static async Task<IResult> HandleGetVersions(Guid recordId, [FromServices] IMediator mediator, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPlayRecordVersionsQuery(recordId, httpContext.User.GetUserId()), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }
```

- [ ] **Step 4: Test + commit** — handler unit test (creator gets list, non-creator → Forbidden). Build green.
```bash
git add -A && git commit -m "feat(play-records): #2437-3 BE get version history query + endpoint"
```

---

## Task 4: BE — restore command + endpoint + integration test

**Files (create):** `Application/Commands/PlayRecords/RestorePlayRecordVersionCommand.cs` + `Handler` + `Validator`. **Modify:** `PlayRecordEndpoints.cs`. Integration test.

- [ ] **Step 1: Command + handler (creator-only)** — `RestorePlayRecordVersionCommand(Guid RecordId, int VersionNumber, Guid UserId) : ICommand`. Handler: inject `IPlayRecordRepository`, `IPlayRecordVersionRepository`, `IUnitOfWork`, `TimeProvider`, `PlayRecordPermissionChecker`. Flow:
  1. Load record (`GetByIdAsync` or `NotFoundException`).
  2. `CanEditAsync(UserId, RecordId)` → else `ForbiddenException`.
  3. Load the target version (`GetByVersionNumberAsync(RecordId, VersionNumber)` or `NotFoundException("PlayRecordVersion", ...)`)
  4. Snapshot the CURRENT state as a new version (so the restore is undo-able) — same as the update handler (GetNextVersionNumber + AddAsync with current values + userId).
  5. `record.UpdateDetails(target.SessionDate, target.Notes, target.Location, _timeProvider);`
  6. `UpdateAsync` + `SaveChangesAsync`; then `PruneOldestAsync(RecordId, 5)` + save.
  Validator: `RecordId`/`UserId` NotEmpty; `VersionNumber` GreaterThan 0.

> Steps 4-5 duplicate the snapshot logic from Task 2. Extract a small internal helper `PlayRecordVersionSnapshotter.SnapshotCurrentAsync(IPlayRecordVersionRepository repo, PlayRecord record, Guid userId, TimeProvider tp, CancellationToken ct)` and call it from BOTH the update handler and the restore handler (DRY).

- [ ] **Step 2: Endpoint**
```csharp
        group.MapPost("/play-records/{recordId:guid}/restore/{versionNumber:int}", HandleRestoreVersion)
            .RequireAuthenticatedUser()
            .Produces(204).Produces(401).Produces(StatusCodes.Status403Forbidden).Produces(404)
            .WithTags("PlayRecords").WithSummary("Restore a play record to a previous version (creator-only)");
```
```csharp
    private static async Task<IResult> HandleRestoreVersion(Guid recordId, int versionNumber, [FromServices] IMediator mediator, HttpContext httpContext, CancellationToken cancellationToken)
    {
        await mediator.Send(new RestorePlayRecordVersionCommand(recordId, versionNumber, httpContext.User.GetUserId()), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
```

- [ ] **Step 3: Integration test** (Testcontainers, mirror `PlayRecordConcurrencyTests`): create a record (notes="A"); update notes="B" (→ version 1 captures "A"); update notes="C" (→ version 2 captures "B"); `GET versions` returns 2 entries DESC; `restore` version 1 → record notes == "A"; verify a new version was captured (the pre-restore "C"); the >5 cap holds after 6 updates. Run → PASS (Docker).

- [ ] **Step 4: Build + tests + commit**
```bash
git add -A && git commit -m "feat(play-records): #2437-3 BE restore version command + endpoint"
```

---

## Task 5: FE — versions API client + hooks

**Files:** modify `play-records.api.ts`, `play-records.schemas.ts`, `usePlayRecords.ts`. Tests.

- [ ] **Step 1: schema** — `PlayRecordVersionSchema = z.object({ versionNumber: z.number().int(), sessionDate: z.string(), notes: z.string().nullable(), location: z.string().nullable(), createdAt: z.string(), createdByUserId: z.string().uuid() })` + type.

- [ ] **Step 2: api** (all `credentials: 'include'` — authenticated):
  - `getVersions(recordId): Promise<PlayRecordVersion[]>` → `GET /play-records/{id}/versions`
  - `restoreVersion(recordId, versionNumber): Promise<void>` → `POST /play-records/{id}/restore/{versionNumber}`
  TDD per method.

- [ ] **Step 3: hooks** — `usePlayRecordVersions(recordId, enabled)` (query, key `playRecordsKeys.versions(recordId)` = `['play-records','versions',recordId]`, enabled) + `useRestorePlayRecordVersion(recordId)` (mutation, on success invalidate `detail(recordId)` + `versions(recordId)`). TDD.

- [ ] **Step 4: commit**
```bash
git add -A && git commit -m "feat(play-records): #2437-3 versions API client + hooks"
```

---

## Task 6: FE — history dialog + creator-only button + i18n

**Files:** create `apps/web/src/components/play-records/PlayRecordHistoryDialog.tsx`; modify `PlayRecordDetailBody.tsx`; i18n it/en. Tests.

- [ ] **Step 1: Dialog** — `PlayRecordHistoryDialog({ recordId, open, onClose })`: `usePlayRecordVersions(recordId, open)` → list of versions (each row: "v{n} · {date} · {who?}" + the snapshotted notes/location preview + a "Ripristina" button → `useRestorePlayRecordVersion`). Empty state when no versions. On successful restore → success toast + close. Labels via `useTranslation`. (Showing `createdByUserId` raw Guid is not user-friendly; for MVP show the date + "Tu"/relative — or just date + values. Keep it simple: date + notes/location preview + restore button. The "who" is available but resolving Guid→name is out of scope; display the date as the primary audit signal.)

- [ ] **Step 2: Wire button in DetailBody** — in the creator-only area (near the share/photo buttons), add a "🕘 Cronologia" button that opens the history dialog. Mount `{isCreator && <PlayRecordHistoryDialog .../>}`.

- [ ] **Step 3: i18n** — `playRecords.history.*` (it/en): button, dialogTitle, dialogDescription, version (`"v{n}"`), restore, restoring, empty, restored, errorRestore.

- [ ] **Step 4: Verify + commit**
- `pnpm test src/components/play-records src/lib/api src/lib/domain-hooks --run` → PASS (incl. existing DetailView/axe)
- `pnpm typecheck && pnpm lint` → clean
```bash
git add -A && git commit -m "feat(play-records): #2437-3 history dialog + creator-only button + i18n"
```

---

## Final verification
- `pnpm test src/components/play-records src/lib/api src/lib/domain-hooks --run` → no regressions
- `pnpm typecheck && pnpm lint` → clean
- `dotnet build apps/api/src/Api/Api.csproj` → BUILD_OK; BE tests pass (integration needs Docker)

## Self-review notes
- **Pre-update snapshot** means version N holds the state BEFORE update N. The first update captures the initial state, so restore can always reach any prior state. The current live state is the record itself (not a version).
- **Restore is versioned**: restoring snapshots the current state first → undo-able. This means a restore consumes a version slot (cap 5 still applies).
- **Cap 5**: `PruneOldestAsync` keeps the 5 most-recent by VersionNumber. VersionNumber is monotonic (max+1), never reused.
- **Creator-only**: both history (GET) and restore (POST) gate on `CanEditAsync` (creator). Consistent with edit being creator-only.
- **DRY**: the snapshot logic is shared between update + restore handlers via `PlayRecordVersionSnapshotter`.
- **Closes #2437** when merged — this is the final sub-PR. The PR body should say `Closes #2437`.
