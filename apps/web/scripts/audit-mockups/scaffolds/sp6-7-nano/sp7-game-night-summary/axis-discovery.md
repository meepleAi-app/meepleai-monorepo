# Axis discovery — sp7-game-night-summary

**Mockup**: `admin-mockups/design_files/sp7-game-night-summary.html` + `.jsx`
**JSX twin lines**: 1134 total
**Route**: `/game-nights/[id]/summary`
**Hero component**: `NightSummaryView` (`apps/web/src/components/features/game-nights/summary/NightSummaryView.tsx`)
**Page-client**: `apps/web/src/app/(authenticated)/game-nights/[id]/summary/_components/NightSummaryClientView.tsx` (`NightSummaryClientView`)
**Route entry**: `apps/web/src/app/(authenticated)/game-nights/[id]/summary/page.tsx`

## Axis matrix

| Axis | Values | JSX evidence (line) |
|------|--------|---------------------|
| `archived` | `boolean` | `<PhoneShell id="state-05-archived"/>` line 1080; NightSummaryView prop line 39 |
| `mobile` | `boolean` | `<PhoneShell id="state-06-mobile-single-col"/>` line 1098; NightSummaryView prop line 38 |
| `games` | `ReadonlyArray<PerGameRecapGame>` — full (3) / single (1) | Frame 03 `state-03-summary-single-game` line 1062 — `games.length === 1` triggers "1 game · serata breve" (NightSummaryView.tsx line 75) |
| `photos` | full (6) / empty | Frame 02 `state-02-summary-no-photos` line 1054 — empty triggers placeholder CTA |
| `mvp` | `MVP | null` | Co-op scenarios — Frame 03 single Spirit Island co-op would set `mvp: null`; co-op game has `coopMode: true` |
| `shareSuccess` | `{ visible: true } | undefined` | Frame 04 `state-04-share-success-toast` line 1071 |
| `eventsCount` | numeric | Frame 01 = 28, Frame 03 single game ≈ 11 |

## Frame matrix (mockup → story)

Source: `<PhoneShell id="state-NN-..."/>` instances lines 1046-1098 (6 frames).

| Frame | Mockup state ID | Variant | Story export | Story name |
|-------|------------------|---------|--------------|------------|
| 01 | `state-01-summary-full` | full recap (3 games + 28 diary + 6 photos + MVP) | `Frame01_SummaryFull` | `01 · Recap completo · 3 games · 28 diary · 6 foto · MVP Davide` |
| 02 | `state-02-summary-no-photos` | full recap + photos empty | `Frame02_SummaryNoPhotos` | `02 · Stesso recap · gallery empty + placeholder CTA "Aggiungi foto"` |
| 03 | `state-03-summary-single-game` | 1 game · no per-game multipli | `Frame03_SummarySingleGame` | `03 · Serata 1 game · no transition · no per-game multipli · stats ridotte` |
| 04 | `state-04-share-success-toast` | post-share toast | `Frame04_ShareSuccessToast` | `04 · Post-share toast "Link copiato" toolkit` |
| 05 | `state-05-archived` | post-archive banner + go-to-list CTA | `Frame05_Archived` | `05 · Post-archive banner muted + CTA "Torna alla lista"` |
| 06 | `state-06-mobile-single-col` | mobile vertical stack | `Frame06_MobileSingleCol` | `06 · Mobile vertical stack (390 fullscreen · padding ridotto)` |

## Canonical pick — P245 multi-route consolidation

Single Next.js route `/game-nights/[id]/summary`. Hero = `NightSummaryView` (Screen M primitive) since it exposes ALL axis values as props directly (no internal state). The page-client `NightSummaryClientView` just wires fixture data + share/archive callbacks.

`PerGameRecapRow` is a sibling primitive — has dedicated `.test.tsx` (line 8 in folder listing) and should also have a dedicated story.

## Phase scope (Phase C-1 vs Phase 4)

- **Frames 01-05**: Desktop primary, story renders canonical.
- **Frame 06 (Mobile single-col)**: render w/ `mobile=true` prop. Full mobile viewport sweep DEFERRED to **Phase 4**.

## Sub-components needing dedicated stories (Stage 4 task)

In folder `apps/web/src/components/features/game-nights/summary/`:
- `NightSummaryView.tsx` (+ `.test.tsx` — convert .test.tsx to .stories.tsx companion)
- `PerGameRecapRow.tsx` (+ `.test.tsx` — same)

Shared UI primitives used:
- `ArchivedBanner` (Frame 05 — `@/components/ui/archived-banner`)
- `KPIStatCard` + `KPIStatGrid` (KPI grid in hero)
- `ShareSuccessToast` (Frame 04 — `@/components/ui/share-success-toast`)

If `.stories.tsx` files not present, queue cluster-queue follow-up.
