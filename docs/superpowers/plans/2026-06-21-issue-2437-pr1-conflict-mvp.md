# #2437-1 — Conflict-UI stale-form (xmin end-to-end) + MVP chip — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Coprire il caso "form stale" sull'edit di un PlayRecord (decisione **C2**): il client legge lo `xmin` quando carica il record, lo rimanda al salvataggio, e se un altro utente ha modificato nel frattempo riceve un 409 con un dialog ricarica/sovrascrivi. Più il **MVP chip** (decisione **M3**: MVP = winner derivato) nella detail view.

**Architecture:** L'infra xmin è già in place (PR-B): `PlayRecord.Xmin` (uint, `IsConcurrencyToken`), repository `Update()` detached con xmin round-trip, middleware `DbUpdateConcurrencyException → 409 + X-Warning-Code: concurrent-edit`. Oggi l'update fa fresh-load → il check scatta solo su race concorrenti. Questo PR fa passare lo xmin **end-to-end** (pattern identico a `SharedGameTranslation`): l'handler chiama `record.SetXmin(command.Xmin)` con il valore dal client. **Robustezza**: xmin è **nullable** ovunque — se il client non lo fornisce (fixture/rollout) l'handler NON chiama SetXmin → fresh-load come oggi (retrocompatibile).

**Tech Stack:** .NET 9 (MediatR, EF Core xmin, Moq/xUnit, Testcontainers) · Next.js/React 19 (TanStack Query, Zod, Vitest).

**Spec:** `docs/superpowers/specs/2026-06-20-issue-2436-prc-2437-spec-panel.md` (#2437 sub-PR 1).

## Reuse map (verificata, file:line)
- **BE template xmin end-to-end**: `SharedGameTranslation` — handler `UpdateGameTranslationCommandHandler.cs:85` (`existing.SetXmin(cmd.Xmin)`); command `UpdateGameTranslationCommand.cs:18-24` (`uint Xmin`).
- **BE PlayRecord**: command `UpdatePlayRecordCommand.cs:10-16`; handler `UpdatePlayRecordCommandHandler.cs:46-53`; endpoint + `UpdateRecordRequest` `PlayRecordEndpoints.cs:201-211,308`; DTO `PlayRecordDto.cs`; query handler `GetPlayRecordQueryHandler.cs`; `PlayRecord.SetXmin` `PlayRecord.cs:54` (internal); repo già detached `PlayRecordRepository.cs:135-196,293,323`; middleware `ApiExceptionHandlerMiddleware.cs:109-115,327-352`.
- **BE test template**: `GameNightPlaylistRowVersionConcurrencyTests.cs` (scenario stale-form 409).
- **FE error class template**: `use-update-session-scores.ts:44-54` (`UpdateSessionScoresError` kind/status/details).
- **FE api client**: `play-records.api.ts:105-115` (`updateRecord`); schemas `play-records.schemas.ts:53-90,161-166`.
- **FE edit page**: `app/(authenticated)/play-records/[id]/edit/page.tsx:59-86` (`handleSubmit`).
- **FE mutation hook**: `usePlayRecords.ts:232-243` (`useUpdateRecord`).
- **FE MVP mount**: `ConnectionBar.tsx:23-30,89-127` (props + chips); `PlayRecordDetailView.tsx:327-333` (ConnectionBar usage), winner resolution `buildClassificaRows`.

---

## Task 1: BE — xmin end-to-end (nullable)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/UpdatePlayRecordCommand.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/UpdatePlayRecordCommandHandler.cs`
- Modify: `apps/api/src/Api/Routing/PlayRecordEndpoints.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/PlayRecords/PlayRecordDto.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetPlayRecordQueryHandler.cs`
- Test: `apps/api/tests/Api.Tests/Integration/GameManagement/PlayRecordConcurrencyTests.cs` (create)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/PlayRecords/GetPlayRecordQueryHandlerTests.cs` (extend)

- [ ] **Step 1: Expose `Xmin` on `PlayRecordDto`**

In `PlayRecordDto.cs`, add `uint Xmin` as the LAST positional parameter of `PlayRecordDto` (after `Photos`):
```csharp
    IReadOnlyList<PlayRecordPhotoDto> Photos,
    uint Xmin
);
```

- [ ] **Step 2: Map `Xmin` in the query handler + extend its unit test (TDD)**

In `GetPlayRecordQueryHandlerTests.cs`, add to the existing photo test (or a new one) an assertion that the DTO carries the entity xmin. Since EF InMemory doesn't populate xmin, set it explicitly on the entity in a new test:
```csharp
    [Fact]
    [Trait("Issue", "2437")]
    public async Task Handle_ExposesXminConcurrencyToken()
    {
        var recordId = Guid.NewGuid();
        var entity = MakePlayRecord(recordId);
        entity.Players = new List<RecordPlayerEntity> { MakePlayer(Guid.NewGuid(), recordId, ("wins", 1)) };
        entity.Xmin = 4242u;
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(new GetPlayRecordQuery(recordId, entity.CreatedByUserId), TestContext.Current.CancellationToken);

        result.Xmin.Should().Be(4242u);
    }
```
Run → FAIL (DTO has no Xmin / constructor arity). Then in `GetPlayRecordQueryHandler.cs`, pass `entity.Xmin` as the last argument of `new PlayRecordDto(...)`:
```csharp
            outcomeType,
            photos,
            entity.Xmin
        );
```
Run → PASS.

- [ ] **Step 3: Add nullable `Xmin` to the command + request + endpoint**

`UpdatePlayRecordCommand.cs` — add `uint? Xmin = null` as the last param:
```csharp
internal record UpdatePlayRecordCommand(
    Guid RecordId,
    Guid UserId,
    DateTime? SessionDate = null,
    string? Notes = null,
    string? Location = null,
    uint? Xmin = null
) : ICommand;
```

`PlayRecordEndpoints.cs:308` — add `uint? Xmin` to `UpdateRecordRequest`:
```csharp
private sealed record UpdateRecordRequest(DateTime? SessionDate, string? Notes, string? Location, uint? Xmin);
```

`PlayRecordEndpoints.cs:201-211` `HandleUpdateRecord` — pass `request.Xmin`:
```csharp
        var command = new UpdatePlayRecordCommand(recordId, httpContext.User.GetUserId(),
            request.SessionDate, request.Notes, request.Location, request.Xmin);
```

- [ ] **Step 4: Handler applies the client xmin (only when provided)**

`UpdatePlayRecordCommandHandler.cs` — after `record.UpdateDetails(...)` (line ~50) and before `UpdateAsync`, add:
```csharp
        // #2437-1: stale-form optimistic concurrency. When the client sends the xmin it read,
        // push it so EF's concurrency check (IsConcurrencyToken on xmin) compares against the
        // value the client saw — a concurrent edit then yields DbUpdateConcurrencyException → 409.
        // When absent (rollout / non-versioned callers), skip → fresh-load behaviour (no check).
        if (command.Xmin.HasValue)
        {
            record.SetXmin(command.Xmin.Value);
        }
```
(`PlayRecord.SetXmin` is `internal`; the handler is in assembly `Api` → accessible. Same as `SharedGameTranslation`.)

- [ ] **Step 5: Build green**

Run: `dotnet build D:/Repositories/meepleai-monorepo-frontend/apps/api/src/Api/Api.csproj 2>&1 | grep -E "error" || echo BUILD_OK`
Expected: `BUILD_OK`

- [ ] **Step 6: Integration test — stale-form yields 409 (TDD, Testcontainers)**

Read `GameNightPlaylistRowVersionConcurrencyTests.cs` first to mirror its fixture/setup. Create `PlayRecordConcurrencyTests.cs` that: (a) creates a PlayRecord via the API/handler; (b) reads it twice (two stale copies, capturing `xmin = X`); (c) updates once (xmin advances to Y in DB); (d) updates again with the stale `xmin = X` → asserts the second update throws `DbUpdateConcurrencyException` (or, if going through the HTTP pipeline, returns 409 with header `X-Warning-Code: concurrent-edit`). Also add a test that an update WITHOUT xmin (null) succeeds (fresh-load fallback, no conflict).

```csharp
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2437")]
public class PlayRecordConcurrencyTests : IntegrationTestBase  // mirror the base used by GameNightPlaylistRowVersionConcurrencyTests
{
    // ... fixture setup mirroring the GameNightPlaylist concurrency test ...

    [Fact]
    public async Task Update_WithStaleXmin_ThrowsConcurrency()
    {
        // Arrange: create a record, capture its xmin, mutate it once (xmin advances).
        // Act: second update reusing the stale xmin.
        // Assert: DbUpdateConcurrencyException (or 409 via the HTTP path).
    }

    [Fact]
    public async Task Update_WithoutXmin_SucceedsFreshLoad()
    {
        // Arrange: create a record. Act: update with command.Xmin == null. Assert: no throw.
    }
}
```
Fill the bodies by adapting the GameNightPlaylist test's create/update helpers to PlayRecord (use `UpdatePlayRecordCommand` with/without `Xmin`). Run the two tests → PASS.

Run: `dotnet test D:/Repositories/meepleai-monorepo-frontend/apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PlayRecordConcurrencyTests|FullyQualifiedName~GetPlayRecordQueryHandlerTests" 2>&1 | tail -8`
Expected: PASS (note: integration tests need Docker; if unavailable, document that the integration test is written but requires Docker to run, and ensure the unit test in Step 2 passes).

- [ ] **Step 7: Commit**
```bash
git add -A
git commit -m "feat(play-records): #2437-1 BE xmin end-to-end for stale-form 409"
```

---

## Task 2: FE — `xmin` on DTO + UpdateRequest schemas

**Files:**
- Modify: `apps/web/src/lib/api/schemas/play-records.schemas.ts`
- Test: `apps/web/src/lib/api/schemas/__tests__/play-records-photo.schema.test.ts` (extend) — or a new test file `play-records-xmin.schema.test.ts`

- [ ] **Step 1: Test (TDD)** — create `apps/web/src/lib/api/schemas/__tests__/play-records-xmin.schema.test.ts`:
```ts
import { describe, it, expect } from 'vitest';

import { PlayRecordDtoSchema, UpdatePlayRecordRequestSchema } from '../play-records.schemas';

const base = {
  id: '550e8400-e29b-41d4-a716-446655440000',
  gameId: null, gameName: 'Catan', sessionDate: '2026-06-21', duration: null,
  status: 'Completed' as const, players: [], scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
  createdByUserId: '550e8400-e29b-41d4-a716-446655440001', visibility: 'Private' as const,
  startTime: null, endTime: null, notes: null, location: null,
  createdAt: '2026-06-21T10:00:00Z', updatedAt: '2026-06-21T10:00:00Z',
};

describe('xmin schemas (#2437-1)', () => {
  it('PlayRecordDtoSchema accepts a numeric xmin and is optional', () => {
    expect(PlayRecordDtoSchema.parse(base).xmin).toBeUndefined();
    expect(PlayRecordDtoSchema.parse({ ...base, xmin: 4242 }).xmin).toBe(4242);
  });
  it('UpdatePlayRecordRequestSchema accepts optional xmin', () => {
    expect(UpdatePlayRecordRequestSchema.parse({ notes: 'x' }).xmin).toBeUndefined();
    expect(UpdatePlayRecordRequestSchema.parse({ notes: 'x', xmin: 7 }).xmin).toBe(7);
  });
});
```
Run → FAIL.

- [ ] **Step 2: Implement** — in `play-records.schemas.ts`, add `xmin: z.number().int().nonnegative().optional()` to BOTH `PlayRecordDtoSchema` (after `photos`) and `UpdatePlayRecordRequestSchema` (after `location`). Comment: `// #2437-1: Postgres xmin optimistic-concurrency token (uint). Optional during rollout.`
Run → PASS.

- [ ] **Step 3: Commit**
```bash
git add apps/web/src/lib/api/schemas/play-records.schemas.ts apps/web/src/lib/api/schemas/__tests__/play-records-xmin.schema.test.ts
git commit -m "feat(play-records): #2437-1 add xmin to DTO + update-request schemas"
```

---

## Task 3: FE — `UpdatePlayRecordError` + `updateRecord` sends xmin & detects 409

**Files:**
- Modify: `apps/web/src/lib/api/play-records.api.ts`
- Test: `apps/web/src/lib/api/__tests__/play-records-update-conflict.test.ts` (create)

- [ ] **Step 1: Test (TDD)** — create `play-records-update-conflict.test.ts`:
```ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { playRecordsApi, UpdatePlayRecordError } from '../play-records.api';

describe('playRecordsApi.updateRecord conflict handling (#2437-1)', () => {
  beforeEach(() => vi.stubGlobal('fetch', vi.fn()));
  afterEach(() => vi.unstubAllGlobals());

  it('sends xmin in the body when provided', async () => {
    const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
    fetchMock.mockResolvedValue({ ok: true, status: 204, headers: new Headers(), json: async () => ({}) });
    await playRecordsApi.updateRecord('r1', { notes: 'hi', xmin: 99 });
    const body = JSON.parse((fetchMock.mock.calls[0][1] as RequestInit).body as string);
    expect(body.xmin).toBe(99);
  });

  it('throws UpdatePlayRecordError kind=conflict on 409 with X-Warning-Code', async () => {
    const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
    fetchMock.mockResolvedValue({
      ok: false, status: 409,
      headers: new Headers({ 'X-Warning-Code': 'concurrent-edit' }),
      json: async () => ({ error: 'concurrent_edit' }),
    });
    await expect(playRecordsApi.updateRecord('r1', { notes: 'hi', xmin: 1 }))
      .rejects.toMatchObject({ kind: 'conflict', status: 409, warningCode: 'concurrent-edit' });
  });
});
```
Run → FAIL.

- [ ] **Step 2: Implement** — in `play-records.api.ts` add the error class (top-level export) and rewrite `updateRecord`:
```ts
export class UpdatePlayRecordError extends Error {
  constructor(
    message: string,
    public readonly kind: 'conflict' | 'forbidden' | 'validation' | 'server',
    public readonly status: number,
    public readonly warningCode?: string
  ) {
    super(message);
    this.name = 'UpdatePlayRecordError';
  }
}
```
```ts
  async updateRecord(recordId: string, updates: UpdatePlayRecordRequest): Promise<void> {
    const res = await fetch(`${BASE_URL}/${recordId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify(updates),
    });
    if (res.ok) return;
    const warningCode = res.headers.get('X-Warning-Code') ?? undefined;
    if (res.status === 409) {
      throw new UpdatePlayRecordError('Modifica concorrente rilevata.', 'conflict', 409, warningCode);
    }
    if (res.status === 403) {
      throw new UpdatePlayRecordError('Non hai i permessi per modificare.', 'forbidden', 403, warningCode);
    }
    if (res.status === 400) {
      throw new UpdatePlayRecordError('Dati non validi.', 'validation', 400, warningCode);
    }
    const err = await res.json().catch(() => ({ message: 'Failed to update record' }));
    throw new UpdatePlayRecordError(err.message || 'Failed to update record', 'server', res.status, warningCode);
  },
```
Run → PASS. Verify the existing `play-records.api` callers still typecheck.

- [ ] **Step 3: Commit**
```bash
git add apps/web/src/lib/api/play-records.api.ts apps/web/src/lib/api/__tests__/play-records-update-conflict.test.ts
git commit -m "feat(play-records): #2437-1 updateRecord sends xmin + typed 409 conflict error"
```

---

## Task 4: FE — `PlayRecordConflictDialog` (greenfield, simple)

**Files:**
- Create: `apps/web/src/components/play-records/PlayRecordConflictDialog.tsx`
- Test: `apps/web/src/components/play-records/__tests__/PlayRecordConflictDialog.test.tsx` (create)

> Greenfield, NOT reusing `editor/ConflictResolutionModal` (that one is RuleSpec-specific with local/remote/merge diffs — overkill here). Two actions: reload (discard my edits, show fresh data) / overwrite (force my edits).

- [ ] **Step 1: Test (TDD)** — create the test:
```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { PlayRecordConflictDialog } from '../PlayRecordConflictDialog';

const labels = {
  title: 'Modifica concorrente',
  description: 'Qualcuno ha modificato questa partita.',
  reload: 'Ricarica',
  overwrite: 'Sovrascrivi',
};

describe('PlayRecordConflictDialog', () => {
  it('fires onReload and onOverwrite', () => {
    const onReload = vi.fn();
    const onOverwrite = vi.fn();
    render(
      <PlayRecordConflictDialog open labels={labels} isOverwriting={false}
        onReload={onReload} onOverwrite={onOverwrite} />
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Ricarica' }));
    fireEvent.click(screen.getByRole('button', { name: 'Sovrascrivi' }));
    expect(onReload).toHaveBeenCalledOnce();
    expect(onOverwrite).toHaveBeenCalledOnce();
  });
});
```
Run → FAIL.

- [ ] **Step 2: Implement** — create `PlayRecordConflictDialog.tsx`:
```tsx
'use client';

import {
  Dialog,
  DialogContent,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';

export interface PlayRecordConflictDialogLabels {
  title: string;
  description: string;
  reload: string;
  overwrite: string;
}

export interface PlayRecordConflictDialogProps {
  open: boolean;
  labels: PlayRecordConflictDialogLabels;
  isOverwriting: boolean;
  onReload: () => void;
  onOverwrite: () => void;
}

export function PlayRecordConflictDialog({
  open,
  labels,
  isOverwriting,
  onReload,
  onOverwrite,
}: PlayRecordConflictDialogProps): React.JSX.Element {
  return (
    <Dialog open={open}>
      <DialogContent hideCloseButton>
        <DialogTitle>⚠️ {labels.title}</DialogTitle>
        <DialogDescription>{labels.description}</DialogDescription>
        <div className="flex justify-end gap-2 pt-2">
          <Button variant="outline" onClick={onReload} disabled={isOverwriting}>
            {labels.reload}
          </Button>
          <Button variant="destructive" onClick={onOverwrite} disabled={isOverwriting}>
            {labels.overwrite}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
```
(If `DialogContent` has no `hideCloseButton` prop, read the primitive and adapt; the dialog must render `role="dialog"`.)
Run → PASS.

- [ ] **Step 3: Commit**
```bash
git add apps/web/src/components/play-records/PlayRecordConflictDialog.tsx apps/web/src/components/play-records/__tests__/PlayRecordConflictDialog.test.tsx
git commit -m "feat(play-records): #2437-1 conflict resolution dialog (reload/overwrite)"
```

---

## Task 5: FE — wire conflict flow in the edit page + i18n

**Files:**
- Modify: `apps/web/src/app/(authenticated)/play-records/[id]/edit/page.tsx`
- Modify: `apps/web/src/locales/it.json`, `apps/web/src/locales/en.json`
- Modify: `apps/web/src/app/(authenticated)/play-records/[id]/edit/page.test.tsx` (extend)

- [ ] **Step 1: i18n** — add under `playRecords.edit` in both locales:
```json
"conflict": {
  "title": "Modifica concorrente",
  "description": "Qualcuno ha modificato questa partita mentre la stavi modificando. Ricarica i dati aggiornati o sovrascrivi con le tue modifiche.",
  "reload": "Ricarica",
  "overwrite": "Sovrascrivi comunque"
}
```
(en: "Concurrent edit" / "Someone modified this game while you were editing. Reload the latest data or overwrite with your changes." / "Reload" / "Overwrite anyway".)

- [ ] **Step 2: Test (TDD)** — read `page.test.tsx` first to learn its mock setup (usePlayRecord/useUpdateRecord/useTranslation). Add a test: when `updateMutation.mutateAsync` rejects with `new UpdatePlayRecordError('x','conflict',409,'concurrent-edit')`, submitting the form shows the conflict dialog (`getByRole('dialog')` with the conflict title). Mock `useUpdateRecord` so its `mutateAsync` rejects with that error. Run → FAIL.

- [ ] **Step 3: Implement** — modify `edit/page.tsx`:
  - Import `UpdatePlayRecordError` from `@/lib/api/play-records.api`, `playRecordsApi` (for the overwrite refetch), and `PlayRecordConflictDialog`.
  - Add state: `const [conflictForm, setConflictForm] = useState<SessionCreateFormData | null>(null);`
  - Extract a `submitUpdate(data, xmin)` helper that builds `{ sessionDate, notes, location, xmin }`, validates with `UpdatePlayRecordRequestSchema`, `await updateMutation.mutateAsync(validated)`, then invalidates + toast + `router.push`.
  - `handleSubmit(data)`: `try { await submitUpdate(data, record.xmin); } catch (e) { if (e instanceof UpdatePlayRecordError && e.kind === 'conflict') setConflictForm(data); else toast.error(...); }`
  - `handleReload()`: `setConflictForm(null); queryClient.invalidateQueries({ queryKey: ['play-records', 'detail', recordId] });` (form re-renders from fresh `initialValues`).
  - `handleOverwrite()`: `if (!conflictForm) return; const fresh = await playRecordsApi.getRecord(recordId); await submitUpdate(conflictForm, fresh.xmin); setConflictForm(null);` (force last-write-wins with the current xmin).
  - Render `<PlayRecordConflictDialog open={conflictForm !== null} isOverwriting={updateMutation.isPending} labels={{ title: t('playRecords.edit.conflict.title'), description: t('playRecords.edit.conflict.description'), reload: t('playRecords.edit.conflict.reload'), overwrite: t('playRecords.edit.conflict.overwrite') }} onReload={handleReload} onOverwrite={handleOverwrite} />`

Run the edit page test → PASS. Run typecheck.

- [ ] **Step 4: Commit**
```bash
git add apps/web/src/app/(authenticated)/play-records/[id]/edit/page.tsx apps/web/src/app/(authenticated)/play-records/[id]/edit/page.test.tsx apps/web/src/locales/it.json apps/web/src/locales/en.json
git commit -m "feat(play-records): #2437-1 wire conflict dialog in edit page (reload/overwrite)"
```

---

## Task 6: FE — MVP chip in the detail view

**Files:**
- Modify: `apps/web/src/components/play-records/detail/ConnectionBar.tsx`
- Modify: `apps/web/src/components/play-records/PlayRecordDetailView.tsx`
- Modify: `apps/web/src/locales/it.json`, `apps/web/src/locales/en.json`
- Modify: `apps/web/src/components/play-records/detail/__tests__/ConnectionBar.test.tsx` (extend)

> M3: MVP = derived winner. Chip appears ONLY when there is exactly ONE winner (no chip for cooperative/no-winner or multi-winner ties — the Classifica already shows co-winners). The orchestrator (DetailView) resolves the name and passes `mvpName` (keeps ConnectionBar pure).

- [ ] **Step 1: Test (TDD)** — in `ConnectionBar.test.tsx`, add: renders an MVP chip when `mvpName` is set (`getByText(/MVP/i)` contains the name); no MVP chip when `mvpName` is undefined/null. Run → FAIL.

- [ ] **Step 2: ConnectionBar** — add `readonly mvpName?: string | null;` to `ConnectionBarProps` and render a `'player'`-tinted chip after the date chip when `mvpName` is truthy:
```tsx
      {mvpName && (
        <span className={CHIP_BASE} style={chipStyle('player')} aria-label={`MVP: ${mvpName}`}>
          <span aria-hidden="true">🎯</span>
          MVP: {mvpName}
        </span>
      )}
```
(Destructure `mvpName` in the component signature.)

- [ ] **Step 3: DetailView resolves the MVP name** — in `PlayRecordDetailView.tsx`, before the `<ConnectionBar .../>` usage compute:
```tsx
  const winnerIds = record.winnerPlayerIds ?? [];
  const mvpName =
    winnerIds.length === 1
      ? (record.players.find(p => p.id === winnerIds[0])?.displayName ?? null)
      : null;
```
Pass `mvpName={mvpName}` to `<ConnectionBar .../>`. (Note: the chip uses a literal "MVP:" prefix in ConnectionBar for simplicity, matching the existing literal-Italian chips like "giocatori"/"Collegamenti" in that component. If i18n is desired later, lift to a label prop — out of scope here. Keep consistent with the component's current literal style.)

- [ ] **Step 4: Verify** — run `pnpm test src/components/play-records/detail/__tests__/ConnectionBar.test.tsx src/components/play-records/__tests__/PlayRecordDetailView.test.tsx --run` → PASS. Run axe test still green.

> i18n note: ConnectionBar currently uses hardcoded Italian literals ("Collegamenti", "giocatori", "Nessuna chat"). To stay consistent and avoid scope-creeping an i18n refactor of the whole component, the MVP chip uses the same literal style ("MVP:"). Skip the `playRecords.detail.mvpChip` i18n key (DO NOT add it) — revisit if ConnectionBar is fully i18n'd later.

- [ ] **Step 5: Commit**
```bash
git add apps/web/src/components/play-records/detail/ConnectionBar.tsx apps/web/src/components/play-records/PlayRecordDetailView.tsx apps/web/src/components/play-records/detail/__tests__/ConnectionBar.test.tsx
git commit -m "feat(play-records): #2437-1 MVP chip in connection bar (derived winner)"
```

---

## Final verification (after all tasks)
- `pnpm test src/components/play-records src/lib/api src/app/\(authenticated\)/play-records --run` → no regressions
- `pnpm typecheck && pnpm lint` → clean
- `dotnet build apps/api/src/Api/Api.csproj` → BUILD_OK; BE tests pass (integration needs Docker)

## Self-review notes (for the executor)
- **xmin is nullable everywhere** — verify the BE handler skips `SetXmin` when null (fresh-load fallback) and the FE sends `xmin` only when the DTO carried one. This keeps MSW fixtures (no xmin) working.
- **Overwrite re-fetches xmin** via `playRecordsApi.getRecord` before re-submitting (last-write-wins with the current token).
- **uint serialization**: xmin ≤ 4.29e9 < `Number.MAX_SAFE_INTEGER` → `z.number()` is safe.
- **MVP chip** stays a literal-Italian chip to match ConnectionBar's existing style; no new i18n key (avoid half-i18n'ing the component).
- **Closes** nothing yet — #2437 needs sub-PR 2 (share) + 3 (audit/restore) before it closes.
