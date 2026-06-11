# Axis discovery — sp7-game-night-live

**Mockup**: `admin-mockups/design_files/sp7-game-night-live.html` + `.jsx`
**JSX twin lines**: 1614 total
**Route**: `/game-nights/[id]/live`
**Hero component**: `NightLiveHub` (`apps/web/src/components/features/game-nights/live/NightLiveHub.tsx`)
**Page-client**: `apps/web/src/app/(authenticated)/game-nights/[id]/live/_components/NightLiveClientView.tsx` (`NightLiveClientView`)
**Route entry**: `apps/web/src/app/(authenticated)/game-nights/[id]/live/page.tsx`

## Axis matrix

| Axis | Values | JSX evidence (line) |
|------|--------|---------------------|
| `status` | `'live' | 'paused' | 'transition'` | Header comment line 11-15 lists 4 status (live default + paused state-04 + transition state-03 + ENDED in description but unused frame); `NightLiveHub` prop `status?: NightLiveStatus` line 36 of component |
| `mobile` | `boolean` | `<PhoneShell>` wrapping frames 07-09 lines 1552-1576 |
| `initialMobileTab` | `'current' | 'planned' | 'diary'` | Frame 07 line 1552 `mobile-tab-current`, frame 08 line 1558 `mobile-tab-planned`, frame 09 line 1564 `mobile-tab-diary` |
| `current` / `total` / `elapsed` | numerics | NightLiveHub prop line 37-39 — frame 01 `current=1, total=3, elapsed='1h 12m'`; frame 02 `current=2, total=3, elapsed='2h 23m'` (mid-night transition) |
| `currentGame` | `NightLiveHubCurrentGame | null` | null in frame 03 transition (line 1494 `transition-pending`); not-null otherwise |
| `plannedGames` | array — status `'inprogress' | 'completed' | 'upcoming'` | frame 01 has `'inprogress' + 'upcoming' + 'upcoming'`; frame 02 has `'completed' + 'inprogress' + 'upcoming'` |
| `diaryEvents` | array | frame 05 line 1517 `diary-empty` (empty array) vs frame 02 mid-night (~5 events) |
| `autoSaveToast` | `{ visible, timestamp } | undefined` | frame 10 line 1581 `auto-save-toast`; NightLiveHub prop line 47 |

## Frame matrix (mockup → story)

Source: `<PhoneShell id="state-NN-..."/>` + `<DesktopShell id="state-NN-..."/>` instances lines 1469-1581. Total **10 frames**.

| Frame | Mockup state ID | status | tab/variant | Story export | Story name |
|-------|------------------|--------|-------------|--------------|------------|
| 01 | `state-01-game-1-in-progress` | live | Game 1 (Brass) in-flight | `Frame01_Game1InProgress` | `01 · Game 1 (Brass) in-progress · diary 5 eventi · 2 upcoming` |
| 02 | `state-02-mid-night-game-2` | live | Mid-night (G1 done, G2 in-flight) | `Frame02_MidNightGame2` | `02 · Mid-night · Game 1 completed · Game 2 in-progress · diary 18 eventi` |
| 03 | `state-03-transition-pending` | transition | transition pane | `Frame03_TransitionPending` | `03 · Transition pending tra Game 1 e Game 2` |
| 04 | `state-04-paused` | paused | overlay agent warning | `Frame04_Paused` | `04 · Serata in pausa · overlay agent warning` |
| 05 | `state-05-diary-empty` | live | diary empty | `Frame05_DiaryEmpty` | `05 · Inizio serata · diary "Nessun evento ancora"` |
| 06 | `state-06-diary-widget-embedded` | live | standalone widget | `Frame06_DiaryWidgetEmbedded` | `06 · Diary widget 320x400 isolato (riuso standalone)` |
| 07 | `state-07-mobile-tab-current` | live | mobile + tab=current | `Frame07_MobileTabCurrent` | `07 · Mobile · Tab "Current" attivo` |
| 08 | `state-08-mobile-tab-planned` | live | mobile + tab=planned | `Frame08_MobileTabPlanned` | `08 · Mobile · Tab "Planned" attivo` |
| 09 | `state-09-mobile-tab-diary` | live | mobile + tab=diary | `Frame09_MobileTabDiary` | `09 · Mobile · Tab "Diary" attivo` |
| 10 | `state-10-auto-save-toast` | live | autoSaveToast.visible=true | `Frame10_AutoSaveToast` | `10 · Toast bottom-right toolkit ogni 60s ("Salvato")` |

## Canonical pick — P245 multi-route consolidation

Single Next.js route `/game-nights/[id]/live`. Hero = `NightLiveHub` (Screen K primitive) since it exposes ALL axis values as props directly. The page-client `NightLiveClientView` just wires the fixture + `GameTransitionDialog` modal on top — for visual coverage we render NightLiveHub directly and document the transition flow as Frame 03 doc-only.

`GameTransitionDialog` (Screen L) is a SIBLING primitive in `components/features/game-nights/transition/` — has its own dedicated story; don't duplicate here.

## Phase scope (Phase C-1 vs Phase 4)

- **Frames 01-06, 10**: Desktop primary, story renders canonical.
- **Frames 07-09 (Mobile tabs)**: render in story w/ `mobile=true`+`initialMobileTab=...`. Full mobile viewport sweep DEFERRED to **Phase 4**.

## Sub-components needing dedicated stories (Stage 4 task)

Already in folder `apps/web/src/components/features/game-nights/live/`:
- `CrossGameDiaryTimeline.tsx` (+ `.test.tsx` — convert to .stories.tsx if missing)
- `DiaryInlineWidget.tsx` (+ `.test.tsx` — widget for Frame 06 standalone)
- `PlannedGamesPane.tsx` (+ `.test.tsx`)
- `NightLiveHub.tsx` (+ `.test.tsx`)

Sibling: `apps/web/src/components/features/game-nights/transition/GameTransitionDialog` for Frame 03.

If `.stories.tsx` files not present, queue cluster-queue follow-up.
