# DS-17-13 sp4-core-content cluster — Design

**Status**: design approved 2026-06-12 sess.46p brainstorming
**Owner**: badsworm@gmail.com
**Sub-issue**: [#2220](https://github.com/meepleAi-app/meepleai-monorepo/issues/2220)
**Parent umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) DS-17 Mockup-to-App Fidelity
**Phase C-2 META spec**: [`2026-06-11-ds-17-phase-c-2-sp4-split-and-ds-17-12-design.md`](2026-06-11-ds-17-phase-c-2-sp4-split-and-ds-17-12-design.md)
**Predecessor**: DS-17-12 sp4-catalog #2214 PR #2218 `18440815d` sess.46p

## 1. Context

DS-17 Phase C-2 step 2/4 — sp4-core-content cluster (kb + editor + toolkit + play-records). 18 stems discovered (vs ~13 META estimate). Effort ~14-16h.

Inherited 6 DEC from Phase C-2 META spec + 3 new sess.46p.

## 2. Decisioni (5 DEC totali: 3 new + 2 inherited)

| # | Decisione | Source |
|---|---|---|
| DEC-1 | Ship all 18 stems combined (single sub-issue) | sess.46p |
| DEC-2 | sp4-kb-globale route-create + ship `/knowledge-base/globale` (sp3-library-public precedent) | sess.46p |
| DEC-3 | Hybrid P251 dispatch (Agent x3 + inline batch x15) | sess.46p |
| DEC-inherited-1 | BGG cleanup Stage 0 prep (conditional su grep audit) | Phase C-2 META |
| DEC-inherited-2 | Forward-refactor → ship + tracking issue + fidelity update | Phase C-2 META + sp3-library-public + sp4-library-mobile precedents |

## 3. Scope (18 stems)

| # | Stem | Route | design_intent | Action | Effort |
|---|---|---|---|---|---|
| 1 | sp4-kb-hub | `(authenticated)/knowledge-base/` | current | Ship (inline) | ~30 min |
| 2 | sp4-kb-detail | `(authenticated)/knowledge-base/[id]/` | **forward-refactor** | **Agent + tracking issue** | ~2h |
| 3 | **sp4-kb-globale** | `(authenticated)/knowledge-base/global/` ✓ EXISTS | current | **Agent story scaffold + MOCKUPS_INDEX mapping** (NO route-create) | ~30 min |
| 4 | sp4-editor-index | `(authenticated)/editor/` | current | Ship (inline) | ~30 min |
| 5 | sp4-editor-proposals-create | `(authenticated)/editor/agent-proposals/create/` | current | Ship (inline) | ~30 min |
| 6 | sp4-editor-proposals-edit | `(authenticated)/editor/agent-proposals/[id]/edit/` | current | Ship (inline) | ~30 min |
| 7 | sp4-editor-proposals-index | `(authenticated)/editor/agent-proposals/` | current | Ship (inline) | ~30 min |
| 8 | sp4-editor-proposals-test | `(authenticated)/editor/agent-proposals/[id]/test/` | current | Ship (inline) | ~30 min |
| 9 | **sp4-toolkit-detail** | `(authenticated)/toolkit/` (canonical P254 multi-route, hub primary) | current | **Agent multi-route + POST-#2096 GameToolboxTab integration verify** | ~1h |
| 10 | sp4-toolkit-history | `(authenticated)/toolkit/history/` | current | Ship (inline) | ~30 min |
| 11 | sp4-toolkit-play | `(authenticated)/toolkit/[sessionId]/` | current | Ship (inline) | ~30 min |
| 12 | sp4-toolkit-stats | `(authenticated)/toolkit/stats/` | current | Ship (inline) | ~30 min |
| 13 | sp4-toolkit-templates | `(authenticated)/toolkit/templates/` | current | Ship (inline) | ~30 min |
| 14 | sp4-play-records-detail | `(authenticated)/play-records/[id]/` | current | Ship (inline) | ~30 min |
| 15 | sp4-play-records-edit | `(authenticated)/play-records/[id]/edit/` | current | Ship (inline) | ~30 min |
| 16 | sp4-play-records-index | `(authenticated)/play-records/` | current | Ship (inline) | ~30 min |
| 17 | sp4-play-records-new | `(authenticated)/play-records/new/` | current | Ship (inline) | ~30 min |
| 18 | sp4-play-records-stats | `(authenticated)/play-records/stats/` | current | Ship (inline) | ~30 min |

**Distribution**: 3 Agent (sp4-kb-detail forward-refactor + sp4-kb-globale route-create + sp4-toolkit-detail multi-route) + 15 inline batch standard = 18 stems.

## 4. Architecture

### 4.1 Stage 0 BGG audit (conditional)

```bash
grep -in "BGG\|BoardGameGeek\|boardgamegeek" \
  admin-mockups/design_files/sp4-{kb,editor,toolkit,play-records}-*.jsx 2>/dev/null
```

If findings → atomic Stage 0 commit + #2151 extend. If clean → skip Stage 0.

### 4.2 Stage 1a sp4-kb-detail forward-refactor (Agent)

Pattern: sp4-library-mobile (DS-17-12) + sp3-library-public (DS-17-10) precedent.
- Agent reads mockup + scaffolds story
- Open designer review tracking issue
- Update fidelity.json `obsolete_tracking_issue: "#<TRACKING_NUM>"` (REQUIRED # prefix)

### 4.3 Stage 1b sp4-kb-globale route-create (Agent)

Pattern: sp3-library-public route-create precedent (DS-17-10 PR #2211).
- Create `apps/web/src/app/(authenticated)/knowledge-base/globale/page.tsx` server wrapper
- Create `apps/web/src/components/features/knowledge-base/KbGlobaleHome.tsx` client component (or alternative path per existing patterns)
- Create Storybook story
- Update MOCKUPS_INDEX.md with new mapping

### 4.4 Stage 2 sp4-toolkit-detail Agent (multi-route + POST-#2096)

Multi-route per MOCKUPS_INDEX: `/toolkit` + sub-routes + `/library/[gameId]/toolbox` + `/library/[gameId]/toolkit` + `/library/private/[id]/toolkit/configure`. Canonical story target = `/toolkit/[id]/` (most specific).

POST-#2096 consideration: `/library/[gameId]/toolbox` was shipped via PR #2207 with GameToolboxTab 1-Card placeholder (M4 milestone). sp4-toolkit-detail mockup intent vs current Toolbox implementation needs reconciliation. Agent dispatches with note: story renders existing `/toolkit/[id]/page.tsx` component (NOT GameToolboxTab on /library route).

### 4.5 Stage 3 15 standard stems inline batch (P251)

Scaffold-template-similar stems batch inline:
- 1 sp4-kb-hub
- 4 sp4-editor-* (index + 3 agent-proposals minus 1 covered by Agent)
- 4 sp4-toolkit-{history,play,stats,templates}
- 5 sp4-play-records-* (detail + edit + index + new + stats)
- 1 sp4-editor-proposals-test

Count check: 1 + 4 + 4 + 5 + 1 = 15. ✓

Common pattern (DS-17-12 lesson):
```tsx
import type { Meta, StoryObj } from '@storybook/react';
import <Page>Page from './page';

const meta: Meta<typeof <Page>Page> = {
  title: 'Authenticated / sp4-<stem>',
  component: <Page>Page,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
  },
};
export default meta;
export const Default: Story = {};
```

For dynamic routes (e.g. `/[id]/`): add `nextjs.navigation.pathname` parameter with fixture UUID.

### 4.6 Component-mock path discovery

No standalone component-mocks in DS-17-13 scope (per MOCKUPS_INDEX). All stems are page-mocks at routes.

## 5. Sequencing

```
Pre-flight (P124):
✅ 1. main-dev pull (done)
✅ 2. gh issue list search → no duplicate (done)
✅ 3. gh issue create #2220 (done)
✅ 4. git checkout -b feature/issue-2220-ds-17-13-sp4-content (done)

Stage 0 BGG audit (~15 min, conditional):
5. grep BGG references in 18 sp4-content stems
6. If found: atomic commit + #2151 extend
7. If clean: skip Stage 0

Stage 1a sp4-kb-detail forward-refactor (~2h):
8. Agent dispatch — scaffold story + tracking issue + fidelity update

Stage 1b sp4-kb-globale route-create (~2h):
9. Agent dispatch — create route + KbGlobaleHome + story + MOCKUPS_INDEX update

Stage 2 sp4-toolkit-detail (~1h):
10. Agent dispatch — multi-route canonical + POST-#2096 reconciliation note

Stage 3 15 standard stems inline batch (~8h):
11. Inline batch scaffold all 15 standard stories
12. Verify typecheck
13. Commit feat(stories): #2220 DS-17-13 sp4-content 15 standard stems

Stage 4 quality gates (~30 min):
14. test + lint + tokens + bgg + fidelity + annotations + typecheck

Stage 5 merge + closure (~30 min):
15. git push -u origin feature/issue-2220-ds-17-13-sp4-content
16. gh pr create --base main-dev
17. Designer review SKIP per Opzione C precedent
18. gh pr merge --admin --squash --delete-branch (P145 40a)
19. gh issue close #2220 + EPIC #2063 Phase C-2 step 2/4 progress
20. Memory entry ds-17-13-sp4-content-shipped.md
```

## 6. Effort recap

| Stage | Effort |
|---|---|
| Pre-flight + sub-issue + branch | ✅ done (~15 min) |
| Stage 0 BGG audit (conditional) | ~15 min |
| Stage 1a sp4-kb-detail forward-refactor | ~2h |
| Stage 1b sp4-kb-globale route-create | ~2h |
| Stage 2 sp4-toolkit-detail Agent | ~1h |
| Stage 3 15 inline batch | ~8h |
| Stage 4 quality gates | ~30 min |
| Stage 5 merge + closure + memory | ~30 min |
| **Total active work** | **~14-16h** (single sub-issue, P145 40a) |

## 7. Risk register

| # | Risk | Mitigation |
|---|---|---|
| R1 | sp4-kb-detail forward-refactor designer rejection | Tracking issue + PR body annotation (sp4-library-mobile precedent) |
| R2 | sp4-kb-globale forward-refactor risk (NEW route) | Agent dispatch + scaffold conservative + tracking issue per future iteration |
| R3 | sp4-toolkit-detail multi-route + POST-#2096 reconciliation | Agent dispatch with explicit POST-#2096 GameToolboxTab note |
| R4 | 18 stems batch context overflow | DEC-3 hybrid P251 mitigates (Agent x3 + inline batch x15 isolated) |
| R5 | Routes verification: editor sub-routes existence | Pre-execution check `apps/web/src/app/(authenticated)/editor/agent-proposals/*/page.tsx` |
| R6 | Effort overrun >16h | Defer 4 toolkit secondary stems to follow-up if needed |

## 8. Out of scope

- ❌ DS-17-14 sp4-core-admin (future Phase C-2 step 3/4)
- ❌ DS-17-15 sp4-sessions skeleton-first (future Phase C-2 step 4/4)
- ❌ Per-game session stories (Phase C-3 follow-up)
- ❌ Visual baseline capture (P252 defer)
- ❌ Sub-route sub-mockup expansion (sp4-toolkit-detail P254 canonical only)
- ❌ Backend changes (pure FE work)
- ❌ EPIC #2096 deliverables re-implementation (POST-#2096 wire only)

## 9. References

| Type | Path / Link |
|---|---|
| Sub-issue | [#2220](https://github.com/meepleAi-app/meepleai-monorepo/issues/2220) |
| Parent umbrella | [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) |
| Phase C-2 META spec | `docs/superpowers/specs/2026-06-11-ds-17-phase-c-2-sp4-split-and-ds-17-12-design.md` |
| Predecessor | DS-17-12 #2214 PR #2218 `18440815d` |
| BGG ToS | #2151 |
| Memory: predecessor patterns | `ds-17-12-sp4-catalog-shipped.md` (P251-P256) |
| EPIC #2096 trigger | PR #2207 `b98e4328b` (sp4-toolkit-detail Stage 2 relevance) |
| Mockup files | `admin-mockups/design_files/sp4-{kb,editor,toolkit,play-records}-*.{html,jsx,fidelity.json}` |

## 10. Acceptance criteria (mirrored in #2220 body)

### Stage 0 BGG audit
- [ ] grep results documented (clean OR cleanup commit)

### Stage 1a sp4-kb-detail
- [ ] Story scaffolded
- [ ] Designer review tracking issue OPENED
- [ ] fidelity.json updated with `obsolete_tracking_issue: "#<NUM>"`

### Stage 1b sp4-kb-globale
- [ ] NEW route `/knowledge-base/globale/page.tsx` created
- [ ] KbGlobaleHome component scaffolded
- [ ] Storybook story created
- [ ] MOCKUPS_INDEX.md mapping added

### Stage 2 sp4-toolkit-detail
- [ ] Story scaffolded canonical `/toolkit/[id]/`
- [ ] Multi-route documented (P254)
- [ ] POST-#2096 reconciliation note in story docblock

### Stage 3 15 standard stems
- [ ] 15 stories scaffolded inline
- [ ] All use @storybook/react import
- [ ] NO hand-written @mockup JSDoc

### Stage 4 quality gates
- [ ] pnpm test pass
- [ ] pnpm lint 0 errors
- [ ] pnpm lint:tokens 0 violations
- [ ] pnpm lint:bgg clean
- [ ] pnpm lint:fidelity all PASS
- [ ] pnpm typecheck 0 errors
- [ ] pnpm mockup-annotations:audit ≥80% mappable

### Stage 5 closure
- [ ] Admin-squash merge P145 40a volta
- [ ] Sub-issue #2220 closed
- [ ] EPIC #2063 Phase C-2 step 2/4 progress note
- [ ] Memory entry written

---

**End of design spec.**
