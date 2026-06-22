# DS-17-15 sp4-sessions skeleton-first cluster — Design + Plan

**Status**: design approved 2026-06-12 sess.46p brainstorming
**Owner**: badsworm@gmail.com
**Sub-issue**: [#2231](https://github.com/meepleAi-app/meepleai-monorepo/issues/2231)
**Parent umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)
**Phase C-2 META spec**: [`2026-06-11-ds-17-phase-c-2-sp4-split-and-ds-17-12-design.md`](2026-06-11-ds-17-phase-c-2-sp4-split-and-ds-17-12-design.md)
**Predecessor**: DS-17-14 #2228 PR #2230 `46ade8cf3` sess.46p
**Phase C-2 milestone**: Merge → **Phase C-2 4/4 COMPLETE**

## 1. Context

DS-17 Phase C-2 step 4/4 FINAL. Skeleton-first scope per META DEC-3: 3 base ship + 7 per-game stub files. Per-game full implementation (MSW + flavor components) deferred Phase C-3.

Combined spec+plan doc per P259 (small cluster pattern, ≤3-4h effort).

## 2. DEC (1 new + 3 inherited)

| # | Decisione | Source |
|---|---|---|
| DEC-1 | 7 stub files (1 per game, 2 Stories: Live + Summary) | sess.46p new |
| DEC-inherited-META-3 | Skeleton-first scope (3 base + per-game lazy) | Phase C-2 META |
| DEC-inherited-BGG-0 | BGG Stage 0 SKIP (0 findings) | DS-17-12-14 precedent |
| DEC-inherited-Phase-C-3 | Per-game full MSW + flavor deferred Phase C-3 | Phase C-2 META |

## 3. Scope

### 3a. 3 base ship stems

| # | Stem | Route | Action |
|---|---|---|---|
| 1 | sp4-session-skeleton-live | `(authenticated)/sessions/live/[sessionId]/` | Ship full story |
| 2 | sp4-session-summary-skeleton | `(authenticated)/sessions/[id]/` | Ship full story |
| 3 | sp4-session-play | `(authenticated)/sessions/[id]/play/` | Ship full story |

### 3b. 7 per-game stub files

Path: `apps/web/src/app/(authenticated)/sessions/_sp4-stubs/<game>.stories.tsx`

| # | Game | Stories | Mockups covered |
|---|---|---|---|
| 4 | catan | Live + Summary | sp4-session-catan-{live,summary} |
| 5 | codenames | Live + Summary | sp4-session-codenames-{live,summary} |
| 6 | paleo | Live + Summary | sp4-session-paleo-{live,summary} |
| 7 | power-grid | Live + Summary | sp4-session-power-grid-{live,summary} |
| 8 | puerto-rico | Live + Summary | sp4-session-puerto-rico-{live,summary} |
| 9 | wingspan | Live + Summary | sp4-session-wingspan-{live,summary} |
| 10 | zombicide | Live + Summary | sp4-session-zombicide-{live,summary} |

## 4. Implementation

### Stage 0: BGG cleanup SKIP (0 findings verified pre-execution)

### Stage 1: Pre-flight routes verify

```bash
ls apps/web/src/app/(authenticated)/sessions/live/[sessionId]/page.tsx \
   apps/web/src/app/(authenticated)/sessions/[id]/page.tsx \
   apps/web/src/app/(authenticated)/sessions/[id]/play/page.tsx
```

If MISSING for any base stem, escalate or ship as stub pattern.

### Stage 2: 3 base ship stories inline (~30 min)

Standard P251 pattern (story imports existing page component).

### Stage 3: 7 stub files inline (~70 min)

Stub pattern (~30 LOC each):
```tsx
import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta = {
  title: 'Authenticated / sp4-session-<game>',
  parameters: {
    docs: {
      description: {
        component: '#2231 DS-17-15. <Game> session stub (Phase C-2 skeleton-first). Live + Summary variants. Full MSW + flavor components deferred Phase C-3.',
      },
    },
  },
};

export default meta;

type Story = StoryObj;

export const Live: Story = {
  render: () => (
    <div className="p-8 text-center text-muted-foreground">
      <h2 className="font-quicksand text-2xl">sp4-session-<game>-live (Stub)</h2>
      <p>Per-game flavor components deferred Phase C-3 follow-up.</p>
      <p className="text-sm">Mockup ref: admin-mockups/design_files/sp4-session-<game>-live.html</p>
    </div>
  ),
};

export const Summary: Story = {
  render: () => (
    <div className="p-8 text-center text-muted-foreground">
      <h2 className="font-quicksand text-2xl">sp4-session-<game>-summary (Stub)</h2>
      <p>Per-game flavor components deferred Phase C-3 follow-up.</p>
      <p className="text-sm">Mockup ref: admin-mockups/design_files/sp4-session-<game>-summary.html</p>
    </div>
  ),
};
```

### Stage 4: Quality gates

### Stage 5: Merge P145 42a + Phase C-2 4/4 COMPLETE milestone + memory

## 5. Effort recap

| Stage | Effort |
|---|---|
| Pre-flight + sub-issue + branch | ✅ done (~10 min) |
| Stage 0 SKIP | 0 min |
| Stage 1 routes verify | ~5 min |
| Stage 2 3 base stories | ~30 min |
| Stage 3 7 stub files | ~70 min |
| Stage 4 gates | ~30 min |
| Stage 5 merge + closure + Phase C-2 milestone | ~30 min |
| **Total** | **~3-4h** |

## 6. Acceptance criteria

### Base ship
- [ ] 3 base stories scaffolded (skeleton-live + summary + play)
- [ ] Existing route verification documented

### Per-game stubs
- [ ] 7 stub files in `sessions/_sp4-stubs/` dir
- [ ] Each stub exports Live + Summary Stories (14 Stories total)
- [ ] Stub content references mockup HTML path

### Quality gates
- [ ] pnpm typecheck 0 errors
- [ ] pnpm lint 0 errors
- [ ] pnpm lint:tokens 0 violations
- [ ] pnpm lint:bgg clean
- [ ] pnpm lint:fidelity all PASS
- [ ] pnpm mockup-annotations:audit ≥80% mappable

### Phase C-2 milestone
- [ ] Admin-squash merge P145 42a
- [ ] Sub-issue #2231 closed
- [ ] EPIC #2063 Phase C-2 4/4 COMPLETE comment
- [ ] Memory entry `ds-17-15-sp4-sessions-shipped.md`
- [ ] Phase C-3 NEXT trigger note

## 7. Out of scope (deferred Phase C-3)

- ❌ Per-game MSW handlers (Catan/Codenames/etc datasets)
- ❌ Per-game flavor components rendering (HexBoard, WordGrid, etc.)
- ❌ Real game mockup parity
- ❌ Visual baseline (P252)
- ❌ DS-17 Phase D + E

## 8. References

- Sub-issue: #2231
- Phase C-2 META: parent spec
- Predecessors: #2218 + #2225 + #2230 (Phase C-2 steps 1-3)
- Memory: `ds-17-14-sp4-admin-shipped.md` (P259 + P260)

---

**End of combined spec+plan.**
