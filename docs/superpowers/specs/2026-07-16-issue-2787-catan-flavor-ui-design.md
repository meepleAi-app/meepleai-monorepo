# Design — #2787 Catan flavor UI (G6a, LIVE)

**Issue**: [#2787](https://github.com/meepleAi-app/meepleai-monorepo/issues/2787) — `[#2377 G6a] feat(session-live): Catan flavor UI (live + summary)`
**Umbrella / Epic**: #2377 (G6 per-game) · #2354 (session-live shell)
**Date**: 2026-07-16
**Status**: design — awaiting user review before writing-plans
**Track**: FE-only (`area/frontend`, `user-facing`, P2)

---

## 1. Goal

Ship the **pilot** per-game flavor for **Catan** inside the session-live shell, mounted polymorphically on the G5 renderers by `gameSlug`. This is the first of 7 games (#2377); it must **validate the ADR-070 lazy `FlavorRenderer` architecture** so the remaining 6 games can parallelize.

Scope is **presentational MVP over real data**: the flavor themes the data the backend actually exposes; it renders **no invented data** (RULES.md "Real Code Only" / no-mock).

## 2. Locked decisions (brainstorming Q&A)

| # | Decision | Choice |
|---|---|---|
| Q1 | Fidelity level | **Tematico su dati reali (MVP)** — no fake board/dice/trades |
| Q2 | Live + Summary | **Solo LIVE in questa PR**; SUMMARY → follow-up sub-issue G6a-2 |
| Q3 | LIVE mount seam | **Tab `flavor` dedicato condizionale** in `RightColumnTabs` (score tab intatto, editor host preservato) |
| Q4 | Governance vs #2234 deferral | **Catan-first #2787**; flip `design_intent` del fidelity live; nota di riconciliazione (link/close #2234 lasciato all'utente) |
| Q5 | SUMMARY complexity | **Deferred** (GameSessionDto senza `gameSlug`, layout lineare, adapter appiattisce score) |

## 3. Data contract (real data available — LIVE)

Source of truth: `LiveSessionDto` (`live-sessions.schemas.ts`) via `useLiveSession(sessionId)`, plus the polymorphic scoring store.

| Datum | Field | Notes |
|---|---|---|
| Game identity | `LiveSessionDto.gameSlug` = `"catan"` | discriminator for `FLAVOR_MAP` |
| Players | `players[]`: `displayName`, `color` (enum `Red…Teal`), `avatarUrl`, `totalScore`, `currentRank`, `isActive`, `role` | real piece color + score + rank |
| Active turn | `currentTurnPlayerId`, `currentTurnIndex` | "Round N" + active highlight |
| Status | `status` (`Created…Completed`) | gating |
| Phase | `TurnPhasesDto.currentPhaseName`, `phaseNames[]` | optional ("Fase: Costruisci"); graceful if absent |
| Dimensions | `scoringConfig.enabledDimensions[]` + `roundScores[]` (`{playerId, round, dimension, value}`) | **conditional** per-category VP breakdown; fallback to totals-only |
| G5 scoring | store `scoringType='Points'` + `scoreData` → `mapScoreDataToPanelData` → `ScoringPanelData` | reuse existing read-only path |

**Not available (omitted, never faked):** hex board state, dice rolls, resource hands, trades, dev cards, robber, dedicated Longest-Road/Largest-Army flags (rendered **only** if present as a scoring dimension).

## 4. Architecture (ADR-070 Option B — fully lazy)

Create the missing dispatcher + Catan flavor module:

```
apps/web/src/components/features/session-live/
  FlavorRenderer.tsx          # NEW — dispatcher: FLAVOR_MAP[gameSlug]?.[view] → dynamic(), else null
  FlavorLoadingSkeleton.tsx   # NEW — <Suspense> fallback
  flavors/
    catan/
      CatanLiveFlavor.tsx      # NEW — themed LIVE view (read-only)
      catan-palette.ts         # NEW — PlayerColor enum → Catan display palette (token-safe)
      _parts/                  # NEW — small presentational sub-components (leaderboard row, turn header, dimension breakdown)
      __tests__/…
```

`FLAVOR_MAP` (single source of the game→module registry):

```ts
type FlavorView = 'live' | 'summary';
interface FlavorEntry { readonly live?: FlavorLoader; readonly summary?: FlavorLoader; }
const FLAVOR_MAP: Record<string, FlavorEntry> = {
  catan: { live: () => import('./flavors/catan/CatanLiveFlavor').then(m => ({ default: m.CatanLiveFlavor })) },
  // summary added by G6a-2; other 6 games by G6b–g
};
```

`FlavorRenderer`:
- Looks up `FLAVOR_MAP[gameSlug]?.[view]`. If absent → returns `null` (graceful fallback to generic renderers).
- Wraps the lazy component in `<Suspense fallback={<FlavorLoadingSkeleton/>}>`.
- Uses `next/dynamic` with `{ ssr: false }` (pattern precedent: `editor/page.tsx:35`, `KbGlobaleView.tsx:56`).
- **Bundle:** the Catan chunk must stay out of the live route's main bundle (verified via `pnpm bundle:check`).

The `view` discriminator is present now but only `live` is implemented (`summary` = YAGNI until G6a-2). Interface is future-proofed, implementation is not.

## 5. Mount seam — LIVE (conditional `flavor` tab)

`RightColumnTabs` currently has a **static** `ORDERED_TABS` + fixed `LiveTab` union + keyboard nav bound to that array. Adding a game-conditional 7th tab requires a **contained** change:

**`RightColumnTabs.tsx`:**
- Add `'flavor'` to `LiveTab` union.
- New prop `showFlavorTab?: boolean` (default `false`). When true, compute `orderedTabs = ['flavor', ...BASE_TABS]`; otherwise `BASE_TABS`. **Flavor leads** (matches mockup: Catan tab is primary).
- Add `tabFlavor` to `RightColumnTabsLabels`; add to `tabLabels` record.
- Pass the **computed** `orderedTabs` to `useTablistKeyboardNav` (roving tabindex must match visible tabs) and to the render map.

**`SessionLiveView.tsx`:**
- Compute `hasFlavor = FLAVOR_MAP[sessionQuery.data?.gameSlug ?? ''] != null` (a tiny exported `hasFlavor(gameSlug)` helper from `FlavorRenderer.tsx` to avoid importing the map directly).
- Pass `showFlavorTab={hasFlavor}` to `RightColumnTabs`.
- Add `{tab === 'flavor' && <FlavorRenderer view="live" gameSlug={…} session={…} scoringPanelData={…} turnPhases={…} labels={…} />}` in `desktopRightColumn` (and the **mobile** tab surface — exact mobile mechanism to be confirmed in the plan; the flavor tab must appear in both).
- `parseLiveTab`: accept `flavor`; if `?tab=flavor` is requested but `!hasFlavor`, fall back to `'score'` (never strand the user on an empty tab).
- Default tab stays `'score'`; flavor is opt-in navigation (no change to default).

Games without a flavor → `showFlavorTab=false` → tab absent → zero behavioural change (regression-safe for all non-Catan sessions).

## 6. Component design — `CatanLiveFlavor` (read-only)

Props (small surface; reads what it needs):
```ts
interface CatanLiveFlavorProps {
  readonly session: LiveSessionDto;                 // players/color/rank/turn/status/scoringConfig/roundScores
  readonly scoringPanelData: ScoringPanelData | null; // G5 Points path (already mapped upstream)
  readonly turnPhases: TurnPhasesDto | null;         // optional phase name
  readonly labels: CatanLiveFlavorLabels;            // i18n, resolved by the shell
}
```

Render (top→bottom):
1. **Turn/phase header** — "Round {currentTurnIndex}" + active player (`currentTurnPlayerId`) + `turnPhases.currentPhaseName` if present. `aria-live="polite"`.
2. **Themed leaderboard** — per player, sorted by `currentRank` (or score desc): piece color swatch from `catan-palette`, `displayName`, `totalScore`, leader crown (rank 1), `isActive` highlight. Reuses the G5 `PointsScoringData` where possible; Catan theming is the visual layer.
3. **VP-by-dimension breakdown** — **only if** `scoringConfig.enabledDimensions.length > 0`: compact per-player/per-dimension values from `roundScores`. Otherwise omitted (totals-only). Longest-Road/Largest-Army surface here **iff** modeled as a dimension.
4. **Catan visual identity** — terrain-palette accents (decorative, token-safe), Catan iconography. Purely presentational, no data claims.

Null/absent data → graceful: `scoringPanelData === null` (SSE not hydrated) → `aria-live` placeholder (existing pattern); no dimensions → totals-only; no phases → header shows round + active player only.

## 7. `catan-palette.ts` (token discipline)

Maps `PlayerColor` enum → a display treatment. **Must** obey ESLint `local/no-hardcoded-color-utility` (DS-15 error): no `bg-white`/`bg-slate-*`/etc. Use either entity utilities, semantic tokens, or arbitrary `bg-[hsl(...)]` (the mockup `.e-bg`/`entityHsl()` pattern; `text-white` allowed alongside a colored bg). Palette values derived from the mockup's `catan.json` terrain colors. Pure, unit-tested mapping (every enum member covered, incl. an `assertNever`-style exhaustiveness guard).

## 8. Error / edge handling

| Case | Behaviour |
|---|---|
| `gameSlug` not in `FLAVOR_MAP` | `FlavorRenderer` → `null`; no tab; generic renderers unaffected |
| `scoringPanelData === null` (pre-SSE) | `aria-live` placeholder inside the flavor panel |
| `enabledDimensions` empty | totals-only leaderboard (no breakdown section) |
| `turnPhases === null` | header omits phase segment |
| `?tab=flavor` on a game without flavor | `parseLiveTab` falls back to `'score'` |
| `prefers-reduced-motion` | honoured (no non-essential motion) |
| Player `color` unknown/new enum value | palette falls back to a neutral token (no crash) |

## 9. Testing

- **Unit** (`__tests__/*.test.tsx`, vitest + RTL + `vi.hoisted`): `FlavorRenderer` dispatch (catan→loads, unknown→null, view routing), `hasFlavor` helper, `catan-palette` exhaustiveness, `CatanLiveFlavor` render across data states (full / no-dimensions / null-score / no-phases), `RightColumnTabs` conditional tab (present when `showFlavorTab`, keyboard nav includes flavor, absent otherwise), `parseLiveTab` flavor fallback.
- **E2E skeleton** (`apps/web/e2e/*.spec.ts`, `?fixture=host`, `data-slot` selectors): Catan session shows the flavor tab, navigating to it renders `CatanLiveFlavor`; non-Catan session has no flavor tab.
- **Axe AA** (`apps/web/e2e/a11y/*.spec.ts`, wcag2aa tags, 0 violations) on the flavor tab panel.
- **Bundle** (`pnpm bundle:check`): Catan flavor chunk lazy — not in the `/sessions/[id]/live` main bundle.
- **`data-slot`** attributes on flavor surfaces for test targeting (convention).

## 10. i18n

New UI strings (turn/phase header, tab label "Catan", leaderboard aria templates, dimension labels) added to **both** `src/locales/it.json` and `src/locales/en.json` under a `pages.sessionLive.flavor.catan.*` namespace. Labels resolved in the shell and passed down (pure components receive strings, matching `ScoringPanelRenderer`/`SessionSummaryView` convention).

## 11. Governance & fidelity.json

- Update **`sp4-session-catan-live.fidelity.json`**: `design_intent: "deferred"` → **`"forward-refactor"`** (the MVP intentionally diverges from the full-board mockup — board/dice/trades omitted; the mockup stays the north-star for a future full-fidelity pass); set `story_path`/`fixtures_path` if applicable.
- Leave **`sp4-session-catan-summary.fidelity.json`** as `deferred` (summary is G6a-2).
- Add a one-line reconciliation note (spec + PR body): #2377/#2787 (live-app) supersedes/parallels the deferred DS-17 track #2234 for Catan LIVE. **Linking/closing #2234 is left to the maintainer** (out of this PR's scope).
- Run `pnpm lint:fidelity` after the edit.

## 12. Out of scope / follow-ups

- **SUMMARY flavor** → open **G6a-2** sub-issue. Prerequisites to resolve there: `gameSlug` (or gameName) on the summary DTO/endpoint OR a `gameId→slug` catalog lookup; a mount slot in the linear `SessionSummaryView`; read raw `GameSessionDto` (`scoringType`/`scoreData`/`color`/`durationMinutes`) instead of the flattening adapter.
- Full interactive board / dice / trades → needs a Catan game-state backend domain; not this FE track.
- Other 6 games (G6b–g) → after this pilot validates the pattern.

## 13. DoD mapping (#2787)

| #2787 DoD item | This PR |
|---|---|
| `CatanFlavorRenderer` (live) wired via lazy `dynamic()` (ADR-070 B) | ✅ `FlavorRenderer` + `CatanLiveFlavor` |
| Vista **live** conforme a mockup | ✅ MVP tematico (board/dice/trades omessi — noted `forward-refactor`) |
| Vista **summary** conforme | ⏭️ deferred → G6a-2 (DoD updated to reflect) |
| Selezione flavor per `gameSlug` + fallback graceful | ✅ `FLAVOR_MAP` + `null` fallback |
| Bundle budget: flavor lazy-loaded | ✅ `pnpm bundle:check` |
| Unit + E2E skeleton + axe AA | ✅ |
| `*.fidelity.json` companion aggiornato | ✅ live flavor; summary stays deferred |
| Aggiornare checklist #2377 (G6a) al merge | ✅ at merge |

## 14. File manifest

**New:** `FlavorRenderer.tsx`, `FlavorLoadingSkeleton.tsx`, `flavors/catan/CatanLiveFlavor.tsx`, `flavors/catan/catan-palette.ts`, `flavors/catan/_parts/*`, `__tests__/*`, `e2e/*catan-flavor*.spec.ts`, a11y spec.
**Modify:** `RightColumnTabs.tsx` (conditional flavor tab + labels), `SessionLiveView.tsx` (compute `hasFlavor`, pass prop, mount `FlavorRenderer`, `parseLiveTab` flavor + mobile surface), `it.json`/`en.json`, `sp4-session-catan-live.fidelity.json`, `.bundle-budgets.json` (if budget note needed).

## 15. Risks / open items (to resolve in plan)

1. **Mobile tab surface** — confirm the mobile mechanism in `SessionLiveView` and wire the flavor tab there too.
2. **`turnPhases` source** — confirm the hook/endpoint loading `TurnPhasesDto` in the live shell (may need `useSessionTools`/phases query); treat as optional.
3. **`fidelity.json`** — set `design_intent: "forward-refactor"` (decided §11); confirm `pnpm lint:fidelity` accepts it.
4. **Palette token compliance** — ensure `catan-palette` passes `local/no-hardcoded-color-utility`.
5. **`RightColumnTabs` reuse** — the component is shared; verify no other consumer breaks from the new optional prop (default `false` = no-op).
