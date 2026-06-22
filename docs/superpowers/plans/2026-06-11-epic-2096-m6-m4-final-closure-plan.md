# EPIC #2096 M6+M4 Final Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor `GameInfoTab.tsx` to 3-Card structure (Descrizione + Informazioni + House Rules CTA) and `GameToolboxTab.tsx` to 1-Card placeholder structure, both using shadcn/ui `Card` primitive. Closes EPIC #2096 (5/7 → 7/7 milestones) and unlocks DS-17-10 sp3 reactivation.

**Architecture:** Pure FE refactor, zero data layer changes. M6 wraps existing `dl/dt/dd` semantic in `<Card><CardContent>` boundary (preserves 9 existing tests). M6 House Rules CTA wires via App Router `useRouter().replace('?tab=houseRules')` → existing `GameTabsPanel` `useEffect` syncs `initialTab` → `activeTab`. M4 is static coming-soon Card with disabled Button + entity-toolkit AA-compliant darker text variant.

**Tech Stack:** Next.js App Router (`useRouter` / `useSearchParams` from `next/navigation`), shadcn/ui `Card`/`Button` primitives, Tailwind semantic tokens (`bg-card`, `border-border/50`, `bg-entity-toolkit/12`, `text-entity-toolkit-text`), Vitest existing tests (text-based assertions resilient), Playwright/Chrome MCP for designer screenshots.

**Spec ref:** [`docs/superpowers/specs/2026-06-11-epic-2096-m6-m4-final-closure-design.md`](../specs/2026-06-11-epic-2096-m6-m4-final-closure-design.md)

**Sub-issue:** [#2188](https://github.com/meepleAi-app/meepleai-monorepo/issues/2188)

**Branch:** `feature/issue-2188-m6-m4-closure` (already created sess.46p)

**Constraints:**
- ❌ DEC-5: NO new unit tests (manual + smoke only)
- ❌ DEC-8: NO changes to `dl/dt/dd` semantic structure inside Card 2 (preserves 9 existing tests)
- ❌ Tab IDs vietato rinominare (#2010)
- ❌ NO BGG references (#1903 ToS)
- ❌ NO new Card primitive — riusa `@/components/ui/card`
- ✅ DEC-6: Designer screenshot review pre-merge mandatory

---

## File Structure

### Files modified

| Path | Responsibility | Change |
|---|---|---|
| `apps/web/src/components/game-detail/tabs/GameInfoTab.tsx` | M6 — Info tab content (description + specs grid + house rules CTA) | Full rewrite (~115 LOC pre → ~135 LOC post) |
| `apps/web/src/components/game-detail/tabs/GameToolboxTab.tsx` | M4 — Toolbox tab placeholder | Full rewrite (~43 LOC pre → ~55 LOC post) |

### Files referenced (read-only)

| Path | Why |
|---|---|
| `apps/web/src/components/ui/card.tsx` | Card primitive import alias (re-export) |
| `apps/web/src/components/ui/data-display/card.tsx` | Card primitive canonical (CardHeader, CardTitle, CardDescription, CardContent, CardFooter) |
| `apps/web/src/components/ui/primitives/button.tsx` | Button primitive canonical |
| `apps/web/src/components/game-detail/GameTabsPanel.tsx` | URL nav sync via `useEffect` su `initialTab` (line 86-91) — no change |
| `apps/web/src/components/game-detail/tabs/types.ts` | `GameTabProps` interface — no change |
| `apps/web/src/components/game-detail/tabs/__tests__/GameInfoTab.test.tsx` | 9 existing tests (regression baseline) — no change |
| `admin-mockups/design_files/sp3-shared-game-detail.jsx` | Mockup reference for parity (ToolkitPublicListItem line 398-494) |
| `admin-mockups/design_files/sp3-shared-game-detail.fidelity.json` | `design_intent` check (skip designer review if `forward-refactor-obsolete`) |

### New files

NONE.

---

## Phase 0 — Pre-flight verification

### Task 0.1: Verify branch state + working tree clean

**Files:**
- Read-only: git state

- [ ] **Step 1: Verify on correct branch**

Run: `git branch --show-current`
Expected: `feature/issue-2188-m6-m4-closure`

- [ ] **Step 2: Verify working tree clean (only untracked `docs/superpowers/prompts/`)**

Run: `git status --short`
Expected:
```
?? docs/superpowers/prompts/
```
(spec doc already committed in `4d59557e7` + `67ef38853`)

- [ ] **Step 3: Verify branch ahead of main-dev**

Run: `git log --oneline main-dev..HEAD`
Expected: 2 commits
```
67ef38853 docs(specs): #2188 self-review fixes (sub-issue refs + AA variant)
4d59557e7 docs(specs): #2188 EPIC #2096 M6+M4 final closure design spec
```

### Task 0.2: Verify existing 9 tests baseline (GREEN before refactor)

**Files:**
- Test: `apps/web/src/components/game-detail/tabs/__tests__/GameInfoTab.test.tsx`

- [ ] **Step 1: Run baseline test suite**

Run: `pnpm --filter @meepleai/web test GameInfoTab --run`
Expected: 9/9 tests pass

If FAIL: STOP. Don't proceed. Baseline must be green before refactor. Investigate pre-existing regression.

### Task 0.3: Verify mockup design_intent (designer review skip gate)

**Files:**
- Read-only: `admin-mockups/design_files/sp3-shared-game-detail.fidelity.json`

- [ ] **Step 1: Check fidelity.json design_intent**

Run: `cat admin-mockups/design_files/sp3-shared-game-detail.fidelity.json`
Expected: JSON output with `design_intent` field

- [ ] **Step 2: Record outcome**

If `design_intent === 'forward-refactor-obsolete'`: skip designer review in Phase 4 (per DEC-6 fallback). Note in PR body.
If `design_intent === 'current'` or `forward-refactor` (non-obsolete): designer review gate active.
If file missing OR `design_intent === 'PENDING'`: assume designer review active (default conservative).

---

## Phase 1 — M6 GameInfoTab Card refactor

### Task 1.1: Refactor GameInfoTab.tsx to 3-Card structure

**Files:**
- Modify: `apps/web/src/components/game-detail/tabs/GameInfoTab.tsx` (full rewrite)

- [ ] **Step 1: Read current file (baseline understanding)**

Run: `cat apps/web/src/components/game-detail/tabs/GameInfoTab.tsx`
Expected: 160 lines as documented in spec § 3.1

- [ ] **Step 2: Replace full file content**

Replace `apps/web/src/components/game-detail/tabs/GameInfoTab.tsx` with the following code:

```tsx
'use client';

import { useRouter, useSearchParams } from 'next/navigation';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/primitives/button';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { cn } from '@/lib/utils';

import type { GameTabProps } from './types';

/**
 * Info tab — shows game metadata, description, and library-specific info.
 * Falls back to a "not in library" empty state when gated.
 *
 * #2096 M6 (sub-issue #2188): refactor from `dl/dt/dd` minimal layout to
 * 3-Card structure (Descrizione + Informazioni + House Rules CTA) using
 * shadcn/ui `Card` primitive. House Rules CTA navigates to `?tab=houseRules`
 * via App Router `router.replace` — `GameTabsPanel` `useEffect` syncs
 * `initialTab` → `activeTab`. Existing 9 unit tests preserved (DEC-8: dl
 * structure unchanged inside Card 2's `<CardContent>`).
 */
export function GameInfoTab({ gameId, variant, isNotInLibrary }: GameTabProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { data: game, isLoading, isError } = useLibraryGameDetail(gameId, !isNotInLibrary);

  const containerClass = cn('flex flex-col', variant === 'desktop' ? 'gap-4 p-6' : 'gap-3 p-4');

  if (isNotInLibrary) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-info" className={containerClass}>
        <p className="text-sm text-muted-foreground">
          Aggiungi questo gioco alla tua libreria per vedere tutti i dettagli.
        </p>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-info" className={containerClass}>
        <p className="text-sm text-muted-foreground">Caricamento in corso…</p>
      </div>
    );
  }

  if (isError || !game) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-info" className={containerClass}>
        <p className="text-sm text-destructive">Impossibile caricare i dettagli del gioco.</p>
      </div>
    );
  }

  const playersLabel =
    game.minPlayers && game.maxPlayers
      ? game.minPlayers === game.maxPlayers
        ? `${game.minPlayers}`
        : `${game.minPlayers}–${game.maxPlayers}`
      : null;

  // DEC: Card 1+2 are static content (no interactive surface) → override
  // shadcn Card default `hover:-translate-y-0.5 hover:shadow-md` so they
  // don't lift on hover. Card 3 (House Rules CTA) keeps default lift since
  // it's clickable.
  const staticCardClass = 'hover:translate-y-0 hover:shadow-sm';

  const titleSizeClass = variant === 'desktop' ? 'text-base' : 'text-sm';

  const handleOpenHouseRules = () => {
    // App Router URL nav — `GameTabsPanel` `useEffect` on `initialTab`
    // change (line 86-91, #2105 M7 review follow-up) syncs `activeTab`.
    // `scroll: false` prevents spurious scroll-to-top on tab switch.
    const params = new URLSearchParams(searchParams?.toString() ?? '');
    params.set('tab', 'houseRules');
    router.replace(`?${params.toString()}`, { scroll: false });
  };

  return (
    <div role="tabpanel" aria-labelledby="game-tab-info" className={containerClass}>
      {/* Card 1: Descrizione (conditional on game.description) */}
      {game.description && (
        <Card className={staticCardClass} data-testid="game-info-description">
          <CardHeader>
            <CardTitle className={titleSizeClass}>Descrizione</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="whitespace-pre-wrap text-sm leading-relaxed text-muted-foreground">
              {game.description}
            </p>
          </CardContent>
        </Card>
      )}

      {/* Card 2: Informazioni (specs grid dl preservato per test stability) */}
      <Card className={staticCardClass}>
        <CardHeader>
          <CardTitle className={titleSizeClass}>Informazioni</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-2 text-sm">
            {/*
              F3 #1974 (audit 2026-06-07, partial): expose Designer in the
              spec list. The mockup ships designer as a first-class metadata
              row, and `LibraryGameDetail` already carries it via the catalog
              fallback in `useLibraryGameDetail`. Hidden when the BE doesn't
              surface it (private games / non-catalog entries).
            */}
            {game.designers && game.designers.length > 0 && (
              <>
                <dt className="text-muted-foreground">Designer</dt>
                <dd className="font-medium text-foreground">
                  {game.designers.map(d => d.name).join(', ')}
                </dd>
              </>
            )}
            {game.gamePublisher && (
              <>
                <dt className="text-muted-foreground">Editore</dt>
                <dd className="font-medium text-foreground">{game.gamePublisher}</dd>
              </>
            )}
            {game.gameYearPublished && (
              <>
                <dt className="text-muted-foreground">Anno</dt>
                <dd className="font-medium text-foreground">{game.gameYearPublished}</dd>
              </>
            )}
            {playersLabel && (
              <>
                <dt className="text-muted-foreground">Giocatori</dt>
                <dd className="font-medium text-foreground">{playersLabel}</dd>
              </>
            )}
            {game.playingTimeMinutes && (
              <>
                <dt className="text-muted-foreground">Durata</dt>
                <dd className="font-medium text-foreground">{game.playingTimeMinutes} min</dd>
              </>
            )}
            {game.complexityRating != null && (
              <>
                <dt className="text-muted-foreground">Complessità</dt>
                <dd className="font-medium text-foreground">
                  {game.complexityRating.toFixed(2)} / 5
                </dd>
              </>
            )}
            {game.categories && game.categories.length > 0 && (
              <>
                <dt className="text-muted-foreground">Categorie</dt>
                <dd className="font-medium text-foreground">
                  {game.categories.map(c => c.name).join(', ')}
                </dd>
              </>
            )}
            {game.mechanics && game.mechanics.length > 0 && (
              <>
                <dt className="text-muted-foreground">Meccaniche</dt>
                <dd className="font-medium text-foreground">
                  {game.mechanics.map(m => m.name).join(', ')}
                </dd>
              </>
            )}
            {game.addedAt && (
              <>
                <dt className="text-muted-foreground">In libreria dal</dt>
                <dd className="font-medium text-foreground">
                  {new Date(game.addedAt).toLocaleDateString('it-IT')}
                </dd>
              </>
            )}
          </dl>
        </CardContent>
      </Card>

      {/* Card 3: House Rules CTA (default lift, always visible) */}
      <Card>
        <CardContent className="flex items-center justify-between gap-4 p-6">
          <div className="flex-1">
            <h4 className="font-heading font-semibold text-foreground">
              House Rules personalizzate
            </h4>
            <p className="text-sm text-muted-foreground">
              Aggiungi varianti e regole della casa per questo gioco.
            </p>
          </div>
          <Button variant="outline" onClick={handleOpenHouseRules}>
            Apri House Rules →
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
```

- [ ] **Step 3: Run regression tests (verify 9/9 still pass)**

Run: `pnpm --filter @meepleai/web test GameInfoTab --run`
Expected: 9/9 tests pass

If FAIL: identify failing assertion(s). Most likely culprits:
- `getByText('Designer')` — text label preserved ✅ (in dl/dt as before)
- `getByText('Descrizione')` — heading text preserved ✅ (now in CardTitle)
- `getByTestId('game-info-description')` — preserved ✅ (on Card 1 wrapper)
- If a test uses `getByRole('definition')` or similar dl-structure assertion → check; spec says no such test exists

If `next/navigation` mocks needed in test (vitest can't resolve useRouter): test file would need extension. But existing tests use `mockState` direct, not router/searchParams, so should pass without mock changes.

- [ ] **Step 4: Run lint + typecheck on file**

Run:
```bash
pnpm --filter @meepleai/web lint -- apps/web/src/components/game-detail/tabs/GameInfoTab.tsx
pnpm --filter @meepleai/web typecheck
```
Expected: 0 errors, 0 warnings

- [ ] **Step 5: Commit M6 implementation**

```bash
git add apps/web/src/components/game-detail/tabs/GameInfoTab.tsx
git commit -m "$(cat <<'EOF'
feat(library): #2188 M6 GameInfoTab refactor to 3-Card structure

EPIC #2096 M6 milestone: Info tab dl/dt/dd minimal → 3-Card layout per
mockup sp3-shared-game-detail parity.

3 Card sequence (top-down):
1. Card 1 Descrizione — conditional su game.description, CardTitle + plain
   whitespace-pre-wrap body. data-testid="game-info-description" preserved
   sul Card wrapper (test resilience).
2. Card 2 Informazioni — dl/dt/dd grid-cols-[auto_1fr] preservato dentro
   CardContent (DEC-8 test stability, 9 existing tests pass 100%).
3. Card 3 House Rules CTA — sempre visibile, Button onClick navigates via
   useRouter().replace('?tab=houseRules', { scroll: false }) — App Router
   canonical pattern, GameTabsPanel sync via useEffect su initialTab.

Card 1+2 hover override (hover:translate-y-0 hover:shadow-sm) per static
content; Card 3 default shadcn lift per interactive CTA.

3 stati invariati (isNotInLibrary / isLoading / isError) + variant
desktop/mobile padding preserved.

Refs: spec docs/superpowers/specs/2026-06-11-epic-2096-m6-m4-final-closure-design.md
Closes part of EPIC #2096.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

Expected: pre-commit hooks pass (typecheck OK), commit lands on `feature/issue-2188-m6-m4-closure`.

---

## Phase 2 — M4 GameToolboxTab Card refactor

### Task 2.1: Refactor GameToolboxTab.tsx to 1-Card placeholder

**Files:**
- Modify: `apps/web/src/components/game-detail/tabs/GameToolboxTab.tsx` (full rewrite)

- [ ] **Step 1: Read current file (baseline understanding)**

Run: `cat apps/web/src/components/game-detail/tabs/GameToolboxTab.tsx`
Expected: 43 lines (3 paragraphs placeholder)

- [ ] **Step 2: Replace full file content**

Replace `apps/web/src/components/game-detail/tabs/GameToolboxTab.tsx` with the following code:

```tsx
'use client';

import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import type { GameTabProps } from './types';

/**
 * Toolbox tab — placeholder describing the toolbox feature.
 * The legacy /toolbox route now redirects to this tab, so a full-screen
 * link is intentionally NOT provided to avoid redirect loops.
 *
 * #2096 M4 (sub-issue #2188): refactor from 3-paragraph placeholder text
 * to 1-Card mockup-style structure (icon 44x44 + Title + Description +
 * body + CTA disabled). No data layer impl — toolkit listing deferred a
 * future EPIC. Icon uses entity-toolkit AA-compliant darker text variant
 * (text-entity-toolkit-text ~5.6:1 ✅ vs bg-entity-toolkit/12, per #1094
 * Real-C-E gamebook).
 */
export function GameToolboxTab({ variant, isNotInLibrary }: GameTabProps) {
  const containerClass = cn('flex flex-col', variant === 'desktop' ? 'gap-4 p-6' : 'gap-3 p-4');

  if (isNotInLibrary) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-toolbox" className={containerClass}>
        <p className="text-sm text-muted-foreground">
          Aggiungi il gioco alla libreria per usare il toolbox.
        </p>
      </div>
    );
  }

  return (
    <div role="tabpanel" aria-labelledby="game-tab-toolbox" className={containerClass}>
      <Card className="hover:translate-y-0 hover:shadow-sm">
        <CardHeader className="flex flex-row items-start gap-3 space-y-0">
          <div
            aria-hidden="true"
            className="flex h-11 w-11 shrink-0 items-center justify-center rounded-md bg-entity-toolkit/12 text-2xl text-entity-toolkit-text"
          >
            🧰
          </div>
          <div className="flex-1 space-y-1.5">
            <CardTitle className={variant === 'desktop' ? 'text-lg' : 'text-base'}>
              Toolbox
            </CardTitle>
            <CardDescription>
              Strumenti rapidi per il gioco: dadi, timer, punteggi, note e altro ancora.
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent>
          <p className="text-xs italic text-muted-foreground">
            Integrazione completa del toolbox in arrivo.
          </p>
        </CardContent>
        <CardFooter>
          <Button variant="outline" disabled className="cursor-not-allowed">
            In arrivo
          </Button>
        </CardFooter>
      </Card>
    </div>
  );
}
```

- [ ] **Step 3: Run lint + typecheck**

Run:
```bash
pnpm --filter @meepleai/web lint -- apps/web/src/components/game-detail/tabs/GameToolboxTab.tsx
pnpm --filter @meepleai/web typecheck
```
Expected: 0 errors, 0 warnings.

If `lint:tokens` flags `bg-entity-toolkit/12` or `text-entity-toolkit-text` → bug in tokens whitelist, NOT in code. Investigate `apps/web/.eslintrc*` or `eslint-plugin-local` rule `local/no-hardcoded-color-utility`.

- [ ] **Step 4: Run full project lint:tokens + lint:bgg gate**

Run:
```bash
pnpm --filter @meepleai/web lint:tokens
pnpm --filter @meepleai/web lint:bgg
```
Expected: 0 violations each.

- [ ] **Step 5: Commit M4 implementation**

```bash
git add apps/web/src/components/game-detail/tabs/GameToolboxTab.tsx
git commit -m "$(cat <<'EOF'
feat(library): #2188 M4 GameToolboxTab refactor to 1-Card placeholder

EPIC #2096 M4 milestone: Toolbox tab 3-paragrafi placeholder → 1-Card
mockup-style placeholder per sp3-shared-game-detail parity.

1 Card structure:
- CardHeader: icon 44x44 (bg-entity-toolkit/12 text-entity-toolkit-text
  AA-compliant darker variant ~5.6:1 ✅ per #1094 Real-C-E gamebook) +
  CardTitle "Toolbox" + CardDescription "Strumenti rapidi…"
- CardContent: italic "Integrazione completa del toolbox in arrivo."
- CardFooter: Button variant="outline" disabled "In arrivo" + cursor-not-allowed

isNotInLibrary fallback copy invariato.
hover:translate-y-0 hover:shadow-sm override (static placeholder, no lift).
Mockup parity 44x44 icon riusa pattern ToolkitPublicListItem (mockup
line 411-417, BoardSize var(--r-md), font-size 22).

No data layer impl (DEC-4 scope reduced): toolkit listing deferred a
future EPIC.

Refs: spec docs/superpowers/specs/2026-06-11-epic-2096-m6-m4-final-closure-design.md
Closes part of EPIC #2096.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

Expected: pre-commit passes, commit lands.

---

## Phase 3 — Quality gates

### Task 3.1: Full GameInfoTab test regression check

**Files:**
- Test: `apps/web/src/components/game-detail/tabs/__tests__/GameInfoTab.test.tsx`

- [ ] **Step 1: Run GameInfoTab test suite (post-refactor)**

Run: `pnpm --filter @meepleai/web test GameInfoTab --run`
Expected: 9/9 tests pass.

If FAIL: rollback to previous commit (`git reset --hard HEAD~2`), inspect failing assertion, file regression bug.

### Task 3.2: Broader test regression check (game-detail subtree)

**Files:**
- Tests: `apps/web/src/components/game-detail/**/*.test.tsx`

- [ ] **Step 1: Run game-detail test subtree**

Run: `pnpm --filter @meepleai/web test game-detail --run`
Expected: all tests pass (includes GameDetailDesktop.test.tsx, GameTabsPanel.test.tsx, GameInfoTab.test.tsx, AgentChatPanel.test.tsx, etc.).

If `GameTabsPanel.test.tsx` fails on URL `?tab=houseRules` flow: review mock for `next/navigation` — likely needs `useSearchParams` mock added. Most likely affected: tests that simulate tab switch via URL.

### Task 3.3: Run project lint suite

- [ ] **Step 1: Run lint + tokens + bgg gates**

Run:
```bash
pnpm --filter @meepleai/web lint
pnpm --filter @meepleai/web lint:tokens
pnpm --filter @meepleai/web lint:bgg
```
Expected: 0 errors each.

### Task 3.4: Run typecheck

- [ ] **Step 1: Full project typecheck**

Run: `pnpm --filter @meepleai/web typecheck`
Expected: 0 errors.

---

## Phase 4 — Designer review gate (DEC-6)

### Task 4.1: Check fidelity.json design_intent (skip-gate)

**Files:**
- Read-only: `admin-mockups/design_files/sp3-shared-game-detail.fidelity.json`

- [ ] **Step 1: Read fidelity classification**

Run: `cat admin-mockups/design_files/sp3-shared-game-detail.fidelity.json | grep -E '"design_intent"|"obsolete_tracking_issue"'`

- [ ] **Step 2: Branch logic**

If `design_intent === 'forward-refactor-obsolete'`:
- Skip Phase 4 Task 4.2–4.5 (no screenshot needed)
- Add PR body note: `**Designer review skipped** — mockup classified `forward-refactor-obsolete` (tracking #<obsolete_tracking_issue>).`
- Proceed directly to Phase 5.

Else (`design_intent === 'current'` | `'forward-refactor'` | `'PENDING'` | missing):
- Proceed to Task 4.2.

### Task 4.2: Boot dev server + seed game

**Files:**
- Dev infrastructure (no source change)

- [ ] **Step 1: Boot dev stack**

Run (in separate terminal or via `run_in_background`):
```bash
cd infra && make dev-core
```
Expected: Docker containers up (api on 8080, web on 3000, postgres, redis).

Wait until web ready: `curl -s http://localhost:3000 | head -20` returns Next.js HTML.

- [ ] **Step 2: Seed a game in library (if not pre-seeded)**

Run (Bash, Git Bash on Windows):
```bash
cd infra/scripts/seed-sp4 && ./run.sh
```

OR if seed script not applicable, manually via admin UI:
1. Login as admin user
2. Catalog → Add Game → "Catan" with all metadata (designers, description, year, complexity)
3. Library → Add Catan to library

Record gameId for next step. Example: `00000000-0000-4000-8000-000000000001` if using seed fixtures.

### Task 4.3: Capture Info tab screenshot (Playwright OR Chrome MCP)

**Files:**
- Output: `pr-screenshots/m6-info-tab.png` (untracked, embedded in PR body via gh-cli)

**Option A: Chrome MCP (preferred, integrated session)**

- [ ] **Step 1: Load Chrome MCP tools**

Run: ToolSearch with query `select:mcp__claude-in-chrome__tabs_create_mcp,mcp__claude-in-chrome__navigate,mcp__claude-in-chrome__resize_window`

- [ ] **Step 2: Open new tab on /library/[seededGameId]?tab=info**

Use `mcp__claude-in-chrome__tabs_create_mcp` with URL `http://localhost:3000/library/<seededGameId>?tab=info`.

- [ ] **Step 3: Resize to desktop 1440x900**

Use `mcp__claude-in-chrome__resize_window` width=1440 height=900.

- [ ] **Step 4: Take screenshot**

Use `mcp__claude-in-chrome__upload_image` or DOM screenshot tool. Save to `pr-screenshots/m6-info-tab.png` (gitignored).

**Option B: Playwright standalone (fallback if Chrome MCP unavailable)**

- [ ] **Step 1: Create one-off Playwright script (DELETE post-screenshot, not committed)**

Create file `apps/web/e2e/_oneoff-m6-m4-screenshot.spec.ts` (excluded from CI via filename pattern OR removed before PR):

```ts
import { test } from '@playwright/test';

const SEEDED_GAME_ID = '00000000-0000-4000-8000-000000000001'; // update if different

test('M6 + M4 screenshot capture', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });

  // M6 Info tab
  await page.goto(`http://localhost:3000/library/${SEEDED_GAME_ID}?tab=info`);
  await page.waitForLoadState('networkidle');
  await page.screenshot({ path: 'pr-screenshots/m6-info-tab.png', fullPage: false });

  // M4 Toolbox tab
  await page.goto(`http://localhost:3000/library/${SEEDED_GAME_ID}?tab=toolbox`);
  await page.waitForLoadState('networkidle');
  await page.screenshot({ path: 'pr-screenshots/m4-toolbox-tab.png', fullPage: false });
});
```

- [ ] **Step 2: Run script**

Run: `pnpm --filter @meepleai/web exec playwright test e2e/_oneoff-m6-m4-screenshot.spec.ts --headed`
Expected: 2 PNGs in `apps/web/pr-screenshots/`.

- [ ] **Step 3: Delete oneoff spec**

Run: `rm apps/web/e2e/_oneoff-m6-m4-screenshot.spec.ts`

### Task 4.4: Capture Toolbox tab screenshot

Already captured in Task 4.3 Step 4 (Chrome MCP) or Task 4.3 Step 2 (Playwright). Confirm both PNGs exist.

- [ ] **Step 1: Verify both screenshots present**

Run: `ls apps/web/pr-screenshots/`
Expected:
```
m6-info-tab.png
m4-toolbox-tab.png
```

If missing: rerun Task 4.3.

### Task 4.5: Push branch + open PR

- [ ] **Step 1: Push branch to origin**

Run: `git push -u origin feature/issue-2188-m6-m4-closure`
Expected: branch tracking set, push succeeds.

If pre-push hook runs full `pnpm build` (~10 min, per CLAUDE.md operations note): wait. Don't kill. If build fails ENOENT on `.next/static/...`: `rm -rf apps/web/.next` + retry.

- [ ] **Step 2: Open PR with embedded screenshots**

Run:
```bash
gh pr create --base main-dev \
  --title "feat(library): EPIC #2096 M6+M4 final closure (#2188)" \
  --body "$(cat <<'EOF'
## Summary

Closes EPIC #2096 con i 2 milestone rimasti (M6 Info card style + M4 Toolbox cards community style) combined per DEC-3 user-locked sess.46p. 5/7 milestone già shipped pre-sessione (M1-M3, M5, M7). Closure di #2096 unlocks DS-17-10 sp3 sub-issue reactivation.

- **M6** — `GameInfoTab.tsx` refactor: dl/dt/dd minimal → 3-Card structure (Descrizione + Informazioni dl wrap + House Rules CTA via `useRouter().replace('?tab=houseRules')`)
- **M4** — `GameToolboxTab.tsx` refactor: 3 paragrafi placeholder → 1-Card mockup-style (icon 44x44 entity-toolkit + Title + Description + body + CTA "In arrivo" disabled)

**DESIGNER REVIEW PENDING** — admin-squash merge bloccato fino a designer 👍 (vedi screenshot sotto).

## Spec + plan

- Design spec: [`docs/superpowers/specs/2026-06-11-epic-2096-m6-m4-final-closure-design.md`](docs/superpowers/specs/2026-06-11-epic-2096-m6-m4-final-closure-design.md)
- Implementation plan: [`docs/superpowers/plans/2026-06-11-epic-2096-m6-m4-final-closure-plan.md`](docs/superpowers/plans/2026-06-11-epic-2096-m6-m4-final-closure-plan.md)
- 8 DEC user-locked (see spec § 2)

## Designer review

### M6 Info tab (post-refactor)

![M6 Info tab screenshot](pr-screenshots/m6-info-tab.png)

3-Card structure: Descrizione → Informazioni dl wrap → House Rules CTA. Hover override applicato a Card 1+2 static.

### M4 Toolbox tab (post-refactor)

![M4 Toolbox tab screenshot](pr-screenshots/m4-toolbox-tab.png)

1-Card placeholder: icon 44x44 `bg-entity-toolkit/12 text-entity-toolkit-text` (AA-compliant ~5.6:1 ✅) + Title + Description + body + Button disabled "In arrivo".

## Test plan

- [x] `pnpm test GameInfoTab` → 9/9 pass (DEC-5 no new tests, regression baseline preserved)
- [x] `pnpm test game-detail` → all pass
- [x] `pnpm lint` → 0 errors
- [x] `pnpm lint:tokens` → 0 violations
- [x] `pnpm lint:bgg` → 0 violations
- [x] `pnpm typecheck` → 0 errors
- [ ] Designer review 👍
- [ ] Manual smoke verify URL nav `?tab=houseRules` flow

## Refs

- Closes #2188 (sub-issue combined M6 + M4)
- Closes EPIC #2096 (if 7/7 milestones complete on merge)
- Unlocks DS-17-10 sp3 reactivation (memory: `ds-17-10-sp3-deferred-decisions`)
- Sibling shipped: PR #2101 (M1) + PR #2103 (M2) + PR #2108 (M7)
- Constraint: #2010 tab IDs lock, #1903 BGG ToS
- A11y gate: #1094 Real-C-E (text-entity-toolkit-text variant)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

**IMPORTANT**: PNG embed `pr-screenshots/*.png` works only se files committed nel branch OR caricati via `gh pr comment --body-file` con attachment. Alternative: upload screenshots come gist OR use `gh issue create` con file attachment then move to PR comment.

Pratico: salva screenshots come Gist via `gh gist create pr-screenshots/m6-info-tab.png --public` → get raw URL → embed in PR body.

OR commit screenshots in `docs/screenshots/2026-06-11-epic-2096-m6-m4/` (committed, not gitignored) and reference relatively. **Trade-off**: aumenta repo size by ~200-500KB per PNG. Generally avoid binary commits, prefer Gist.

### Task 4.6: Wait for designer 👍

- [ ] **Step 1: Notify designer (out-of-band — Slack/email/issue comment)**

Out of scope of this plan. Process documented in PR body.

- [ ] **Step 2: Wait for review comment with 👍**

Async block — proceed to Phase 5 only on 👍.

If designer requests changes:
- Apply revisions in new commit(s) on `feature/issue-2188-m6-m4-closure`
- Re-capture screenshots (Task 4.3)
- Update PR body with new screenshots
- Re-request review

---

## Phase 5 — Merge + closure

### Task 5.1: Admin-squash merge (P145 37a volta)

- [ ] **Step 1: Verify CI checks green**

Run: `gh pr checks <PR_NUM>`
Expected: all required checks pass (typecheck, lint, test, a11y, etc.).

- [ ] **Step 2: Admin-squash merge with branch delete**

Run: `gh pr merge <PR_NUM> --admin --squash --delete-branch`
Expected: PR merged on `main-dev`, branch `feature/issue-2188-m6-m4-closure` deleted from remote.

- [ ] **Step 3: Local cleanup**

```bash
git checkout main-dev
git pull --ff-only
git branch -D feature/issue-2188-m6-m4-closure
```

Expected: main-dev updated with merge commit, local branch removed.

### Task 5.2: Close sub-issue #2188

- [ ] **Step 1: Close with PR ref + AC evidence**

```bash
gh issue close 2188 --comment "$(cat <<'EOF'
Shipped via PR #<PR_NUM> (`<merge_commit_sha>`).

## AC closure evidence

### M6
- ✅ GameInfoTab.tsx usa Card primitive shadcn da @/components/ui/card
- ✅ 3 Card sequence: Descrizione + Informazioni + House Rules CTA
- ✅ Card 1+2 hover override (hover:translate-y-0 hover:shadow-sm)
- ✅ Card 3 CTA Button → useRouter().replace('?tab=houseRules')
- ✅ dl/dt/dd preservato in Card 2 CardContent
- ✅ data-testid="game-info-description" preservato
- ✅ 3 stati invariati
- ✅ 9 existing unit test pass 100%

### M4
- ✅ GameToolboxTab.tsx usa Card primitive
- ✅ 1 Card icon 44x44 bg-entity-toolkit/12 text-entity-toolkit-text (AA-compliant)
- ✅ isNotInLibrary fallback invariato
- ✅ Mockup parity verified

### Quality gates
- ✅ pnpm test GameInfoTab → 9/9 pass
- ✅ pnpm lint + lint:tokens + lint:bgg → 0 violations
- ✅ pnpm typecheck → 0 errors
- ✅ 2 screenshot embedded
- ✅ Designer 👍
- ✅ Admin-squash merge + branch deleted
EOF
)"
```

### Task 5.3: Update EPIC #2096 body (M6 ✓ + M4 ✓)

- [ ] **Step 1: Fetch current EPIC body**

```bash
gh issue view 2096 --json body --jq '.body' > /tmp/epic-2096-body.md
```

- [ ] **Step 2: Edit body to mark M6 + M4 rows complete**

Open `/tmp/epic-2096-body.md` and replace:
- `- [ ] **M4 — Tab content rebuild stile community** (P2, ~4h):` → `- [x] **M4 — Tab content rebuild stile community** (P2, ~4h): ✅ shipped PR #<PR_NUM>` (truncate description tail OK)
- `- [ ] **M6 — Card style community per Info tab** (P3, ~2h):` → `- [x] **M6 — Card style community per Info tab** (P3, ~2h): ✅ shipped PR #<PR_NUM>`

- [ ] **Step 3: Push body update**

```bash
gh issue edit 2096 --body-file /tmp/epic-2096-body.md
```

### Task 5.4: Close EPIC #2096 (if 7/7 milestones complete)

- [ ] **Step 1: Verify all milestones shipped**

Check EPIC body: M1+M2+M3+M4+M5+M6+M7 all ✅. If yes:

- [ ] **Step 2: Close EPIC**

```bash
gh issue close 2096 --reason completed --comment "$(cat <<'EOF'
🎉 EPIC #2096 closure — 7/7 milestone shipped.

| Milestone | Sub-issue | PR |
|---|---|---|
| M1 GameHero v2 | #2100 | #2101 |
| M2 Tabs animated underline | #2102 | #2103 |
| M3 ConnectionBar pip community | (inline) | various |
| M4 Toolbox cards community style | #2188 | #<PR_NUM> |
| M5 ContributorsStrip | #2036 alt track | various |
| M6 Card style Info tab | #2188 | #<PR_NUM> |
| M7 Layout restructure | #2105 | #2108 |

Total effort: ~16h cumulative (M1+M2+M3+M5+M7 ~10h pre-sessione 46p, M4+M6 ~3h35 sess.46p).

Unlocks DS-17-10 sp3 sub-issue reactivation (memory: ds-17-10-sp3-deferred-decisions.md).
EOF
)"
```

### Task 5.5: Memory entry + DS-17-10 sp3 reactivation trigger

- [ ] **Step 1: Write memory entry**

Create file `C:\Users\Utente\.claude\projects\D--Repositories-meepleai-monorepo-frontend\memory\epic-2096-closure-shipped.md`:

```markdown
---
name: epic-2096-closure-shipped
description: "EPIC #2096 /library/[gameId] sp3 mockup rebuild — 7/7 milestone shipped sess.46p — unlocks DS-17-10 sp3"
metadata:
  node_type: memory
  type: project
---

# EPIC #2096 closure shipped sess.46p

EPIC #2096 `/library/[gameId] sp3 mockup rebuild` CLOSED sess.46p 2026-06-11 via PR #<PR_NUM> (admin-squash P145 37a). 2 milestone final combined (M6 Info card + M4 Toolbox coming-soon) via sub-issue #2188.

## Shipped scope

| Milestone | Sub-issue | PR | Notes |
|---|---|---|---|
| M1 GameHero v2 | #2100 | #2101 (`e4c6d100d`) | Pre sess.46p |
| M2 Tabs animated underline | #2102 | #2103 (`af5145562`) | Pre sess.46p |
| M3 ConnectionBar pip community | (inline) | various | Pre sess.46p |
| M4 Toolbox cards community style | #2188 | #<PR_NUM> | Sess.46p — coming-soon Card |
| M5 ContributorsStrip | #2036 alt track | various | Pre sess.46p |
| M6 Info card style | #2188 | #<PR_NUM> | Sess.46p — 3-Card structure |
| M7 Layout restructure | #2105 | #2108 (`d691b8ceb`) | Pre sess.46p |

Total ~16h cumulative.

## DS-17-10 sp3 reactivation

Trigger MET. `gh issue view 2096 --json state` → CLOSED. Per memory note `ds-17-10-sp3-deferred-decisions`:
- Re-open brainstorming DS-17-10 sp3 with 3 pre-locked decisions
- Verify rebuild deliverables landed in `(public)/shared-games/[id]/page.tsx` or `library/[gameId]`
- Story `sp3-shared-game-detail.stories.tsx` must use POST-rebuild component

## Patterns confirmed

- P145 admin-squash 37a volta
- P74 close-as-shipped (M3 + M5 already shipped inline before sub-issue tracking caught up)
- P124 pre-decomposition search (verified no duplicate sub-issue for M6/M4)
- DEC-3 combined sub-issue saves ~30 min overhead vs split
- DEC-5 manual + smoke only (9 existing test resilient to layout refactor)
- DEC-6 designer screenshot pre-merge worked smoothly (Chrome MCP/Playwright capture pattern)
- DEC-7 shadcn/ui Card primitive consolidates entity-color hover pattern
- DEC-8 dl/dt/dd preservation = max test stability + accessibility

## Links

- Parent EPIC: #2096
- Sub-issue closing: #2188
- Spec: docs/superpowers/specs/2026-06-11-epic-2096-m6-m4-final-closure-design.md
- Plan: docs/superpowers/plans/2026-06-11-epic-2096-m6-m4-final-closure-plan.md
- Reactivation target: DS-17-10 sp3 (memory: ds-17-10-sp3-deferred-decisions)
- Sibling shipped: PR #2173 DS-17-11 sp6-7-nano (sess.46o predecessor)
```

- [ ] **Step 2: Update MEMORY.md index**

Add new entry at top:

```
- [#2096 EPIC closure 7/7 M6+M4 shipped](epic-2096-closure-shipped.md) — Sess.46p 2026-06-11 EPIC #2096 CLOSED via PR #<PR_NUM> admin-squash P145 37a. Sub-issue #2188 combined M6 Info card + M4 Toolbox coming-soon. 8 DEC user-locked, ~3h35 active vs ~6h cap (40% reduction). Unlocks DS-17-10 sp3 reactivation (memory: ds-17-10-sp3-deferred-decisions).
```

- [ ] **Step 3: Notify user**

Final message to user:
> 🎉 EPIC #2096 CLOSED — 7/7 milestone shipped. Sub-issue #2188 merged via PR #<PR_NUM>. **DS-17-10 sp3 sub-issue ready for reactivation** — vuoi riaprire brainstorming con il context preservato in memory `ds-17-10-sp3-deferred-decisions`?

---

## Self-review checklist (run BEFORE marking plan complete)

- [ ] Phase 0 covers branch verification + baseline test
- [ ] Phase 1 covers M6 with full code sample (no placeholders)
- [ ] Phase 2 covers M4 with full code sample (no placeholders)
- [ ] Phase 3 covers all quality gates (test + lint + tokens + bgg + typecheck)
- [ ] Phase 4 covers DEC-6 designer review gate with Chrome MCP OR Playwright fallback
- [ ] Phase 5 covers merge + closure + memory entry
- [ ] All 8 DEC from spec § 2 referenced and respected
- [ ] No TBD/TODO placeholders
- [ ] All code blocks are complete (no `// ...` ellipsis in critical paths)
- [ ] Import paths verified (`@/components/ui/card` re-export + `@/components/ui/primitives/button` canonical)
- [ ] AA compliance applied to M4 icon (`text-entity-toolkit-text`)
- [ ] DEC-8 dl/dt/dd preservation explicit in Phase 1
- [ ] Tab ID lock (#2010) respected (no tab rename in any task)
- [ ] BGG ToS guard (#1903) respected (no BGG references in code or commit msg)
- [ ] Pre-flight P124 verified in Phase 0
- [ ] Branch hygiene #806 respected (already on feature branch from pre-plan setup)

---

**End of implementation plan.**
