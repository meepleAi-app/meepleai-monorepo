# Mockup-to-route Conformity Gate

> **Issue:** #1069 (WS-C Mockup Conformity Roadmap, umbrella #1066)
> **Status:** Phase 1 shipped 2026-05-13 — config + loader. Workflows + Playwright projects ship in Phase 2/3.

## Overview

The conformity gate answers a question that the two existing visual-regression workflows do **not**:

| Workflow | Compares | Catches |
|----------|----------|---------|
| `visual-regression-mockups.yml` | mockup HTML vs committed PNG | mockup HTML drift |
| `visual-regression-migrated.yml` | live route vs committed PNG | route implementation drift |
| **`visual-regression-conformity.yml`** (Phase 3) | **live route vs mockup PNG** | **route deviation from canonical mockup** |

Without this gate, an implementation can drift 75–85% from its canonical mockup and every check stays green (this is exactly what the 2026-05-12 audit measured).

## Phase 1 deliverables (this PR)

- `apps/web/e2e/visual-conformity/mockup-ownership.schema.json` — JSON Schema for the ownership map (versioned)
- `apps/web/e2e/visual-conformity/mockup-ownership.bootstrap.json` — minimal bootstrap with **2 routes** (AC-C.7)
- `apps/web/scripts/conformity-ownership.ts` — loader, validator, default merger
- `apps/web/scripts/__tests__/conformity-ownership.test.ts` — unit tests

The Playwright spec, workflows, and committed baselines arrive in Phase 2/3.

## Bootstrap route set (AC-C.7)

The bootstrap intentionally maps only two routes so the gate can ship without depending on WS-F (ownership metadata):

| Route | Mockup | Rationale |
|-------|--------|-----------|
| `/library` | `sp4-library-desktop.html` | Wave B.3 stable baseline (PR #638). Brownfield migration complete, sentinel `?state=` pattern in place. |
| `/library/{gameId}` | `nanolith-runthrough-game-detail.html` | Synergistic with WS-B (#1068) fix and WS-D Exemplar (#1093). Pre-fix gap ~95%. |

WS-F (#1072) will generalize the schema and auto-discover route↔mockup pairs.

## Conformity formula (AC-C.2)

The gate uses Playwright's built-in screenshot comparison, which is a thin wrapper over `pixelmatch`:

```ts
await expect(page).toHaveScreenshot('library.desktop.png', {
  threshold: route.threshold,           // per-pixel YIQ sensitivity (default 0.1)
  maxDiffPixelRatio: route.conformityRatio, // aggregate pass criterion (default 0.05)
});
```

Definitions:

| Parameter | Meaning | Default |
|-----------|---------|---------|
| `threshold` | Per-pixel YIQ color-space delta below which two pixels are considered identical. Range `[0, 1]`. Lower = stricter. | `0.1` |
| `maxDiffPixelRatio` | `mismatchedPixels / totalPixels` must be **less than** this for the test to pass. Range `[0, 1]`. | `0.05` |

### Numerical example

A 1280×720 viewport has **921,600** total pixels. With `maxDiffPixelRatio: 0.05` (5%):

- Pass: ≤ 46,080 pixels differ (after `threshold` filter)
- Fail: ≥ 46,081 pixels differ

If a route has tight margins (e.g. complex gradients that always anti-alias differently), override per route in `mockup-ownership.bootstrap.json`:

```json
{
  "id": "library",
  "conformityRatio": 0.08,
  "threshold": 0.15
}
```

Both fields are validated to `[0, 1]` by the loader. Tighter overrides (lower values) are encouraged once a route stabilizes.

## Default viewports

Bootstrap defaults match the canonical mockup viewports:

| Viewport | Dimensions | Source |
|----------|------------|--------|
| `desktop` | 1280×720 | Wave B/C/D pattern |
| `mobile` | 375×740 | Wave B/C/D pattern |

Routes can override per-viewport (e.g. add `tablet`) via `viewports[]` on the route entry.

## Editing the ownership map

1. Edit `apps/web/e2e/visual-conformity/mockup-ownership.bootstrap.json`. The file references `mockup-ownership.schema.json` via `$schema` — your IDE will validate inline.
2. Run the loader test to confirm the change is valid:
   ```bash
   cd apps/web
   pnpm vitest run scripts/__tests__/conformity-ownership.test.ts
   ```
3. Commit the JSON change. Once Phase 3 ships, the workflow will pick up the change automatically.

### Adding a new route

Append to `routes[]`:

```json
{
  "id": "sessions-live",
  "livePath": "/sessions/{sessionId}/live",
  "liveFixture": { "sessionId": "demo-session" },
  "mockup": "sp4-session-live.html",
  "triggerPaths": [
    "apps/web/src/app/(authenticated)/sessions/[sessionId]/live/**",
    "apps/web/src/components/features/sessions/**"
  ],
  "notes": "Wave D.2 (PR #749 + #753 + #754)"
}
```

Rules enforced by the loader:

- `id` must be unique and match `^[a-z][a-z0-9-]*$` (slug)
- `livePath` must start with `/`
- Every `{placeholder}` in `livePath` must have a matching key in `liveFixture`
- `mockup` must end with `.html` (relative to `admin-mockups/design_files/`)
- `triggerPaths` must be a non-empty array of globs

Adding a route does **not** yet make Phase 3 enforce it — until the workflow ships, this is data-only.

## What ships in Phases 2/3/4

| Phase | Deliverable |
|-------|-------------|
| **2** | `playwright.config.ts` projects `conformity-desktop` + `conformity-mobile`; `apps/web/e2e/visual-conformity/library.spec.ts` + `library-game-detail.spec.ts`; font lock in `serve-mockups.cjs` (AC-C.8) |
| **3** | `.github/workflows/visual-regression-conformity.yml` (gate) + `bootstrap-mockup-baselines.yml` (manual + auto-trigger on mockup change); committed `__mockup__/*.png` baselines |
| **4** | `docs/for-developers/audits/conformity-waivers.md` audit log; waiver issue automation (AC-C.5) |

## References

- Umbrella: [#1066](https://github.com/meepleAi-app/meepleai-monorepo/issues/1066)
- WS-C issue: [#1069](https://github.com/meepleAi-app/meepleai-monorepo/issues/1069)
- Spec: `docs/for-developers/specs/2026-05-12-mockup-conformity-roadmap.md` §3 WS-C
- Sibling: `docs/for-developers/testing/frontend/visual-state-coverage.md` (WS-D Foundation)
