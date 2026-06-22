# Designer Review Queue — DS-17 Phase C-1 cluster: sp6-7-nano

**Generated**: 2026-06-11
**Source**: Phase C-1 cluster migration (post Phase B audit)
**Cluster**: sp6-7-nano

## Shipped stories (require designer review)

- [ ] `sp7-game-night-create` (12 frames)
  - story_path: `apps/web/src/app/(authenticated)/game-nights/new/game-night-create.stories.tsx`
  - fixtures_path: `apps/web/src/__tests__/fixtures/mockup-pilots/sp6-7-nano/sp7-game-night-create.ts`
- [ ] `sp7-game-night-detail-rsvp` (14 frames)
  - story_path: `apps/web/src/app/(authenticated)/game-nights/[id]/game-night-detail-rsvp.stories.tsx`
  - fixtures_path: `apps/web/src/__tests__/fixtures/mockup-pilots/sp6-7-nano/sp7-game-night-detail-rsvp.ts`
- [ ] `sp7-game-night-live` (10 frames)
  - story_path: `apps/web/src/app/(authenticated)/game-nights/[id]/live/game-night-live.stories.tsx`
  - fixtures_path: `apps/web/src/__tests__/fixtures/mockup-pilots/sp6-7-nano/sp7-game-night-live.ts`
- [ ] `sp7-game-night-summary` (8 frames)
  - story_path: `apps/web/src/app/(authenticated)/game-nights/[id]/summary/game-night-summary.stories.tsx`
  - fixtures_path: `apps/web/src/__tests__/fixtures/mockup-pilots/sp6-7-nano/sp7-game-night-summary.ts`

## Obsolete deferred (Phase B tracking)

_None._

## Pair disagreements (require arbitration)

_None._

## Forward-refactor — route missing (DEFERRED Phase D tracking)

6 stems with mockup but NO existing app route. Story migration deferred to Phase D dedicated sub-issue.

- `sp7-game-night-transition.html` + `.jsx` — Route `/game-nights/[id]/transition` MISSING
- `sp7-game-night-join-public.jsx` (JSX-only, no HTML twin) — Route `/game-nights/join/[code]` (public) MISSING
- `sp6-libro-game-index.html` + `.jsx` — Route `/libro-game/*` MISSING completely
- `sp6-libro-game-resume-state.html` + `.jsx` — Route MISSING
- `sp6-libro-game-photo-upload.html` + `.jsx` — Route MISSING + BGG ecosystem refactor (BGG cleanup completed Stage 0)
- `sp6-libro-game-quota-credits.jsx` (JSX-only) — Route MISSING

**Tracking issue raggruppato**: `[DS-17 Phase D] sp6-7-nano forward-refactor — libro-game ecosystem + game-night gap stems implementation` (opened during Task 20).

## How to approve

Comment on PR with magic phrase:

```
DESIGNER APPROVED: <ISO date> <your-name>
```

(Same protocol as Phase B; sign-off optional per user decision.)
