# DS-17-12 sp4-core-catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate 14 sp4-core-catalog mockup stems to Storybook stories (1 skip + 1 forward-refactor + 12 standard ship), closing DS-17 Phase C-2 step 1/4.

**Architecture:** Pure FE work. Stage 0 BGG cleanup (4 mockup files identified containing user-facing BGG references). Stage 1 sp4-library-mobile forward-refactor (Agent dispatch + tracking issue). Stage 2 sp4-add-game-bgg-step skip (closure note). Stage 3 hybrid P251 dispatch: Agent for sp4-game-detail (POST-#2096 wire verify) + inline batch 11 standard stems. Stage 4 quality gates. Stage 5 merge + closure.

**Tech Stack:** Next.js App Router, Storybook 8 (`@storybook/react`), Tailwind semantic tokens, `pnpm lint:*` gates (tokens / bgg / fidelity / annotations).

**Spec ref:** [`docs/superpowers/specs/2026-06-11-ds-17-phase-c-2-sp4-split-and-ds-17-12-design.md`](../specs/2026-06-11-ds-17-phase-c-2-sp4-split-and-ds-17-12-design.md)

**Sub-issue:** [#2214](https://github.com/meepleAi-app/meepleai-monorepo/issues/2214)

**Branch:** `feature/issue-2214-ds-17-12-sp4-catalog` (pre-flight done — spec committed `69c20fd72`)

**Constraints applied** (DS-17-10 review fix lessons):
- ✅ `@storybook/react` import (NOT `@storybook/nextjs`)
- ✅ NO hand-written `@mockup` JSDoc (let injector handle in Stage 4)
- ✅ `<Link>` from `next/link` for navigation (NOT raw `<a href>`)
- ✅ Multi-route mockup → canonical story target (1 story + comment fratelli)
- ✅ `pnpm test:storybook:snapshots:update` correct command if baseline needed
- ✅ fidelity.json `obsolete_tracking_issue: "#1234"` format (NOT `"1234"`)

---

## File Structure

### Files modified (Stage 0 BGG cleanup, 4 files)

| Path | Lines | Edits |
|---|---|---|
| `admin-mockups/design_files/sp4-library-desktop.jsx` | 118, 1051, 1064, 1448, 1524 | Remove user-facing BGG import buttons + replace text |
| `admin-mockups/design_files/sp4-game-detail.jsx` | 390 | Replace `'Rating BGG'` label → `'Rating'` or `'Voto community'` |
| `admin-mockups/design_files/sp4-upload-wizard-extended.jsx` | 100-103 | Remove "Da BoardGameGeek" upload option entirely |
| `admin-mockups/design_files/sp4-add-game-drawer.jsx` | Lines 9-11 (ADR-059 comment) | **PRESERVE** — documents ADR-059 admin-only constraint, NOT user-facing violation. Skip edit. |

### Files created (Stage 1 sp4-library-mobile forward-refactor)

| Path | Responsibility |
|---|---|
| `apps/web/src/app/(authenticated)/library/page-mobile.stories.tsx` | sp4-library-mobile forward-refactor scaffold story (separate from desktop) OR `(authenticated)/library/page.stories.tsx` mobile variant Frame |

### Files created (Stage 3 standard stems, 12)

| Stem | Story file | Notes |
|---|---|---|
| sp4-library-desktop | `(authenticated)/library/page.stories.tsx` | Includes mobile Frame if combined per sp4-library-mobile |
| sp4-library-wishlist | `(authenticated)/library/wishlist/page.stories.tsx` | Static catalog wishlist |
| sp4-add-game-drawer | `apps/web/src/app/(authenticated)/library/AddGameDrawer.stories.tsx` (next to component, NAMED export) | Confirmed path post review — `import { AddGameDrawer } from './AddGameDrawer'` |
| sp4-add-game-pdf-dedup | `(authenticated)/library/private/add/page.stories.tsx` | Wizard variant |
| sp4-games-index | `(authenticated)/games/page.stories.tsx` | Catalog list |
| sp4-game-detail | `(authenticated)/games/[id]/page.stories.tsx` | **Agent dispatch** — POST-#2096 wire verify (M1-M7 deliverables from PR #2207) |
| sp4-agents-index | `(authenticated)/agents/page.stories.tsx` | Agents list |
| sp4-agent-detail | `(authenticated)/agents/[id]/page.stories.tsx` | Agent detail |
| sp4-game-chat-tab | `apps/web/src/components/features/game-chat/GameChatTab.stories.tsx` | Component-mock — path verified `apps/web/src/components/features/game-chat/GameChatTab.tsx` |
| sp4-citation-pdf-viewer | Component-mock path TBD via grep (likely `apps/web/src/components/chat/panel/ChatCitationCard.stories.tsx` or `chat-unified/CitationBlock.stories.tsx`) | Verify in Stage 3 |
| sp4-discover | `(authenticated)/discover/page.stories.tsx` | Discover surface |
| sp4-upload-wizard-extended | `apps/web/src/app/(authenticated)/gamebook/upload/page.stories.tsx` | **DIFFERENT ROUTE** from pdf-dedup (review fix). MOCKUPS_INDEX line 151 maps to `/upload` + `/gamebook/upload`. Canonical = `/gamebook/upload`. |

### Files modified (Stage 1 + Stage 2 fidelity updates)

| Path | Action |
|---|---|
| `admin-mockups/design_files/sp4-library-mobile.fidelity.json` | Update `story_path` + `obsolete_tracking_issue` to new tracking issue |
| `admin-mockups/design_files/sp4-add-game-bgg-step.fidelity.json` | Update closure note (DS-17-12 reference) |
| `admin-mockups/MOCKUPS_INDEX.md` | Verify sp4-library-mobile + sp4-add-game-bgg-step mappings (likely already mapped from Phase B audit) |

### Files referenced (read-only)

| Path | Why |
|---|---|
| `admin-mockups/design_files/sp4-*.{jsx,fidelity.json}` | Mockup sources per stem |
| `apps/web/src/app/(authenticated)/library/page.tsx` | Existing route component |
| `apps/web/src/components/features/game-chat/GameChatTab.tsx` | Component-mock target verified |
| `apps/web/src/components/game-detail/GameDetailDesktop.tsx` | POST-#2096 deliverables (M1-M7 wired by PR #2207) |
| Sibling story patterns (DS-17-10) | `(public)/shared-games/page.stories.tsx`, `(public)/how-it-works/page.stories.tsx` etc — established `@storybook/react` pattern |

---

## Stage 0 — BGG cleanup atomic commit (~30 min)

### Task 0.1: Edit 3 mockup files (BGG user-facing references)

**Files:**
- Modify: `admin-mockups/design_files/sp4-library-desktop.jsx` (5 lines: 118, 1051, 1064, 1448, 1524)
- Modify: `admin-mockups/design_files/sp4-game-detail.jsx` (line 390)
- Modify: `admin-mockups/design_files/sp4-upload-wizard-extended.jsx` (lines 100-103)
- **DO NOT** modify `sp4-add-game-drawer.jsx` (ADR-059 docstring is documentation, preserve)

- [ ] **Step 1: Verify branch state**

Run: `git branch --show-current`
Expected: `feature/issue-2214-ds-17-12-sp4-catalog`

Run: `git log --oneline -3`
Expected: spec doc `69c20fd72` at HEAD.

- [ ] **Step 2: Edit sp4-library-desktop.jsx line 118**

Read context:
```bash
sed -n '115,125p' admin-mockups/design_files/sp4-library-desktop.jsx
```

Find button text containing `Importa BGG`. Edit:
```
Find:    >↓ Importa BGG</button>
Replace: >+ Aggiungi gioco</button>
```

Also handle the `onClick` window.location navigation to `sp4-add-game-bgg-step.html` — replace with `sp4-add-game-drawer.html`:
```
Find:    window.location.href = 'sp4-add-game-bgg-step.html';
Replace: window.location.href = 'sp4-add-game-drawer.html';
```

- [ ] **Step 3: Edit sp4-library-desktop.jsx line 1051**

Read context:
```bash
sed -n '1048,1055p' admin-mockups/design_files/sp4-library-desktop.jsx
```

Find: `'Inizia aggiungendo il tuo primo gioco. Importa la collezione da BoardGameGeek o cerca per titolo.'`
Replace: `'Inizia aggiungendo il tuo primo gioco dal catalogo condiviso o crea un gioco custom.'`

- [ ] **Step 4: Edit sp4-library-desktop.jsx line 1064**

Find button text `↓ Importa da BGG`:
```
Replace: + Aggiungi gioco
```

Also update onClick href: `sp4-add-game-bgg-step.html` → `sp4-add-game-drawer.html`.

- [ ] **Step 5: Edit sp4-library-desktop.jsx lines 1448 + 1524**

Line 1448 contains a state matrix description for `empty-first-run` state mentioning "4 suggerimenti BGG". Edit:
```
Find: 4 suggerimenti BGG con add inline
Replace: 4 suggerimenti catalogo con add inline
```

Line 1524 description mentions "3 suggerimenti BGG con +". Edit similarly:
```
Find: 3 suggerimenti BGG con +
Replace: 3 suggerimenti catalogo con +
```

- [ ] **Step 6: Edit sp4-game-detail.jsx line 390**

Read context:
```bash
sed -n '385,395p' admin-mockups/design_files/sp4-game-detail.jsx
```

Find `{ label:'Rating BGG', value: game.rating }`:
```
Replace: { label:'Voto community', value: game.rating }
```

- [ ] **Step 7: Edit sp4-upload-wizard-extended.jsx lines 100-103**

Read context:
```bash
sed -n '95,110p' admin-mockups/design_files/sp4-upload-wizard-extended.jsx
```

Find BGG upload option block:
```jsx
{
  id: 'bgg',
  ...
  title: 'Da BoardGameGeek',
  sub: 'Cerca nel catalogo BGG',
}
```

Remove the entire BGG option object from the upload sources array. Verify the surrounding array remains valid JS (no broken commas).

- [ ] **Step 8: Run BGG verification grep**

```bash
grep -in "BGG\|BoardGameGeek\|boardgamegeek" \
  admin-mockups/design_files/sp4-{library-desktop,game-detail,upload-wizard-extended}.jsx \
  2>/dev/null
```

Expected output: ONLY references inside comments documenting ADR-059 or technical context (not user-facing buttons/labels). If user-facing references remain, fix them.

Note: `sp4-add-game-drawer.jsx` ADR-059 docstring (lines 9-11) is preserved intentionally.

- [ ] **Step 9: Commit BGG cleanup**

```bash
git add admin-mockups/design_files/sp4-library-desktop.jsx \
        admin-mockups/design_files/sp4-game-detail.jsx \
        admin-mockups/design_files/sp4-upload-wizard-extended.jsx

git commit -m "$(cat <<'EOF'
chore(mockups): #2214 DS-17-12 BGG removal sp4 catalog cluster

DEC-inherited-1 + DEC-Pilot-7: BGG cleanup Stage 0 prep work pre-AI dispatch.

3 mockup JSX files edited (sp4-add-game-drawer.jsx ADR-059 docstring preserved):

sp4-library-desktop.jsx:
- Line 118: "Importa BGG" button → "Aggiungi gioco" + onClick → sp4-add-game-drawer
- Line 1051: BGG import copy → "catalogo condiviso"
- Line 1064: "Importa da BGG" button → "Aggiungi gioco" + onClick → sp4-add-game-drawer
- Line 1448 + 1524: state matrix "suggerimenti BGG" → "suggerimenti catalogo"

sp4-game-detail.jsx:
- Line 390: label "Rating BGG" → "Voto community"

sp4-upload-wizard-extended.jsx:
- Lines 100-103: removed "Da BoardGameGeek" upload option block entirely

Post-cleanup: AI dispatch reads BGG-free state. ADR-059 admin-only constraint
documented in sp4-add-game-drawer.jsx docstring (preserved).

Refs: #2214, #2151 BGG ToS umbrella, #1903 #2123 codebase clean.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 10: Extend #2151 with new findings**

```bash
gh issue comment 2151 --body "$(cat <<'EOF'
**DS-17-12 sub-issue #2214 sess.46p — 7 nuovi findings sp4-catalog cluster** (Phase B audit miss):

| Mockup | Line | Severity | Description |
|---|---|---|---|
| sp4-library-desktop.jsx | 118 | HIGH | "Importa BGG" CTA button user-facing (removed → "Aggiungi gioco") |
| sp4-library-desktop.jsx | 1051 | MEDIUM | "Importa la collezione da BoardGameGeek" copy (replaced → catalogo condiviso) |
| sp4-library-desktop.jsx | 1064 | HIGH | "Importa da BGG" CTA button user-facing (removed → "Aggiungi gioco") |
| sp4-library-desktop.jsx | 1448 | LOW | "suggerimenti BGG" in state matrix description (replaced → catalogo) |
| sp4-library-desktop.jsx | 1524 | LOW | "suggerimenti BGG" in state matrix description (replaced → catalogo) |
| sp4-game-detail.jsx | 390 | MEDIUM | "Rating BGG" label user-facing (replaced → "Voto community") |
| sp4-upload-wizard-extended.jsx | 100-103 | HIGH | "Da BoardGameGeek" upload option block (removed entire block) |

Cleanup atomic commit landed in feature/issue-2214-ds-17-12-sp4-catalog branch. Note: sp4-add-game-drawer.jsx lines 9-11 ADR-059 docstring **preserved** (documents admin-only constraint, NOT user-facing violation).

Pattern: DEC-Pilot-7 (BGG cleanup as Stage 0 prep work pre-AI dispatch) identico a DS-17-11 sp6-7-nano + DS-17-10 sp3.
EOF
)"
```

---

## Stage 1 — sp4-library-mobile forward-refactor (~2h)

### Task 1.1: Dispatch Agent for sp4-library-mobile scaffold + tracking issue

**Files:**
- Create: story file at appropriate path (Agent discovers)
- Modify: `admin-mockups/design_files/sp4-library-mobile.fidelity.json`
- Side effect: gh issue create for designer review tracking

- [ ] **Step 1: Dispatch implementer subagent for sp4-library-mobile**

Use subagent-driven-development implementer-prompt.md template. Full task description:

```
You are implementing sp4-library-mobile forward-refactor scaffold for DS-17-12 #2214.

Context:
- design_intent: forward-refactor (designer review pending)
- Mockup: admin-mockups/design_files/sp4-library-mobile.{html,jsx,fidelity.json}
- Existing route: apps/web/src/app/(authenticated)/library/page.tsx (desktop primary)
- Pattern reference: sp3-library-public forward-refactor handling (PR #2211)

Steps:
1. Read mockup admin-mockups/design_files/sp4-library-mobile.jsx (verify it's mobile <768px variant)
2. Decide story location:
   - Option A: combine into existing (authenticated)/library/page.stories.tsx as `MobileVariant` Story
   - Option B: separate file `(authenticated)/library/page-mobile.stories.tsx`
   Choose based on existing pattern. If library/page.stories.tsx doesn't exist, create new file.
3. Scaffold story renders existing library page component with mobile viewport parameter.
4. Open designer review tracking issue:
   gh issue create --title "Designer review sp4-library-mobile forward-refactor (DS-17-12 #2214 follow-up)" \
     --label "area/frontend,mockup-drift" \
     --body "<body explaining forward-refactor status, mobile <768px scope, DS-17-12 ref>"
5. Update sp4-library-mobile.fidelity.json:
   - story_path: <new story file path>
   - obsolete_tracking_issue: "#<NEW_TRACKING_NUM>" (REQUIRED # prefix per fidelity validator)
6. Commit feat(stories): #2214 sp4-library-mobile forward-refactor scaffold + tracking

Constraints:
- Use @storybook/react (NOT @storybook/nextjs)
- NO hand-written @mockup JSDoc (let injector handle Stage 4)
- Verify pnpm typecheck passes
- Update MOCKUPS_INDEX.md if /library mobile mapping missing

Working directory: D:\Repositories\meepleai-monorepo-frontend
Branch: feature/issue-2214-ds-17-12-sp4-catalog

Report: DONE / DONE_WITH_CONCERNS / BLOCKED / NEEDS_CONTEXT with commit SHA + tracking issue # + files changed.
```

- [ ] **Step 2: Dispatch spec compliance reviewer**

Verify Agent output matches spec § 4.2 forward-refactor pattern + DEC-inherited-2.

- [ ] **Step 3: Dispatch code quality reviewer**

Verify story file follows @storybook/react pattern + no hand-written @mockup + lint:fidelity format.

---

## Stage 2 — sp4-add-game-bgg-step skip (~5 min)

### Task 2.1: Document skip + fidelity closure note

**Files:**
- Modify: `admin-mockups/design_files/sp4-add-game-bgg-step.fidelity.json` (closure note)
- NO story file created

- [ ] **Step 1: Verify state**

```bash
jq -r '.acceptance.design_intent, .acceptance.obsolete_tracking_issue' admin-mockups/design_files/sp4-add-game-bgg-step.fidelity.json
```

Expected pre-existing state (confirmed by review):
- `design_intent`: `forward-refactor-obsolete`
- `obsolete_tracking_issue`: `#2145` (pre-existing tracker)

If different from above, STOP and re-check.

- [ ] **Step 2: NO FIDELITY EDIT NEEDED — skip is documented**

The existing `obsolete_tracking_issue: "#2145"` is the canonical tracker for this obsolete mockup. DS-17-12 does NOT overwrite this — instead, the PR body and sub-issue closure comment will document that this stem is skipped per pre-existing closure tracker. No commit for fidelity update.

- [ ] **Step 3: Verify route MISSING (pre-flight confirmation)**

```bash
[ -f apps/web/src/app/\(authenticated\)/library/proposals/page.tsx ] && echo "EXISTS — escalate" || echo "MISSING — skip confirmed"
```

Expected: `MISSING — skip confirmed`. Route `(authenticated)/library/proposals/page.tsx` does NOT exist. Documents the skip rationale.

NO COMMIT in Stage 2 — skip is implicit (no story file, no fidelity edit). Documented in PR body Stage 5.

---

## Stage 3 — Standard stems hybrid dispatch (~7h)

### Task 3.1: Agent dispatch sp4-game-detail (POST-#2096 wire verify)

**Files:**
- Create: `apps/web/src/app/(authenticated)/games/[id]/page.stories.tsx`

- [ ] **Step 1: Dispatch implementer subagent**

```
You are implementing sp4-game-detail story migration for DS-17-12 #2214.

Context:
- Mockup: admin-mockups/design_files/sp4-game-detail.{html,jsx,fidelity.json}
- Route: apps/web/src/app/(authenticated)/games/[id]/page.tsx (existing)
- POST-EPIC #2096 deliverables: uses `GameDetailView` or similar component shipped via PR #2207
- M1-M7 wire verify: GameHero v2 + animated tabs + ConnectionBar + ContributorsStrip + Info card + Toolbox card + Layout

Steps:
1. Read existing route page.tsx + page-client.tsx (if exists) to identify component
2. Verify M1-M7 deliverables wire correctly:
   - Primary component: apps/web/src/app/(authenticated)/games/[id]/_components/GameDetailView.tsx (orchestrator with FSM + useLibraryGameDetail + tabs config)
   - Sub-component: apps/web/src/components/game-detail/GameDetailDesktop.tsx (POST-PR #2207, layout wired internally by GameDetailView)
   - Check imports and props on both
3. Scaffold page.stories.tsx with MSW handlers:
   - /api/v1/library/[gameId] returns LibraryGameDetail fixture
   - Story variants: Default + EmptyContent + LoadingState + ErrorState (if applicable)
4. Verify MSW fixture shape matches LibraryGameDetail interface from apps/web/src/hooks/queries/useLibrary.ts

Story scaffold pattern (use canonical @storybook/react):
```tsx
import type { Meta, StoryObj } from '@storybook/react';
import GameDetailPage from './page';
// ... MSW handlers
```

Constraints:
- @storybook/react import (NOT @storybook/nextjs)
- NO hand-written @mockup JSDoc
- MSW fixture shape verified against actual type
- BGG references already removed in Stage 0

Working directory: D:\Repositories\meepleai-monorepo-frontend

Report status + commit SHA + MSW fixture shape used.
```

- [ ] **Step 2: Dispatch spec reviewer**

Verify M1-M7 deliverables wire verified + MSW handlers present.

- [ ] **Step 3: Dispatch code quality reviewer**

Verify pattern adherence + token compliance.

### Task 3.2: Inline batch 11 standard stems

**Files (create):**
- `apps/web/src/app/(authenticated)/library/page.stories.tsx`
- `apps/web/src/app/(authenticated)/library/wishlist/page.stories.tsx`
- `apps/web/src/app/(authenticated)/library/private/add/page.stories.tsx` (covers sp4-add-game-pdf-dedup + sp4-upload-wizard-extended via Frame variants)
- `apps/web/src/app/(authenticated)/games/page.stories.tsx`
- `apps/web/src/app/(authenticated)/agents/page.stories.tsx`
- `apps/web/src/app/(authenticated)/agents/[id]/page.stories.tsx`
- `apps/web/src/app/(authenticated)/discover/page.stories.tsx`
- `apps/web/src/components/features/game-chat/GameChatTab.stories.tsx`
- Component-mock for sp4-citation-pdf-viewer (path TBD via grep Step 1)
- Component-mock for sp4-add-game-drawer (path TBD via grep Step 1)

- [ ] **Step 1: Discover component-mock target paths**

```bash
# sp4-citation-pdf-viewer
find apps/web/src/components -iname "*citation*" -name "*.tsx" 2>/dev/null | grep -v test
# Likely: apps/web/src/components/chat-unified/CitationBlock.tsx OR chat/panel/ChatCitationCard.tsx

# sp4-add-game-drawer
find apps/web/src/components -iname "*add*game*" -name "*.tsx" 2>/dev/null | grep -v test
# Likely: apps/web/src/components/features/add-game/AddGameDrawer.tsx OR similar
```

Record paths. If MISSING for either: scaffold inline as story rendering a placeholder OR document as deferred follow-up.

- [ ] **Step 2: Scaffold sp4-library-desktop story**

Create `apps/web/src/app/(authenticated)/library/page.stories.tsx`:

```tsx
/**
 * sp4-library-desktop — DS-17-12 #2214 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-library-desktop.{html,jsx}`.
 * POST Stage 0 BGG cleanup.
 */

import type { Meta, StoryObj } from '@storybook/react';

import LibraryPage from './page';

const meta: Meta<typeof LibraryPage> = {
  title: 'Authenticated / sp4-library-desktop',
  component: LibraryPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2214 DS-17-12. Authenticated library catalog (desktop primary). Mockup-aligned post Stage 0 BGG cleanup.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof LibraryPage>;

export const Default: Story = {};
```

- [ ] **Step 3: Scaffold sp4-library-wishlist story**

Create `apps/web/src/app/(authenticated)/library/wishlist/page.stories.tsx`:

```tsx
/**
 * sp4-library-wishlist — DS-17-12 #2214 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-library-wishlist.{html,jsx}`.
 */

import type { Meta, StoryObj } from '@storybook/react';

import WishlistPage from './page';

const meta: Meta<typeof WishlistPage> = {
  title: 'Authenticated / sp4-library-wishlist',
  component: WishlistPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component: '#2214 DS-17-12. Wishlist sub-page of authenticated library.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof WishlistPage>;

export const Default: Story = {};
```

- [ ] **Step 4: Scaffold sp4-add-game-pdf-dedup story (canonical for upload wizard)**

Create `apps/web/src/app/(authenticated)/library/private/add/page.stories.tsx`:

```tsx
/**
 * sp4-add-game-pdf-dedup + sp4-upload-wizard-extended — DS-17-12 #2214.
 *
 * Multi-mockup route: same `/library/private/add` page covers
 * - sp4-add-game-pdf-dedup.html (PDF dedup wizard step)
 * - sp4-upload-wizard-extended.html (extended wizard flow with BGG option removed Stage 0)
 *
 * Canonical story renders the existing wizard page component.
 */

import type { Meta, StoryObj } from '@storybook/react';

import PrivateAddPage from './page';

const meta: Meta<typeof PrivateAddPage> = {
  title: 'Authenticated / sp4-add-game-pdf-dedup',
  component: PrivateAddPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2214 DS-17-12. Add game wizard (PDF dedup step). Covers sp4-add-game-pdf-dedup.html + sp4-upload-wizard-extended.html mockups (canonical route).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof PrivateAddPage>;

export const Default: Story = {};
```

- [ ] **Step 5: Scaffold sp4-games-index story**

Create `apps/web/src/app/(authenticated)/games/page.stories.tsx`:

```tsx
/**
 * sp4-games-index — DS-17-12 #2214 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-games-index.{html,jsx}`.
 */

import type { Meta, StoryObj } from '@storybook/react';

import GamesIndexPage from './page';

const meta: Meta<typeof GamesIndexPage> = {
  title: 'Authenticated / sp4-games-index',
  component: GamesIndexPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component: '#2214 DS-17-12. Authenticated games catalog index.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof GamesIndexPage>;

export const Default: Story = {};
```

- [ ] **Step 6: Scaffold sp4-agents-index story**

Create `apps/web/src/app/(authenticated)/agents/page.stories.tsx`:

```tsx
/**
 * sp4-agents-index — DS-17-12 #2214 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-agents-index.{html,jsx}`.
 */

import type { Meta, StoryObj } from '@storybook/react';

import AgentsIndexPage from './page';

const meta: Meta<typeof AgentsIndexPage> = {
  title: 'Authenticated / sp4-agents-index',
  component: AgentsIndexPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component: '#2214 DS-17-12. Authenticated agents catalog.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof AgentsIndexPage>;

export const Default: Story = {};
```

- [ ] **Step 7: Scaffold sp4-agent-detail story**

Create `apps/web/src/app/(authenticated)/agents/[id]/page.stories.tsx`:

```tsx
/**
 * sp4-agent-detail — DS-17-12 #2214 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-agent-detail.{html,jsx}`.
 */

import type { Meta, StoryObj } from '@storybook/react';

import AgentDetailPage from './page';

const meta: Meta<typeof AgentDetailPage> = {
  title: 'Authenticated / sp4-agent-detail',
  component: AgentDetailPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/agents/sp4-agent-detail-fixture',
      },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component: '#2214 DS-17-12. Authenticated agent detail view.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof AgentDetailPage>;

export const Default: Story = {};
```

- [ ] **Step 8: Scaffold sp4-discover story**

Create `apps/web/src/app/(authenticated)/discover/page.stories.tsx`:

```tsx
/**
 * sp4-discover — DS-17-12 #2214 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-discover.{html,jsx}`.
 */

import type { Meta, StoryObj } from '@storybook/react';

import DiscoverPage from './page';

const meta: Meta<typeof DiscoverPage> = {
  title: 'Authenticated / sp4-discover',
  component: DiscoverPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component: '#2214 DS-17-12. Discover surface for community content.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof DiscoverPage>;

export const Default: Story = {};
```

- [ ] **Step 9: Scaffold sp4-game-chat-tab component-mock story**

Path verified: `apps/web/src/components/features/game-chat/GameChatTab.tsx`.

Create `apps/web/src/components/features/game-chat/GameChatTab.stories.tsx`:

```tsx
/**
 * sp4-game-chat-tab — DS-17-12 #2214 sub-issue.
 *
 * Component-mock embedded in /games/[id] + /library/[gameId]/agent routes.
 * Mockup parity: `admin-mockups/design_files/sp4-game-chat-tab.{html,jsx}`.
 */

import type { Meta, StoryObj } from '@storybook/react';

import { GameChatTab } from './GameChatTab';

const meta: Meta<typeof GameChatTab> = {
  title: 'Component-mocks / sp4-game-chat-tab',
  component: GameChatTab,
  parameters: {
    layout: 'padded',
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2214 DS-17-12. Game chat tab embedded in game detail + library agent routes. Component-mock (no standalone route).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof GameChatTab>;

export const Default: Story = {
  args: {
    // GameChatTab props discovered via grep in Step 1 — adapt to actual signature
  },
};
```

NOTE: Verify GameChatTab props signature via Read tool before completing. Adapt `args` to match the actual interface.

- [ ] **Step 10: Scaffold sp4-citation-pdf-viewer component-mock story**

Path discovered in Step 1. Likely `apps/web/src/components/chat-unified/CitationBlock.tsx` or `apps/web/src/components/chat/panel/ChatCitationCard.tsx`. Use the path identified in Step 1.

Scaffold pattern similar to GameChatTab.stories.tsx (above) with title `'Component-mocks / sp4-citation-pdf-viewer'`.

- [ ] **Step 11: Scaffold sp4-add-game-drawer component-mock story (path confirmed post review)**

Path: `apps/web/src/app/(authenticated)/library/AddGameDrawer.stories.tsx` (next to component, NOT in `components/features/`).

Create file:

```tsx
/**
 * sp4-add-game-drawer — DS-17-12 #2214 sub-issue.
 *
 * Component-mock embedded in /library route (Add game drawer flow).
 * Mockup parity: `admin-mockups/design_files/sp4-add-game-drawer.{html,jsx}`.
 * Post Stage 0 BGG cleanup (sp4-add-game-drawer.jsx ADR-059 docstring preserved per legal constraint).
 */

import type { Meta, StoryObj } from '@storybook/react';

import { AddGameDrawer } from './AddGameDrawer';

const meta: Meta<typeof AddGameDrawer> = {
  title: 'Component-mocks / sp4-add-game-drawer',
  component: AddGameDrawer,
  parameters: {
    layout: 'padded',
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2214 DS-17-12. Add game drawer embedded in /library route. Component-mock (no standalone route).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof AddGameDrawer>;

export const Default: Story = {
  args: {
    open: true,
    onClose: () => undefined,
  },
};

export const Closed: Story = {
  args: {
    open: false,
    onClose: () => undefined,
  },
};
```

Verify `AddGameDrawer` named export signature pre-scaffold. Adapt args if interface differs.

- [ ] **Step 11.5: Scaffold sp4-upload-wizard-extended story (separate route per review fix)**

Create `apps/web/src/app/(authenticated)/gamebook/upload/page.stories.tsx`:

```tsx
/**
 * sp4-upload-wizard-extended — DS-17-12 #2214 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-upload-wizard-extended.{html,jsx}`.
 * Post Stage 0 BGG cleanup (BGG upload option block removed).
 *
 * MOCKUPS_INDEX line 151: `sp4-upload-wizard-extended.html` → `/upload`, `/gamebook/upload (partial)`.
 * Canonical story target = `/gamebook/upload`.
 */

import type { Meta, StoryObj } from '@storybook/react';

import GamebookUploadPage from './page';

const meta: Meta<typeof GamebookUploadPage> = {
  title: 'Authenticated / sp4-upload-wizard-extended',
  component: GamebookUploadPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2214 DS-17-12. Upload wizard extended flow (gamebook upload). BGG upload option removed in Stage 0 cleanup.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof GamebookUploadPage>;

export const Default: Story = {};
```

- [ ] **Step 11.6: Update 3 fidelity.json story_path fields (review fixes IMPORTANT-1 + IMPORTANT-4)**

3 stems whose fidelity.json `story_path` field needs updating post-story-creation:

```bash
# sp4-add-game-drawer
jq '.acceptance.story_path = "apps/web/src/app/(authenticated)/library/AddGameDrawer.stories.tsx"' \
  admin-mockups/design_files/sp4-add-game-drawer.fidelity.json > /tmp/sp4-add-game-drawer-tmp.json
mv /tmp/sp4-add-game-drawer-tmp.json admin-mockups/design_files/sp4-add-game-drawer.fidelity.json

# sp4-library-wishlist
jq '.acceptance.story_path = "apps/web/src/app/(authenticated)/library/wishlist/page.stories.tsx"' \
  admin-mockups/design_files/sp4-library-wishlist.fidelity.json > /tmp/sp4-library-wishlist-tmp.json
mv /tmp/sp4-library-wishlist-tmp.json admin-mockups/design_files/sp4-library-wishlist.fidelity.json

# sp4-upload-wizard-extended
jq '.acceptance.story_path = "apps/web/src/app/(authenticated)/gamebook/upload/page.stories.tsx"' \
  admin-mockups/design_files/sp4-upload-wizard-extended.fidelity.json > /tmp/sp4-upload-wizard-tmp.json
mv /tmp/sp4-upload-wizard-tmp.json admin-mockups/design_files/sp4-upload-wizard-extended.fidelity.json
```

Verify with `pnpm --filter @meepleai/web lint:fidelity` — all should PASS.

- [ ] **Step 12: Verify typecheck**

```bash
pnpm --filter @meepleai/web typecheck
```

Expected: 0 errors. If failures, identify which story import is broken (likely component-mock path mismatch — re-grep + fix).

- [ ] **Step 13: Commit Stage 3 batch**

```bash
git add apps/web/src/app/\(authenticated\)/library/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/library/wishlist/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/library/private/add/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/games/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/agents/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/agents/\[id\]/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/discover/page.stories.tsx \
        apps/web/src/components/features/game-chat/GameChatTab.stories.tsx \
        <citation viewer story path from Step 10> \
        <add-game-drawer story path from Step 11>

git commit -m "$(cat <<'EOF'
feat(stories): #2214 DS-17-12 sp4-catalog 10 standard stems batch

Stage 3 inline batch (P251 hybrid pattern): 10 new page.stories.tsx + component-mock stories.

| Stem | Story file | Notes |
| --- | --- | --- |
| sp4-library-desktop | (authenticated)/library/page.stories.tsx | Mockup-aligned post BGG cleanup |
| sp4-library-wishlist | (authenticated)/library/wishlist/page.stories.tsx | Wishlist sub-page |
| sp4-add-game-pdf-dedup | (authenticated)/library/private/add/page.stories.tsx | Canonical for upload wizard (also covers sp4-upload-wizard-extended) |
| sp4-games-index | (authenticated)/games/page.stories.tsx | Catalog index |
| sp4-agents-index | (authenticated)/agents/page.stories.tsx | Agents list |
| sp4-agent-detail | (authenticated)/agents/[id]/page.stories.tsx | Agent detail |
| sp4-discover | (authenticated)/discover/page.stories.tsx | Discover surface |
| sp4-game-chat-tab | components/features/game-chat/GameChatTab.stories.tsx | Component-mock |
| sp4-citation-pdf-viewer | <discovered path>.stories.tsx | Component-mock |
| sp4-add-game-drawer | <discovered path>.stories.tsx | Component-mock |

Pattern: @storybook/react import (DS-17-10 lesson). Default story renders existing component. NO MSW handlers (static pages or covered by route component's data fetching). NO hand-written @mockup JSDoc.

sp4-upload-wizard-extended covered by sp4-add-game-pdf-dedup canonical story (same route, multi-mockup canonical pattern P254).

DEC-5: no new tests added.

Refs: #2214, spec section 4.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Stage 4 — Quality gates (~30 min)

### Task 4.1: Full quality gate sweep

- [ ] **Step 1: Run mockup-annotations:inject (idempotent)**

```bash
pnpm --filter @meepleai/web mockup-annotations:inject --apply
```

This injects `@mockup` JSDoc markers into page.tsx files based on MOCKUPS_INDEX.md mappings. If any new files were added without entries in INDEX, this is a no-op (will be flagged in audit Step 2).

- [ ] **Step 2: Run all gates in parallel-safe order**

```bash
pnpm --filter @meepleai/web test --run
pnpm --filter @meepleai/web lint
pnpm --filter @meepleai/web lint:tokens
pnpm --filter @meepleai/web lint:bgg
pnpm --filter @meepleai/web lint:fidelity
pnpm --filter @meepleai/web mockup-annotations:audit --denominator mappable --threshold 80
pnpm --filter @meepleai/web typecheck
```

Expected: 0 errors / 0 violations / ≥80% mappable coverage on each.

- [ ] **Step 3: Address gate failures**

- `lint` warning on `<a>` tag: replace with `<Link>` from `next/link` (DS-17-10 lesson).
- `lint:fidelity` format error: `obsolete_tracking_issue` must have `#` prefix.
- `lint:bgg` violations: re-run Stage 0 cleanup (likely missed reference).
- `lint:tokens` violations: replace hardcoded color utilities with semantic tokens.
- `mockup-annotations:audit` <80%: verify MOCKUPS_INDEX.md mappings exist for new routes.

If any gate-fix commits needed, commit with:
```bash
git commit -m "fix(library-public): #2214 Stage 4 quality gate fixes"
```

(Adjust scope name based on which file is fixed.)

- [ ] **Step 4: Re-run gates if fixes applied**

Repeat Step 2 until all gates pass.

---

## Stage 5 — Merge + closure (~30 min)

### Task 5.1: Push + open PR

- [ ] **Step 1: Verify commit chain**

```bash
git log --oneline main-dev..HEAD
```

Expected commits in order:
- `69c20fd72` docs(specs) — design spec
- `<plan commit>` docs(plans) — implementation plan
- `<Stage 0>` chore(mockups) — BGG cleanup
- `<Stage 1>` feat(stories) — sp4-library-mobile forward-refactor
- `<Stage 2>` chore(mockups) — sp4-add-game-bgg-step skip closure
- `<Stage 3.1>` feat(stories) — sp4-game-detail POST-#2096 wire
- `<Stage 3.2>` feat(stories) — 10 standard stems batch
- `<Stage 4>` (optional) fix — quality gate fixes

- [ ] **Step 2: Push branch (pre-push hook ~10 min `pnpm build`)**

```bash
git push -u origin feature/issue-2214-ds-17-12-sp4-catalog
```

Wait for pre-push hook completion (~10 min). If `.next/static/*` ENOENT: `rm -rf apps/web/.next && retry`.

- [ ] **Step 3: Open PR**

```bash
gh pr create --base main-dev \
  --title "feat(stories): #2214 DS-17-12 sp4-core-catalog — 14 stems" \
  --body "$(cat <<'EOF'
## Summary

Closes DS-17 Phase C-2 step 1/4 (post Phase C-1 3/3 closure sess.46p). Unblocks DS-17-13 sp4-core-content brainstorm.

14 sp4-catalog stems shipped per DEC distribution:
- **1 SKIP**: sp4-add-game-bgg-step (forward-refactor-obsolete, route missing)
- **1 FORWARD-REFACTOR**: sp4-library-mobile (Agent dispatch + tracking issue #<TRACKING_NUM>)
- **1 POST-#2096 WIRE VERIFY**: sp4-game-detail (Agent dispatch, M1-M7 wired)
- **11 STANDARD INLINE BATCH** (P251 pattern): library-desktop + library-wishlist + add-game-drawer + add-game-pdf-dedup (canonical for upload wizard) + games-index + agents-index + agent-detail + game-chat-tab + citation-pdf-viewer + discover + (upload-wizard-extended subsumed)

## Stage 0 BGG cleanup

3 mockup files edited (7 user-facing BGG references removed). sp4-add-game-drawer.jsx ADR-059 docstring preserved (documents admin-only constraint). #2151 extended with 7 new findings.

## Stage 3 baseline capture DEFERRED

P252 defer pattern: Storybook + Playwright runner ops heavy, non-blocking per merge.

## ⏳ DESIGNER REVIEW SKIPPED

User waiver Opzione C precedent. sp4-library-mobile forward-refactor → tracking issue #<TRACKING_NUM> OPENED per future iteration.

## Spec + plan

- Design spec: `docs/superpowers/specs/2026-06-11-ds-17-phase-c-2-sp4-split-and-ds-17-12-design.md`
- Implementation plan: `docs/superpowers/plans/2026-06-11-ds-17-12-sp4-core-catalog-plan.md`
- 6 DEC user-locked (4 new sess.46p + 2 inherited Phase C-1)

## Test plan

- [x] `pnpm test` → 0 regression
- [x] `pnpm lint` → 0 errors
- [x] `pnpm lint:tokens` → 0 violations
- [x] `pnpm lint:bgg` → clean
- [x] `pnpm lint:fidelity` → all PASS
- [x] `pnpm typecheck` → 0 errors
- [x] `pnpm mockup-annotations:audit` → ≥80% mappable
- [x] Backend build clean (pre-push hook)
- [ ] Baseline capture DEFERRED (P252)
- [ ] Designer review SKIPPED per user waiver (Opzione C)

## Refs

- Closes #2214 (sub-issue)
- DS-17 Phase C-2 step 1/4 progress (umbrella #2063)
- Phase C-1 closure precedent: PR #2164 + #2173 + #2211
- BGG ToS: #1903 #2123 #2151
- Designer review follow-up tracking: #<TRACKING_NUM>
- EPIC #2096 closure trigger: PR #2207 `b98e4328b` (POST-rebuild deliverables)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Replace `<TRACKING_NUM>` with actual issue number from Task 1.1 Step 4.

### Task 5.2: Admin-squash merge P145 39a

- [ ] **Step 1: Verify PR checks**

```bash
gh pr checks <PR_NUM>
```

Expected: required checks pass (or admin override per P145 pattern).

- [ ] **Step 2: Admin-squash merge**

```bash
gh pr merge <PR_NUM> --admin --squash --delete-branch
```

- [ ] **Step 3: Local sync**

```bash
git checkout main-dev
# Discard regen audit files if needed
git checkout -- audits/ 2>/dev/null || true
git pull --ff-only
```

### Task 5.3: Close sub-issue + EPIC progress

- [ ] **Step 1: Close #2214 with AC evidence**

```bash
gh issue close 2214 --reason completed --comment "$(cat <<'EOF'
🎉 Shipped via PR #<PR_NUM> (`<merge_sha>` admin-squash P145 39a volta).

## AC closure evidence

### Stage 0 BGG cleanup ✅
- 3 mockup files edited (sp4-library-desktop + sp4-game-detail + sp4-upload-wizard-extended)
- 7 user-facing BGG references removed
- sp4-add-game-drawer.jsx ADR-059 docstring preserved (admin-only constraint documentation)
- #2151 extended with 7 new findings

### Stage 1 sp4-library-mobile forward-refactor ✅
- Scaffold story created (path: <story path>)
- Designer review tracking issue #<TRACKING_NUM> OPENED
- sp4-library-mobile.fidelity.json updated with obsolete_tracking_issue

### Stage 2 sp4-add-game-bgg-step skip ✅
- No story created (forward-refactor-obsolete, route /library/proposals missing)
- fidelity.json closure note updated (DS-17-12 reference)

### Stage 3 12 standard stems ✅
- sp4-game-detail Agent dispatch (POST-EPIC #2096 wire verified)
- 11 standard stems inline batch (P251 pattern)
- Multi-mockup canonical: sp4-add-game-pdf-dedup covers sp4-upload-wizard-extended (P254 pattern)
- Component-mocks: sp4-game-chat-tab + sp4-citation-pdf-viewer + sp4-add-game-drawer

### Stage 4 quality gates ✅
- pnpm test 0 regression
- pnpm lint 0 errors
- pnpm lint:tokens 0 violations
- pnpm lint:bgg clean
- pnpm lint:fidelity all PASS
- pnpm typecheck 0 errors
- pnpm mockup-annotations:audit ≥80% mappable

### Stage 5 closure ✅
- Admin-squash merge P145 39a volta
- Designer review SKIPPED per user waiver Opzione C

DS-17 Phase C-2 progress: 1/4 step shipped. NEXT: DS-17-13 sp4-core-content brainstorm.
EOF
)"
```

- [ ] **Step 2: EPIC #2063 progress comment**

```bash
gh issue comment 2063 --body "$(cat <<'EOF'
🎉 **DS-17 Phase C-2 step 1/4 SHIPPED via PR #<PR_NUM>** (sub-issue #2214 sess.46p 2026-06-11).

## Phase C-2 progress

| Step | Sub-issue | PR | Status |
|---|---|---|---|
| 1 — sp4-core-catalog | #2214 | #<PR_NUM> | ✅ MERGED sess.46p |
| 2 — sp4-core-content | TBD | TBD | 🚧 Future brainstorm |
| 3 — sp4-core-admin | TBD | TBD | 🚧 Future brainstorm |
| 4 — sp4-sessions skeleton | TBD | TBD | 🚧 Future brainstorm |

Phase C-2 cumulative effort: ~10-11h (DS-17-12). Estimated ~40-46h total for Phase C-2.

## Carry-over

- Per-game session stories (Catan/Codenames/etc.) deferred Phase C-3
- Visual baseline capture (DEC-3) deferred follow-up (P252)
- sp4-library-mobile forward-refactor designer review tracking #<TRACKING_NUM>
EOF
)"
```

### Task 5.4: Memory entry + DS-17-13 trigger note

**Files:**
- Create: `C:\Users\Utente\.claude\projects\D--Repositories-meepleai-monorepo-frontend\memory\ds-17-12-sp4-catalog-shipped.md`
- Modify: `C:\Users\Utente\.claude\projects\D--Repositories-meepleai-monorepo-frontend\memory\MEMORY.md` (add index entry)

- [ ] **Step 1: Write memory entry**

Create file with content:

```markdown
---
name: ds-17-12-sp4-catalog-shipped
description: "DS-17-12 sp4-core-catalog migration — 14 stems shipped (1 skip + 1 forward-refactor + 12 standard) sess.46p — Phase C-2 step 1/4 closure"
metadata:
  node_type: memory
  type: project
---

# DS-17-12 sp4-core-catalog shipped sess.46p

DS-17 Phase C-2 step 1/4 **CLOSED** sess.46p 2026-06-11 via PR #<PR_NUM> (`<merge_sha>` admin-squash P145 39a volta). Sub-issue #2214 cluster sp4-catalog (14 stems migration).

## Shipped scope (14 stems)

| Stem | Action | Story file |
|---|---|---|
| sp4-library-desktop | Ship | `(authenticated)/library/page.stories.tsx` |
| sp4-library-mobile | Ship forward-refactor + tracking #<TRACKING_NUM> | `<sp4-library-mobile path>` |
| sp4-library-wishlist | Ship | `(authenticated)/library/wishlist/page.stories.tsx` |
| sp4-add-game-bgg-step | SKIP (obsolete + missing route) | None — fidelity closure note |
| sp4-add-game-drawer | Ship component-mock | `<add-game-drawer path>` |
| sp4-add-game-pdf-dedup | Ship canonical for upload wizard | `(authenticated)/library/private/add/page.stories.tsx` |
| sp4-games-index | Ship | `(authenticated)/games/page.stories.tsx` |
| sp4-game-detail | Ship Agent POST-#2096 | `(authenticated)/games/[id]/page.stories.tsx` |
| sp4-agents-index | Ship | `(authenticated)/agents/page.stories.tsx` |
| sp4-agent-detail | Ship | `(authenticated)/agents/[id]/page.stories.tsx` |
| sp4-game-chat-tab | Ship component-mock | `features/game-chat/GameChatTab.stories.tsx` |
| sp4-citation-pdf-viewer | Ship component-mock | `<citation viewer path>` |
| sp4-discover | Ship | `(authenticated)/discover/page.stories.tsx` |
| sp4-upload-wizard-extended | Subsumed in pdf-dedup canonical | N/A (multi-mockup canonical) |

## Effort recap

~10-11h cumulative sess.46p:
- Stage 0 BGG cleanup ~30 min (3 mockup files edited + #2151 extended with 7 findings)
- Stage 1 sp4-library-mobile ~2h (Agent dispatch + tracking issue)
- Stage 2 sp4-add-game-bgg-step skip ~5 min (closure note)
- Stage 3 standard hybrid ~7h (1 Agent POST-#2096 + 11 inline batch)
- Stage 4 quality gates ~30 min
- Stage 5 merge + closure 30 min

## DS-17 Phase C-2 progress

| Step | Sub-issue | PR |
|---|---|---|
| 1 — catalog | #2214 | #<PR_NUM> |
| 2 — content | TBD | TBD |
| 3 — admin | TBD | TBD |
| 4 — sessions skeleton | TBD | TBD |

## NEW patterns discovered

(Fill in based on execution findings.)

## Links

- Sub-issue closed: #2214
- PR: #<PR_NUM>
- Spec: docs/superpowers/specs/2026-06-11-ds-17-phase-c-2-sp4-split-and-ds-17-12-design.md
- Plan: docs/superpowers/plans/2026-06-11-ds-17-12-sp4-core-catalog-plan.md
- Designer review follow-up: #<TRACKING_NUM>
- Predecessor: [[ds-17-10-sp3-cluster-shipped]] (Phase C-1 final)
- DS-17 umbrella: #2063
```

Replace runtime values.

- [ ] **Step 2: Update MEMORY.md index**

Add new entry at top of MEMORY.md:

```markdown
- [DS-17-12 sp4-catalog shipped](ds-17-12-sp4-catalog-shipped.md) — DS-17 Phase C-2 step 1/4 **CLOSED** sess.46p 2026-06-11 via PR #<PR_NUM> (admin-squash P145 39a). Sub-issue #2214 cluster sp4-catalog (14 stems: 1 skip + 1 forward-refactor + 12 standard). Hybrid P251 dispatch (Agent x2 sp4-game-detail+sp4-library-mobile + inline batch x11). 6 DEC totali (4 new + 2 inherited). Effort ~10-11h. Phase C-2 progress: 1/4 step. NEXT DS-17-13 sp4-core-content brainstorm.
```

- [ ] **Step 3: Notify completion**

Final message to user: "DS-17-12 sp4-catalog CLOSED. DS-17 Phase C-2 1/4 step shipped. DS-17-13 sp4-core-content brainstorm ready as next step."

---

## Self-review checklist (run BEFORE marking plan complete)

- [ ] Stage 0 covers all 7 BGG findings in 3 files + #2151 extension
- [ ] Stage 1 covers sp4-library-mobile forward-refactor (Agent dispatch + tracking issue)
- [ ] Stage 2 covers sp4-add-game-bgg-step skip (closure note + missing route documented)
- [ ] Stage 3.1 covers sp4-game-detail POST-#2096 wire verify (Agent dispatch)
- [ ] Stage 3.2 covers 10 standard stems (multi-mockup canonical noted)
- [ ] All 6 DEC respected (4 new + 2 inherited)
- [ ] No TBD/TODO placeholders (runtime values <PR_NUM> / <TRACKING_NUM> / <merge_sha> documented)
- [ ] All code samples use @storybook/react (DS-17-10 lesson)
- [ ] No hand-written @mockup JSDoc in scaffolds (DS-17-10 lesson)
- [ ] Component-mock path discovery documented for sp4-citation-pdf-viewer + sp4-add-game-drawer
- [ ] Pre-flight P124 already done
- [ ] Branch hygiene #806 respected

---

**End of implementation plan.**
