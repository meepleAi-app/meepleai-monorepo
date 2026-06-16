# T7 — Designer Review Prep (Issue #2374 G1 60/40 layout)

> Plan: `docs/superpowers/plans/2026-06-15-issue-2374-session-live-g1-3-col-layout.md` §4 T7
> Branch: `feature/issue-2374-3col-layout`
> Mockup: `admin-mockups/design_files/sp4-session-skeleton-live.{html,jsx}`
> Fidelity meta: `admin-mockups/design_files/sp4-session-skeleton-live.fidelity.json`

## What changed

- Desktop body collapsed from 3 zones (LEFT sidebar 280px / CENTER flex / RIGHT 340px) to **2 zones at 60/40 ratio** (`grid-template-columns: minmax(0,3fr) minmax(0,2fr)`).
- LEFT 60% now stacks `ChatAgentPanel` (mockup "magnet") above `ActionLogTimeline` — chat is always visible.
- RIGHT 40% now hosts `RightColumnTabs` with canonical mockup tab keys (`score | turn | widget | notes`).
- `TurnIndicator` + `PlayerRosterLive` moved from LEFT sidebar (deleted) into the RIGHT `turn` tab.
- All raw `bg-[hsl(240,40%,*)]` literals replaced with semantic tokens (`bg-background` / `bg-card`).
- Parent `sessions/[id]/layout.tsx` suppresses the topbar mini-nav tab strip on `/live` only (R-3 mitigation).
- URL back-compat preserved: legacy `?tab=tools|chat|notes` maps to `?tab=widget|score|notes` via `parseLiveTab`.

## Before / After

- **Before**: No baseline image — the visual regression test suite was removed on 2026-05-20 (CLAUDE.md §302). Refer to the legacy 3-column layout described in commit history (last touched in Wave D.2, Issues #746 + #750).
- **After**: `claudedocs/2026-06-15-issue-2374-after.png` — 1440x900 desktop screenshot, dark theme, host fixture, Score tab active.

## Designer review checklist

- [ ] LEFT column proportions feel right (~60% of available width, not crushed below ~720px on mid laptops).
- [ ] ChatAgentPanel "magnet" header (avatar + Online pip + agent name + latency + ChatAgent pill) matches mockup `sp4-session-skeleton-parts.jsx` L91-135.
- [ ] ActionLogTimeline reads naturally stacked below ChatAgentPanel (no awkward gap).
- [ ] RIGHT column tab strip uses canonical labels (Score / Turni / Widget / Note in IT, Score / Turn / Widget / Notes in EN).
- [ ] No double tablist visible at the top of the page (parent miniNav is suppressed — locked by E2E assertion).
- [ ] `border-entity-session` accent on active tab is visible (per D-4 entity color mapping).
- [ ] No raw HSL backgrounds remain (`pnpm lint:tokens` is clean).

## Self-waiver (single-person team)

Per **P250** (sess.46p), the user is the designer of record for this codebase; the `designer_approved_by` field stays as `"user@meepleAi (self-waiver P250, single-person team)"` until external designer review is requested.

`designer_approved_on` refreshed to **2026-06-15** to reflect the G1 60/40 layout rework sanity check.

## Mobile

Out of scope here — covered by T9 (mobile bottom-sheet drawer) in a subsequent commit. The fidelity `viewports` field stays as `["desktop"]` for T7 and gets expanded to include `"mobile"` when T9 ships.

## How to re-capture the screenshot

```bash
# From apps/web
NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1 \
  pnpm exec playwright test e2e/a11y/session-live.spec.ts \
    --project=desktop-chrome --grep "G1 desktop 60/40"
```

The new G1 a11y tests (T6) already validate the 60/40 grid contract continuously; the PNG is only for human review.
