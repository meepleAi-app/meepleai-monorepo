# DS-17-14 sp4-core-admin cluster — Design + Plan

**Status**: design approved 2026-06-12 sess.46p brainstorming
**Owner**: badsworm@gmail.com
**Sub-issue**: [#2228](https://github.com/meepleAi-app/meepleai-monorepo/issues/2228)
**Parent umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)
**Phase C-2 META spec**: [`2026-06-11-ds-17-phase-c-2-sp4-split-and-ds-17-12-design.md`](2026-06-11-ds-17-phase-c-2-sp4-split-and-ds-17-12-design.md)
**Predecessor**: DS-17-13 sp4-content #2220 PR #2225 `7a53f848e` sess.46p

## 1. Context

DS-17 Phase C-2 step 3/4 — sp4-core-admin (smallest cluster: 4 SKIP + 4 ship). Combined spec+plan doc per ridotto scope. Effort ~2-3h.

## 2. DEC (3 inherited only — no new decisions)

| # | Decisione | Source |
|---|---|---|
| DEC-inherited-1 | BGG cleanup Stage 0 SKIP (0 BGG findings verified) | Phase C-2 META |
| DEC-inherited-2 | Forward-refactor → tracking N/A (no forward-refactor stems) | Phase C-2 META |
| DEC-Pilot-SKIP | Obsolete stems → SKIP + pre-existing tracking preserved | DS-17-12 sp4-add-game-bgg-step precedent |

## 3. Scope (8 stems)

| # | Stem | Route | design_intent | Action |
|---|---|---|---|---|
| 1 | sp4-dashboard | (skip) | forward-refactor-obsolete | SKIP (#2144 pre-existing) |
| 2 | sp4-hub-agents | (skip) | forward-refactor-obsolete | SKIP (#2143 pre-existing) |
| 3 | sp4-hub-games | (skip) | forward-refactor-obsolete | SKIP (#2142 pre-existing) |
| 4 | sp4-hub-toolkits | (skip) | forward-refactor-obsolete | SKIP (#2147 pre-existing) |
| 5 | sp4-players-index | `(authenticated)/players/` | current | Ship inline |
| 6 | sp4-player-detail | `(authenticated)/players/[id]/` | current | Ship inline |
| 7 | sp4-game-nights-index | `(authenticated)/game-nights/` | current | Ship inline |
| 8 | sp4-sessions-index | `(authenticated)/sessions/` | current | Ship inline |

## 4. Implementation (5 stages, ~2-3h)

### Stage 0: SKIP (0 BGG findings)

### Stage 1: N/A (no forward-refactor)

### Stage 2: Document 4 SKIPs in PR body. NO fidelity edits (pre-existing trackers).

### Stage 3: 4 inline batch stories

Pattern (DS-17-13 lesson, P251):
```tsx
import type { Meta, StoryObj } from '@storybook/react';
import <Page> from './page';

const meta: Meta<typeof <Page>> = {
  title: 'Authenticated / sp4-<STEM>',
  component: <Page>,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true, navigation: { pathname: '...' } },
    viewport: { defaultViewport: 'desktop' },
    docs: { description: { component: '#2228 DS-17-14. ...' } },
  },
};
export default meta;
type Story = StoryObj<typeof <Page>>;
export const Default: Story = {};
```

Files to create:
- `apps/web/src/app/(authenticated)/players/page.stories.tsx`
- `apps/web/src/app/(authenticated)/players/[id]/page.stories.tsx` (dynamic, pathname fixture)
- `apps/web/src/app/(authenticated)/game-nights/page.stories.tsx`
- `apps/web/src/app/(authenticated)/sessions/page.stories.tsx`

### Stage 4: Quality gates (typecheck + lint + tokens + bgg + fidelity + annotations)

### Stage 5: Merge P145 41a + EPIC #2063 Phase C-2 step 3/4 progress + memory

## 5. Acceptance criteria

- [ ] 4 stories created (players + player-detail + game-nights + sessions)
- [ ] PR body documents 4 SKIP stems with pre-existing trackers
- [ ] pnpm typecheck 0 errors
- [ ] pnpm lint 0 errors
- [ ] pnpm lint:fidelity all PASS
- [ ] pnpm mockup-annotations:audit ≥80% mappable
- [ ] Admin-squash merge P145 41a
- [ ] EPIC #2063 Phase C-2 step 3/4 progress note
- [ ] Memory entry written

## 6. References

- Sub-issue: #2228
- Predecessor: #2225 DS-17-13 (P251-P258 patterns)
- Phase C-2 META: parent spec
- Obsolete trackers (preserved): #2144 + #2143 + #2142 + #2147

---

**End of combined spec+plan.**
