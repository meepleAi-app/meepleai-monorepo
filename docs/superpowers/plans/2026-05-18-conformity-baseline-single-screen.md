# Conformity Baseline Single-Screen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Risolvere il conformity-debt #1269 sostituendo il pattern `fullPage` capture del mockup gallery con cattura `locator`-based del solo screen marcato via `data-conformity-screen`, riducendo i 4 baseline da 4000-12000px a ~900-1800px e allineandoli alle live route render.

**Architecture:** Marker DOM-based (`data-conformity-screen="default-{viewport}"`) iniettato nei 4 mockup HTML/JSX wrappa lo screen target. `bootstrap.spec.ts` cattura solo il locator (hard-fail se assente o duplicato). `conformity.spec.ts` ottiene runbook per le 2 route mancanti (`player-detail`, `game-nights-index`). PR #1 (code) + workflow_dispatch post-merge → auto-PR #2 (baseline regenerated su CI Ubuntu).

**Tech Stack:** Playwright 1.49 (visual regression), TypeScript, Next.js 16 App Router, React 18.3 (mockup React-bootstrapped), HTML5 data-attributes.

**Spec ref:** `docs/superpowers/specs/2026-05-18-conformity-baseline-single-screen-design.md`

**Issue ref:** #1269 (closes), #1023 (umbrella DS), #1066 (umbrella WS-C)

---

## File Structure (locked-in decisions)

| File | Responsibility | Action |
|------|----------------|--------|
| `apps/web/e2e/visual-conformity/bootstrap.spec.ts` | Capture baseline PNGs from mockup HTML | Modify: switch da `expect(page).toHaveScreenshot({fullPage:true})` a `expect(page.locator(selector)).toHaveScreenshot()` |
| `apps/web/e2e/visual-conformity/conformity.spec.ts` | Compare live route vs baseline | Modify: aggiungere 2 RUNBOOK entries (player-detail, game-nights-index) |
| `apps/web/e2e/visual-conformity/__tests__/conformity-bootstrap-marker.test.ts` | Unit test marker enforcement | Create: vitest test che esercita la logica selector resolution senza Playwright |
| `admin-mockups/design_files/nanolith-runthrough-game-detail.html` | Pure-CSS mockup library-game-detail | Modify: aggiungere `data-conformity-screen` su sezione "Stato 1 · Default" + sua phone |
| `admin-mockups/design_files/sp4-library-desktop.jsx` | React mockup library | Modify: marker su `DesktopFrame #09` + primo `PhoneShell` con stato default |
| `admin-mockups/design_files/sp4-player-detail.jsx` | React mockup player-detail | Modify: marker su `DesktopFrame #01 Overview` + corrispondente `PhoneShell` |
| `admin-mockups/design_files/sp4-game-nights-index.jsx` | React mockup game-nights-index | Modify: marker su `DesktopFrame #06 Empty` (vedi spec R6) + `PhoneShell` empty |
| `docs/for-developers/testing/frontend/mockup-conformity.md` | Conformity gate runbook | Modify: aggiungere section "Single-screen baseline marker contract" |

**Branch:** `feature/issue-1269-conformity-baseline-single-screen` da `main-dev` (per project rule: detect parent branch).

**Commit cadence:** 1 commit per Task (12 commits totali). PR atomica (tutti i task in 1 PR per evitare bisect breakage — vedi Fowler review #F2).

---

## Pre-flight Tasks

### Task 0: Branch creation + safety check

**Files:** N/A (git operations)

- [ ] **Step 0.1: Verify HEAD on main-dev clean**

Run: `git branch --show-current`
Expected: `main-dev`

Run: `git status`
Expected: clean tree (nothing to commit)

If HEAD is not on `main-dev`: STOP. Run `git checkout main-dev` first.

- [ ] **Step 0.2: Pull latest main-dev with fast-forward only**

Run: `git pull --ff-only`
Expected: `Already up to date.` OR fast-forward applied.

If pull fails (diverged): STOP. Resolve divergence before proceeding.

- [ ] **Step 0.3: Create feature branch**

Run: `git checkout -b feature/issue-1269-conformity-baseline-single-screen`
Expected: `Switched to a new branch 'feature/issue-1269-conformity-baseline-single-screen'`

- [ ] **Step 0.4: Record parent branch (per CLAUDE.md PR rule)**

Run: `git config branch.feature/issue-1269-conformity-baseline-single-screen.parent main-dev`
Expected: no output (config set silently)

Verify:
Run: `git config branch.feature/issue-1269-conformity-baseline-single-screen.parent`
Expected: `main-dev`

- [ ] **Step 0.5: Commit checkpoint (no changes — just to verify hooks work)**

This step has no commit (branch creation alone, no diff). Proceed to Task 1.

---

## Implementation Tasks

### Task 1: Unit test scaffolding for marker validation logic

**Goal:** Estraggo la logica di selector resolution e validation in helper testabile via Vitest (no Playwright dependency). Permette TDD del Task 2 (modifica bootstrap.spec.ts) senza eseguire Playwright suite intera ogni ciclo.

**Files:**
- Create: `apps/web/e2e/visual-conformity/marker-utils.ts`
- Create: `apps/web/e2e/visual-conformity/__tests__/marker-utils.test.ts`

- [ ] **Step 1.1: Write the failing test (marker-utils.test.ts)**

Create `apps/web/e2e/visual-conformity/__tests__/marker-utils.test.ts` with:

```typescript
import { describe, it, expect } from 'vitest';

import {
  getViewportFromProjectName,
  buildMarkerSelector,
  validateMarkerCount,
} from '../marker-utils';

describe('marker-utils', () => {
  describe('getViewportFromProjectName', () => {
    it('returns "desktop" for conformity-bootstrap-desktop', () => {
      expect(getViewportFromProjectName('conformity-bootstrap-desktop')).toBe('desktop');
    });

    it('returns "mobile" for conformity-bootstrap-mobile', () => {
      expect(getViewportFromProjectName('conformity-bootstrap-mobile')).toBe('mobile');
    });

    it('returns "desktop" for conformity-verify-desktop', () => {
      expect(getViewportFromProjectName('conformity-verify-desktop')).toBe('desktop');
    });

    it('returns "mobile" for conformity-verify-mobile', () => {
      expect(getViewportFromProjectName('conformity-verify-mobile')).toBe('mobile');
    });

    it('throws for unknown project name', () => {
      expect(() => getViewportFromProjectName('desktop-chrome')).toThrow(
        /Unknown conformity project: desktop-chrome/
      );
    });

    it('throws for empty string', () => {
      expect(() => getViewportFromProjectName('')).toThrow(/Unknown conformity project/);
    });
  });

  describe('buildMarkerSelector', () => {
    it('builds selector for default-desktop', () => {
      expect(buildMarkerSelector('desktop')).toBe('[data-conformity-screen="default-desktop"]');
    });

    it('builds selector for default-mobile', () => {
      expect(buildMarkerSelector('mobile')).toBe('[data-conformity-screen="default-mobile"]');
    });
  });

  describe('validateMarkerCount', () => {
    it('passes silently when count is 1', () => {
      expect(() => validateMarkerCount(1, 'sp4-library-desktop.html', 'default-desktop')).not.toThrow();
    });

    it('throws actionable message when count is 0', () => {
      expect(() =>
        validateMarkerCount(0, 'sp4-future-route.html', 'default-desktop')
      ).toThrow(
        'Mockup sp4-future-route.html missing required marker. ' +
          'Add data-conformity-screen="default-desktop" to the element that ' +
          'represents the canonical default desktop screen.'
      );
    });

    it('throws when count is 2+ (duplicate marker)', () => {
      expect(() =>
        validateMarkerCount(2, 'sp4-library-desktop.html', 'default-desktop')
      ).toThrow(
        'Mockup sp4-library-desktop.html has 2 elements with data-conformity-screen="default-desktop". ' +
          'Exactly one is required.'
      );
    });

    it('throws with correct count for 5 duplicates', () => {
      expect(() =>
        validateMarkerCount(5, 'sp4-library-desktop.html', 'default-mobile')
      ).toThrow(
        'Mockup sp4-library-desktop.html has 5 elements with data-conformity-screen="default-mobile". ' +
          'Exactly one is required.'
      );
    });
  });
});
```

- [ ] **Step 1.2: Run test to verify it fails**

Run from `apps/web/` directory:
```
pnpm exec vitest run e2e/visual-conformity/__tests__/marker-utils.test.ts
```
Expected: FAIL with `Failed to resolve import "../marker-utils"` (file doesn't exist yet).

- [ ] **Step 1.3: Write minimal implementation (marker-utils.ts)**

Create `apps/web/e2e/visual-conformity/marker-utils.ts`:

```typescript
/**
 * WS-C single-screen marker utilities — shared by bootstrap.spec.ts and unit tests.
 *
 * Extracts viewport detection, selector construction, and marker count validation
 * to enable unit testing the contract enforcement logic without spinning up Playwright.
 *
 * Refs: spec docs/superpowers/specs/2026-05-18-conformity-baseline-single-screen-design.md
 */

export type ConformityViewport = 'desktop' | 'mobile';

const PROJECT_TO_VIEWPORT: Record<string, ConformityViewport> = {
  'conformity-bootstrap-desktop': 'desktop',
  'conformity-bootstrap-mobile': 'mobile',
  'conformity-verify-desktop': 'desktop',
  'conformity-verify-mobile': 'mobile',
};

/**
 * Map Playwright project name → viewport label. Throws for unrecognized projects
 * to prevent silent misclassification (Fowler review #F1).
 */
export function getViewportFromProjectName(projectName: string): ConformityViewport {
  const viewport = PROJECT_TO_VIEWPORT[projectName];
  if (!viewport) {
    throw new Error(
      `Unknown conformity project: ${projectName}. ` +
        `Expected one of: ${Object.keys(PROJECT_TO_VIEWPORT).join(', ')}.`
    );
  }
  return viewport;
}

/**
 * Build the CSS selector for the single-screen marker.
 */
export function buildMarkerSelector(viewport: ConformityViewport): string {
  return `[data-conformity-screen="default-${viewport}"]`;
}

/**
 * Validate that exactly one marker element was found in the mockup DOM.
 * Throws with actionable error messages (Adzic review #A1 Scenarios 2-3).
 */
export function validateMarkerCount(
  count: number,
  mockupFilename: string,
  markerValue: string
): void {
  if (count === 1) return;

  const viewportLabel = markerValue.endsWith('-desktop') ? 'desktop' : 'mobile';

  if (count === 0) {
    throw new Error(
      `Mockup ${mockupFilename} missing required marker. ` +
        `Add data-conformity-screen="${markerValue}" to the element that ` +
        `represents the canonical default ${viewportLabel} screen.`
    );
  }

  throw new Error(
    `Mockup ${mockupFilename} has ${count} elements with data-conformity-screen="${markerValue}". ` +
      `Exactly one is required.`
  );
}
```

- [ ] **Step 1.4: Run test to verify it passes**

Run from `apps/web/` directory:
```
pnpm exec vitest run e2e/visual-conformity/__tests__/marker-utils.test.ts
```
Expected: PASS (all 12 tests green).

- [ ] **Step 1.5: Commit**

```
git add apps/web/e2e/visual-conformity/marker-utils.ts apps/web/e2e/visual-conformity/__tests__/marker-utils.test.ts
git commit -m "$(cat <<'EOF'
feat(conformity): marker-utils helper + 12 unit tests (refs #1269)

Extract viewport detection, selector construction, and marker count
validation as testable pure functions. Enables TDD of bootstrap.spec.ts
modification without Playwright spin-up cycle.

Spec: docs/superpowers/specs/2026-05-18-conformity-baseline-single-screen-design.md

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Modify bootstrap.spec.ts to use locator-based screenshot

**Goal:** Sostituire la cattura `fullPage` con cattura del solo locator marked. Hard-fail con messaggi actionable se marker mancante o duplicato.

**Files:**
- Modify: `apps/web/e2e/visual-conformity/bootstrap.spec.ts`

- [ ] **Step 2.1: Read current bootstrap.spec.ts (familiarize before editing)**

Read full file: `apps/web/e2e/visual-conformity/bootstrap.spec.ts`.
Note: lines 89-103 contain the test loop with `expect(page).toHaveScreenshot()` to replace.

- [ ] **Step 2.2: Replace the screenshot capture logic**

Edit `apps/web/e2e/visual-conformity/bootstrap.spec.ts`:

Replace the entire test block (currently lines 85-103) with:

```typescript
test.describe('WS-C Conformity — mockup baseline bootstrap', () => {
  // Bootstrap snapshots are deterministic by design; retries would mask flake.
  test.describe.configure({ retries: 0 });

  for (const route of ownership.routes) {
    test(`mockup baseline: ${route.id} (${route.mockup})`, async ({ page }, testInfo) => {
      const viewport = getViewportFromProjectName(testInfo.project.name);
      const markerValue = `default-${viewport}`;
      const selector = buildMarkerSelector(viewport);

      const url = `/${route.mockup}`;
      await page.goto(url, { waitUntil: 'networkidle' });
      await waitForMockupReady(page);

      // Single-screen marker contract enforcement (spec §Marker Contract).
      // The mockup HTML must expose exactly ONE element matching the marker
      // for the current viewport. Hard-fail with actionable message otherwise.
      const target = page.locator(selector);
      const count = await target.count();
      validateMarkerCount(count, route.mockup, markerValue);

      // Snapshot lands at __mockup__/{route.id}.{viewport}.png via project-level
      // snapshotPathTemplate. Bootstrap mode (Phase 3 workflow) passes
      // --update-snapshots to (re)generate; verify mode asserts existence.
      // Capture ONLY the marked locator (not fullPage) so the baseline matches
      // the single-screen live render rather than the multi-state catalog.
      await expect(target).toHaveScreenshot(`${route.id}.png`);
    });
  }
});
```

Also add the import at the top of the file (after existing imports, around line 19):

```typescript
import {
  buildMarkerSelector,
  getViewportFromProjectName,
  validateMarkerCount,
} from './marker-utils';
```

- [ ] **Step 2.3: Update the JSDoc header to reflect single-screen pattern**

Edit the top-of-file comment block (lines 1-14) of `bootstrap.spec.ts`. Replace the entire comment with:

```typescript
/**
 * WS-C Phase 2 — Mockup baseline bootstrap (single-screen mode, v2).
 *
 * Run ONLY by Phase 3 `bootstrap-mockup-baselines.yml` workflow (manual dispatch
 * + auto-trigger on `admin-mockups/design_files/**` change) and from local dev
 * via `pnpm test:visual:conformity:bootstrap:update`.
 *
 * Iterates each entry in `mockup-ownership.bootstrap.json`, navigates the
 * corresponding mockup HTML on the static server (:5174), waits for React + JSX
 * + fonts to settle, then captures the screenshot of the SINGLE element marked
 * with `data-conformity-screen="default-{viewport}"`.
 *
 * Hard-fails if the marker is missing or duplicated — the contract is documented
 * in the spec (Marker Contract section) and enforced by `marker-utils.ts`.
 *
 * Refs: #1269 (single-screen refactor), #1069 (WS-C), #1066 (umbrella), AC-C.6.
 * Spec: docs/superpowers/specs/2026-05-18-conformity-baseline-single-screen-design.md
 */
```

- [ ] **Step 2.4: Sanity check — typecheck the spec file**

Run from `apps/web/` directory:
```
pnpm exec tsc --noEmit
```
Expected: PASS (no TypeScript errors). If errors mention `marker-utils` exports → re-verify Task 1 step 1.3 implementation.

- [ ] **Step 2.5: Commit**

```
git add apps/web/e2e/visual-conformity/bootstrap.spec.ts
git commit -m "$(cat <<'EOF'
feat(conformity): bootstrap.spec.ts single-screen marker capture (refs #1269)

Replace fullPage capture of multi-state mockup gallery with locator-based
capture of single screen marked via data-conformity-screen attribute.
Hard-fail with actionable error if marker is missing or duplicated.

Resolves the structural diff (4000-12000px vs 900-1800px live render) that
caused PR #1267 conformity-waiver bypass.

Spec: docs/superpowers/specs/2026-05-18-conformity-baseline-single-screen-design.md

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Inject marker in nanolith-runthrough mockup (pure-HTML, simplest)

**Goal:** Aggiungere `data-conformity-screen="default-desktop"` e `default-mobile` al mockup pure-HTML `nanolith-runthrough-game-detail.html`. È il più semplice perché HTML statico — niente JSX babel-standalone.

**Files:**
- Modify: `admin-mockups/design_files/nanolith-runthrough-game-detail.html`

- [ ] **Step 3.1: Read the "Stato 1 · Default" section (lines 347-487)**

Read range `admin-mockups/design_files/nanolith-runthrough-game-detail.html` lines 340-500 to identify:
- The `<section class="state-section">` for "Stato 1 · Default" (line 347)
- The `<div class="frames">` (line 352) containing the desktop + phone frames
- The first `.desktop` element and first `.phone` element inside this section

- [ ] **Step 3.2: Inject `default-desktop` marker on the desktop frame**

Edit `admin-mockups/design_files/nanolith-runthrough-game-detail.html`.

Find the FIRST `<div class="desktop">` element inside the `<section class="state-section" aria-labelledby="s1">` block (around line 352-487). Add the `data-conformity-screen="default-desktop"` attribute to it:

```html
<!-- BEFORE (example) -->
<div class="desktop">

<!-- AFTER -->
<div class="desktop" data-conformity-screen="default-desktop">
```

Use Read tool first to verify the exact line, then use Edit tool with sufficient context to make the match unique within the Stato 1 section only.

- [ ] **Step 3.3: Inject `default-mobile` marker on the phone frame**

Find the FIRST `<div class="phone">` element inside the SAME `<section class="state-section" aria-labelledby="s1">` block. Add the `data-conformity-screen="default-mobile"` attribute:

```html
<!-- BEFORE (example) -->
<div class="phone">

<!-- AFTER -->
<div class="phone" data-conformity-screen="default-mobile">
```

- [ ] **Step 3.4: Verify markers are unique in the file (only in Stato 1)**

Run:
```
grep -c 'data-conformity-screen="default-desktop"' admin-mockups/design_files/nanolith-runthrough-game-detail.html
```
Expected: `1`

Run:
```
grep -c 'data-conformity-screen="default-mobile"' admin-mockups/design_files/nanolith-runthrough-game-detail.html
```
Expected: `1`

- [ ] **Step 3.5: Visually verify mockup still renders correctly**

Run the mockup server from `apps/web/`:
```
pnpm exec node ./scripts/serve-mockups.cjs &
```

Open http://localhost:5174/nanolith-runthrough-game-detail.html in browser. Verify "Stato 1 · Default" section renders identically (no layout regression). Stop the server (Ctrl+C or kill background process).

If layout broken: revert step 3.2/3.3 and find a parent wrapper that doesn't carry positioning styles.

- [ ] **Step 3.6: Commit**

```
git add admin-mockups/design_files/nanolith-runthrough-game-detail.html
git commit -m "$(cat <<'EOF'
feat(mockup): nanolith-runthrough single-screen markers (refs #1269)

Add data-conformity-screen="default-desktop" and "default-mobile" markers
to the Stato 1 Default section. Bootstrap workflow will capture only these
locators on next dispatch, replacing the multi-state gallery baseline.

Mockup file: nanolith-runthrough-game-detail.html
Marks: 1 desktop frame + 1 phone frame, both in section[aria-labelledby=s1].

Spec: docs/superpowers/specs/2026-05-18-conformity-baseline-single-screen-design.md

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Inject marker in sp4-library-desktop.jsx

**Goal:** Aggiungere markers su `DesktopFrame #09` (default desktop) e primo `PhoneShell` con stato default (mobile). Pattern React-JSX, marker iniettato come prop su DesktopFrame/PhoneShell che lo propaga al `<div>` wrapper esterno.

**Files:**
- Modify: `admin-mockups/design_files/sp4-library-desktop.jsx`

- [ ] **Step 4.1: Read DesktopFrame component definition (lines 1402-1432)**

Read `admin-mockups/design_files/sp4-library-desktop.jsx` lines 1380-1432 to confirm the `DesktopFrame` and `PhoneShell` component signatures and the outer wrapper element.

- [ ] **Step 4.2: Extend DesktopFrame to accept conformityMarker prop**

Edit `admin-mockups/design_files/sp4-library-desktop.jsx`.

Find this exact block (around lines 1402-1432):

```jsx
const DesktopFrame = ({ label, desc, children, fullWidth }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}>
```

Replace with:

```jsx
const DesktopFrame = ({ label, desc, children, fullWidth, conformityMarker }) => (
  <div
    style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}
    {...(conformityMarker ? { 'data-conformity-screen': conformityMarker } : {})}
  >
```

- [ ] **Step 4.3: Extend PhoneShell to accept conformityMarker prop**

Find this block (around lines 1388-1400):

```jsx
const PhoneShell = ({ label, desc, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 10 }}>
```

Replace with:

```jsx
const PhoneShell = ({ label, desc, children, conformityMarker }) => (
  <div
    style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 10 }}
    {...(conformityMarker ? { 'data-conformity-screen': conformityMarker } : {})}
  >
```

- [ ] **Step 4.4: Apply `default-desktop` marker on DesktopFrame #09**

Find this block (around line 1492-1495):

```jsx
          <DesktopFrame label="09 · Desktop · All · Grid 4-col + Activity rail"
            desc="Vista power-user completa: hero pieno (stats + 3 CTA), 6 tabs, filtri cross-entity sticky, grid 4-col mix entity. Sidebar destra 'Ultime modifiche' timeline + scorciatoie.">
            <DesktopFrameInner stateOverride={null} initialTab="all" initialView="grid" withRail/>
          </DesktopFrame>
```

Replace with:

```jsx
          <DesktopFrame label="09 · Desktop · All · Grid 4-col + Activity rail"
            desc="Vista power-user completa: hero pieno (stats + 3 CTA), 6 tabs, filtri cross-entity sticky, grid 4-col mix entity. Sidebar destra 'Ultime modifiche' timeline + scorciatoie."
            conformityMarker="default-desktop">
            <DesktopFrameInner stateOverride={null} initialTab="all" initialView="grid" withRail/>
          </DesktopFrame>
```

- [ ] **Step 4.5: Apply `default-mobile` marker on first MOBILE_STATES PhoneShell**

Find this block (around lines 1478-1487):

```jsx
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <PhoneScreen
                stateOverride={s.state || null}
                initialTab={s.tab}
                initialView={s.view}
                drawerOpen={s.drawerOpen || false}/>
            </PhoneShell>
          ))}
        </div>
```

We need to mark ONLY the first PhoneShell. Replace with:

```jsx
        <div className="phones-grid">
          {MOBILE_STATES.map((s, idx) => (
            <PhoneShell
              key={s.id}
              label={s.label}
              desc={s.desc}
              conformityMarker={idx === 0 ? 'default-mobile' : undefined}>
              <PhoneScreen
                stateOverride={s.state || null}
                initialTab={s.tab}
                initialView={s.view}
                drawerOpen={s.drawerOpen || false}/>
            </PhoneShell>
          ))}
        </div>
```

- [ ] **Step 4.6: Verify exactly one marker each in the file**

Run:
```
grep -c "'data-conformity-screen': 'default-desktop'" admin-mockups/design_files/sp4-library-desktop.jsx
```
Expected: `0` (because the marker is interpolated via prop conditional, not as literal in JSX).

Instead verify via:
```
grep -c "conformityMarker=\"default-desktop\"" admin-mockups/design_files/sp4-library-desktop.jsx
```
Expected: `1`

```
grep -c "conformityMarker=\"default-mobile\"" admin-mockups/design_files/sp4-library-desktop.jsx
```
Expected: `0` (because mobile marker is passed conditionally as `idx === 0 ? 'default-mobile' : undefined`).

Verify mobile marker presence via:
```
grep -c "'default-mobile'" admin-mockups/design_files/sp4-library-desktop.jsx
```
Expected: `1`

- [ ] **Step 4.7: Visually verify mockup renders correctly**

Start mockup server:
```
pnpm exec node ./scripts/serve-mockups.cjs &
```

Open http://localhost:5174/sp4-library-desktop.html. Verify:
- DesktopFrame #09 renders (look for "09 · Desktop · All · Grid 4-col + Activity rail" label).
- Mobile section "8 stati / variazioni" renders 8 phone shells.
- DevTools console → inspect the `DesktopFrame #09` outer wrapper → verify it has `data-conformity-screen="default-desktop"` attribute.
- Inspect the FIRST PhoneShell → verify it has `data-conformity-screen="default-mobile"`.
- Inspect any OTHER PhoneShell → verify it has NO `data-conformity-screen` attribute (idx > 0 → undefined → not rendered).

Stop server.

- [ ] **Step 4.8: Commit**

```
git add admin-mockups/design_files/sp4-library-desktop.jsx
git commit -m "$(cat <<'EOF'
feat(mockup): sp4-library-desktop single-screen markers (refs #1269)

Extend DesktopFrame + PhoneShell components to accept conformityMarker prop
that renders as data-conformity-screen attribute on the outer wrapper.

Apply markers:
- DesktopFrame #09 (All · Grid 4-col + Activity rail) → default-desktop
- First MOBILE_STATES PhoneShell (default state) → default-mobile

Other frames/shells unchanged (no marker propagation).

Spec: docs/superpowers/specs/2026-05-18-conformity-baseline-single-screen-design.md

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Inject marker in sp4-player-detail.jsx

**Goal:** Marker su `DesktopFrame Desktop · 01 · Registered · Overview` + primo PhoneShell mobile (variant=registered, tab=overview).

**Files:**
- Modify: `admin-mockups/design_files/sp4-player-detail.jsx`

- [ ] **Step 5.1: Read DesktopFrame + PhoneShell component definitions**

Read `admin-mockups/design_files/sp4-player-detail.jsx` to identify the `DesktopFrame` and `PhoneShell` component signatures. Use grep:

```
grep -n "const DesktopFrame\|const PhoneShell" admin-mockups/design_files/sp4-player-detail.jsx
```

Note the line numbers. The components are likely defined in similar style to sp4-library-desktop.jsx.

- [ ] **Step 5.2: Extend DesktopFrame component to accept conformityMarker prop**

Read the current `DesktopFrame` component definition (around the line found in 5.1). Apply the SAME pattern as Task 4.2:

Find:
```jsx
const DesktopFrame = ({ label, desc, children }) => (
  <div style={{ ... }}>
```

Replace with:
```jsx
const DesktopFrame = ({ label, desc, children, conformityMarker }) => (
  <div
    style={{ ... }}
    {...(conformityMarker ? { 'data-conformity-screen': conformityMarker } : {})}
  >
```

(Preserve the exact `style` content of the original.)

- [ ] **Step 5.3: Extend PhoneShell component the same way**

Read the current `PhoneShell` component definition. Apply same pattern:

Find:
```jsx
const PhoneShell = ({ label, desc, children }) => (
  <div style={{ ... }}>
```

Replace with:
```jsx
const PhoneShell = ({ label, desc, children, conformityMarker }) => (
  <div
    style={{ ... }}
    {...(conformityMarker ? { 'data-conformity-screen': conformityMarker } : {})}
  >
```

- [ ] **Step 5.4: Apply `default-desktop` marker on DesktopFrame #01 Overview**

Find this block (around line 1425-1428):

```jsx
        <DesktopFrame label="Desktop · 01 · Registered · Overview"
          desc="Hero 128px avatar gradient + nome 36 + handle/città/membro mono + 4 stat top-line + ConnectionBar 6 pip sticky + tabs animated underline + body 2-col grid (KPI top, agente preferito, top games leaderboard, trend chart).">
          <DesktopScreen variant="registered" initialTab="overview"/>
        </DesktopFrame>
```

Replace with:

```jsx
        <DesktopFrame label="Desktop · 01 · Registered · Overview"
          desc="Hero 128px avatar gradient + nome 36 + handle/città/membro mono + 4 stat top-line + ConnectionBar 6 pip sticky + tabs animated underline + body 2-col grid (KPI top, agente preferito, top games leaderboard, trend chart)."
          conformityMarker="default-desktop">
          <DesktopScreen variant="registered" initialTab="overview"/>
        </DesktopFrame>
```

- [ ] **Step 5.5: Apply `default-mobile` marker on first MOBILE_STATES PhoneShell**

Find the MOBILE_STATES.map block (around line 1478-1482):

```jsx
          {MOBILE_STATES.map(m => (
            <PhoneShell key={m.id} label={m.label} desc={m.desc}>
              <MobileScreen stateOverride={m.stateOverride} variant={m.variant} initialTab={m.tab}/>
            </PhoneShell>
          ))}
```

Replace with:

```jsx
          {MOBILE_STATES.map((m, idx) => (
            <PhoneShell
              key={m.id}
              label={m.label}
              desc={m.desc}
              conformityMarker={idx === 0 ? 'default-mobile' : undefined}>
              <MobileScreen stateOverride={m.stateOverride} variant={m.variant} initialTab={m.tab}/>
            </PhoneShell>
          ))}
```

- [ ] **Step 5.6: Verify marker counts**

Run:
```
grep -c "conformityMarker=\"default-desktop\"" admin-mockups/design_files/sp4-player-detail.jsx
```
Expected: `1`

Run:
```
grep -c "'default-mobile'" admin-mockups/design_files/sp4-player-detail.jsx
```
Expected: `1`

- [ ] **Step 5.7: Visually verify mockup renders correctly**

Start mockup server, open http://localhost:5174/sp4-player-detail.html. Verify:
- DesktopFrame #01 Overview renders without layout change.
- First PhoneShell in mobile section has `data-conformity-screen="default-mobile"` (DevTools inspect).
- Mobile section "7 stati" is intact.

**CRITICAL CHECK (per Risk R4 in spec):** Verify that the FIRST MOBILE_STATES entry corresponds to a "registered overview" variant (not "guest" or "empty"). Inspect the array definition in the file. If the first entry is NOT a registered-overview default state, change the marker condition to `m.id === 'registered-overview'` (or equivalent stable identifier) instead of `idx === 0`. Document the decision in the commit message.

Stop server.

- [ ] **Step 5.8: Commit**

```
git add admin-mockups/design_files/sp4-player-detail.jsx
git commit -m "$(cat <<'EOF'
feat(mockup): sp4-player-detail single-screen markers (refs #1269)

Extend DesktopFrame + PhoneShell components to accept conformityMarker prop.
Apply markers:
- DesktopFrame Desktop · 01 · Registered · Overview → default-desktop
- First MOBILE_STATES PhoneShell (registered overview) → default-mobile

Verified: marker scene matches PlayerDetailView fixture render
(IS_VISUAL_TEST_BUILD = Sara Rossi profile, overview tab default).

Spec: docs/superpowers/specs/2026-05-18-conformity-baseline-single-screen-design.md

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Inject marker in sp4-game-nights-index.jsx (with R6 mitigation)

**Goal:** Apply markers su mockup game-nights. Per spec Risk R6 e Open Issue §1: la live route in CI senza fixture renderizza `game-nights-empty` state. Per matchare, marker su `DesktopFrame #06 Empty` (e mobile empty equivalent). Se non praticabile, escalation entro 2h investigation budget.

**Files:**
- Modify: `admin-mockups/design_files/sp4-game-nights-index.jsx`

- [ ] **Step 6.1: Investigation budget — verify live route render in CI**

Read `apps/web/src/app/(authenticated)/game-nights/_content.tsx` lines 280-289 (the `allVms.length === 0` empty branch):

The empty state renders:
- `<GameNightsHeader>` (always, even in empty)
- `<EmptyState data-testid="game-nights-empty">` with `<h2>`, `<p>`, `<button>` "Crea la prima serata"

This matches the mockup "Desktop · 06 · Empty" scene which renders "Header normale ma body empty state: icona event 80px + h3 + CTA gradient grande '+ Crea la prima serata'." ✅ Path R6 (a) is viable.

- [ ] **Step 6.2: Read DesktopFrame component definition (already PHone in sp4-game-nights-index.jsx)**

Run:
```
grep -n "const DesktopFrame\|const PhoneShell" admin-mockups/design_files/sp4-game-nights-index.jsx
```

Read the relevant lines to confirm component signatures.

- [ ] **Step 6.3: Extend DesktopFrame + PhoneShell with conformityMarker prop**

Apply the same pattern as Task 4.2 and 4.3 (or Task 5.2 and 5.3) to the `DesktopFrame` and `PhoneShell` components in `sp4-game-nights-index.jsx`. Use Read tool to get exact current shapes before Edit.

- [ ] **Step 6.4: Apply `default-desktop` marker on DesktopFrame #06 Empty**

Find this block (around line 1158-1161):

```jsx
        <DesktopFrame label="Desktop · 06 · Empty"
          desc="Header normale ma body empty state: icona event 80px + h3 + CTA gradient grande '+ Crea la prima serata'.">
          <DesktopScreen initialView="calendar" stateOverride="empty"/>
        </DesktopFrame>
```

Replace with:

```jsx
        <DesktopFrame label="Desktop · 06 · Empty"
          desc="Header normale ma body empty state: icona event 80px + h3 + CTA gradient grande '+ Crea la prima serata'."
          conformityMarker="default-desktop">
          <DesktopScreen initialView="calendar" stateOverride="empty"/>
        </DesktopFrame>
```

- [ ] **Step 6.5: Identify and mark mobile empty PhoneShell**

Read MOBILE_STATES array definition in `sp4-game-nights-index.jsx`. Find the entry with `stateOverride: 'empty'` or equivalent empty indication. Note its `id` field value.

Find the MOBILE_STATES.map block. Replace the un-marked map with conditional marker:

```jsx
          {MOBILE_STATES.map((m, idx) => (
            <PhoneShell
              key={m.id}
              label={m.label}
              desc={m.desc}
              conformityMarker={m.stateOverride === 'empty' ? 'default-mobile' : undefined}>
              <MobileScreen initialView={m.view} initialRole={m.role}
                stateOverride={m.stateOverride} drawerDay={m.drawerDay}/>
            </PhoneShell>
          ))}
```

(The key change: `conformityMarker={m.stateOverride === 'empty' ? 'default-mobile' : undefined}` instead of `idx === 0` — because mobile default per spec wave 3 is empty state for parity with CI live render.)

If MOBILE_STATES has NO entry with `stateOverride: 'empty'`: STOP. Escalation: open follow-up issue per Open Issue §1 (build-time fixture) and skip game-nights mobile marker. Mark only desktop. Documented in commit.

- [ ] **Step 6.6: Verify marker counts**

Run:
```
grep -c "conformityMarker=\"default-desktop\"" admin-mockups/design_files/sp4-game-nights-index.jsx
```
Expected: `1`

Run:
```
grep -c "'default-mobile'" admin-mockups/design_files/sp4-game-nights-index.jsx
```
Expected: `1` (if mobile empty entry exists) or `0` (if escalated to follow-up).

- [ ] **Step 6.7: Visually verify mockup**

Start mockup server, open http://localhost:5174/sp4-game-nights-index.html. Verify:
- DesktopFrame #06 Empty has `data-conformity-screen="default-desktop"` (DevTools inspect).
- Mobile section: ONE PhoneShell with the empty stateOverride has `data-conformity-screen="default-mobile"`.
- All other frames/shells have NO marker.

Stop server.

- [ ] **Step 6.8: Commit**

```
git add admin-mockups/design_files/sp4-game-nights-index.jsx
git commit -m "$(cat <<'EOF'
feat(mockup): sp4-game-nights-index single-screen markers (refs #1269)

Extend DesktopFrame + PhoneShell components to accept conformityMarker prop.
Apply markers:
- DesktopFrame Desktop · 06 · Empty → default-desktop
- MOBILE_STATES entry with stateOverride=empty → default-mobile

Rationale (spec Risk R6): live route in CI without backend at :8080 renders
game-nights-empty state. Matching the empty mockup scene ensures conformity
gate passes deterministic without requiring build-time fixture (deferred to
Open Issue §1 if needed).

Spec: docs/superpowers/specs/2026-05-18-conformity-baseline-single-screen-design.md

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: Add RUNBOOK entries for player-detail + game-nights-index in conformity.spec.ts

**Goal:** `conformity.spec.ts` throw error "No runbook for route id" per le 2 route nuove. Aggiungere entries con livePath fixture (player-detail usa `IS_VISUAL_TEST_BUILD` short-circuit, game-nights usa empty state in assenza di backend).

**Files:**
- Modify: `apps/web/e2e/visual-conformity/conformity.spec.ts`

- [ ] **Step 7.1: Read conformity.spec.ts RUNBOOKS object (lines 59-68)**

Read `apps/web/e2e/visual-conformity/conformity.spec.ts` lines 50-75 to confirm current RUNBOOKS structure.

- [ ] **Step 7.2: Add 2 new RUNBOOK entries**

Edit `apps/web/e2e/visual-conformity/conformity.spec.ts`.

Find this exact block (lines 59-68):

```typescript
const RUNBOOKS: Record<string, RouteRunbook> = {
  library: {
    readySelector: '[data-slot="library-hub-v2"]',
    contentSelector: '[data-slot="library-grid-card"]',
  },
  'library-game-detail': {
    search: '?state=default',
    readySelector: 'main',
  },
};
```

Replace with:

```typescript
const RUNBOOKS: Record<string, RouteRunbook> = {
  library: {
    readySelector: '[data-slot="library-hub-v2"]',
    contentSelector: '[data-slot="library-grid-card"]',
  },
  'library-game-detail': {
    search: '?state=default',
    readySelector: 'main',
  },
  'player-detail': {
    // PlayerDetailView short-circuits to Sara Rossi fixture when
    // NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1 (baked into CI prod build).
    // ?state=default is the explicit no-op override that confirms fixture path.
    search: '?state=default',
    readySelector: '[data-slot="player-detail-view"]',
  },
  'game-nights-index': {
    // No build-time fixture for game-nights (tracked as Open Issue §1 in spec).
    // Live route in CI without backend renders empty state — matched by
    // mockup marker on DesktopFrame #06 Empty (spec Risk R6 mitigation path a).
    readySelector: '[data-testid="game-nights-empty"]',
  },
};
```

- [ ] **Step 7.3: Verify typecheck still passes**

Run from `apps/web/` directory:
```
pnpm exec tsc --noEmit
```
Expected: PASS.

- [ ] **Step 7.4: Verify all 4 routes in ownership now have runbook (no more "No runbook" errors at parse time)**

Run a syntax sanity check:
```
grep -E "^\s*'(library|library-game-detail|player-detail|game-nights-index)':" apps/web/e2e/visual-conformity/conformity.spec.ts | wc -l
```
Expected: `4`

- [ ] **Step 7.5: Commit**

```
git add apps/web/e2e/visual-conformity/conformity.spec.ts
git commit -m "$(cat <<'EOF'
feat(conformity): add RUNBOOK entries for player-detail + game-nights-index (refs #1269)

Eliminates "No runbook for route id" hard-error on 2 of 4 routes in waiver
PR #1267. Pairs with the single-screen marker bootstrap refactor:

- player-detail: ?state=default + [data-slot="player-detail-view"] selector,
  pairs with sp4-player-detail.jsx Desktop · 01 Overview marker.
- game-nights-index: empty-state ready selector, pairs with
  sp4-game-nights-index.jsx Desktop · 06 Empty marker (spec R6 path a).

Spec: docs/superpowers/specs/2026-05-18-conformity-baseline-single-screen-design.md

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: Documentation update — mockup-conformity.md

**Goal:** Documentare il contratto `data-conformity-screen` nel runbook esistente. Necessario per onboarding di nuovi mockup post-merge.

**Files:**
- Modify: `docs/for-developers/testing/frontend/mockup-conformity.md`

- [ ] **Step 8.1: Read current mockup-conformity.md table of contents**

Read `docs/for-developers/testing/frontend/mockup-conformity.md` first 100 lines to find an appropriate insertion point for the new section. Likely insertion location: after a section describing baseline regeneration but before "Troubleshooting".

- [ ] **Step 8.2: Append "Single-screen baseline marker contract" section**

Edit `docs/for-developers/testing/frontend/mockup-conformity.md`.

Append the following new section at an appropriate location (after baseline regeneration section, before troubleshooting):

```markdown
## Single-screen baseline marker contract

> Introduced 2026-05-18 (issue #1269) — fixes the structural diff between
> multi-state mockup gallery captures and single-screen live route renders.

### Why

Mockup HTML files under `admin-mockups/design_files/` are **design catalogs**
that stack every state variant (default + loading + empty + error + mobile +
desktop) vertically. Capturing `fullPage` of the mockup produces a 4000-12000px
tall baseline, while the live Next.js route renders a single screen of
900-1800px. The diff was always ~80% pixels different, regardless of
implementation faithfulness.

### Contract

Every mockup mapped in `mockup-ownership.bootstrap.json` MUST expose exactly
ONE element matching the marker pattern, per viewport:

```html
<div data-conformity-screen="default-desktop">
  <!-- The exact content the live route renders at 1280×N viewport -->
</div>
<div data-conformity-screen="default-mobile">
  <!-- The exact content the live route renders at 375×N viewport -->
</div>
```

**Rules** (enforced by `apps/web/e2e/visual-conformity/marker-utils.ts`):

- Exactly **one** element per `data-conformity-screen` value per mockup.
- Marker values are case-sensitive lowercase: `default-desktop`, `default-mobile`.
- The marked element MUST be in normal document flow (no `position:absolute`,
  no `transform:scale(…)`) so `boundingBox()` returns full content dimensions.
- The marked element MUST NOT be hidden via `display:none`, `visibility:hidden`,
  or `opacity:0`.
- **v1 scope**: only `default-{viewport}` supported. Future multi-state
  (e.g. `empty-desktop`) would extend via `ownership.json` `screens: []` field.

### Hard-fail behavior

If a mockup lacks the marker, `bootstrap.spec.ts` throws:

```
Mockup <filename> missing required marker. Add data-conformity-screen="default-<viewport>"
to the element that represents the canonical default <viewport> screen.
```

If a mockup has the marker on 2+ elements:

```
Mockup <filename> has <N> elements with data-conformity-screen="default-<viewport>".
Exactly one is required.
```

These errors are by design — silent fallbacks would re-introduce the original
bug. Add the marker before mapping a new route in `mockup-ownership.bootstrap.json`.

### How to apply marker to a new mockup

**React-bootstrapped mockup (`*.jsx`)** — pattern: extend wrapper component:

```jsx
const DesktopFrame = ({ label, desc, children, conformityMarker }) => (
  <div
    style={{ /* original styles */ }}
    {...(conformityMarker ? { 'data-conformity-screen': conformityMarker } : {})}
  >
    {children}
  </div>
);

// Apply to the canonical scene:
<DesktopFrame
  label="Desktop · 01 · Default"
  conformityMarker="default-desktop">
  <YourDefaultScene/>
</DesktopFrame>
```

**Pure-HTML mockup (`*.html`)** — add attribute directly:

```html
<section class="state-section" aria-labelledby="s1">
  <span class="label">Stato 1 · Default</span>
  <h2 id="s1">Default state</h2>
  <div class="frames">
    <div class="desktop" data-conformity-screen="default-desktop">
      <!-- canonical default desktop content -->
    </div>
    <div class="phone" data-conformity-screen="default-mobile">
      <!-- canonical default mobile content -->
    </div>
  </div>
</section>
```

### Choosing which scene to mark

Pick the scene that matches what the **live route renders deterministically in
CI** (without backend at :8080):

- Routes with `IS_VISUAL_TEST_BUILD` fixture (e.g. `/library`, `/players/[id]`):
  mark the **populated default** scene (fixture renders deterministic data).
- Routes without fixture (e.g. `/game-nights`): mark the **empty state** scene
  (live route renders empty state when no backend data is available).

If neither matches, you have two options:
1. Add a build-time `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED` short-circuit
   to the route's orchestrator (pattern: see `lib/library/visual-test-fixture.ts`).
2. Document the case in `mockup-ownership.bootstrap.json` `notes` and open a
   follow-up issue.
```

- [ ] **Step 8.3: Commit**

```
git add docs/for-developers/testing/frontend/mockup-conformity.md
git commit -m "$(cat <<'EOF'
docs(conformity): add single-screen baseline marker contract section (refs #1269)

Documents the data-conformity-screen contract introduced by issue #1269.
Includes rules, hard-fail behavior, JSX/HTML implementation patterns, and
guidance for choosing which scene to mark (fixture vs empty state).

Spec: docs/superpowers/specs/2026-05-18-conformity-baseline-single-screen-design.md

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

### Task 9: Local validation — run conformity bootstrap to verify markers work

**Goal:** Run `pnpm test:visual:conformity:bootstrap` locally to verify all 4 mockups have markers correctly detected. Don't `--update-snapshots` (baseline regen reserved for CI).

**Files:** N/A (verification only)

- [ ] **Step 9.1: Stop any background mockup server from previous tasks**

Run:
```
ps aux | grep "serve-mockups" | grep -v grep
```
If output shows running processes, kill them:
```
kill <PID>
```

- [ ] **Step 9.2: Run conformity bootstrap (verify mode — no --update)**

Run from `apps/web/` directory:
```
pnpm test:visual:conformity:bootstrap
```

This will:
- Start mockup static server on :5174
- Run bootstrap.spec.ts on both desktop and mobile projects
- For each route, navigate the mockup, find the marker, screenshot the locator.
- COMPARE against the existing committed baseline PNG.

**Expected outcome:** 8 tests FAIL with "image dimensions don't match" because the new locator-based screenshot is ~1800px tall but the committed baseline (from PR #1234) is 4000-12000px tall.

**This is expected and good** — it confirms:
1. Marker detection works (no "missing marker" hard-fail).
2. Locator screenshot capture works (sized to actual content, not fullPage).
3. The diff is exactly the "structural bug" that this PR fixes — and PR #2
   auto-baseline regen will resolve it.

If a test fails with "Mockup ... missing required marker" → STOP. Re-verify
the relevant Task 3/4/5/6 marker injection.

If a test fails with `TypeError` or import error → STOP. Re-verify
Task 1/2 marker-utils + bootstrap.spec.ts changes.

- [ ] **Step 9.3: Inspect one failed PNG to confirm locator was captured correctly**

Run:
```
ls apps/web/test-results/ | head -5
```

You should see directories like `bootstrap-WS-C-Conformity-mockup-baseline-library-id-library-conformity-bootstrap-desktop/`.

Navigate to one of them, open the `*-actual.png` file in an image viewer (or via macOS `open`, Linux `xdg-open`, Windows `start`). Verify:
- The PNG shows ONLY the marked desktop frame (NOT the entire multi-state catalog).
- Dimensions are roughly 1280px wide × 800-2200px tall (per AC-2).

- [ ] **Step 9.4: Run marker-utils unit tests one more time (regression check)**

Run from `apps/web/` directory:
```
pnpm exec vitest run e2e/visual-conformity/__tests__/marker-utils.test.ts
```
Expected: PASS (all 12 tests green).

- [ ] **Step 9.5: Commit checkpoint (no changes — verification only)**

This step has no commit. If all checks pass, proceed to Task 10.

---

### Task 10: Push branch + open PR with waiver rationale

**Goal:** Push branch to remote, open PR targeting `main-dev`, apply `conformity-waiver` label with the spec-defined rationale comment, document the auto-debt-issue cycle in the PR body.

**Files:** N/A (GitHub operations)

- [ ] **Step 10.1: Push branch to remote**

Run:
```
git push -u origin feature/issue-1269-conformity-baseline-single-screen
```
Expected: branch pushed, upstream tracking set.

- [ ] **Step 10.2: Verify no unintended files staged**

Run:
```
git diff main-dev...HEAD --stat
```
Expected: only the 8 files modified in Tasks 1-8 should appear:
- `apps/web/e2e/visual-conformity/marker-utils.ts` (NEW)
- `apps/web/e2e/visual-conformity/__tests__/marker-utils.test.ts` (NEW)
- `apps/web/e2e/visual-conformity/bootstrap.spec.ts`
- `apps/web/e2e/visual-conformity/conformity.spec.ts`
- `admin-mockups/design_files/nanolith-runthrough-game-detail.html`
- `admin-mockups/design_files/sp4-library-desktop.jsx`
- `admin-mockups/design_files/sp4-player-detail.jsx`
- `admin-mockups/design_files/sp4-game-nights-index.jsx`
- `docs/for-developers/testing/frontend/mockup-conformity.md`

If any other file appears → investigate before pushing.

- [ ] **Step 10.3: Create PR targeting main-dev**

Run:
```
gh pr create --base main-dev --title "fix(conformity): single-screen baseline markers (closes #1269)" --body "$(cat <<'EOF'
## Summary
- Replaces the WS-C conformity baseline pattern: from `fullPage` capture of multi-state mockup gallery (4000-12000px) to `locator` capture of single screen marked via `data-conformity-screen` (~900-1800px, matches live route render).
- Closes #1269 (conformity-debt waived in PR #1267, expires 2026-06-17).
- Resolves structural diff bug introduced PR #1117/#1234 that caused all 4 routes (library, library-game-detail, player-detail, game-nights-index) to always fail conformity gate regardless of implementation faithfulness.

## Changes
- `marker-utils.ts` + 12 vitest unit tests for selector resolution + hard-fail validation.
- `bootstrap.spec.ts` locator-based screenshot with hard-fail on missing/duplicate marker.
- `conformity.spec.ts` 2 new RUNBOOK entries (player-detail, game-nights-index).
- 4 mockup HTML/JSX with `data-conformity-screen` markers on the canonical default scene.
- `docs/for-developers/testing/frontend/mockup-conformity.md` updated with contract documentation.

## ⚠️ Waiver + Auto-debt-issue cycle (expected)

This PR applies a temporary `conformity-waiver` label because the conformity gate (`visual-regression-conformity.yml`) triggers on `admin-mockups/design_files/**` paths and would fail against the OLD multi-state baseline. The waiver is short-lived:

1. **PR #1 (this)** — code changes only. Waiver lifetime 7 days.
2. **After merge** — `bootstrap-mockup-baselines.yml` auto-fires on `admin-mockups/**` push, regenerating 8 new single-screen baselines. Opens auto-PR #2.
3. **PR #2 merge** — conformity gate becomes green for all 4 routes.

The waiver triggers `conformity-debt-issue.yml` to auto-create a NEW debt-issue (with fresh waiver-key); that new issue auto-closes when PR #2 merges and gate is green. Issue #1269 (the ORIGINAL debt) closes via "Closes #1269" syntax in this PR body — that's NOT a double-close conflict, the two issues are distinct cycles.

## Test plan
- [x] `pnpm exec vitest run e2e/visual-conformity/__tests__/marker-utils.test.ts` — 12 tests passing
- [x] `pnpm test:visual:conformity:bootstrap` locally — confirms locator capture works, fails on dimension diff vs old baseline (expected, fixes in PR #2)
- [x] Visual inspection: each mockup renders correctly with marker injected, DevTools confirms exactly 1 marker per viewport
- [ ] Post-merge: workflow_dispatch on `bootstrap-mockup-baselines.yml` → review auto-PR #2 → merge
- [ ] Post-PR-#2-merge: smoke conformity gate on any subsequent PR touching one of the 4 trigger paths → SUCCESS

## Spec / Design
- Design spec: `docs/superpowers/specs/2026-05-18-conformity-baseline-single-screen-design.md`
- Implementation plan: `docs/superpowers/plans/2026-05-18-conformity-baseline-single-screen.md`
- Spec review (sc:spec-panel critique): Wiegers + Fowler + Nygard + Adzic + Crispin, 4 P0 fixes applied inline

Refs: #1023 (DS umbrella), #1066 (WS-C umbrella), #1117 (PR that introduced bug), #1234 (last multi-state baseline regen)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
Expected: PR URL printed.

Note the PR number (let's call it `<PR_N>`).

- [ ] **Step 10.4: Apply conformity-waiver label**

Run:
```
gh pr edit <PR_N> --add-label conformity-waiver
```
Expected: label added.

- [ ] **Step 10.5: Post the waiver rationale comment (exact format required by parser)**

Run:
```
gh pr comment <PR_N> --body "$(cat <<'EOF'
> Conformity waiver rationale:
> Reason: Refactor framework conformity baseline pattern — single-screen markers replace gallery-style fullPage capture. Baseline regen avverrà via auto-PR post-merge da bootstrap-mockup-baselines.yml su trigger admin-mockups/design_files/** push.
> Expiry: 2026-05-25
> Routes: library library-game-detail player-detail game-nights-index
EOF
)"
```
Expected: comment posted.

- [ ] **Step 10.6: Verify CI checks running and conformity-debt-issue.yml fires**

Run:
```
gh pr checks <PR_N>
```
Verify:
- `Conformity Waiver Rationale` → SUCCESS (parser accepted the format above)
- `Conformity Gate (Desktop + Mobile)` → SKIPPED (waiver label opt-out)
- `Manage conformity debt issue` → SUCCESS (auto-created new debt-issue)

If any of these fail → investigate before merge (e.g. if "Conformity Waiver Rationale" fails, the comment format is wrong; re-post the comment with the exact format from step 10.5).

---

### Task 11: Request code review + address findings

**Goal:** Get the PR reviewed via the project's standard subagent code-review flow. Apply findings ≥ 80 confidence inline.

**Files:** N/A (review process)

- [ ] **Step 11.1: Run /code-review:code-review on the PR**

Per project memory rule: `/implementa` and feature PRs MUST include code review before merging.

Run from any terminal:
```
/code-review:code-review <PR_N>
```

Or invoke via the agent flow if available.

- [ ] **Step 11.2: Triage findings by confidence score**

For each finding returned:
- **Score ≥ 80**: apply fix in a new commit `fix(review): <description> (refs #1269)`.
- **Score 60-79**: discuss with reviewer or document as follow-up issue.
- **Score < 60**: ignore (low-confidence noise per project policy).

Per project memory `feedback_code_review_subagent_stacked_pr.md`: if the review reports false-positive BLOCKERS on cross-stack symbols, re-invoke with diff path scope.

- [ ] **Step 11.3: Commit any review fixes + push**

Per-fix commits (one per finding) pushed to the branch. CI re-runs automatically.

---

### Task 12: Merge PR #1 + trigger baseline regen + merge PR #2

**Goal:** Merge PR #1. Verify auto-trigger of `bootstrap-mockup-baselines.yml`. Review auto-PR #2. Merge PR #2. Smoke-test conformity gate.

**Files:** N/A (GitHub operations)

- [ ] **Step 12.1: Verify PR #1 ready to merge**

Run:
```
gh pr view <PR_N> --json statusCheckRollup,reviewDecision
```
Verify:
- All required checks GREEN or non-blocking
- `reviewDecision: APPROVED` (or review-not-required)
- No `merge_state: blocked`

- [ ] **Step 12.2: Merge PR #1 (squash, per repo convention)**

Run:
```
gh pr merge <PR_N> --squash --delete-branch
```
Expected: PR merged, branch deleted from remote.

- [ ] **Step 12.3: Verify bootstrap workflow auto-fired**

Wait ~2 min for GitHub to process the merge event. Then run:
```
gh run list --workflow=bootstrap-mockup-baselines.yml --limit 3
```
Expected: a recent run with status `in_progress` or `completed` triggered by the merge commit.

If NO run appears within 5 minutes:
- Verify the merge commit touched `admin-mockups/design_files/**` paths: `git log --name-only --oneline -1 main-dev`
- If yes, manually dispatch: `gh workflow run bootstrap-mockup-baselines.yml -f reason="Issue #1269 baseline regen post-merge"`

- [ ] **Step 12.4: Wait for auto-PR #2 creation**

Run periodically (max 15 min):
```
gh pr list --search "in:title baselines author:app/github-actions" --json number,title,createdAt --limit 5
```

When auto-PR appears (title `chore(conformity): regenerate mockup baselines (#1069)`), note its number `<PR_N2>`.

- [ ] **Step 12.5: Review auto-PR #2 baseline PNG dimensions**

Run:
```
gh pr view <PR_N2> --json files --jq '.files[].path'
```
Expected: 8 paths under `apps/web/e2e/visual-conformity/__mockup__/`.

Download one of the new PNGs to verify dimensions:
```
gh pr diff <PR_N2> --name-only
```

Use any image inspector (or just check file size — should be much smaller than the previous baseline since dimensions are smaller). Alternative: run from `apps/web/`:
```
gh pr checkout <PR_N2>
ls -la e2e/visual-conformity/__mockup__/*.png
```

Compare file sizes to git history (pre-PR-#2-merge):
```
git log --stat --oneline -1 -- apps/web/e2e/visual-conformity/__mockup__/
```

Expected: new PNGs significantly smaller (~200-500KB vs 1-2MB) due to reduced dimensions.

- [ ] **Step 12.6: Merge PR #2**

Run:
```
gh pr merge <PR_N2> --squash --delete-branch
```

- [ ] **Step 12.7: Smoke test — verify conformity gate now green on a trivial PR**

Optional but recommended: open a small follow-up PR that touches one of the trigger paths (e.g. minor doc tweak in `apps/web/src/app/(authenticated)/library/_components/`). Verify `Conformity Gate (Desktop + Mobile)` returns SUCCESS.

Alternative: check the next existing PR in the queue that touches these paths.

- [ ] **Step 12.8: Verify #1269 closed automatically**

Run:
```
gh issue view 1269 --json state,closedAt,stateReason
```
Expected: `state: CLOSED`, closed at the time of PR #1 merge via `Closes #1269` syntax.

- [ ] **Step 12.9: Verify auto-debt-issue (sibling) closed**

The waiver applied in Task 10 created an auto-debt-issue (e.g. `#1270`). Verify it closed after PR #2 merge:

```
gh issue list --label conformity-debt --state open
```
Expected: NO open conformity-debt issues with routes matching `library`, `library-game-detail`, `player-detail`, `game-nights-index`.

If the auto-debt-issue is still open → manually close with comment explaining PR #2 made the gate green:
```
gh issue close <AUTO_ISSUE_N> --comment "Resolved by PR #<PR_N2> baseline regen; conformity gate green for all 4 routes (verified Task 12.7)."
```

---

## Self-Review

### 1. Spec coverage check

| Spec section / requirement | Plan task |
|----------------------------|-----------|
| Marker contract (DOM-based, exactly one, normal flow, not hidden) | Task 2 (validateMarkerCount) + Task 3-6 (marker injection) |
| Hard-fail behavior with actionable message | Task 1.3 (validateMarkerCount impl) + Task 2.2 (call in spec) |
| Bootstrap impl change (locator vs fullPage) | Task 2.2 |
| 2 new RUNBOOK entries (player-detail, game-nights-index) | Task 7.2 |
| 4 mockup HTML/JSX markers | Tasks 3-6 |
| Documentation update (mockup-conformity.md) | Task 8 |
| Migration path: PR #1 + workflow dispatch + auto-PR #2 | Task 10 + Task 12 |
| Waiver rationale exact format | Task 10.5 |
| Auto-debt-issue cycle explicit | Task 10.3 (PR body) + Task 12.9 |
| Gherkin Scenario 1 (happy path) | Task 9 (local verification) |
| Gherkin Scenario 2 (missing marker hard-fail) | Task 1.1 test + Task 9 (would surface if regression) |
| Gherkin Scenario 3 (duplicate marker hard-fail) | Task 1.1 test |
| Gherkin Scenario 4 (conformity gate green post merge) | Task 12.7 smoke test |
| Gherkin Scenario 5 (regression caught) | Implicit in CI gate behavior post-merge — no specific task needed |
| Edge case React mockup | Tasks 4, 5, 6 (3 React mockups) |
| Edge case Pure-CSS mockup | Task 3 (nanolith-runthrough HTML) |
| Edge case Shadow DOM / iframe | OUT OF SCOPE per spec — no task |
| AC-1 (hard-fail) | Task 1.1 tests + Task 9 local verification |
| AC-2 (height delta < 200px) | Task 9.3 visual PNG inspection |
| AC-3 (4 routes have runbook) | Task 7.4 grep verification |
| AC-4 (conformity gate green post PR #2) | Task 12.7 |
| AC-5 (issue #1269 closed) | Task 12.8 |
| AC-6 (docs section) | Task 8 |
| Out of Scope §1 game-nights fixture | Task 6.5 (R6 path a) + commit message documents fallback |
| Out of Scope §2 multi-state | Documented in marker-utils.ts comment + mockup-conformity.md |
| Out of Scope §3 verify:mockup-pins validator | NOT in this plan (follow-up issue) |
| Rollback path PR #1 | Self-evident `git revert` — no specific task |
| Risk R1-R7 mitigations | R1 in Task 4-6.7 visual check; R2 marker contract docs; R3 explicit in Task 6.1; R4 explicit check in 5.7; R5 Task 10.4-10.5; R6 Task 6 strategy; R7 implicit (CI ubuntu pinned) |

**Coverage gap?** No major gaps. Out of Scope items §1-3 correctly excluded as follow-up issues (per spec).

### 2. Placeholder scan

Searched for: TBD, TODO, "fill in", "implement later", "Similar to Task", "Add appropriate".

Found: ZERO instances. (The word "implementation" appears in Task 1.3 step name "Write minimal implementation" — this is the standard TDD vocabulary, not a placeholder.)

### 3. Type consistency

- `ConformityViewport` type defined in Task 1.3, used in Task 2.2 imports — consistent.
- `getViewportFromProjectName`, `buildMarkerSelector`, `validateMarkerCount` — all 3 helper names consistent across Tasks 1, 2.
- `conformityMarker` JSX prop name — consistent across Tasks 4, 5, 6.
- `data-conformity-screen` attribute name — consistent everywhere.
- Marker values `default-desktop`, `default-mobile` — case + spelling consistent.
- PR number placeholders `<PR_N>` `<PR_N2>` clearly distinguished.

No type inconsistencies.
