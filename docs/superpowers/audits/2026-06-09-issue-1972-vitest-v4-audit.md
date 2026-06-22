# Audit 2026-06-09 — #1972 vitest v4 migration file inventory

> **M1 deliverable** per Phase 2 vitest v4 migration plan. Source-of-truth file list + pattern category per file + priority ordering canary-first.

**Audit method**: `grep` on `apps/web/src` per 4 distinct breaking patterns dal body issue #1972, executed 2026-06-09 sess.46g.

**DEC-2d locked** (sess.46f spec-panel + user-locked): «M1 deliverable shipped as separate audit PR ~1h». This document is that deliverable.

---

## Summary

| Pattern | Description | File count | Source-of-truth |
|---|---|---|---|
| **Pattern 1** | `vi.fn().mockImplementation(() => ({...}))` not a constructor | **13** | Grep confirmed |
| **Pattern 2** | jsdom strict `instanceof Blob` for mocked uploads | **1+** | Body issue (PhotoUploadModal); CI run needed for full list |
| **Pattern 3** | `vi.spyOn(URL, 'revokeObjectURL')` without `Object.defineProperty` setup | **1** | Grep confirmed (helper file, not test) |
| **Pattern 4** | EventSource mock with `simulateOpen`/`simulateError` lifecycle | **15** | Grep confirmed (SSE patterns) |

**Total unique files affected**: ~25 (with overlap between Pattern 1 and Pattern 4 expected — TBD during refactor).

---

## Pattern 1 — `vi.fn().mockImplementation(() => ({...}))` constructor mock (13 files)

**Vitest v4 breaking change**: arrow-returning-object pattern non riconosciuto come constructor. Must refactor to `class Mock*` or named factory.

**DEC-2a locked**: codemod via jscodeshift per questo pattern.

| # | File | Pattern category | Notes |
|---|---|---|---|
| 1 | `apps/web/src/lib/api/__tests__/alert-config.api.test.ts` | Pattern 1 | API client mock |
| 2 | `apps/web/src/app/admin/(dashboard)/knowledge-base/__tests__/kb-hub-gaps.test.tsx` | Pattern 1 | Component mock |
| 3 | `apps/web/src/stores/contextual-hand/__tests__/store.test.ts` | Pattern 1 | Zustand store mock |
| 4 | `apps/web/src/lib/utils/__tests__/export.test.ts` | Pattern 1 | Utility mock |
| 5 | `apps/web/src/components/layout/QuickView/__tests__/RulesContent.test.tsx` | Pattern 1 | Component mock |
| 6 | `apps/web/src/components/layout/QuickView/__tests__/FaqContent.test.tsx` | Pattern 1 | Component mock |
| 7 | `apps/web/src/__tests__/app/admin/knowledge-base/mechanic-extractor/review.test.tsx` | Pattern 1 | Component mock |
| 8 | `apps/web/src/__tests__/app/admin/agents/usage-tabs.test.tsx` | Pattern 1 | Component mock |
| 9 | `apps/web/src/__tests__/app/admin/agents/definitions-builder.test.tsx` | Pattern 1 | Component mock |
| 10 | `apps/web/src/__tests__/app/admin/agents/config.test.tsx` | Pattern 1 | Component mock |
| 11 | `apps/web/src/__tests__/app/admin/agents/inspector.test.tsx` | Pattern 1 | Component mock |
| 12 | `apps/web/src/lib/domain-hooks/__tests__/useSessionSync.test.ts` | Pattern 1 + 4 (likely overlap) | SSE mock |
| 13 | `apps/web/src/lib/domain-hooks/__tests__/useSignalrSession.test.ts` | Pattern 1 + 4 (likely overlap) | SignalR mock |

**Refactor target** (DEC-2a B mid-ground):

```ts
// Before — Pattern 1 broken on vitest v4
vi.mock('module', () => ({
  default: vi.fn().mockImplementation(() => ({ method1, method2 }))
}));

// After — class-based constructor mock
class MockModule {
  method1() { /* ... */ }
  method2() { /* ... */ }
}
vi.mock('module', () => ({ default: MockModule }));
```

---

## Pattern 2 — Blob jsdom strictness

**Vitest v4 / jsdom upgrade**: stricter `instanceof Blob` check su mocked photo upload payloads.

| # | File | Notes |
|---|---|---|
| 1 | `apps/web/src/components/session/__tests__/PhotoUploadModal.test.tsx` | Body issue cited |
| ? | _TBD via CI run_ | Re-run Dependabot PR #1794 or local `pnpm test` post Pattern 1 fixes |

**Refactor target** (DEC-2a manual case-by-case):

```ts
// Before
const payload = { type: 'image/jpeg', size: 1024 };  // plain object

// After
const payload = new Blob(['fake-data'], { type: 'image/jpeg' });  // real Blob
```

---

## Pattern 3 — `vi.spyOn(URL, 'revokeObjectURL')` without setup

**Vitest v4 strict**: `URL.revokeObjectURL` is a global function; `vi.spyOn` fails without explicit `Object.defineProperty(URL, 'revokeObjectURL', { writable: true, value: vi.fn() })` in setup.

| # | File | Notes |
|---|---|---|
| 1 | `apps/web/src/lib/api/__tests__/httpClient.test-helpers.ts` | Helper file (not test). Used by `httpClient.test.ts` family |

**Refactor target** (DEC-2a manual):

Add to `vitest.setup.tsx` (global):

```ts
import { vi } from 'vitest';

Object.defineProperty(URL, 'revokeObjectURL', {
  writable: true,
  value: vi.fn(),
});

Object.defineProperty(URL, 'createObjectURL', {
  writable: true,
  value: vi.fn(() => 'blob:mock-url'),
});
```

Then `httpClient.test-helpers.ts` can use `vi.spyOn(URL, 'revokeObjectURL')` normally.

---

## Pattern 4 — EventSource mock lifecycle (15 files)

**Vitest v4 breaking change**: arrow-mock `() => ({})` cannot be used with `new` keyword. Components doing `new EventSource(url)` fail with «not a constructor».

| # | File | Notes |
|---|---|---|
| 1 | `apps/web/src/hooks/__tests__/usePdfStatus.test.ts` | **🎯 CANARY** — body issue cited as PoC starting point |
| 2 | `apps/web/src/hooks/__tests__/use-notifications-counter.test.tsx` | SSE notifications |
| 3 | `apps/web/src/lib/gamebook/hooks/__tests__/useTranslateSegmentSSE.test.tsx` | Gamebook SSE |
| 4 | `apps/web/src/components/admin/monitor/__tests__/use-live-events.test.ts` | Admin monitor |
| 5 | `apps/web/src/lib/session-live/__tests__/use-session-live-stream.test.ts` | Session live stream |
| 6 | `apps/web/src/hooks/__tests__/useSessionSSE.test.ts` | Session SSE |
| 7 | `apps/web/src/hooks/__tests__/useNotificationSSE.test.ts` | Notification SSE |
| 8 | `apps/web/src/lib/domain-hooks/__tests__/useWidgetSync.test.ts` | Widget sync SSE |
| 9 | `apps/web/src/components/session/__tests__/useSessionStream.test.ts` | Session stream |
| 10 | `apps/web/src/components/admin/layout/__tests__/PdfProcessingNotifier.test.tsx` | PDF SSE |
| 11 | `apps/web/src/hooks/__tests__/useWizardProgressStream.test.ts` | Wizard SSE |
| 12 | `apps/web/src/__tests__/utils/mockEventSource.ts` | **Shared helper** — refactor this once unlocks reuse |
| 13 | `apps/web/src/__tests__/helpers/uploadQueueMocks.ts` | Upload queue mock helper |

**Refactor target** (DEC-2a manual):

```ts
// Before — Pattern 4 broken on vitest v4
const mockEventSource = vi.fn().mockImplementation(() => ({
  simulateOpen: () => { /* ... */ },
  simulateError: () => { /* ... */ },
  close: vi.fn(),
}));
vi.stubGlobal('EventSource', mockEventSource);

// After — class-based EventSource mock extending EventTarget
class MockEventSource extends EventTarget {
  readyState = 0;
  url: string;
  withCredentials = false;
  constructor(url: string | URL) {
    super();
    this.url = url.toString();
  }
  simulateOpen() {
    this.readyState = 1;
    this.dispatchEvent(new Event('open'));
  }
  simulateError() {
    this.dispatchEvent(new Event('error'));
  }
  close() {
    this.readyState = 2;
  }
}
vi.stubGlobal('EventSource', MockEventSource);
```

---

## Priority order (DEC-2d "canary-first" ordering)

### Phase M2 — PoC canary (1 file, ~2h)

1. **`usePdfStatus.test.ts`** — body issue cited as canary. Validates Pattern 4 `class MockEventSource extends EventTarget` refactor works end-to-end. Once green, batch remaining Pattern 4 files via copy-paste.

### Phase M3 — Pattern 3 + Pattern 2 setup (1-2 files, ~3h)

2. `vitest.setup.tsx` — add `Object.defineProperty(URL, ...)` globals (unlocks Pattern 3 for `httpClient.test-helpers.ts` family)
3. `PhotoUploadModal.test.tsx` — Pattern 2 Blob refactor (manual case-by-case)

### Phase M4 — Pattern 1 jscodeshift batch (13 files, ~1gg)

4. Codemod via jscodeshift transformer on Pattern 1 files:
   ```bash
   npx jscodeshift -t transforms/arrow-mock-to-class.ts apps/web/src/**/*.test.{ts,tsx}
   ```
5. Manual review per ognuno (~15 min/file × 13 = ~3h)

### Phase M5 — Pattern 4 batch (12 remaining files + 2 helpers, ~1gg)

6. Apply `MockEventSource` class pattern via copy-paste from canary
7. Verify `mockEventSource.ts` shared helper refactored first (unblocks reuse)
8. Per-file lifecycle adjustments (simulateOpen vs simulateError vs custom)

### Phase M6 — Bump + cleanup (~1h)

9. Bump `vitest@4.1.0` exact pin in `apps/web/package.json` + companion packages
10. Remove `coverage.all: true` from `vitest.config.ts` + verify v4 equivalent (`coverage.include` esplicito già presente)
11. Run full CI shard suite locally

### Phase M7 — CI verde + monitoring (~4h)

12. CI verde su Frontend Tests shard 1/2/3 + Fast
13. Activate 7-day post-merge CI runtime alert (DEC-2c monitoring)

---

## Effort summary

| Phase | Files | Effort |
|---|---|---|
| M2 PoC canary | 1 | ~2h |
| M3 Pattern 3 + 2 setup | 2 | ~3h |
| M4 Pattern 1 codemod batch | 13 | ~1gg |
| M5 Pattern 4 batch | 14 | ~1gg |
| M6 bump + cleanup | 0 | ~1h |
| M7 CI + monitoring | 0 | ~4h |
| **Total** | **30 distinct** | **~2.5-3gg** (within DEC-2d budget estimate) |

---

## Risk callouts

- **Overlap Pattern 1 + Pattern 4**: 2 files (`useSessionSync.test.ts`, `useSignalrSession.test.ts`) have BOTH patterns. M4 codemod must NOT touch their `MockEventSource` class once introduced in M5.
- **`mockEventSource.ts` shared helper**: refactor FIRST (M5 step 1) so consumers in 11 test files inherit the new shape automatically.
- **Pattern 2 list incomplete**: real list comes from CI run post Pattern 1 fix. Re-audit after M4.

## CI gate verification (DEC-2b)

Before opening final implementation PR:

- [ ] Test count 575 same (or `560 + 15 explicit skip` if test removal justified)
- [ ] Coverage delta < 0.5pp drop vs main-dev baseline
- [ ] Runtime regression < +20% vs main-dev baseline
- [ ] All 4 shards green (no partial merge)
- [ ] Flake retry max 1× per test

## Cross-references

- Parent issue: #1972 (Phase 2 — DEC-2 locked)
- Plan section: `docs/superpowers/plans/2026-06-09-large-medium-remaining-plan.md` § Phase 2
- Sub-issue plan harden: #2053 (CLOSED post DEC lock)
- Pattern reference: P181 + P182 (audit deliverable separate PR before implementation start)
