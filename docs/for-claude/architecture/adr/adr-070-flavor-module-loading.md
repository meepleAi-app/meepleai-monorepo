# ADR-070 — Flavor Module Loading: Bundled vs Lazy + Cache TTL Strategy

**Status**: Proposed
**Date**: 2026-06-15
**Deciders**: @badsworm (pending ratification at PR review)
**Tracking**: [#2363](https://github.com/meepleAi-app/meepleai-monorepo/issues/2363) Wave 2 — US-INT-4 (per-game UI extensions: premium game flavors)
**Related**: [umbrella #2342](https://github.com/meepleAi-app/meepleai-monorepo/issues/2342) · issue #2356 (G7 SessionStateRenderer) · SP4 session mockups

## Context

The `admin-mockups/design_files/` directory contains flavor modules for 7 premium games:
- **Catan**: `sp4-session-catan-flavor.jsx`, `sp4-session-catan-live.jsx`, `sp4-session-catan-summary.jsx`, `sp4-session-catan-parts.jsx`, `sp4-session-catan-data.jsx`
- **Codenames**: `sp4-session-codenames-flavor.jsx`, `sp4-session-codenames-live.jsx`, `sp4-session-codenames-summary.jsx`, `sp4-session-codenames-parts.jsx`, `sp4-session-codenames-bodies.jsx`
- **Paleo**: `sp4-session-paleo-flavor.jsx`, `sp4-session-paleo-live.jsx`, `sp4-session-paleo-summary.jsx`, `sp4-session-paleo-parts.jsx`
- **Power Grid**: `sp4-session-power-grid-live.jsx`, `sp4-session-power-grid-summary.jsx`
- **Puerto Rico**: `sp4-session-puerto-rico-live.jsx`, `sp4-session-puerto-rico-summary.jsx`
- **Wingspan**: `sp4-session-wingspan-live.jsx`, `sp4-session-wingspan-summary.jsx`
- **Zombicide**: `sp4-session-zombicide-live.jsx`, `sp4-session-zombicide-summary.jsx`

The Catan flavor module (`sp4-session-catan-flavor.jsx`) exports rich game-specific primitives via `window.CatanFlavor`: hex geometry, number tokens, settlements, cities, roads, board rendering, resource tokens, and player badges. These are complex SVG-heavy components — Catan alone is ~600 LOC of game-specific visual logic with canvas/SVG layout computations.

The current live session view (`SessionLiveView.tsx`) has a generic 3-column shell. Flavor modules extend this shell with game-specific panels (e.g., Catan's hex board in the center column, Codenames' word grid, Wingspan's bird card display). The `SessionStateRenderer` (ADR-071, G7) wraps all panel states — flavor content is `kind: 'default'` passthrough to the game-specific renderer.

The existing `apps/web` codebase uses Next.js `dynamic()` in multiple places:
- `KbGlobaleView.tsx` — `KbDocViewerDesktopLazy`, `DrawerShellLazy`, `KbEditorDesktopLazy`
- `editor/page.tsx` — `EditorClient` (with `{ ssr: false }`)
- `play-records/page.tsx` — `PlayHistory`
- Chat thread view, admin cost breakdown panels

These follow the `next/dynamic` pattern with optional `ssr: false` for client-heavy components and inline `loading` skeletons. No flavor module loading infrastructure exists yet in `apps/web`.

**Bundle budget**: no `.bundle-budgets.json` found in the repository root or `apps/web/`. The Next.js `next.config.js` has no custom `experimental.bundlePages` or `sizeLimit` configuration.

**7 games, 2 views each**: each game has a live view and summary view flavor. The live flavor is larger (real-time board state); the summary flavor is smaller (final scoring display). Conservative estimate per game: live flavor ~30–80 KB gzipped (Catan is the heaviest with hex geometry).

**Existing cache infrastructure**: Next.js App Router has built-in router cache (client-side, TTL configurable via `staleTimes`), full-route cache (server-side, opt-out via `dynamic = 'force-dynamic'`), and `fetch` memoization. For static flavor assets, HTTP `Cache-Control` headers via Next.js `public/` folder or API route responses are the standard mechanism.

## Problem

The specific architectural question: **should flavor modules be bundled into the main session-live chunk, lazy-loaded per game on navigation, or a hybrid; and what cache TTL strategy ensures re-renders during live sessions do not re-fetch the flavor module?**

Sub-decisions:
1. **Loading strategy**: all 7 games bundled (single chunk) vs lazy per-game (`dynamic()` import on navigation) vs hybrid (top-N bundled, others lazy).
2. **Cache TTL**: Next.js router cache `staleTimes` vs HTTP `Cache-Control` vs Service Worker vs no special caching (rely on Next.js built-in module chunk caching).
3. **Cold-start latency budget**: what is acceptable latency from session load to flavor panel visible?
4. **SSR vs client-only**: should flavor components render on the server (SSR) or be client-only (`{ ssr: false }`)?

## Options Considered

### Option A — All 7 games bundled into the session-live chunk

All game flavor components are imported statically at the top of `SessionLiveView.tsx` (or a sibling `FlavorPanel.tsx`). The session-live route chunk includes all game flavors.

**Pros**:
- Zero cold-start latency for flavor panel — already in bundle when the session page loads.
- No network waterfall: flavor component renders synchronously with the session shell.
- No dynamic import lifecycle complexity (no `Suspense`, no loading fallback for the flavor panel).

**Cons**:
- Bundle size: 7 games × estimated 30–80 KB gzipped = 210–560 KB added to the session-live chunk, downloaded for every user of any live session regardless of which game they are playing.
- The session-live route is already large (SSE hook, score editor, roster panel, action log, tools rail, chat). Adding 560 KB of game-specific code unconditionally would be the largest route chunk in the app.
- LCP (Largest Contentful Paint) for the session shell degrades proportionally to the chunk size increase.

**Risks**: Unacceptable bundle bloat. Users playing a game not in the 7 premium set (generic sessions) download ~560 KB of dead code.

**Impact**: ~0.5 day. Trivially implemented.

---

### Option B — Fully lazy per game: `dynamic()` import on session load (recommended)

Each game flavor component is a dynamically imported Next.js chunk. A `FlavorRenderer` component resolves the correct flavor by `gameId` or `gameSlug` and calls `dynamic(() => import('./flavors/catan/CatanLiveFlavor'))`.

```tsx
// apps/web/src/components/features/session-live/FlavorRenderer.tsx
const FLAVOR_MAP: Record<string, () => Promise<...>> = {
  catan:       () => import('./flavors/catan/CatanLiveFlavor'),
  codenames:   () => import('./flavors/codenames/CodenamesLiveFlavor'),
  paleo:       () => import('./flavors/paleo/PaleoLiveFlavor'),
  'power-grid':() => import('./flavors/power-grid/PowerGridLiveFlavor'),
  'puerto-rico': () => import('./flavors/puerto-rico/PuertoRicoLiveFlavor'),
  wingspan:    () => import('./flavors/wingspan/WingspanLiveFlavor'),
  zombicide:   () => import('./flavors/zombicide/ZombicideLiveFlavor'),
};
```

The `FlavorRenderer` wraps with `<Suspense fallback={<FlavorLoadingSkeleton />}>`. The session shell renders immediately; the flavor panel shows a skeleton for ~100–300 ms (network + parse) then hydrates.

**Pros**:
- Zero impact on session-live route bundle for users of generic sessions or non-premium games.
- Each flavor chunk is cached by the browser after first load — subsequent navigations to the same game's live session use the cached chunk (no re-fetch).
- Next.js code-splits each `dynamic()` import automatically — one chunk per game flavor.
- Matches the established pattern in `KbGlobaleView.tsx`, `editor/page.tsx`, etc.
- `{ ssr: false }` (client-only) is appropriate because flavor modules use SVG geometry, canvas rendering, and game-state hooks that are inherently client-side. Consistent with `EditorClient` pattern.

**Cons**:
- Cold-start latency: a user navigating to a Catan live session for the first time sees a flavor skeleton for ~100–300 ms (network) + ~20–50 ms (JS parse) before the board renders. This is a one-time cost per flavor per browser cache lifecycle.
- A `Suspense` boundary is required — the `FlavorLoadingSkeleton` must visually integrate with the 3-column shell to avoid layout shift.
- `FLAVOR_MAP` must be maintained as new games are added (O(1) per new entry — low burden).

**Risks**: Low. The `dynamic()` pattern is well-established. Browser chunk cache is persistent across sessions (until `next build` produces new hashes). Layout shift during the skeleton → flavor transition must be designed carefully.

**Impact**: ~2 days. New `flavors/` directory structure, `FlavorRenderer` component, skeleton integration, `{ ssr: false }` configuration.

---

### Option C — Hybrid: top-3 games bundled, others lazy

Bundle Catan, Codenames, and Wingspan (highest-traffic games) statically. Lazy-load the other 4 (Paleo, Power Grid, Puerto Rico, Zombicide).

**Pros**: Eliminates cold-start for the most common games; limits bundle bloat to ~180 KB (3 × ~60 KB).

**Cons**:
- Requires knowing which games are "most popular" at build time — a product assumption that may become stale.
- Creates a two-class system in the codebase: bundled flavors are imported differently from lazy flavors, increasing maintenance complexity.
- Without real traffic data, the "top 3" selection is arbitrary.

**Risks**: Moderate. If traffic patterns shift, the bundled flavors become dead weight.

**Impact**: ~2.5 days. More complex than pure lazy.

---

### Option D — Flavor modules served as external JSON + React components registered on mount

Flavor modules are loaded as external JSON data (game board spec) + pre-registered React components (static registry at build time). Allows the flavor data to change without a new deployment.

**Pros**: Maximum flexibility — flavors can be updated server-side.

**Cons**: Significant infrastructure investment (external flavor registry, dynamic component registry, security review for external component loading). Far out of scope for US-INT-4.

**Impact**: ~10+ days. Excluded.

## Decision

**Adopt Option B**: fully lazy per-game `dynamic()` import with `{ ssr: false }`, organized under `apps/web/src/components/features/session-live/flavors/<game>/`.

**Cache TTL**: rely on Next.js built-in browser chunk cache. Each flavor chunk is content-hashed at build time (Next.js default). Browser caches the chunk indefinitely by content hash — no TTL configuration needed. On next deployment, content hashes change and the browser fetches the updated chunk. No Service Worker or explicit `Cache-Control` header configuration is required for flavor modules.

**Rationale**: Option B is the only option that keeps the session-live initial bundle unchanged for generic sessions while delivering sub-300ms first-paint for flavor panels. The cold-start cost is a one-time per-browser-per-build-cycle event, mitigated by the `Suspense` skeleton. Option C introduces maintenance complexity with no reliable traffic data to justify bundle-bundling choices. Content-hashed chunk caching (Next.js default) solves the re-render during live sessions question: the browser serves the cached chunk from memory/disk after the first load — no re-fetch during a live session.

## Consequences

**Positive**:
- Session-live route bundle size is unaffected for ~93% of sessions (non-premium games or generic sessions).
- Flavor module updates deploy independently — new build hash, browser re-fetches once.
- `Suspense`-based loading integrates naturally with `SessionStateRenderer`'s `kind: 'loading'` skeleton.
- Pattern is consistent and discoverable: all 7 flavors are under `flavors/<game>/`, `FlavorRenderer` is the single dispatch point.

**Negative**:
- First visit to a premium game session incurs ~100–300ms skeleton display. Acceptable given the live-session context (users expect a brief load before live data appears anyway).
- `FLAVOR_MAP` in `FlavorRenderer` must be manually updated when a new premium game is added. Missing entry = no flavor rendered (falls through to generic session shell — safe default).
- `{ ssr: false }` means flavor components do not contribute to server-side rendered HTML — no SEO impact (live session routes are authenticated, not indexed).

**Trade-offs**:
- The `Suspense` fallback skeleton must be carefully designed to avoid Cumulative Layout Shift (CLS) when the flavor mounts. The skeleton should reserve the same space as the flavor panel.
- Content-hashed caching means flavor modules live in the browser cache until the next deployment. During long live sessions (>1 hour) that span a deployment, the user will use the cached (pre-deployment) chunk — no mid-session chunk invalidation. This is the correct behavior for session continuity.

## Implementation Guidance

1. **Directory structure**:
   ```
   apps/web/src/components/features/session-live/
     flavors/
       catan/         CatanLiveFlavor.tsx, CatanSummaryFlavor.tsx
       codenames/     CodenamesLiveFlavor.tsx, CodeNamesSummaryFlavor.tsx
       paleo/         PaleoLiveFlavor.tsx, PaleoSummaryFlavor.tsx
       power-grid/    PowerGridLiveFlavor.tsx, PowerGridSummaryFlavor.tsx
       puerto-rico/   PuertoRicoLiveFlavor.tsx, PuertoRicoSummaryFlavor.tsx
       wingspan/      WingspanLiveFlavor.tsx, WingspanSummaryFlavor.tsx
       zombicide/     ZombicideLiveFlavor.tsx, ZombicideSummaryFlavor.tsx
     FlavorRenderer.tsx      (FLAVOR_MAP + dynamic dispatch)
     FlavorLoadingSkeleton.tsx (Suspense fallback)
   ```

2. **`FlavorRenderer` props**: `gameSlug: string | null`, `view: 'live' | 'summary'`, `sessionData: SessionLiveState`. If `gameSlug` is not in `FLAVOR_MAP`, render `null` (generic session shell handles content).

3. **`dynamic()` configuration**: `{ ssr: false, loading: () => <FlavorLoadingSkeleton /> }`.

4. **Integration in `SessionLiveView`**: replace the center column placeholder with `<FlavorRenderer gameSlug={activeSession.gameSlug} view="live" sessionData={liveState} />`. The `gameSlug` must be added to the session DTO and `LiveSessionFixture` type.

5. **Bundle audit**: after implementation, run `next build` and inspect `.next/static/chunks/` to confirm each flavor is a separate chunk file (not merged into the session-live route chunk). Expected chunk count: 7 live + 7 summary = 14 additional chunks.

6. **`FlavorLoadingSkeleton`**: reserve the center column dimensions (full-height, full-width) with a `bg-muted/30 animate-pulse` placeholder to eliminate CLS.

## Rollback / Reversibility

`FlavorRenderer` is a purely additive component. Rollback = stop importing `FlavorRenderer` in `SessionLiveView.tsx` and remove the `flavors/` directory. Session-live view falls back to the generic 3-column shell — no behavioral regression. Flavor chunks are orphaned in the build but not loaded.

## References

- Catan flavor module — `admin-mockups/design_files/sp4-session-catan-flavor.jsx`
- SP4 game session mockups — `admin-mockups/design_files/sp4-session-{game}-{live,summary}.{html,jsx}`
- `SessionLiveView.tsx` — `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx`
- Existing `dynamic()` usage — `apps/web/src/app/(authenticated)/knowledge-base/global/_components/KbGlobaleView.tsx:56-73`
- `EditorClient` (`ssr: false`) — `apps/web/src/app/(authenticated)/editor/page.tsx:35`
- `SessionStateRenderer` (G7) — `apps/web/src/components/features/session-live/SessionStateRenderer.tsx`
- ADR-071 — live session 5-state FSM (sister ADR)
- Memory: `mini-nav-slot-cta-convention.md`, `route-group-audience-not-feature.md`
