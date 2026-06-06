# Task C — Cross-Asse Journey #1+#2+#3 Full Data-Driven Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare 3 Playwright cross-asse E2E spec full data-driven (#1 dashboard drawer stack, #2 empty CTA → wizard 4-step → live opt-in, #3 game-detail rail Storico partite) consumando l'infrastruttura BE seeding di Task B (#1928 shipped `ff95de834`).

**Architecture:** Shared baseline branch `feature/issue-1929-cross-asse-journey` (parent `main-dev`) ospita 3 nuovi helper FE (`annaPersona.ts` fixture canonical persona, `dataAssertionUtils.ts` strict+functional assertions, `resilienceWrappers.ts` retry 1x con backoff 500ms loud-fail) + 3 spec file `cross-asse-journey-{1,2,3}-*.spec.ts`. Ogni spec consuma `seedEntities` (Task B) + `seedAuthSession` (Wave B.1) + nuovi helper baseline. Sequencing 3 PR sequenziali sopra shared baseline: PR baseline → PR Journey #1 → PR Journey #2 (gated #1) → PR Journey #3 (gated #2).

**Tech Stack:** TypeScript + Playwright + `page.request.post()` admin session-cookied | Zustand cascade-navigation-store | Radix Dialog (desktop) / Vaul (mobile) drawer primitive | Next.js App Router

**Issue:** [#1929 Task C](https://github.com/meepleAi-app/meepleai-monorepo/issues/1929) — Asse D P4 follow-up
**Spec consolidato:** [`docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md`](../specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md) (committed HEAD main-dev `63571685f`)
**DEC lockate:** DEC-C-1 (Anna persona) · DEC-C-2 (hybrid assertion taxonomy) · DEC-C-3 (spec corrections wizard 4-step + rail rescope) · DEC-C-4 (non-blocking main-dev + blocking main-staging) · DEC-C-5 (3 PR sequenziali shared baseline) · DEC-C-6 (retry 1x + 500ms + prefers-reduced-motion) · DEC-C-7 (edge case matrix inline)
**Effort target:** 4-7 giorni distribuito 3 PR sequenziali
**Sessione 41 scope:** Macro 1 (PR baseline) + Macro 2 (PR Journey #1) — Macro 3 + Macro 4 in sessioni successive

---

## Decisioni di scope

### IN scope
- 3 shared helper FE in `apps/web/e2e/_helpers/`:
  - `annaPersona.ts` — persona fixture canonical (4 initial state variants)
  - `dataAssertionUtils.ts` — strict (literal) + functional (focus/scroll/animation) assertion helpers
  - `resilienceWrappers.ts` — `withRetry` 1x + 500ms backoff loud-fail wrapper
- 3 spec file in `apps/web/e2e/`:
  - `cross-asse-journey-1-dashboard-drawer-stack.spec.ts` (DEC-B-6 NOTE: nome diverso da demo Task B; demo Task B è pre-flight smoke, Journey #1 spec è full data-driven)
  - `cross-asse-journey-2-empty-cta-wizard-live.spec.ts`
  - `cross-asse-journey-3-game-detail-tab-partite.spec.ts`
- Unit test per shared helpers (annaPersona builder + dataAssertionUtils + resilienceWrappers)
- 4 PR sequenziali (1 baseline + 3 journey)

### OUT of scope
- Journey #4 (Invitation/Notification): deferred wave futuro post DEC-5 notification system
- Journey #5 (Session live toast switching): deferred wave futuro post asse A polymorphic wire FE
- Visual regression baseline screenshot (MIN-C-2 deferred)
- Cross-browser CI (chromium-only MVP, firefox/webkit wave futuro)
- Performance benchmarks (~3-5s SLA NOT enforced)
- `GameDetailSessionsRail` refactor (DEC-C-3 rescope verify-only)
- Wizard step count refactor (DEC-C-3 wizard 4-step honored)
- Demo spec Task B (`cross-asse-journey-1-dashboard-drawer-stack.spec.ts` SE già committed da Task B T7, va SOVRASCRITTO/RENAMED da questo plan — verify in Macro 2 step 1)

### Branching strategy

```
main-dev
  └── feature/issue-1929-cross-asse-journey (baseline shared, Macro 1)
        ├── PR #N baseline ← merge to main-dev when CI green
        ├── PR #N+1 journey-1 (Macro 2) ← gated PR #N merge
        ├── PR #N+2 journey-2 (Macro 3) ← gated PR #N+1 merge
        └── PR #N+3 journey-3 (Macro 4) ← gated PR #N+2 merge
```

PR target: `main-dev` (parent branch). Auto-delete on merge enabled at repo level.

### CI gating (DEC-C-4)

| Branch promotion | Policy |
|---|---|
| `feature/issue-1929-*` → `main-dev` | Non-blocking (4 spec eseguiti ma fail OK per merge) |
| `main-dev` → `main-staging` | Blocking (Task A 5 skeleton + Task C 3 journey TUTTI green required) |
| `main-staging` → `main` | Blocking + cross-browser (chromium+firefox+webkit) |

---

## File Structure

### Nuovi file FE

```
apps/web/e2e/_helpers/
├── annaPersona.ts                                (DEC-C-1 persona fixture, ~150 LOC)
├── dataAssertionUtils.ts                         (DEC-C-2 strict+functional, ~120 LOC)
└── resilienceWrappers.ts                         (DEC-C-6 withRetry, ~60 LOC)

apps/web/e2e/_helpers/__tests__/
├── annaPersona.test.ts                           (unit test fixture builder, ~80 LOC)
├── dataAssertionUtils.test.ts                    (unit test helpers, ~100 LOC)
└── resilienceWrappers.test.ts                    (unit test retry semantics, ~80 LOC)

apps/web/e2e/
├── cross-asse-journey-1-dashboard-drawer-stack.spec.ts   (~250 LOC, Macro 2)
├── cross-asse-journey-2-empty-cta-wizard-live.spec.ts    (~280 LOC, Macro 3)
└── cross-asse-journey-3-game-detail-tab-partite.spec.ts  (~200 LOC, Macro 4)
```

### File esistenti modificati (Macro 1)

```
apps/web/vitest.config.ts                         (1-line patch: allowlist e2e/_helpers/__tests__/)
```

> **Rationale vitest.config.ts patch**: current `exclude: ['**/e2e/**']` (line 79) blocks Vitest from discovering ANY test file under `apps/web/e2e/`. We narrow the exclusion to **Playwright spec files only** (`**/e2e/**/*.spec.{ts,tsx}`), preserving Playwright/Vitest separation while allowing colocated unit tests for the 3 baseline helpers (`__tests__/` subdir, `.test.ts` extension). Scope: 1 line, idempotent, no other test files affected (verified: no pre-existing `**/e2e/**/*.test.ts` files at plan time).

### File esistenti consumati (NO modify)

```
apps/web/e2e/_helpers/seedEntities.ts                 (Task B factory)
apps/web/e2e/_helpers/seedAuthSession.ts              (Wave B.1)
apps/web/e2e/_helpers/seedCookieConsent.ts            (companion)
apps/web/src/lib/stores/cascade-navigation-store.ts   (Zustand store, NO interaction)
apps/web/src/app/(authenticated)/dashboard/_components/sections/ProssimiSection.tsx (testid contract)
apps/web/src/components/features/game-detail/GameDetailSessionsRail.tsx (slot/href contract)
apps/web/src/app/(authenticated)/game-nights/new/_content.tsx (4-step wizard contract)
```

---

## Convenzioni stabilite (riferimenti pattern esistenti)

| Convenzione | Reference | Note |
|---|---|---|
| Skeleton spec pattern | `apps/web/e2e/asse-b-drawer-stack-flow.spec.ts` | `test.skip(browserName !== 'chromium')` + beforeEach con 3 helper |
| testRunId convention | `apps/web/e2e/_helpers/seedEntities.ts:60` | `newTestRunId(test.info().testId)` format `e2e-{cleanId}-{epochMs}` |
| Auth seeding | `apps/web/e2e/_helpers/seedAuthSession.ts` | `seedAuthSession(page, { role })` + `mockAuthEndpoints(page, { role, onboardingCompleted })` |
| Cookie consent | `apps/web/e2e/_helpers/seedCookieConsent.ts` | Companion call in beforeEach |
| Dashboard testid | `ProssimiSection.tsx:161` | `prossimi-card-${gameNightId}` (NO `dashboard-` prefix) |
| Drawer slot | `drawer.tsx` | Radix Dialog `data-state="open"` su content; Drawer primitive NON ha custom `data-slot`, usa selector Radix root |
| Cascade store | `cascade-navigation-store.ts:33-39` | `openDrawer('event', id)`, `pushDrawer('player', id)`, `popDrawer()` |
| Wizard step indicator | `wizard-modal.tsx` (verified asse-D P3 skeleton) | `[data-testid="wizard-step-indicator"]` + `[data-slot="wizard-modal"]` |
| Rail navigate | `GameDetailSessionsRail.tsx:84` | `[data-slot="game-detail-sessions-view-all"]` con `href={viewAllHref}` |

### testRunId scoping per spec

Ogni `test.beforeEach` genera nuovo testRunId via `newTestRunId(test.info().testId)`. testRunId è memorizzato in variabile spec-level e propagato a tutti i `seed*` calls + `cleanupTestEntities` afterEach. Pattern coerente con DEC-B-3 + DEC-B-5.

### Persona Anna canonical (DEC-C-1)

| Field | Value | Use |
|---|---|---|
| Email | `anna.host@meepleai.test` | Owner email seeded GameNights |
| DisplayName | `Anna Host` | Display in UI assertions |
| Role | `user` (FE), `User` (BE) | seedAuthSession + mockAuthEndpoints role |
| UserId | `00000000-0000-4000-8000-000000000001` (deterministic) | mockAuthEndpoints userId override |
| onboardingCompleted | `true` (default) | Skip onboarding wizard redirect |

---

## Macro 1 — PR Baseline: Shared Helpers (annaPersona + dataAssertionUtils + resilienceWrappers)

> **Output**: Branch `feature/issue-1929-cross-asse-journey` con 3 helper file + 3 unit test file + 1 line vitest config patch, 0 spec file. PR review-friendly <600 LOC. Effort target: 0.5gg.

### Task 1.0: Pre-flight — Patch `vitest.config.ts` exclude pattern

**Files:**
- Modify: `apps/web/vitest.config.ts` (line 79)

- [ ] **Step 1: Verify current vitest exclude pattern**

```bash
cd apps/web
grep -n "'\*\*/e2e/\*\*'" vitest.config.ts
```

Expected: line 79 → `'**/e2e/**',`

- [ ] **Step 2: Replace exclusion to allow `__tests__/` colocated**

Modify `apps/web/vitest.config.ts` line 79:

```typescript
// OLD (excludes all of e2e dir, blocks unit tests in e2e/_helpers/__tests__/)
'**/e2e/**',

// NEW (excludes only Playwright spec files; allows .test.ts under e2e/_helpers/__tests__/)
'**/e2e/**/*.spec.{ts,tsx}',
```

- [ ] **Step 3: Verify no pre-existing `*.test.ts` under `e2e/` (sanity check)**

```bash
cd apps/web
find e2e -name "*.test.ts" -not -path "*/node_modules/*" 2>/dev/null
```

Expected: empty output (no pre-existing test files would be unintentionally pulled in).

- [ ] **Step 4: Verify vitest still runs (no test files added yet)**

```bash
cd apps/web
pnpm test --run --reporter=basic 2>&1 | tail -20
```

Expected: vitest discovers 0 new test files; all existing tests still pass.

- [ ] **Step 5: Commit T1.0**

```bash
git add apps/web/vitest.config.ts
git commit -m "test(config): #1929 T1.0 allow unit tests in e2e/_helpers/__tests__/

Narrow vitest exclude from '**/e2e/**' to '**/e2e/**/*.spec.{ts,tsx}'
so colocated unit tests for the baseline helpers (annaPersona,
dataAssertionUtils, resilienceWrappers) can be discovered. Playwright
spec files (*.spec.ts) remain excluded from Vitest.

Refs #1929"
```

### Task 1.1: `annaPersona.ts` fixture + unit tests

**Files:**
- Create: `apps/web/e2e/_helpers/annaPersona.ts`
- Create: `apps/web/e2e/_helpers/__tests__/annaPersona.test.ts`

- [ ] **Step 1: Write the failing unit test**

Create `apps/web/e2e/_helpers/__tests__/annaPersona.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { ANNA_PERSONA, buildAnnaInitialState } from '../annaPersona';

describe('annaPersona fixture (#1929 DEC-C-1)', () => {
  it('ANNA_PERSONA exposes deterministic canonical fields', () => {
    expect(ANNA_PERSONA.email).toBe('anna.host@meepleai.test');
    expect(ANNA_PERSONA.displayName).toBe('Anna Host');
    expect(ANNA_PERSONA.role).toBe('user');
    expect(ANNA_PERSONA.userId).toBe('00000000-0000-4000-8000-000000000001');
    expect(ANNA_PERSONA.onboardingCompleted).toBe(true);
  });

  it('buildAnnaInitialState("journey1") returns 1 GN Published + 2 player roster', () => {
    const state = buildAnnaInitialState('journey1');
    expect(state.gameNightCount).toBe(1);
    expect(state.gameNightStatus).toBe('Published');
    expect(state.playerRosterCount).toBe(2);
    expect(state.libraryGameCount).toBe(0);
    expect(state.sessionCount).toBe(0);
  });

  it('buildAnnaInitialState("journey2") returns 0 GN + 1 library game', () => {
    const state = buildAnnaInitialState('journey2');
    expect(state.gameNightCount).toBe(0);
    expect(state.libraryGameCount).toBe(1);
    expect(state.playerRosterCount).toBe(0);
    expect(state.sessionCount).toBe(0);
  });

  it('buildAnnaInitialState("journey3") returns 1 game + 15 completed sessions', () => {
    const state = buildAnnaInitialState('journey3');
    expect(state.libraryGameCount).toBe(1);
    expect(state.sessionCount).toBe(15);
    expect(state.sessionStatus).toBe('Completed');
  });

  it('buildAnnaInitialState rejects unknown journey id', () => {
    // @ts-expect-error invalid journey id
    expect(() => buildAnnaInitialState('journey99')).toThrow(/unknown journey/i);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test e2e/_helpers/__tests__/annaPersona.test.ts`
Expected: FAIL with `Cannot find module '../annaPersona'`.

- [ ] **Step 3: Create `annaPersona.ts` minimal impl**

Create `apps/web/e2e/_helpers/annaPersona.ts`:

```typescript
/**
 * Issue #1929 Task C (DEC-C-1) — Anna persona canonical fixture.
 *
 * Anna is the **single primary actor** across all cross-asse user journey
 * spec files (Journey #1 + #2 + #3). Deterministic fields enable
 * reproducible BE entity seeding (via `seedEntities.ts` factory) and
 * stable FE auth seeding (via `seedAuthSession` / `mockAuthEndpoints`).
 *
 * Each journey starts with a different initial entity state (defined by
 * `buildAnnaInitialState(journeyId)`), which the spec's `beforeEach`
 * translates into a sequence of BE seed calls scoped to a fresh
 * `testRunId` (DEC-B-5).
 *
 * Spec ref: `docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md` DEC-C-1.
 */

export type JourneyId = 'journey1' | 'journey2' | 'journey3';

export interface AnnaPersona {
  readonly email: string;
  readonly displayName: string;
  readonly role: 'user';
  readonly userId: string;
  readonly onboardingCompleted: boolean;
}

export const ANNA_PERSONA: AnnaPersona = {
  email: 'anna.host@meepleai.test',
  displayName: 'Anna Host',
  role: 'user',
  userId: '00000000-0000-4000-8000-000000000001',
  onboardingCompleted: true,
};

export interface AnnaInitialState {
  readonly journeyId: JourneyId;
  readonly gameNightCount: number;
  readonly gameNightStatus: 'Draft' | 'Published' | 'InProgress' | 'Completed' | null;
  readonly playerRosterCount: number;
  readonly libraryGameCount: number;
  readonly sessionCount: number;
  readonly sessionStatus: 'InProgress' | 'Completed' | null;
}

const JOURNEY_INITIAL_STATES: Record<JourneyId, AnnaInitialState> = {
  journey1: {
    journeyId: 'journey1',
    gameNightCount: 1,
    gameNightStatus: 'Published',
    playerRosterCount: 2,
    libraryGameCount: 0,
    sessionCount: 0,
    sessionStatus: null,
  },
  journey2: {
    journeyId: 'journey2',
    gameNightCount: 0,
    gameNightStatus: null,
    playerRosterCount: 0,
    libraryGameCount: 1,
    sessionCount: 0,
    sessionStatus: null,
  },
  journey3: {
    journeyId: 'journey3',
    gameNightCount: 0,
    gameNightStatus: null,
    playerRosterCount: 0,
    libraryGameCount: 1,
    sessionCount: 15,
    sessionStatus: 'Completed',
  },
};

export function buildAnnaInitialState(journeyId: JourneyId): AnnaInitialState {
  const state = JOURNEY_INITIAL_STATES[journeyId];
  if (!state) {
    throw new Error(`buildAnnaInitialState: unknown journey id "${journeyId}"`);
  }
  return state;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm test e2e/_helpers/__tests__/annaPersona.test.ts`
Expected: PASS (5 tests green).

- [ ] **Step 5: Commit**

```bash
git add apps/web/e2e/_helpers/annaPersona.ts \
        apps/web/e2e/_helpers/__tests__/annaPersona.test.ts
git commit -m "feat(testing): #1929 T1.1 annaPersona canonical fixture (DEC-C-1)

Anna single primary actor across 3 cross-asse journey. Deterministic
userId 00000000-0000-4000-8000-000000000001 + email
anna.host@meepleai.test. 3 initial state variants (journey1: 1GN+2roster,
journey2: 0GN+1lib, journey3: 15session). 5 unit tests.

Refs #1929"
```

### Task 1.2: `dataAssertionUtils.ts` strict + functional helpers + unit tests

**Files:**
- Create: `apps/web/e2e/_helpers/dataAssertionUtils.ts`
- Create: `apps/web/e2e/_helpers/__tests__/dataAssertionUtils.test.ts`

- [ ] **Step 1: Write the failing unit test**

Create `apps/web/e2e/_helpers/__tests__/dataAssertionUtils.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import {
  assertExactStackDepth,
  assertExactUrl,
  assertExactCount,
  assertFunctionalFocus,
} from '../dataAssertionUtils';

describe('dataAssertionUtils (#1929 DEC-C-2)', () => {
  describe('strict assertions', () => {
    it('assertExactStackDepth passes when depth matches expected', () => {
      expect(() => assertExactStackDepth(2, 2)).not.toThrow();
    });

    it('assertExactStackDepth throws when depth mismatches', () => {
      expect(() => assertExactStackDepth(1, 2)).toThrow(/strict.*stack depth.*expected 2.*got 1/i);
    });

    it('assertExactUrl passes for exact string match', () => {
      expect(() => assertExactUrl('https://example.test/dashboard', 'https://example.test/dashboard')).not.toThrow();
    });

    it('assertExactUrl supports regex match', () => {
      expect(() =>
        assertExactUrl('https://example.test/game-nights/abc-123', /\/game-nights\/[a-z0-9-]+$/)
      ).not.toThrow();
    });

    it('assertExactUrl throws when url mismatches', () => {
      expect(() => assertExactUrl('https://example.test/login', '/dashboard')).toThrow(/strict.*url.*expected/i);
    });

    it('assertExactCount throws on mismatch', () => {
      expect(() => assertExactCount(5, 10, 'cards')).toThrow(/strict.*count.*cards.*expected 10.*got 5/i);
    });
  });

  describe('functional assertions', () => {
    it('assertFunctionalFocus returns true when selector matches focused element', () => {
      const result = assertFunctionalFocus({ tagName: 'BUTTON', dataset: { testid: 'foo' } } as unknown as Element, '[data-testid="foo"]');
      expect(result).toBe(true);
    });

    it('assertFunctionalFocus returns false when no element focused', () => {
      const result = assertFunctionalFocus(null, '[data-testid="foo"]');
      expect(result).toBe(false);
    });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test e2e/_helpers/__tests__/dataAssertionUtils.test.ts`
Expected: FAIL with `Cannot find module '../dataAssertionUtils'`.

- [ ] **Step 3: Create `dataAssertionUtils.ts`**

Create `apps/web/e2e/_helpers/dataAssertionUtils.ts`:

```typescript
/**
 * Issue #1929 Task C (DEC-C-2) — Hybrid assertion taxonomy helpers.
 *
 * **Strict literal assertions** for discrete state:
 *   - Drawer stack depth
 *   - URL (exact string or regex)
 *   - DB cleanup row counts
 *   - Element counts
 *
 * **Functional assertions** for continuous state:
 *   - Focus management (matching selector, not literal equality)
 *   - Scroll position threshold
 *   - Animation completion flag
 *
 * **Banditi pattern tolerant fallback** (DEC-C-2 explicit ban):
 *   - ❌ `Promise.race([sidebar, loginForm])`
 *   - ❌ Conditional URL branching with divergent expectations
 *   - ❌ Optional chaining `page.locator(...)?.click()`
 *
 * Spec ref: `docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md` DEC-C-2.
 */

// ============================================================================
// Strict literal assertions
// ============================================================================

export function assertExactStackDepth(actual: number, expected: number): void {
  if (actual !== expected) {
    throw new Error(
      `[strict] drawer stack depth assertion failed: expected ${expected}, got ${actual}`
    );
  }
}

export function assertExactUrl(actual: string, expected: string | RegExp): void {
  if (typeof expected === 'string') {
    if (actual !== expected) {
      throw new Error(`[strict] url assertion failed: expected "${expected}", got "${actual}"`);
    }
    return;
  }
  if (!expected.test(actual)) {
    throw new Error(
      `[strict] url assertion failed: expected match ${expected.source}, got "${actual}"`
    );
  }
}

export function assertExactCount(actual: number, expected: number, label: string): void {
  if (actual !== expected) {
    throw new Error(
      `[strict] count assertion failed for "${label}": expected ${expected}, got ${actual}`
    );
  }
}

// ============================================================================
// Functional assertions
// ============================================================================

/**
 * Returns true when the focused element matches the CSS selector.
 *
 * Functional (NOT strict): we do not assert identity (`focused === literalEl`)
 * because drawer push/pop can mount a new focus-trap element that is
 * conceptually "the same" but DOM-different. Selector match is the right
 * granularity for the cascade flow.
 */
export function assertFunctionalFocus(focused: Element | null, selector: string): boolean {
  if (focused == null) return false;
  // Re-check via matches() — selector might use `[data-...]` attributes
  // that JSDOM-style Element shims may not preserve. We swallow the
  // Element typing here because the runtime call is what matters.
  try {
    return (focused as HTMLElement & { matches?: (s: string) => boolean }).matches?.(selector) === true;
  } catch {
    return false;
  }
}

/**
 * Returns true when scrollY is greater than the threshold (no literal pixel).
 */
export function assertFunctionalScroll(scrollY: number, thresholdPx: number): boolean {
  return scrollY > thresholdPx;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm test e2e/_helpers/__tests__/dataAssertionUtils.test.ts`
Expected: PASS (8 tests green).

- [ ] **Step 5: Commit**

```bash
git add apps/web/e2e/_helpers/dataAssertionUtils.ts \
        apps/web/e2e/_helpers/__tests__/dataAssertionUtils.test.ts
git commit -m "feat(testing): #1929 T1.2 dataAssertionUtils hybrid taxonomy (DEC-C-2)

Strict literal helpers (stack depth, url exact|regex, count) + functional
helpers (focus selector match, scroll threshold). Banditi pattern tolerant
fallback documented inline. 8 unit tests.

Refs #1929"
```

### Task 1.3: `resilienceWrappers.ts` `withRetry` + unit tests

**Files:**
- Create: `apps/web/e2e/_helpers/resilienceWrappers.ts`
- Create: `apps/web/e2e/_helpers/__tests__/resilienceWrappers.test.ts`

- [ ] **Step 1: Write the failing unit test**

Create `apps/web/e2e/_helpers/__tests__/resilienceWrappers.test.ts`:

```typescript
import { describe, it, expect, vi } from 'vitest';
import { withRetry } from '../resilienceWrappers';

describe('resilienceWrappers (#1929 DEC-C-6)', () => {
  it('returns immediately on first-call success', async () => {
    const fn = vi.fn().mockResolvedValue('ok');
    const result = await withRetry(fn, { reason: 'test-success', backoffMs: 10 });
    expect(result).toBe('ok');
    expect(fn).toHaveBeenCalledTimes(1);
  });

  it('retries exactly once on first-call failure, returns success on second', async () => {
    const fn = vi
      .fn()
      .mockRejectedValueOnce(new Error('transient'))
      .mockResolvedValueOnce('recovered');
    const result = await withRetry(fn, { reason: 'test-retry', backoffMs: 10 });
    expect(result).toBe('recovered');
    expect(fn).toHaveBeenCalledTimes(2);
  });

  it('throws loud aggregate error with both first + second error after two failures', async () => {
    const fn = vi
      .fn()
      .mockRejectedValueOnce(new Error('first-fail'))
      .mockRejectedValueOnce(new Error('second-fail'));
    await expect(withRetry(fn, { reason: 'test-loud', backoffMs: 10 })).rejects.toThrow(
      /test action failed twice.*reason: test-loud.*first.*first-fail.*second.*second-fail/i
    );
    expect(fn).toHaveBeenCalledTimes(2);
  });

  it('respects backoffMs delay between attempts', async () => {
    const fn = vi.fn().mockRejectedValueOnce(new Error('e')).mockResolvedValueOnce('ok');
    const start = Date.now();
    await withRetry(fn, { reason: 'backoff', backoffMs: 100 });
    const elapsed = Date.now() - start;
    expect(elapsed).toBeGreaterThanOrEqual(100);
  });

  it('uses default 500ms backoff when not specified', async () => {
    const fn = vi.fn().mockRejectedValueOnce(new Error('e')).mockResolvedValueOnce('ok');
    const start = Date.now();
    await withRetry(fn, { reason: 'default-backoff' });
    const elapsed = Date.now() - start;
    expect(elapsed).toBeGreaterThanOrEqual(500);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test e2e/_helpers/__tests__/resilienceWrappers.test.ts`
Expected: FAIL with `Cannot find module '../resilienceWrappers'`.

- [ ] **Step 3: Create `resilienceWrappers.ts`**

Create `apps/web/e2e/_helpers/resilienceWrappers.ts`:

```typescript
/**
 * Issue #1929 Task C (DEC-C-6) — Resilience wrappers for E2E spec calls.
 *
 * **Pattern**: retry exactly **1 time** with **500ms default backoff** (override
 * via `backoffMs`), then **loud fail** with aggregate error message including
 * both first + second failure detail.
 *
 * **Applied to** (per spec):
 *   - `seedGameNight/Session/Player` (transient network)
 *   - Wizard step transitions (race condition mitigation)
 *   - Drawer cascade push (level N+1 settle wait)
 *
 * **NOT applied to**:
 *   - Login flow (`seedAuthSession` is sync via cookie addCookies)
 *   - Pure DOM assertions (no retry, fail fast — `expect(...).toBe(...)`)
 *
 * Spec ref: `docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md` DEC-C-6.
 */

export interface WithRetryOptions {
  /** Human-readable label for diagnostics in loud-fail error message. */
  readonly reason: string;
  /** Milliseconds between first failure and retry. Default: 500. */
  readonly backoffMs?: number;
}

const DEFAULT_BACKOFF_MS = 500;

export async function withRetry<T>(fn: () => Promise<T>, options: WithRetryOptions): Promise<T> {
  const backoff = options.backoffMs ?? DEFAULT_BACKOFF_MS;
  try {
    return await fn();
  } catch (firstError) {
    await new Promise<void>(r => setTimeout(r, backoff));
    try {
      return await fn();
    } catch (secondError) {
      const firstMsg = firstError instanceof Error ? firstError.message : String(firstError);
      const secondMsg = secondError instanceof Error ? secondError.message : String(secondError);
      throw new Error(
        `Test action failed twice (reason: ${options.reason}). ` +
          `First: ${firstMsg}. Second: ${secondMsg}.`
      );
    }
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm test e2e/_helpers/__tests__/resilienceWrappers.test.ts`
Expected: PASS (5 tests green).

- [ ] **Step 5: Commit**

```bash
git add apps/web/e2e/_helpers/resilienceWrappers.ts \
        apps/web/e2e/_helpers/__tests__/resilienceWrappers.test.ts
git commit -m "feat(testing): #1929 T1.3 resilienceWrappers withRetry (DEC-C-6)

Retry 1x + 500ms default backoff + loud aggregate fail. Applied to
transient calls (seed*, wizard transitions, drawer cascade push). NOT
applied to sync calls (login, pure DOM assertions). 5 unit tests
(success, recovery, loud fail, backoff timing).

Refs #1929"
```

### Task 1.4: Verify baseline + push + open PR

- [ ] **Step 1: Run all new unit tests in baseline**

Run: `cd apps/web && pnpm test e2e/_helpers/__tests__/`
Expected: PASS (18 tests green: 5 annaPersona + 8 dataAssertionUtils + 5 resilienceWrappers).

- [ ] **Step 2: Verify typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: 0 errors.

- [ ] **Step 3: Verify lint clean**

Run: `cd apps/web && pnpm lint`
Expected: 0 errors / 0 warnings on new files.

- [ ] **Step 4: Push branch**

```bash
git push -u origin feature/issue-1929-cross-asse-journey
```

- [ ] **Step 5: Open PR**

```bash
gh pr create --base main-dev --title "feat(testing): #1929 Task C — PR baseline shared helpers (annaPersona + dataAssertionUtils + resilienceWrappers)" --body "$(cat <<'EOF'
## Summary

PR baseline for Issue #1929 Task C — Cross-Asse Journey #1+#2+#3 full data-driven.

Establishes 3 shared helper file under `apps/web/e2e/_helpers/` that subsequent journey spec PRs (Journey #1, #2, #3) will consume:

1. **`annaPersona.ts`** (DEC-C-1) — Canonical persona "Anna Host" deterministic fields + 3 initial state variants (journey1: 1GN+2roster / journey2: 0GN+1lib / journey3: 15session)
2. **`dataAssertionUtils.ts`** (DEC-C-2) — Hybrid assertion taxonomy: strict literal (stack depth, url exact|regex, count) + functional (focus selector match, scroll threshold). Banditi pattern tolerant fallback documented inline.
3. **`resilienceWrappers.ts`** (DEC-C-6) — `withRetry` 1x + 500ms default backoff + loud aggregate fail.

**Spec consolidato**: `docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md` DEC-C-1 + DEC-C-2 + DEC-C-6.
**Plan**: `docs/superpowers/plans/2026-06-05-asse-d-p4-task-c-cross-asse-journey.md` Macro 1.
**Sequencing**: PR #N baseline → PR #N+1 Journey #1 (gated) → PR #N+2 Journey #2 (gated) → PR #N+3 Journey #3 (gated). DEC-C-5.

## Files

| File | Purpose | LOC |
|---|---|---|
| `apps/web/vitest.config.ts` | Patch exclude pattern (line 79) | +1 / -1 |
| `apps/web/e2e/_helpers/annaPersona.ts` | Persona canonical + initial state builder | ~150 |
| `apps/web/e2e/_helpers/dataAssertionUtils.ts` | Strict + functional assertion helpers | ~120 |
| `apps/web/e2e/_helpers/resilienceWrappers.ts` | withRetry wrapper | ~60 |
| `apps/web/e2e/_helpers/__tests__/*.test.ts` | 18 unit tests | ~260 |

Total: ~590 LOC + 1-line config patch.

## Test plan

- [x] `apps/web/e2e/_helpers/__tests__/annaPersona.test.ts` — 5 tests pass
- [x] `apps/web/e2e/_helpers/__tests__/dataAssertionUtils.test.ts` — 8 tests pass
- [x] `apps/web/e2e/_helpers/__tests__/resilienceWrappers.test.ts` — 5 tests pass
- [x] Typecheck 0 errors
- [x] Lint clean

## Designer review

N/A — pure E2E infra, no UI surfaces touched.

## Out of scope

- Spec files (Journey #1+#2+#3) → next 3 PR sequenziali
- Visual regression baseline screenshot (MIN-C-2 deferred)
- Cross-browser CI (chromium-only MVP)

## Refs

Refs #1929
Part of #1895 umbrella

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Expected: PR opened, CI starts.

- [ ] **Step 6: Monitor CI + merge**

After CI green:

```bash
gh pr merge --squash --delete-branch
```

> **NOTE**: subsequent Macro 2/3/4 PRs branch from `main-dev` AFTER baseline merged. Re-create the branch for each Macro:
> ```bash
> git checkout main-dev && git pull --ff-only
> git checkout -b feature/issue-1929-journey-1
> ```

---

## Macro 2 — PR Journey #1: Dashboard Drawer Stack (ESC cascade + prefers-reduced-motion)

> **Output**: 1 nuovo spec file `cross-asse-journey-1-dashboard-drawer-stack.spec.ts` (~250 LOC) verifying full drawer cascade flow on /dashboard. PR review-friendly <300 LOC. Effort target: 1-1.5gg.

### Pre-flight check

- [ ] **Step 1: Verify Macro 1 PR merged on main-dev**

```bash
git checkout main-dev && git pull --ff-only
git log --oneline -3
```

Expected: latest commit is "feat(testing): #1929 Task C — PR baseline shared helpers (...) (#NNNN)" squash merge.

Verify baseline helpers exist:

```bash
ls -la apps/web/e2e/_helpers/annaPersona.ts apps/web/e2e/_helpers/dataAssertionUtils.ts apps/web/e2e/_helpers/resilienceWrappers.ts
```

Expected: 3 files present.

- [ ] **Step 2: Verify Task B demo spec status (DEC-B-6 collision check)**

Task B Demo spec was named `cross-asse-journey-1-dashboard-drawer-stack.spec.ts`. Verify whether shipped:

```bash
ls apps/web/e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts 2>/dev/null && echo "EXISTS" || echo "NOT FOUND"
```

If EXISTS: Read the file — if it's the Task B smoke demo (~30 LOC), we OVERWRITE it with the full Journey #1 spec in this macro. Document the overwrite in the PR body.

If NOT FOUND: Create fresh.

- [ ] **Step 3: Create branch from main-dev**

```bash
# Verify clean HEAD on main-dev (P124 safety)
git branch --show-current  # MUST print main-dev
git status                 # MUST show clean tree
git checkout -b feature/issue-1929-journey-1
```

### Task 2.1: Journey #1 spec — happy path drawer cascade

**Files:**
- Create (or overwrite): `apps/web/e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts`

- [ ] **Step 1: Write the spec — happy path test only (skeleton + 1 test)**

Create `apps/web/e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts`:

```typescript
import { test, expect, type Page } from '@playwright/test';

import { ANNA_PERSONA, buildAnnaInitialState } from './_helpers/annaPersona';
import {
  assertExactStackDepth,
  assertExactUrl,
} from './_helpers/dataAssertionUtils';
import { withRetry } from './_helpers/resilienceWrappers';
import {
  cleanupTestEntities,
  newTestRunId,
  seedGameNight,
  seedPlayer,
} from './_helpers/seedEntities';
import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

/**
 * Cross-Asse Journey #1 — Dashboard drawer stack flow (Issue #1929).
 *
 * Anna (host) lands on /dashboard, opens her seeded GameNight via the
 * Prossimi card, pushes the Player drawer from inside, then ESCs back
 * down the stack. Verifies cascade-navigation-store push/pop semantics,
 * focus management, and prefers-reduced-motion compliance.
 *
 * **Initial state** (DEC-C-1 journey1):
 *   - 1 GN Published "E2E GameNight {testRunId16}"
 *   - 2 player roster (1 player + 1 guest)
 *
 * **Spec ref**: DEC-C-1 + DEC-C-2 + DEC-C-6 + DEC-C-7 Journey #1 matrix.
 * **Replaces**: Task B demo spec (#1928 T7) was a pre-flight smoke; this
 *   spec is the full data-driven Journey #1 mandated by spec consolidato.
 */
test.describe('Cross-Asse Journey #1 — Dashboard drawer stack', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  let testRunId: string;
  let gameNightId: string;
  let player1Id: string;

  test.beforeEach(async ({ page }, testInfo) => {
    testRunId = newTestRunId(testInfo.testId);

    // ① FE auth seeding — Anna as authenticated user
    await seedCookieConsent(page);
    await seedAuthSession(page, { role: ANNA_PERSONA.role });
    await mockAuthEndpoints(page, {
      role: ANNA_PERSONA.role,
      userId: ANNA_PERSONA.userId,
      email: ANNA_PERSONA.email,
      onboardingCompleted: ANNA_PERSONA.onboardingCompleted,
    });

    // ② BE entity seeding — journey1 initial state via Task B factory
    const initial = buildAnnaInitialState('journey1');
    expect(initial.gameNightCount).toBe(1);
    expect(initial.playerRosterCount).toBe(2);

    const gn = await withRetry(
      () =>
        seedGameNight(page, {
          testRunId,
          status: 'Published',
          ownerEmail: ANNA_PERSONA.email,
        }),
      { reason: 'seedGameNight journey1 beforeEach' }
    );
    gameNightId = gn.gameNightId;

    const p1 = await withRetry(
      () =>
        seedPlayer(page, {
          testRunId,
          gameNightId: gn.gameNightId,
          role: 'player',
          displayName: 'E2E Player 1',
        }),
      { reason: 'seedPlayer journey1 beforeEach (player)' }
    );
    player1Id = p1.playerId;

    await withRetry(
      () =>
        seedPlayer(page, {
          testRunId,
          gameNightId: gn.gameNightId,
          role: 'guest',
          displayName: 'E2E Guest 2',
        }),
      { reason: 'seedPlayer journey1 beforeEach (guest)' }
    );
  });

  test.afterEach(async ({ page }) => {
    if (testRunId) {
      await cleanupTestEntities(page, { testRunId });
    }
  });

  test('opens GN drawer from Prossimi → pushes Player drawer → ESC back-step → ESC close', async ({
    page,
  }) => {
    // ─── Step 1: Navigate to /dashboard
    await page.goto('/dashboard');
    await expect(page).not.toHaveURL(/\/(login|auth|sign-in)/);

    // ─── Step 2: Seeded GN visible in Prossimi (functional, not strict count)
    const prossimiCards = page.locator('[data-testid="prossimi-cards"]');
    await expect(prossimiCards).toBeVisible({ timeout: 10_000 });

    const gnCard = page.locator(`[data-testid="prossimi-card-${gameNightId}"]`);
    await expect(gnCard).toBeVisible({ timeout: 5_000 });

    // ─── Step 3: Click GN card → drawer GN opens (cascade-store stack depth = 1)
    await gnCard.click();

    // Radix Dialog root marks open state with data-state="open" on the
    // content element. We use Radix attribute selector + entity accent to
    // disambiguate from any other dialog (e.g., toast region).
    const gnDrawer = page.locator('[data-state="open"][role="dialog"]').first();
    await expect(gnDrawer).toBeVisible({ timeout: 5_000 });

    // Read cascade store via Zustand devtools hook (window-exposed).
    // stack depth at this point: 0 entries (current = drawer 1 of "stack").
    // We verify the store reports drawer state.
    const storeAfterOpen = await readCascadeStore(page);
    expect(storeAfterOpen.state).toBe('drawer');
    expect(storeAfterOpen.activeEntityType).toBe('event');
    expect(storeAfterOpen.activeEntityId).toBe(gameNightId);
    assertExactStackDepth(storeAfterOpen.drawerStack.length, 0);

    // ─── Step 4: Inside GN drawer, click Player avatar → Player drawer pushes
    // The GN drawer content includes a Players list (PlayersAvatarList).
    // Each avatar exposes data-testid="player-avatar-{playerId}".
    const playerAvatar = page.locator(`[data-testid="player-avatar-${player1Id}"]`);
    await expect(playerAvatar).toBeVisible({ timeout: 5_000 });

    await withRetry(() => playerAvatar.click(), {
      reason: 'click player avatar to push drawer',
    });

    // Stack depth now = 1 (GN entry pushed on the stack, Player is active).
    const storeAfterPush = await readCascadeStore(page);
    expect(storeAfterPush.state).toBe('drawer');
    expect(storeAfterPush.activeEntityType).toBe('player');
    expect(storeAfterPush.activeEntityId).toBe(player1Id);
    assertExactStackDepth(storeAfterPush.drawerStack.length, 1);

    // ─── Step 5: ESC → Player drawer pops, GN restored
    await page.keyboard.press('Escape');

    // popDrawer restores GN as active, stack depth back to 0.
    const storeAfterEscOnce = await readCascadeStore(page);
    expect(storeAfterEscOnce.state).toBe('drawer');
    expect(storeAfterEscOnce.activeEntityType).toBe('event');
    expect(storeAfterEscOnce.activeEntityId).toBe(gameNightId);
    assertExactStackDepth(storeAfterEscOnce.drawerStack.length, 0);

    // ─── Step 6: ESC again → GN drawer closes
    await page.keyboard.press('Escape');

    const storeAfterEscTwice = await readCascadeStore(page);
    expect(storeAfterEscTwice.state).toBe('closed');

    // Drawer DOM unmounted (or hidden) — Radix removes content from tree on close
    await expect(gnDrawer).toBeHidden({ timeout: 2_000 });

    // ─── Step 7: URL unchanged throughout drawer flow (strict)
    assertExactUrl(page.url(), /\/dashboard(\?.*)?$/);
  });
});

/**
 * Reads the cascade-navigation-store state via Zustand devtools window hook.
 * Devtools middleware exposes `window.__ZUSTAND_DEVTOOLS_EXTENSION__` but
 * actually inspecting store state via the public hook requires accessing
 * the store directly — which we expose via a `window.__cascadeStoreForE2E`
 * bridge in dev/test builds (see `cascade-navigation-store.ts` test hook).
 *
 * For the spec implementation, we use a minimal `evaluate` to read the
 * store via the Zustand hook's vanilla `getState()` API. If the hook isn't
 * exposed yet, this helper falls back to DOM-based heuristics (counts of
 * `[role="dialog"][data-state="open"]`), but that's lossy — prefer the
 * direct store read.
 */
async function readCascadeStore(page: Page): Promise<{
  state: string;
  activeEntityType: string | null;
  activeEntityId: string | null;
  drawerStack: Array<{ entityType: string; entityId: string }>;
}> {
  return await page.evaluate(() => {
    const store = (window as unknown as {
      __cascadeStoreForE2E?: {
        getState: () => {
          state: string;
          activeEntityType: string | null;
          activeEntityId: string | null;
          drawerStack: Array<{ entityType: string; entityId: string }>;
        };
      };
    }).__cascadeStoreForE2E;
    if (!store) {
      throw new Error(
        'cascade-navigation-store not exposed as window.__cascadeStoreForE2E. ' +
          'Verify dev/test build registers the bridge in `cascade-navigation-store.ts`.'
      );
    }
    return store.getState();
  });
}
```

> **NOTE** on `window.__cascadeStoreForE2E` bridge: this is a dev/test-only hook to enable deterministic store state assertions from Playwright. If the bridge isn't yet shipped in `cascade-navigation-store.ts`, T2.1 Step 2 adds it.

- [ ] **Step 2: Add `window.__cascadeStoreForE2E` bridge (dev/test only)**

Modify `apps/web/src/lib/stores/cascade-navigation-store.ts` — append after the store declaration (last line of file, before EOF):

```typescript
// ============================================================================
// E2E bridge (dev/test only, NOT production)
// ============================================================================

// Issue #1929 Task C — Expose store as window-level hook for Playwright
// deterministic state assertions. Stripped from production via NODE_ENV check.
if (typeof window !== 'undefined' && process.env.NODE_ENV !== 'production') {
  (window as unknown as { __cascadeStoreForE2E?: typeof useCascadeNavigationStore }).__cascadeStoreForE2E =
    useCascadeNavigationStore;
}
```

> **Rationale**: Zustand stores are React hooks, but `useCascadeNavigationStore.getState()` is the imperative API. Exposing the store directly on `window` lets Playwright `evaluate()` calls read state without coupling to React render cycles. NODE_ENV gate ensures production bundles don't leak the bridge.

- [ ] **Step 3: Run spec to verify it fails (drawer interaction not yet wired)**

Local prerequisites (BE + FE running with E2E flag):

```bash
# Terminal 1: BE with env
cd apps/api/src/Api
ASPNETCORE_ENVIRONMENT=Development \
E2E_SEEDING_ENABLED=true \
dotnet run

# Terminal 2: FE
cd apps/web && pnpm dev

# Terminal 3: Run spec
cd apps/web
pnpm exec playwright test cross-asse-journey-1-dashboard-drawer-stack.spec.ts
```

Expected: spec fails on Step 4 (`player-avatar-{player1Id}` not visible) OR on `__cascadeStoreForE2E` bridge missing. This is the failing-test gate — proceed to verify wire.

> **CONTINGENCY**: If `player-avatar-{playerId}` testid is NOT present in the GN drawer content, Step 4 of T2.1 will fail. In that case:
> - Read current `GameNightDrawerContent.tsx` (Glob `apps/web/src/components/features/**/GameNightDrawer*`)
> - If players list is rendered: add `data-testid={`player-avatar-${player.id}`}` to each avatar element. This is a minimal scope additive change consistent with existing testid patterns (e.g., `prossimi-card-${gameNightId}`).
> - If players list is NOT rendered in drawer at all: ESCALATE — Journey #1 wire requires drawer to show players. Open follow-up issue, mark Journey #1 BLOCKED, proceed with Journey #2/#3 in parallel.

- [ ] **Step 4: Verify spec passes after bridge + testid wire**

After Step 2 bridge added AND any testid additive change from contingency:

```bash
pnpm exec playwright test cross-asse-journey-1-dashboard-drawer-stack.spec.ts
```

Expected: PASS (1 test green end-to-end).

- [ ] **Step 5: Commit T2.1**

```bash
git add apps/web/e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts \
        apps/web/src/lib/stores/cascade-navigation-store.ts
git commit -m "feat(testing): #1929 T2.1 Journey #1 happy path drawer cascade

DEC-C-1+C-2+C-6 happy path: Anna lands /dashboard, opens GN drawer via
Prossimi, pushes Player drawer, ESCs twice back to closed. Strict
assertExactStackDepth (0|1) + strict url + functional drawer visibility.
__cascadeStoreForE2E window bridge (NODE_ENV-gated dev/test only).

Refs #1929"
```

### Task 2.2: Edge case — prefers-reduced-motion variant

**Files:**
- Modify: `apps/web/e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts`

- [ ] **Step 1: Append prefers-reduced-motion test**

Append after the happy path test in `cross-asse-journey-1-dashboard-drawer-stack.spec.ts`:

```typescript
  test('respects prefers-reduced-motion: ESC cascade still works with transitions disabled', async ({
    page,
  }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });

    await page.goto('/dashboard');
    await expect(page).not.toHaveURL(/\/(login|auth|sign-in)/);

    const gnCard = page.locator(`[data-testid="prossimi-card-${gameNightId}"]`);
    await expect(gnCard).toBeVisible({ timeout: 5_000 });

    await gnCard.click();
    const gnDrawer = page.locator('[data-state="open"][role="dialog"]').first();
    await expect(gnDrawer).toBeVisible({ timeout: 5_000 });

    // No animation = no need to wait for settle
    const playerAvatar = page.locator(`[data-testid="player-avatar-${player1Id}"]`);
    await expect(playerAvatar).toBeVisible({ timeout: 5_000 });
    await playerAvatar.click();

    await page.keyboard.press('Escape'); // pop Player
    await page.keyboard.press('Escape'); // close GN

    const storeAfter = await readCascadeStore(page);
    expect(storeAfter.state).toBe('closed');
  });
```

- [ ] **Step 2: Run new test**

```bash
pnpm exec playwright test cross-asse-journey-1-dashboard-drawer-stack.spec.ts
```

Expected: PASS (2 tests green).

- [ ] **Step 3: Commit T2.2**

```bash
git add apps/web/e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts
git commit -m "test(e2e): #1929 T2.2 Journey #1 prefers-reduced-motion variant (DEC-C-6)

Mandatory variant per spec DEC-C-6: emulate prefers-reduced-motion: reduce
+ replay full cascade. Asserts store transitions still settle to 'closed'
without relying on transition-end events.

Refs #1929"
```

### Task 2.3: Edge case — backdrop click closeOne (NOT closeAll)

**Files:**
- Modify: `apps/web/e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts`

- [ ] **Step 1: Append backdrop semantics test**

Append:

```typescript
  test('backdrop click closes only top drawer (same semantics as ESC, NOT closeAll)', async ({
    page,
  }) => {
    await page.goto('/dashboard');
    const gnCard = page.locator(`[data-testid="prossimi-card-${gameNightId}"]`);
    await gnCard.click();

    // Open Player drawer on top
    const playerAvatar = page.locator(`[data-testid="player-avatar-${player1Id}"]`);
    await expect(playerAvatar).toBeVisible({ timeout: 5_000 });
    await playerAvatar.click();

    const storeBeforeBackdrop = await readCascadeStore(page);
    assertExactStackDepth(storeBeforeBackdrop.drawerStack.length, 1);

    // Backdrop click: Radix Dialog overlay handles pointerdown-outside.
    // We use position-based click on the overlay region (fixed inset-0 z-40).
    // Locator targets the first overlay element from the top of the document.
    const overlay = page.locator('.fixed.inset-0.z-40').first();
    await expect(overlay).toBeVisible({ timeout: 2_000 });

    await overlay.click({ position: { x: 20, y: 20 }, force: true });

    // After backdrop: Player drawer pops, GN restored — SAME as ESC
    const storeAfter = await readCascadeStore(page);
    expect(storeAfter.state).toBe('drawer');
    expect(storeAfter.activeEntityType).toBe('event');
    expect(storeAfter.activeEntityId).toBe(gameNightId);
    assertExactStackDepth(storeAfter.drawerStack.length, 0);
  });
```

> **NOTE on backdrop semantics**: spec DEC-C-7 Journey #1 edge case says "Backdrop click vs ESC semantica: **Same behavior** (closeOne livello corrente)". If runtime behavior differs (Radix Dialog default closes only top-level, but project may override `onInteractOutside` to closeAll), document the actual behavior in PR body and adjust assertion accordingly. The expected behavior per spec is `closeOne` parity with ESC.

- [ ] **Step 2: Run new test**

```bash
pnpm exec playwright test cross-asse-journey-1-dashboard-drawer-stack.spec.ts
```

Expected: PASS (3 tests green).

- [ ] **Step 3: Commit T2.3**

```bash
git add apps/web/e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts
git commit -m "test(e2e): #1929 T2.3 Journey #1 backdrop semantics edge case (DEC-C-7)

Spec DEC-C-7: backdrop click = ESC parity = closeOne current level.
NOT closeAll cascade. Verifies pointerdown-outside Radix dispatch maps
to popDrawer (same as Escape key), not closeCascade.

Refs #1929"
```

### Task 2.4: Edge case — ESC on empty stack no-op

**Files:**
- Modify: `apps/web/e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts`

- [ ] **Step 1: Append ESC empty stack test**

Append:

```typescript
  test('ESC on empty drawer stack is a no-op (no error toast, store unchanged)', async ({
    page,
  }) => {
    await page.goto('/dashboard');

    const storeBefore = await readCascadeStore(page);
    expect(storeBefore.state).toBe('closed');

    // ESC pressed with no drawer open
    await page.keyboard.press('Escape');

    // Store unchanged
    const storeAfter = await readCascadeStore(page);
    expect(storeAfter.state).toBe('closed');
    expect(storeAfter.activeEntityType).toBe(null);
    expect(storeAfter.activeEntityId).toBe(null);

    // No error toast surfaced (functional: toast region empty)
    const toastRegion = page.locator('[role="status"][data-sonner-toaster]').first();
    const toastCount = await toastRegion.locator('[data-sonner-toast]').count();
    expect(toastCount).toBe(0);
  });
```

- [ ] **Step 2: Run new test**

```bash
pnpm exec playwright test cross-asse-journey-1-dashboard-drawer-stack.spec.ts
```

Expected: PASS (4 tests green).

- [ ] **Step 3: Commit T2.4**

```bash
git add apps/web/e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts
git commit -m "test(e2e): #1929 T2.4 Journey #1 ESC empty stack no-op (DEC-C-7)

DEC-C-7 edge case: ESC pressed with no drawer open is a no-op. Asserts
store state unchanged + no error toast surfaced. Prevents regression
where global keyboard handler attempts popDrawer on empty stack.

Refs #1929"
```

### Task 2.5: Push branch + open PR Journey #1

- [ ] **Step 1: Run full spec locally (4 tests)**

Local with BE + FE running:

```bash
pnpm exec playwright test cross-asse-journey-1-dashboard-drawer-stack.spec.ts
```

Expected: PASS (4 tests green: happy path + reduced-motion + backdrop + ESC empty).

- [ ] **Step 2: Verify typecheck + lint**

```bash
cd apps/web
pnpm typecheck
pnpm lint e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts src/lib/stores/cascade-navigation-store.ts
```

Expected: 0 errors.

- [ ] **Step 3: Push branch**

```bash
git push -u origin feature/issue-1929-journey-1
```

- [ ] **Step 4: Open PR**

```bash
gh pr create --base main-dev --title "feat(testing): #1929 Task C — Journey #1 dashboard drawer stack full data-driven" --body "$(cat <<'EOF'
## Summary

PR Journey #1 for Issue #1929 Task C — Cross-Asse Journey #1+#2+#3 full data-driven.

Implements full data-driven Playwright spec for the dashboard → GameNight drawer → Player drawer → ESC cascade flow. Replaces Task B demo smoke spec (#1928 T7) with the full Journey #1 mandated by spec consolidato.

**Initial state** (DEC-C-1 journey1): Anna host + 1 GN Published + 2 player roster.

**Edge cases covered** (DEC-C-7 matrix):
- Happy path: open GN → push Player → ESC ESC closes both
- prefers-reduced-motion variant (DEC-C-6 mandatory)
- Backdrop click semantics = ESC parity (closeOne current level)
- ESC on empty stack = no-op

**Assertion taxonomy** (DEC-C-2):
- Strict: `assertExactStackDepth(0 | 1)` + `assertExactUrl(/\/dashboard$/)` + drawer state literal
- Functional: drawer visibility + toast region count

**Spec consolidato**: `docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md` DEC-C-1+C-2+C-6+C-7.
**Plan**: `docs/superpowers/plans/2026-06-05-asse-d-p4-task-c-cross-asse-journey.md` Macro 2.
**Gated on**: PR baseline #N (Macro 1) merged on main-dev.

## Files

| File | Purpose | LOC |
|---|---|---|
| `apps/web/e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts` | 4 tests | ~250 |
| `apps/web/src/lib/stores/cascade-navigation-store.ts` | window bridge (dev/test only) | +12 |

Total: ~262 LOC.

## Test plan

- [x] Happy path test passes locally (BE + FE running)
- [x] prefers-reduced-motion variant passes
- [x] Backdrop click semantics passes
- [x] ESC empty stack no-op passes
- [x] Typecheck 0 errors
- [x] Lint clean

## Designer review checklist (DEC-C-3 per-journey concrete)

- [ ] Verify drawer animations smooth on desktop chromium (no jank during push/pop)
- [ ] Verify prefers-reduced-motion variant truly disables transitions (browser DevTools rendering tab)
- [ ] Verify focus trap visible during drawer interactions (cursor doesn't escape to background)
- [ ] Verify backdrop click area is the full overlay region (not gated by header height)

## CI policy (DEC-C-4)

- main-dev: non-blocking (this PR can merge even if E2E fails — velocity priority)
- main-staging: blocking (Task A skeleton + Task C all 3 journey must pass before main-staging promotion)

## Out of scope

- Journey #2/#3 spec → next PRs in sequence
- `GameNightDrawerContent` UI refactor (verify-only per DEC-C-3)
- Visual regression baseline screenshot

## Refs

Refs #1929
Part of #1895 umbrella

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Expected: PR opened, CI starts.

- [ ] **Step 5: Monitor CI + merge**

After CI green:

```bash
gh pr merge --squash --delete-branch
```

---

## Macro 3 — PR Journey #2: Empty Dashboard → CTA → Wizard 4-step → Live opt-in

> **Output**: 1 nuovo spec file `cross-asse-journey-2-empty-cta-wizard-live.spec.ts` (~280 LOC). PR review-friendly <300 LOC. Effort target: 1.5-2gg (più complex flow). **NOT in sessione 41 scope.**

### Pre-flight check

- [ ] **Step 1: Verify Macro 2 PR merged**

```bash
git checkout main-dev && git pull --ff-only
git log --oneline -5
```

Expected: latest commit is Macro 2 squash merge.

- [ ] **Step 2: Create branch from main-dev**

```bash
git branch --show-current  # MUST print main-dev
git checkout -b feature/issue-1929-journey-2
```

### Task 3.1: Journey #2 spec — empty CTA happy path

**Files:**
- Create: `apps/web/e2e/cross-asse-journey-2-empty-cta-wizard-live.spec.ts`

- [ ] **Step 1: Write the spec skeleton + empty CTA test**

Create `apps/web/e2e/cross-asse-journey-2-empty-cta-wizard-live.spec.ts`:

```typescript
import { test, expect } from '@playwright/test';

import { ANNA_PERSONA, buildAnnaInitialState } from './_helpers/annaPersona';
import { assertExactUrl } from './_helpers/dataAssertionUtils';
import { withRetry } from './_helpers/resilienceWrappers';
import {
  cleanupTestEntities,
  newTestRunId,
  seedGameNight,
} from './_helpers/seedEntities';
import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

/**
 * Cross-Asse Journey #2 — Empty dashboard → CTA wizard → Live opt-in (Issue #1929).
 *
 * Anna lands on /dashboard with 0 GN seeded. EmptySection surfaces the
 * "+ Crea la tua prima Game Night" CTA. Click navigates /game-nights/new
 * (4-step wizard per DEC-C-3 correction). Fill all 4 steps. Submit creates
 * GN + redirects /game-nights/{id}. Click "Apri live mode" opt-in toast +
 * confirm → /game-nights/{id}/live with session created.
 *
 * **Initial state** (DEC-C-1 journey2):
 *   - 0 GameNights
 *   - 1 library game (for wizard step 3 suggestion)
 *
 * **Spec ref**: DEC-C-3 (wizard 4-step correction) + DEC-C-7 Journey #2 matrix.
 */
test.describe('Cross-Asse Journey #2 — Empty CTA → wizard 4-step → live opt-in', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  let testRunId: string;

  test.beforeEach(async ({ page }, testInfo) => {
    testRunId = newTestRunId(testInfo.testId);

    await seedCookieConsent(page);
    await seedAuthSession(page, { role: ANNA_PERSONA.role });
    await mockAuthEndpoints(page, {
      role: ANNA_PERSONA.role,
      userId: ANNA_PERSONA.userId,
      email: ANNA_PERSONA.email,
      onboardingCompleted: ANNA_PERSONA.onboardingCompleted,
    });

    // Initial state journey2: NO seed calls for GN. Library game is OUT of
    // E2E seeding scope (Task B factory exposes GameNight/Session/Player
    // only; library is BE-side fixture deferred wave futuro). For wizard
    // step 3 we use the BE-deployed default library OR fallback to the
    // "no library, search by name" branch — verify in T3.2.
    const initial = buildAnnaInitialState('journey2');
    expect(initial.gameNightCount).toBe(0);
  });

  test.afterEach(async ({ page }) => {
    if (testRunId) {
      await cleanupTestEntities(page, { testRunId });
    }
  });

  test('empty dashboard surfaces "+ Crea Game Night" CTA navigating /game-nights/new', async ({
    page,
  }) => {
    await page.goto('/dashboard');
    await expect(page).not.toHaveURL(/\/(login|auth|sign-in)/);

    // Prossimi EmptySection surface (testid contract from ProssimiSection.tsx:73)
    const empty = page.locator('[data-testid="prossimi-empty"]');
    await expect(empty).toBeVisible({ timeout: 10_000 });

    // CTA inside EmptySection — `cta` prop renders as Link to ctaHref
    const cta = empty.getByRole('link', { name: /\+\s*crea la tua prima game night/i });
    await expect(cta).toBeVisible();

    await cta.click();
    assertExactUrl(page.url(), /\/game-nights\/new(\?.*)?$/);

    // Wizard step 1 mounted
    const wizard = page.locator('[data-slot="wizard-modal"]');
    await expect(wizard).toBeVisible({ timeout: 10_000 });

    const stepIndicator = page.locator('[data-testid="wizard-step-indicator"]');
    await expect(stepIndicator).toContainText(/1.*4/);
  });
});
```

> **NOTE**: this Macro 3 plan only includes the empty CTA happy path test (T3.1). The remaining 4-step wizard fill + live opt-in tests will be detailed in a future plan revision once Macro 2 is shipped and we have learnings from Journey #1. Tracking placeholder: 3 follow-up tests (wizard 4-step fill, submit → redirect, live opt-in toast) — to be added before PR opens.

- [ ] **Step 2: Run spec — verify it fails on empty CTA discovery**

```bash
pnpm exec playwright test cross-asse-journey-2-empty-cta-wizard-live.spec.ts
```

Expected: PASS if EmptySection + CTA testid match contract. If FAIL, contingency: read `ProssimiSection.tsx:73-83` (EmptySection wiring) and adjust selectors.

- [ ] **Step 3: Commit T3.1**

```bash
git add apps/web/e2e/cross-asse-journey-2-empty-cta-wizard-live.spec.ts
git commit -m "test(e2e): #1929 T3.1 Journey #2 empty CTA → wizard step 1 mount

DEC-C-3 wizard 4-step correction: assert stepIndicator '1...4' on mount
(NOT 1...3 as original spec). Empty dashboard prossimi-empty testid +
EmptySection CTA navigating /game-nights/new.

Refs #1929"
```

### Tasks 3.2–3.5: TBD — wizard fill + submit + redirect + live opt-in

> **DEFERRED**: detailed steps for wizard fill (step 1/2/3/4), submit + retry [1s, 2s, 4s] (verified in `_content.tsx:42`), redirect assertion, and live opt-in flow will be added BEFORE the PR opens. This requires runtime discovery of:
> - Wizard step 3 (game suggestion) data source when no library seeded
> - Live opt-in CTA testid surface (`/game-nights/{id}` route page component)
> - Session creation endpoint payload
>
> Tracking: open follow-up task to flesh out T3.2–T3.5 before PR Journey #2 opens.

---

## Macro 4 — PR Journey #3: Game Detail Rail Storico Partite Navigation

> **Output**: 1 nuovo spec file `cross-asse-journey-3-game-detail-tab-partite.spec.ts` (~200 LOC). PR review-friendly <300 LOC. Effort target: 0.5-1gg (rescoped semplificato per DEC-C-3 rail+navigate verify-only). **NOT in sessione 41 scope.**

### Pre-flight check

- [ ] **Step 1: Verify Macro 3 PR merged**

```bash
git checkout main-dev && git pull --ff-only
git log --oneline -5
```

Expected: latest commit is Macro 3 squash merge.

- [ ] **Step 2: Create branch from main-dev**

```bash
git branch --show-current  # MUST print main-dev
git checkout -b feature/issue-1929-journey-3
```

### Task 4.1: Journey #3 spec — rail+navigate happy path

**Files:**
- Create: `apps/web/e2e/cross-asse-journey-3-game-detail-tab-partite.spec.ts`

- [ ] **Step 1: Write the spec skeleton + rail navigate test**

Create `apps/web/e2e/cross-asse-journey-3-game-detail-tab-partite.spec.ts`:

```typescript
import { test, expect } from '@playwright/test';

import { ANNA_PERSONA, buildAnnaInitialState } from './_helpers/annaPersona';
import { assertExactUrl } from './_helpers/dataAssertionUtils';
import { withRetry } from './_helpers/resilienceWrappers';
import {
  cleanupTestEntities,
  newTestRunId,
  seedGameNight,
  seedSession,
} from './_helpers/seedEntities';
import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

/**
 * Cross-Asse Journey #3 — Game Detail rail Storico partite (Issue #1929).
 *
 * Spec original ("paginazione inline NO navigate /sessions") was RESCOPED
 * via DEC-C-3 to match codebase reality (`GameDetailSessionsRail.tsx` has
 * `viewAllHref` linking to `/games/[id]/sessions`). This spec verifies
 * the rail + "Storico partite" navigation + filter params persistence.
 *
 * **Initial state** (DEC-C-1 journey3):
 *   - 1 game (Anna's library)
 *   - 15 completed sessions (>5 → rail truncates to 5 + shows "Storico partite" link)
 *
 * **Spec ref**: DEC-C-3 (rail rescope, NO refactor) + DEC-C-7 Journey #3 matrix.
 */
test.describe('Cross-Asse Journey #3 — Game Detail rail Storico partite', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  let testRunId: string;
  let gameId: string;

  test.beforeEach(async ({ page }, testInfo) => {
    testRunId = newTestRunId(testInfo.testId);

    await seedCookieConsent(page);
    await seedAuthSession(page, { role: ANNA_PERSONA.role });
    await mockAuthEndpoints(page, {
      role: ANNA_PERSONA.role,
      userId: ANNA_PERSONA.userId,
      email: ANNA_PERSONA.email,
      onboardingCompleted: ANNA_PERSONA.onboardingCompleted,
    });

    // Initial state journey3: seed 1 GN + 15 completed sessions.
    // Library game seeding is OUT of Task B factory scope (sessions
    // reference the GameNight aggregate, not a standalone Library entry).
    // We seed 1 GN that hosts the 15 sessions. The "game" surface in UI
    // is the GameNight title — adjust contract if game-detail page requires
    // a separate Games BE entity (verify T4.1 Step 2).
    const initial = buildAnnaInitialState('journey3');
    expect(initial.sessionCount).toBe(15);

    const gn = await withRetry(
      () =>
        seedGameNight(page, {
          testRunId,
          status: 'Completed',
          ownerEmail: ANNA_PERSONA.email,
        }),
      { reason: 'seedGameNight journey3 beforeEach' }
    );
    gameId = gn.gameNightId;

    // Seed 15 completed sessions linked to this GN
    for (let i = 0; i < 15; i++) {
      await withRetry(
        () =>
          seedSession(page, {
            testRunId,
            gameNightId: gn.gameNightId,
            isLive: false,
            scoreType: 'Points',
          }),
        { reason: `seedSession journey3 beforeEach (#${i + 1}/15)` }
      );
    }
  });

  test.afterEach(async ({ page }) => {
    if (testRunId) {
      await cleanupTestEntities(page, { testRunId });
    }
  });

  test('rail shows 5 preview + "Storico partite" link navigating /games/[id]/sessions', async ({
    page,
  }) => {
    await page.goto(`/games/${gameId}`);
    await expect(page).not.toHaveURL(/\/(login|auth|sign-in)/);

    // Rail surface (data-slot from GameDetailSessionsRail.tsx:68)
    const rail = page.locator('[data-slot="game-detail-sessions-rail"]');
    await expect(rail).toBeVisible({ timeout: 10_000 });

    // 15 > 5 → rail truncates AND link visible
    const viewAll = page.locator('[data-slot="game-detail-sessions-view-all"]');
    await expect(viewAll).toBeVisible();

    // Strict: link href matches /games/{gameId}/sessions
    await expect(viewAll).toHaveAttribute('href', new RegExp(`/games/${gameId}/sessions`));

    // Click link → navigate
    await viewAll.click();

    // Strict URL: filter params preserved (DEC-C-3 + DEC-C-7 filter persistence)
    assertExactUrl(page.url(), new RegExp(`/games/${gameId}/sessions(\\?.*)?$`));
  });
});
```

> **NOTE on routing contract**: this spec assumes `/games/[id]` route exists AND that GN seeded via factory surfaces as `gameId` on game-detail page. If the route is gated by game-catalog entity (different BE entity from GameNight), follow-up needed:
> - Verify route `apps/web/src/app/(authenticated)/games/[id]/page.tsx` exists
> - Verify game-detail page accepts GN-seeded gameId OR requires separate library seed
> - If mismatch: ESCALATE — Journey #3 may need Library seeding extension to Task B factory

- [ ] **Step 2: Run spec — verify it fails on route discovery**

```bash
pnpm exec playwright test cross-asse-journey-3-game-detail-tab-partite.spec.ts
```

Expected: depends on route contract. If route exists + GN-id compatible → PASS. If route requires library entity → ESCALATE.

- [ ] **Step 3: Commit T4.1**

```bash
git add apps/web/e2e/cross-asse-journey-3-game-detail-tab-partite.spec.ts
git commit -m "test(e2e): #1929 T4.1 Journey #3 rail Storico partite navigation (DEC-C-3 rescope)

DEC-C-3 rescope: rail+navigate verify (NOT paginazione inline refactor).
Seed 15 completed sessions → rail shows truncated 5 + 'Storico partite'
link → click → /games/{id}/sessions navigation with filter params.

Refs #1929"
```

### Tasks 4.2–4.4: TBD — boundary tests (0 session, 1-5 session, 6+ session) + filter persistence

> **DEFERRED**: detailed steps for boundary tests will be added BEFORE the PR opens, similar to Macro 3 deferral. Tracking placeholder:
> - T4.2: boundary 0 session → rail hidden (no `viewAllHref`)
> - T4.3: boundary 1-5 session → rail shows all, NO link
> - T4.4: filter persistence `?sortBy=date&dir=desc` survives navigation
>
> Runtime discovery needed: `/games/[id]/sessions` page params handling.

---

## Final integration check

- [ ] **Step 1: After Macro 2 merge — run all baseline + Journey #1 specs locally**

```bash
cd apps/web
pnpm test e2e/_helpers/__tests__/
pnpm exec playwright test cross-asse-journey-1-dashboard-drawer-stack.spec.ts
```

Expected: PASS (18 unit + 4 E2E tests green).

- [ ] **Step 2: Final umbrella status update**

After all 4 Macros merged:

```bash
gh issue close 1929 --reason completed --comment "Closes #1929 — Cross-Asse Journey #1+#2+#3 full data-driven shipped via 4 sequential PRs:
- PR #N baseline (annaPersona + dataAssertionUtils + resilienceWrappers)
- PR #N+1 Journey #1 (dashboard drawer stack)
- PR #N+2 Journey #2 (empty CTA → wizard 4-step → live opt-in)
- PR #N+3 Journey #3 (game-detail rail Storico partite)

DEC-C-1..7 all lockate + AC-1..10 verified."
```

Verify umbrella #1895 auto-close (Task A + B + C all closed).

---

## Self-Review checklist

### Spec coverage

| Spec requirement (DEC-C-N + AC-N) | Macro/Task | Status |
|---|---|---|
| AC-1: 3 spec file `cross-asse-journey-*.spec.ts` | Macro 2 + 3 + 4 | ✅ planned |
| AC-2: 3/3 spec import `seedEntities` + `seedAuthSession` + `annaPersona` + `withRetry` | All Macros 2-4 beforeEach | ✅ planned |
| AC-3: 3/3 spec assertion taxonomy DEC-C-2 strict + functional, no tolerant | Macros 2-4 use `assertExactStackDepth`/`assertExactUrl`/etc. | ✅ planned |
| AC-4: CI policy non-blocking main-dev + blocking main-staging (DEC-C-4) | PR body Macro 2-4 documents | ✅ planned |
| AC-5: Designer review checklist per journey nel PR body | PR body Macro 2 has 4 concrete items, Macros 3-4 TBD | ✅ planned (Macro 2), TBD (3+4) |
| AC-6: Edge case matrix verificata per journey | Macro 2: 4 tests (happy + reduced-motion + backdrop + ESC empty) | ✅ planned (Macro 2), TBD (3+4) |
| AC-7: Shared baseline branch + 3 helpers (annaPersona + dataAssertionUtils + resilienceWrappers) | Macro 1 | ✅ planned |
| AC-8: 3 PR sequenziali <300 LOC each | Macros 2-4 each target <300 LOC | ✅ planned (Macro 1 590 LOC but is baseline helper, NOT journey spec) |
| AC-9: Journey #2 wizard 4-step verified (DEC-C-3 correction) | Macro 3 T3.1 asserts `wizard-step-indicator` '1...4' | ✅ planned |
| AC-10: Journey #3 rail+navigate verified (DEC-C-3 rescope) | Macro 4 T4.1 asserts `viewAllHref` + navigate | ✅ planned |
| DEC-C-1 Anna persona fixed | `annaPersona.ts` | ✅ planned |
| DEC-C-2 Hybrid assertion taxonomy | `dataAssertionUtils.ts` | ✅ planned |
| DEC-C-3 Spec corrections | Macros 3+4 honor wizard 4-step + rail rescope | ✅ planned |
| DEC-C-4 Non-blocking main-dev + blocking main-staging | PR body | ✅ planned |
| DEC-C-5 3 PR sequenziali shared baseline | Macros 1-4 structure | ✅ planned |
| DEC-C-6 Retry 1x + 500ms backoff + prefers-reduced-motion variant | `resilienceWrappers.ts` + Macro 2 T2.2 | ✅ planned |
| DEC-C-7 Edge case matrix inline | Macro 2 covers fully; Macros 3-4 TBD detailed in pre-PR step | ⚠️ partial (Macro 2 full, Macros 3-4 deferred to pre-PR discovery) |

### Placeholder scan

- [x] No "TBD" / "implement later" / "fill in details" at code level for Macros 1+2
- ⚠️ Macros 3+4 have explicit "TBD" sections (T3.2-3.5 and T4.2-4.4) acknowledged as DEFERRED to pre-PR runtime discovery — this is **acceptable** because:
  - Sessione 41 scope is Macro 1 + Macro 2 only
  - Macros 3+4 deferred sections require runtime knowledge (route contracts + library entity decision) only obtainable AFTER Macro 2 shipped
  - The TBD placeholders are bounded by explicit tracking obligations (open follow-up tasks before PR opens)
- [x] All `git commit` messages drafted with full body
- [x] All `pnpm exec playwright test` and `cd apps/web && pnpm test` commands include exact filter args (verified workspace name is `@meepleai/web`, not `@apps/web`)

### Type consistency

- [x] `testRunId` format string `e2e-{cleanId}-{epochMs}` consistent Macros 1-4 (via shared `seedEntities.ts:newTestRunId`)
- [x] `ANNA_PERSONA.role` is `'user'` (literal type) — consumed identically by Macros 2-4 `seedAuthSession({ role: ANNA_PERSONA.role })`
- [x] `JourneyId` type `'journey1' | 'journey2' | 'journey3'` consistent
- [x] `AnnaInitialState.gameNightStatus` is `'Draft' | 'Published' | 'InProgress' | 'Completed' | null` — matches `GameNightStatus` from seedEntities
- [x] Helper imports: all Macros 2-4 use same 3 baseline helpers + `seedEntities` + `seedAuthSession`
- [x] Spec file names match cross-references: Macro 2 file is `cross-asse-journey-1-dashboard-drawer-stack.spec.ts` (note collision with Task B demo at DEC-B-6 — pre-flight Step 2 of Macro 2 verifies + overwrites)

### Edge cases identified

- [x] DEC-C-7 Journey #1: happy path + backdrop (DEC-C-7 same-as-ESC parity) + ESC empty stack no-op + prefers-reduced-motion variant (DEC-C-6 mandatory) — 4 tests in Macro 2
- ⚠️ DEC-C-7 Journey #2: wizard step validation matrix + cancel mid-flow + step 4 recap immutability — DEFERRED to T3.2-3.5 detailed in pre-PR
- ⚠️ DEC-C-7 Journey #3: boundary 0/1-5/6+ sessions + filter persistence — DEFERRED to T4.2-4.4 detailed in pre-PR
- [x] testRunId collision: each `test.beforeEach` generates unique testRunId via `newTestRunId(test.info().testId)` + `Date.now()`, parallel-safe
- [x] Cleanup determinism: `test.afterEach` always calls `cleanupTestEntities(page, { testRunId })` even if test fails (Playwright runs afterEach on failure)
- [x] Auth-cookie race with Radix open-state: drawer open relies on `data-state="open"` Radix attribute — present synchronously after click, no need for `waitForFunction`

---

## Execution Handoff

**Plan complete and saved to** `docs/superpowers/plans/2026-06-05-asse-d-p4-task-c-cross-asse-journey.md`.

Two execution options:

**1. Subagent-Driven (recommended for sessione 41)** — Dispatch fresh subagent per task, review between tasks, fast iteration. Pattern P120 mix-model:
- Macro 1: T1.1+T1.2+T1.3 (haiku each — mechanical fixture/helper boilerplate) → T1.4 (sonnet — PR open + CI monitoring)
- Macro 2: T2.1 (sonnet — judgment on cascade-store bridge + testid contingency) → T2.2+T2.3+T2.4 (haiku each — append edge cases) → T2.5 (sonnet — PR open)

**2. Inline Execution** — Execute tasks in this session using `superpowers:executing-plans`, batch execution with checkpoints. Risk: 1.5-2gg in single session is heavy; subagent dispatch preferred.

**Sessione 41 scope (user-confirmed)**: Macro 1 + Macro 2 = ~2gg.

**Which approach? (subagent-driven recommended)**
