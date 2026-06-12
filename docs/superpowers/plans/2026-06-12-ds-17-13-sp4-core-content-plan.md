# DS-17-13 sp4-core-content Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate 18 sp4-content cluster stems to Storybook stories (1 forward-refactor + 1 route-create + 1 Agent multi-route + 15 inline batch standard), closing DS-17 Phase C-2 step 2/4.

**Architecture:** Pure FE work. Stage 0 BGG cleanup (1 file identified: sp4-kb-globale.jsx lines 2549-2550). Stage 1a sp4-kb-detail forward-refactor (Agent dispatch + tracking issue). Stage 1b sp4-kb-globale NEW route + KbGlobaleHome component (Agent dispatch). Stage 2 sp4-toolkit-detail multi-route + POST-#2096 reconciliation (Agent dispatch). Stage 3 15 standard stems inline batch (P251 hybrid pattern). Stage 4 quality gates. Stage 5 merge + closure.

**Tech Stack:** Next.js App Router, Storybook 8 (`@storybook/react`), Tailwind semantic tokens, `pnpm lint:*` gates.

**Spec ref:** [`docs/superpowers/specs/2026-06-12-ds-17-13-sp4-core-content-design.md`](../specs/2026-06-12-ds-17-13-sp4-core-content-design.md)

**Sub-issue:** [#2220](https://github.com/meepleAi-app/meepleai-monorepo/issues/2220)

**Branch:** `feature/issue-2220-ds-17-13-sp4-content` (pre-flight done — spec committed)

**Lessons applied (DS-17-12 review P255 + P256)**:
- ✅ `@storybook/react` import (NOT `@storybook/nextjs`)
- ✅ NO hand-written `@mockup` JSDoc (let injector handle Stage 4)
- ✅ Multi-mockup different routes → separate stories (P256)
- ✅ Pre-flight component path verification
- ✅ `obsolete_tracking_issue: "#1234"` format (REQUIRED # prefix)

**Pre-flight route verification (all PASS pre-execution)**:
- ✓ `(authenticated)/knowledge-base/` (page.tsx exists)
- ✓ `(authenticated)/editor/`, `editor/agent-proposals/`, `editor/agent-proposals/create/`, `editor/agent-proposals/[id]/edit/`, `editor/agent-proposals/[id]/test/`
- ✓ `(authenticated)/toolkit/` + `[sessionId]/` + `history/` + `stats/` + `templates/` + `play/`
- ✓ `(authenticated)/play-records/` + `[id]/` + `new/` + `stats/`
- ❌ `(authenticated)/knowledge-base/globale/` MISSING → Stage 1b route-create
- ❌ `(authenticated)/knowledge-base/[id]/` need verify (kb-detail forward-refactor handling)

---

## File Structure

### Files modified (Stage 0 BGG cleanup)

| Path | Lines | Edit |
|---|---|---|
| `admin-mockups/design_files/sp4-kb-globale.jsx` | 2549, 2550 | Remove "Connetti BGG" + "BoardGameGeek" references |

### Files created (Stage 1b route-create — Agent dispatch decides exact paths)

| Path | Responsibility |
|---|---|
| `apps/web/src/app/(authenticated)/knowledge-base/globale/page.tsx` | Server wrapper |
| `apps/web/src/app/(authenticated)/knowledge-base/globale/page.stories.tsx` | Storybook entry |
| `apps/web/src/components/features/knowledge-base/KbGlobaleHome.tsx` (or alternative path per existing patterns) | Client component |

### Files modified (Stage 1b)

| Path | Action |
|---|---|
| `admin-mockups/design_files/sp4-kb-globale.fidelity.json` | Update `story_path` |
| `admin-mockups/MOCKUPS_INDEX.md` | Add sp4-kb-globale → /knowledge-base/globale mapping |

### Files created (Stage 3 inline batch, 15 stories)

| Stem | Story file |
|---|---|
| sp4-kb-hub | `apps/web/src/app/(authenticated)/knowledge-base/page.stories.tsx` |
| sp4-editor-index | `apps/web/src/app/(authenticated)/editor/page.stories.tsx` |
| sp4-editor-proposals-index | `apps/web/src/app/(authenticated)/editor/agent-proposals/page.stories.tsx` |
| sp4-editor-proposals-create | `apps/web/src/app/(authenticated)/editor/agent-proposals/create/page.stories.tsx` |
| sp4-editor-proposals-edit | `apps/web/src/app/(authenticated)/editor/agent-proposals/[id]/edit/page.stories.tsx` |
| sp4-editor-proposals-test | `apps/web/src/app/(authenticated)/editor/agent-proposals/[id]/test/page.stories.tsx` |
| sp4-toolkit-history | `apps/web/src/app/(authenticated)/toolkit/history/page.stories.tsx` |
| sp4-toolkit-play | `apps/web/src/app/(authenticated)/toolkit/play/page.stories.tsx` |
| sp4-toolkit-stats | `apps/web/src/app/(authenticated)/toolkit/stats/page.stories.tsx` |
| sp4-toolkit-templates | `apps/web/src/app/(authenticated)/toolkit/templates/page.stories.tsx` |
| sp4-play-records-index | `apps/web/src/app/(authenticated)/play-records/page.stories.tsx` |
| sp4-play-records-detail | `apps/web/src/app/(authenticated)/play-records/[id]/page.stories.tsx` |
| sp4-play-records-edit | `apps/web/src/app/(authenticated)/play-records/[id]/edit/page.stories.tsx` |
| sp4-play-records-new | `apps/web/src/app/(authenticated)/play-records/new/page.stories.tsx` |
| sp4-play-records-stats | `apps/web/src/app/(authenticated)/play-records/stats/page.stories.tsx` |

Total: 15 inline batch story files (P251).

### Files created (Stage 1a + Stage 2 Agent dispatch)

| Stem | Story file |
|---|---|
| sp4-kb-detail (Agent) | `apps/web/src/app/(authenticated)/knowledge-base/[id]/page.stories.tsx` |
| sp4-toolkit-detail (Agent, P254 canonical) | `apps/web/src/app/(authenticated)/toolkit/page.stories.tsx` |

### Files modified (Stage 3 fidelity updates if needed)

Implementer verifies each 15 stem fidelity.json `story_path` post-creation.

---

## Stage 0 — BGG cleanup (~15 min)

### Task 0.1: Remove BGG references in sp4-kb-globale.jsx

**Files:**
- Modify: `admin-mockups/design_files/sp4-kb-globale.jsx` lines 2549-2550

- [ ] **Step 1: Verify branch state**

```bash
git branch --show-current
git log --oneline -3
```

Expected: branch `feature/issue-2220-ds-17-13-sp4-content`, spec doc at HEAD.

- [ ] **Step 2: Read context around lines 2549-2550**

```bash
sed -n '2545,2555p' admin-mockups/design_files/sp4-kb-globale.jsx
```

Expected: context shows `icon: '🎲', title: 'Connetti BGG',` and `desc: 'Sincronizza la tua collezione BoardGameGeek e importa i manuali in un click.',`.

- [ ] **Step 3: Edit BGG references**

Replace title + desc:

```
Find:    icon: '🎲', title: 'Connetti BGG',
Replace: icon: '🎲', title: 'Importa da catalogo',
```

```
Find:    desc: 'Sincronizza la tua collezione BoardGameGeek e importa i manuali in un click.',
Replace: desc: 'Importa i manuali dal catalogo condiviso interno in un click.',
```

- [ ] **Step 4: Verify cleanup**

```bash
grep -in "BGG\|BoardGameGeek\|boardgamegeek" admin-mockups/design_files/sp4-kb-globale.jsx 2>/dev/null
```

Expected: 0 lines (or only inside code comments documenting context).

- [ ] **Step 5: Commit Stage 0**

```bash
git add admin-mockups/design_files/sp4-kb-globale.jsx
git commit -m "$(cat <<'EOF'
chore(mockups): #2220 DS-17-13 BGG removal sp4-kb-globale

DEC-inherited-1 + DEC-Pilot-7: BGG cleanup Stage 0 prep work pre-AI dispatch.

1 mockup file edited:
- sp4-kb-globale.jsx lines 2549-2550: "Connetti BGG" + "BoardGameGeek" import card → "Importa da catalogo" + "catalogo condiviso interno"

Post-cleanup: AI dispatch Stage 1b kb-globale route-create reads BGG-free state.

Refs: #2220, #2151 BGG ToS umbrella.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 6: Extend #2151**

```bash
gh issue comment 2151 --body "$(cat <<'EOF'
**DS-17-13 sub-issue #2220 sess.46p — 2 nuovi findings sp4-content** (Phase B audit miss):

| Mockup | Line | Severity | Description |
|---|---|---|---|
| sp4-kb-globale.jsx | 2549 | HIGH | "Connetti BGG" import card title user-facing |
| sp4-kb-globale.jsx | 2550 | HIGH | "Sincronizza la tua collezione BoardGameGeek" card description user-facing |

Cleanup atomic commit landed in feature/issue-2220-ds-17-13-sp4-content branch. Pattern: DEC-Pilot-7 identico a DS-17-11 + DS-17-10 + DS-17-12.

Total sp4-content audit findings: only sp4-kb-globale contained BGG references (other 17 stems clean per pre-flight grep).
EOF
)"
```

---

## Stage 1a — sp4-kb-detail forward-refactor (~2h)

### Task 1.1: Agent dispatch for sp4-kb-detail scaffold + tracking issue

**Pattern reference**: sp4-library-mobile (DS-17-12, PR #2218) + sp3-library-public (DS-17-10, PR #2211).

- [ ] **Step 1: Pre-flight check kb-detail route**

```bash
ls apps/web/src/app/\(authenticated\)/knowledge-base/\[id\]/ 2>/dev/null
```

If MISSING: escalate (route-create out of scope, need additional sub-issue).
If EXISTS: proceed.

- [ ] **Step 2: Dispatch implementer subagent**

Use general-purpose Sonnet. Full task:

```
You are implementing sp4-kb-detail forward-refactor scaffold for DS-17-13 #2220.

Context:
- design_intent: forward-refactor (designer review pending, G4 v3 pivot deferred per MOCKUPS_INDEX comment)
- Mockup: admin-mockups/design_files/sp4-kb-detail.{html,jsx,fidelity.json}
- Existing route: apps/web/src/app/(authenticated)/knowledge-base/[id]/page.tsx
- Pattern reference: sp4-library-mobile (DS-17-12) + sp3-library-public (DS-17-10)

Steps:
1. Read mockup admin-mockups/design_files/sp4-kb-detail.jsx (first 50 lines)
2. Read existing route apps/web/src/app/(authenticated)/knowledge-base/[id]/page.tsx
3. Scaffold story at apps/web/src/app/(authenticated)/knowledge-base/[id]/page.stories.tsx with @storybook/react import + nextjs.navigation.pathname fixture UUID + Default story rendering existing page component
4. Open designer review tracking issue:
   gh issue create --title "Designer review sp4-kb-detail forward-refactor (DS-17-13 #2220 follow-up)" \
     --label "area/frontend,mockup-drift" \
     --body "<body explaining forward-refactor + G4 v3 pivot pending + DS-17-13 ref>"
5. Update sp4-kb-detail.fidelity.json:
   - story_path = "apps/web/src/app/(authenticated)/knowledge-base/[id]/page.stories.tsx"
   - obsolete_tracking_issue = "#<NEW_TRACKING_NUM>" (REQUIRED # prefix per fidelity validator)
6. Commit feat(stories): #2220 sp4-kb-detail forward-refactor

Constraints:
- @storybook/react import (NOT @storybook/nextjs)
- NO hand-written @mockup JSDoc
- Verify typecheck + lint:fidelity pass
- ENV: Windows PowerShell or Git Bash compatible

Working directory: D:\Repositories\meepleai-monorepo-frontend
Branch: feature/issue-2220-ds-17-13-sp4-content

Report: DONE / DONE_WITH_CONCERNS / BLOCKED with commit SHA + tracking issue # + files changed.
```

- [ ] **Step 3: Verify Agent output**

```bash
git log --oneline -3
ls apps/web/src/app/\(authenticated\)/knowledge-base/\[id\]/page.stories.tsx
jq '.acceptance.obsolete_tracking_issue, .acceptance.story_path' admin-mockups/design_files/sp4-kb-detail.fidelity.json
```

Expected: tracking issue # like `#22XX`, story_path populated, commit on branch.

---

## Stage 1b — sp4-kb-globale route-create (~2h)

### Task 1.2: Agent dispatch for sp4-kb-globale NEW route + KbGlobaleHome

**Pattern reference**: sp3-library-public route-create (DS-17-10, PR #2211).

- [ ] **Step 1: Dispatch implementer subagent**

```
You are implementing sp4-kb-globale NEW route + component for DS-17-13 #2220.

Context:
- MISSING route: apps/web/src/app/(authenticated)/knowledge-base/globale/
- Mockup: admin-mockups/design_files/sp4-kb-globale.{html,jsx,fidelity.json} (large file, ~2500+ LOC)
- design_intent: current (POST-Stage 0 BGG cleanup)
- Pattern reference: sp3-library-public route-create DS-17-10 PR #2211 (apps/web/src/app/(public)/library-public/)

Steps:
1. Read first 80 lines of mockup + identify top-level page component name + sections
2. Identify reuse primitives via grep:
   - grep -rln "HeroGradient" apps/web/src/components --include="*.tsx" | head -3
   - grep -rln "MeepleCard" apps/web/src/components --include="*.tsx" | head -3
3. Create page.tsx server wrapper at apps/web/src/app/(authenticated)/knowledge-base/globale/page.tsx with mock data fixtures inline (Stage 1 simplification)
4. Create KbGlobaleHome client component at apps/web/src/components/features/knowledge-base/KbGlobaleHome.tsx (or alternative path matching existing patterns)
5. Create Storybook story at apps/web/src/app/(authenticated)/knowledge-base/globale/page.stories.tsx
6. Add basic smoke test (~3 it() blocks resilient assertions)
7. Update sp4-kb-globale.fidelity.json story_path
8. Update admin-mockups/MOCKUPS_INDEX.md with sp4-kb-globale → /knowledge-base/globale mapping (under SP4 — Authenticated core section)
9. Commit feat(knowledge-base): #2220 sp4-kb-globale route + KbGlobaleHome

Constraints:
- @storybook/react import
- NO hand-written @mockup JSDoc
- Use semantic Tailwind tokens (bg-card, border-border/50, text-foreground, bg-entity-*/12)
- Use HeroGradient primitive if API matches (apps/web/src/components/ui/hero-gradient/hero-gradient.tsx)
- A11y compliance (text-entity-toolkit-text for AA contrast on bg-entity-toolkit/12)
- NO BGG references (Stage 0 cleanup done in mockup, codebase BGG-free)
- Mock data fixtures inline (no real backend wire)
- Verify pnpm typecheck + lint:fidelity + lint:bgg pass

Working directory: D:\Repositories\meepleai-monorepo-frontend

Report: DONE / DONE_WITH_CONCERNS / BLOCKED with commit SHA + files created + paths verified.
```

- [ ] **Step 2: Verify Agent output**

```bash
git log --oneline -3
ls apps/web/src/app/\(authenticated\)/knowledge-base/globale/page.tsx \
   apps/web/src/app/\(authenticated\)/knowledge-base/globale/page.stories.tsx
find apps/web/src/components -name "KbGlobaleHome.tsx" 2>/dev/null
grep "sp4-kb-globale" admin-mockups/MOCKUPS_INDEX.md
```

Expected: NEW route + KbGlobaleHome component + story + MOCKUPS_INDEX mapping.

---

## Stage 2 — sp4-toolkit-detail multi-route + POST-#2096 (~1h)

### Task 2.1: Agent dispatch for sp4-toolkit-detail canonical multi-route

**Pattern reference**: P254 multi-route canonical (DS-17-12 sp3-legal precedent).

- [ ] **Step 1: Dispatch implementer subagent**

```
You are implementing sp4-toolkit-detail story for DS-17-13 #2220.

Context:
- Multi-route mockup per MOCKUPS_INDEX line 150: `/toolkit` + sub-routes, `/library/[gameId]/toolbox`, `/library/[gameId]/toolkit`, `/library/private/[id]/toolkit/configure`
- Canonical story target = `/toolkit/page.tsx` (most representative)
- POST-#2096 consideration: `/library/[gameId]/toolbox` was shipped via PR #2207 with GameToolboxTab 1-Card placeholder (M4 milestone). sp4-toolkit-detail mockup intent vs GameToolboxTab implementation needs reconciliation NOTE in story docblock — story renders /toolkit/page.tsx component (NOT GameToolboxTab on /library route).
- Mockup: admin-mockups/design_files/sp4-toolkit-detail.{html,jsx,fidelity.json}
- Existing route: apps/web/src/app/(authenticated)/toolkit/page.tsx

Steps:
1. Read mockup head (first 50 lines) to identify component structure
2. Read existing route apps/web/src/app/(authenticated)/toolkit/page.tsx
3. Scaffold story at apps/web/src/app/(authenticated)/toolkit/page.stories.tsx with:
   - @storybook/react import
   - title: 'Authenticated / sp4-toolkit-detail'
   - Component reference: existing toolkit page
   - JSDoc explaining multi-route canonical + POST-#2096 reconciliation note
4. Update sp4-toolkit-detail.fidelity.json story_path
5. Commit feat(stories): #2220 sp4-toolkit-detail multi-route canonical

Constraints:
- @storybook/react import
- NO hand-written @mockup JSDoc
- Multi-route note: P254 canonical + 3 alternate routes documented in docblock
- POST-#2096 note: differentiate sp4-toolkit-detail mockup vs M4 GameToolboxTab implementation
- Verify pnpm typecheck pass

Working directory: D:\Repositories\meepleai-monorepo-frontend

Report: DONE / DONE_WITH_CONCERNS / BLOCKED with commit SHA + files created.
```

- [ ] **Step 2: Verify Agent output**

```bash
git log --oneline -3
ls apps/web/src/app/\(authenticated\)/toolkit/page.stories.tsx
```

---

## Stage 3 — 15 standard stems inline batch (~5-6h)

### Task 3.1: Scaffold 15 stories (P251 inline batch)

**Files (create 15 story files):**
- See File Structure section.

- [ ] **Step 1: Verify all 15 target routes exist**

```bash
for path in \
  "apps/web/src/app/(authenticated)/knowledge-base/page.tsx" \
  "apps/web/src/app/(authenticated)/editor/page.tsx" \
  "apps/web/src/app/(authenticated)/editor/agent-proposals/page.tsx" \
  "apps/web/src/app/(authenticated)/editor/agent-proposals/create/page.tsx" \
  "apps/web/src/app/(authenticated)/editor/agent-proposals/[id]/edit/page.tsx" \
  "apps/web/src/app/(authenticated)/editor/agent-proposals/[id]/test/page.tsx" \
  "apps/web/src/app/(authenticated)/toolkit/history/page.tsx" \
  "apps/web/src/app/(authenticated)/toolkit/play/page.tsx" \
  "apps/web/src/app/(authenticated)/toolkit/stats/page.tsx" \
  "apps/web/src/app/(authenticated)/toolkit/templates/page.tsx" \
  "apps/web/src/app/(authenticated)/play-records/page.tsx" \
  "apps/web/src/app/(authenticated)/play-records/[id]/page.tsx" \
  "apps/web/src/app/(authenticated)/play-records/[id]/edit/page.tsx" \
  "apps/web/src/app/(authenticated)/play-records/new/page.tsx" \
  "apps/web/src/app/(authenticated)/play-records/stats/page.tsx"; do
  if [ -f "$path" ]; then
    echo "✓ $path"
  else
    echo "✗ MISSING $path"
  fi
done
```

Expected: 15 ✓. If MISSING: investigate per stem — may need alternative path or skip if route truly doesn't exist.

- [ ] **Step 2: Create 15 story files using common pattern**

For each stem, create the corresponding story file using this template (substitute `<STEM>`, `<RouteComponentName>`, `<RoutePathname>`):

```tsx
/**
 * sp4-<STEM> — DS-17-13 #2220 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-<STEM>.{html,jsx}`.
 */

import type { Meta, StoryObj } from '@storybook/react';

import <RouteComponentName>Page from './page';

const meta: Meta<typeof <RouteComponentName>Page> = {
  title: 'Authenticated / sp4-<STEM>',
  component: <RouteComponentName>Page,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      // For dynamic routes, add navigation.pathname:
      // navigation: { pathname: '/.../fixture-id' },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component: '#2220 DS-17-13. <description>',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof <RouteComponentName>Page>;

export const Default: Story = {};
```

**Per-stem instantiation map**:

1. **sp4-kb-hub** → `knowledge-base/page.stories.tsx`, `KbHubPage`, description: "Knowledge base hub catalog index"
2. **sp4-editor-index** → `editor/page.stories.tsx`, `EditorIndexPage`, description: "Editor index landing page"
3. **sp4-editor-proposals-index** → `editor/agent-proposals/page.stories.tsx`, `AgentProposalsIndexPage`, description: "Agent proposals index"
4. **sp4-editor-proposals-create** → `editor/agent-proposals/create/page.stories.tsx`, `AgentProposalsCreatePage`, description: "Agent proposal create wizard"
5. **sp4-editor-proposals-edit** → `editor/agent-proposals/[id]/edit/page.stories.tsx`, `AgentProposalsEditPage`, description: "Agent proposal edit", dynamic route `pathname: '/editor/agent-proposals/sp4-fixture-id/edit'`
6. **sp4-editor-proposals-test** → `editor/agent-proposals/[id]/test/page.stories.tsx`, `AgentProposalsTestPage`, description: "Agent proposal test runner", dynamic route `pathname: '/editor/agent-proposals/sp4-fixture-id/test'`
7. **sp4-toolkit-history** → `toolkit/history/page.stories.tsx`, `ToolkitHistoryPage`, description: "Toolkit session history"
8. **sp4-toolkit-play** → `toolkit/play/page.stories.tsx`, `ToolkitPlayPage`, description: "Toolkit play view"
9. **sp4-toolkit-stats** → `toolkit/stats/page.stories.tsx`, `ToolkitStatsPage`, description: "Toolkit statistics"
10. **sp4-toolkit-templates** → `toolkit/templates/page.stories.tsx`, `ToolkitTemplatesPage`, description: "Toolkit templates library"
11. **sp4-play-records-index** → `play-records/page.stories.tsx`, `PlayRecordsIndexPage`, description: "Play records list"
12. **sp4-play-records-detail** → `play-records/[id]/page.stories.tsx`, `PlayRecordDetailPage`, description: "Play record detail", dynamic route `pathname: '/play-records/sp4-fixture-id'`
13. **sp4-play-records-edit** → `play-records/[id]/edit/page.stories.tsx`, `PlayRecordEditPage`, description: "Play record edit", dynamic route `pathname: '/play-records/sp4-fixture-id/edit'`
14. **sp4-play-records-new** → `play-records/new/page.stories.tsx`, `PlayRecordNewPage`, description: "Play record new form"
15. **sp4-play-records-stats** → `play-records/stats/page.stories.tsx`, `PlayRecordsStatsPage`, description: "Play records statistics"

Note: `RouteComponentName` is the default export name from each `page.tsx`. Read the actual export to confirm (most Next.js pages use generic `export default function Page() {}` or named exports like `LibraryPage`). The story file references the component via default import `import <Name>Page from './page'`.

- [ ] **Step 3: Verify typecheck**

```bash
pnpm --filter @meepleai/web typecheck 2>&1 | tail -5
```

Expected: 0 errors. If failures, identify which story import is broken — likely default export name mismatch.

- [ ] **Step 4: Commit Stage 3 batch**

```bash
git add apps/web/src/app/\(authenticated\)/knowledge-base/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/editor/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/editor/agent-proposals/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/editor/agent-proposals/create/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/editor/agent-proposals/\[id\]/edit/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/editor/agent-proposals/\[id\]/test/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/toolkit/history/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/toolkit/play/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/toolkit/stats/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/toolkit/templates/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/play-records/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/play-records/\[id\]/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/play-records/\[id\]/edit/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/play-records/new/page.stories.tsx \
        apps/web/src/app/\(authenticated\)/play-records/stats/page.stories.tsx

git commit -m "$(cat <<'EOF'
feat(stories): #2220 DS-17-13 sp4-content 15 standard stems batch

Stage 3 P251 inline batch: 15 new page.stories.tsx files for sp4-content cluster (DS-17-12 successful pattern).

Knowledge base: sp4-kb-hub
Editor: sp4-editor-index + 4 agent-proposals (index/create/edit/test)
Toolkit: sp4-toolkit-history + play + stats + templates
Play records: sp4-play-records-index + detail + edit + new + stats

Pattern: @storybook/react import. NO hand-written @mockup JSDoc (injector Stage 4). Default story renders existing page component. nextjs.navigation.pathname fixture for dynamic routes.

DEC-5: no new tests added.

Refs: #2220, spec section 4.5.

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

This injects `@mockup` JSDoc markers into page.tsx files for new routes (sp4-kb-globale + any new stories).

- [ ] **Step 2: Run quality gates**

```bash
pnpm --filter @meepleai/web typecheck
pnpm --filter @meepleai/web lint
pnpm --filter @meepleai/web lint:tokens
pnpm --filter @meepleai/web lint:bgg
pnpm --filter @meepleai/web lint:fidelity
pnpm --filter @meepleai/web mockup-annotations:audit --denominator mappable --threshold 80
pnpm --filter @meepleai/web test --run
```

Expected: 0 errors / 0 violations / ≥80% mappable / 0 test regression.

- [ ] **Step 3: Address gate failures**

Common fixes (DS-17-12 lessons):
- `lint` warning `<a>` instead of `<Link>` → replace with `Link from 'next/link'`
- `lint:fidelity` format → `obsolete_tracking_issue` MUST have `#` prefix
- `lint:tokens` → use semantic tokens (bg-card / border-border/50 / etc)
- `lint:bgg` violations → re-verify Stage 0 cleanup completeness

If fixes needed:
```bash
git commit -m "fix(scope): #2220 Stage 4 quality gate fixes"
```

- [ ] **Step 4: Re-run gates until all pass**

---

## Stage 5 — Merge + closure (~30 min)

### Task 5.1: Push branch

- [ ] **Step 1: Verify commit chain**

```bash
git log --oneline main-dev..HEAD
```

Expected commits:
- spec doc
- plan doc
- Stage 0 BGG cleanup
- Stage 1a sp4-kb-detail (Agent commit)
- Stage 1b sp4-kb-globale (Agent commit, possibly multiple)
- Stage 2 sp4-toolkit-detail (Agent commit)
- Stage 3 inline batch 15 stems
- (Optional) Stage 4 fix commit

- [ ] **Step 2: Push (pre-push hook ~10 min)**

```bash
git push -u origin feature/issue-2220-ds-17-13-sp4-content
```

Wait for hook completion. If `.next/static/*` ENOENT: `rm -rf apps/web/.next && retry`.

### Task 5.2: Open PR

- [ ] **Step 1: gh pr create**

```bash
gh pr create --base main-dev \
  --title "feat(stories): #2220 DS-17-13 sp4-core-content — 18 stems" \
  --body "$(cat <<'EOF'
## Summary

Closes DS-17 Phase C-2 step 2/4 (post DS-17-12 sp4-catalog #2218 sess.46p). Unblocks DS-17-14 sp4-core-admin brainstorm.

18 sp4-content stems shipped per DEC distribution:
- **1 FORWARD-REFACTOR**: sp4-kb-detail (Agent + tracking #<TRACKING_KB_NUM>)
- **1 ROUTE-CREATE**: sp4-kb-globale (Agent NEW /knowledge-base/globale + KbGlobaleHome)
- **1 MULTI-ROUTE**: sp4-toolkit-detail (Agent canonical P254 + POST-#2096 reconciliation)
- **15 STANDARD INLINE BATCH** (P251): kb-hub + 5 editor + 4 toolkit secondary + 5 play-records

## Stage 0 BGG cleanup

1 mockup file edited (sp4-kb-globale.jsx lines 2549-2550). #2151 extended with 2 new findings.

## Stage 3 baseline DEFERRED

P252 defer pattern: visual gate non-blocking per merge.

## ⏳ DESIGNER REVIEW SKIPPED

User waiver Opzione C. sp4-kb-detail + sp4-kb-globale forward-refactor → tracking issues OPENED.

## Spec + plan

- Spec: `docs/superpowers/specs/2026-06-12-ds-17-13-sp4-core-content-design.md`
- Plan: `docs/superpowers/plans/2026-06-12-ds-17-13-sp4-core-content-plan.md`
- 5 DEC user-locked (3 new + 2 inherited)

## Test plan

- [x] pnpm typecheck → 0 errors
- [x] pnpm lint → 0 errors
- [x] pnpm lint:tokens → 0 violations
- [x] pnpm lint:bgg → clean
- [x] pnpm lint:fidelity → all PASS
- [x] pnpm mockup-annotations:audit ≥80%
- [x] Backend build clean (pre-push)
- [ ] Designer review SKIPPED

## Refs

- Closes #2220
- DS-17 Phase C-2 step 2/4 progress (#2063)
- Predecessor: PR #2218 (DS-17-12)
- BGG ToS: #2151
- Forward-refactor tracking: #<TRACKING_KB_NUM>

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Replace `<TRACKING_KB_NUM>` with actual tracking issue from Stage 1a.

### Task 5.3: Admin-squash merge + sub-issue close

- [ ] **Step 1: Merge P145 40a**

```bash
gh pr merge <PR_NUM> --admin --squash --delete-branch
```

- [ ] **Step 2: Pull main-dev**

```bash
git checkout main-dev
git checkout -- audits/ 2>/dev/null || true
git pull --ff-only
```

- [ ] **Step 3: Close #2220**

```bash
gh issue close 2220 --reason completed --comment "$(cat <<'EOF'
🎉 Shipped via PR #<PR_NUM> (`<merge_sha>` admin-squash P145 40a volta).

## AC closure evidence

### Stage 0 BGG cleanup ✅
- sp4-kb-globale.jsx lines 2549-2550 edited (Connetti BGG card removed)
- #2151 extended with 2 findings

### Stage 1a sp4-kb-detail forward-refactor ✅
- Story scaffolded
- Designer review tracking #<TRACKING_KB_NUM> OPENED
- fidelity.json updated

### Stage 1b sp4-kb-globale route-create ✅
- NEW route /knowledge-base/globale + KbGlobaleHome
- Storybook story + smoke test
- MOCKUPS_INDEX mapping added

### Stage 2 sp4-toolkit-detail multi-route ✅
- Canonical story /toolkit + P254 multi-route documented
- POST-#2096 GameToolboxTab reconciliation note

### Stage 3 15 inline batch ✅
- 15 stories scaffolded (P251 pattern)
- All @storybook/react import, NO hand-written @mockup JSDoc

### Stage 4 quality gates ✅
- typecheck 0 errors
- lint 0 errors / tokens 0 / bgg clean / fidelity all PASS
- mockup-annotations:audit ≥80% mappable

### Stage 5 closure ✅
- Admin-squash merge P145 40a
- Designer review SKIPPED per Opzione C

DS-17 Phase C-2 progress: 2/4 step shipped. NEXT: DS-17-14 sp4-core-admin brainstorm.
EOF
)"
```

- [ ] **Step 4: EPIC #2063 progress**

```bash
gh issue comment 2063 --body "$(cat <<'EOF'
🎉 **DS-17 Phase C-2 step 2/4 SHIPPED via PR #<PR_NUM>** (sub-issue #2220 sess.46p 2026-06-12).

## Phase C-2 progress

| Step | Sub-issue | PR | Status |
|---|---|---|---|
| 1 — sp4-core-catalog | #2214 | #2218 | ✅ MERGED |
| 2 — sp4-core-content | #2220 | #<PR_NUM> | ✅ MERGED |
| 3 — sp4-core-admin | TBD | TBD | 🚧 Future brainstorm |
| 4 — sp4-sessions skeleton | TBD | TBD | 🚧 Future brainstorm |

Phase C-2 50% complete (2/4). Cumulative ~25-27h effort.

## Carry-over

- sp4-kb-detail + sp4-kb-globale forward-refactor designer review
- Visual baseline DEFERRED (P252)
EOF
)"
```

### Task 5.4: Memory entry

- [ ] **Step 1: Write `ds-17-13-sp4-content-shipped.md`**

Create file at `C:\Users\Utente\.claude\projects\D--Repositories-meepleai-monorepo-frontend\memory\ds-17-13-sp4-content-shipped.md`:

```markdown
---
name: ds-17-13-sp4-content-shipped
description: "DS-17-13 sp4-core-content migration — 18 stems shipped sess.46p — Phase C-2 step 2/4 closure"
metadata:
  node_type: memory
  type: project
---

# DS-17-13 sp4-core-content shipped sess.46p

DS-17 Phase C-2 step 2/4 **CLOSED** sess.46p 2026-06-12 via PR #<PR_NUM> (`<merge_sha>` admin-squash P145 40a volta). Sub-issue #2220 cluster sp4-content (18 stems).

## Shipped scope

| Category | Stems | Notes |
|---|---|---|
| Knowledge base | 3 | kb-hub + kb-detail forward-refactor + kb-globale NEW route |
| Editor | 5 | index + 4 agent-proposals (index/create/edit/test) |
| Toolkit | 5 | detail multi-route + history + play + stats + templates |
| Play records | 5 | index + detail + edit + new + stats |

## Effort recap

~14-16h cumulative.

## Patterns confirmed

P145 admin-squash 40a, P251 hybrid (Agent x3 + inline batch x15), P252 baseline defer, P254 multi-route canonical, P255 plan-review pre-execution, P256 multi-mockup different routes.

## NEW patterns

(Fill in based on execution findings.)

## Links

- Sub-issue: #2220 closed
- PR: #<PR_NUM>
- Spec: docs/superpowers/specs/2026-06-12-ds-17-13-sp4-core-content-design.md
- Plan: docs/superpowers/plans/2026-06-12-ds-17-13-sp4-core-content-plan.md
- DS-17 umbrella: #2063
- Predecessor: [[ds-17-12-sp4-catalog-shipped]]
```

- [ ] **Step 2: Update MEMORY.md index**

Add new entry at top of MEMORY.md (after the most recent DS-17-12 entry):

```markdown
- [DS-17-13 sp4-content shipped](ds-17-13-sp4-content-shipped.md) — DS-17 Phase C-2 step 2/4 **CLOSED** sess.46p 2026-06-12 via PR #<PR_NUM> (admin-squash P145 40a). Sub-issue #2220 cluster sp4-content (18 stems: 3 kb + 5 editor + 5 toolkit + 5 play-records). Hybrid Agent x3 + inline batch x15. 5 DEC totali. Effort ~14-16h. Phase C-2 50% complete (2/4). NEXT DS-17-14 sp4-core-admin brainstorm.
```

- [ ] **Step 3: Notify completion**

Final message: "DS-17-13 sp4-content CLOSED. DS-17 Phase C-2 2/4 step shipped. DS-17-14 sp4-core-admin brainstorm next."

---

## Self-review checklist

- [ ] Stage 0 covers sp4-kb-globale.jsx BGG cleanup (only file with refs per pre-flight grep)
- [ ] Stage 1a Agent dispatch context complete (sp4-kb-detail)
- [ ] Stage 1b Agent dispatch context complete (sp4-kb-globale NEW route)
- [ ] Stage 2 Agent dispatch context complete (sp4-toolkit-detail multi-route + POST-#2096)
- [ ] Stage 3 covers 15 standard stems with per-stem instantiation map
- [ ] Stage 4 covers all quality gates from spec § 10
- [ ] Stage 5 covers merge + closure + memory + EPIC progress
- [ ] All 5 DEC respected
- [ ] No TBD/TODO placeholders (runtime values <PR_NUM> / <TRACKING_KB_NUM> / <merge_sha> documented)
- [ ] All code samples use @storybook/react (DS-17-12 lesson P255)
- [ ] No hand-written @mockup JSDoc (DS-17-12 lesson P255)
- [ ] obsolete_tracking_issue # prefix mandatory (DS-17-12 lesson)
- [ ] Pre-flight P124 already done
- [ ] Branch hygiene #806 respected

---

**End of implementation plan.**
