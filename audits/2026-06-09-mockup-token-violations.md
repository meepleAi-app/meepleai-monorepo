# Mockup Token Violations — Inventory (DS-17-2)

| Field | Value |
|---|---|
| **Date** | 2026-06-09 |
| **Generator** | `pnpm lint:tokens:mockups` (DS-17-2) |
| **Spec** | [`2026-06-09-mockup-to-app-drift-spec-panel-review.md`](../docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md) |
| **Scope** | `admin-mockups/**/*.{html,jsx,css}` |
| **Total violations** | 1500 |
| **Files scanned** | 334 |
| **Files affected** | 13 |
| **Baseline ceiling** | 1500 |

## Forbidden legacy families

These names will be REMOVED in DS-16 (token bridge unwind). New mockup work must use the canonical semantic tokens (`--background`, `--foreground`, `--muted-foreground`, `--card`, `--border`, `--primary`, …).

| Family | Pattern | Count |
|---|---|---|
| `bg-base` | `var(--bg-base)` | 12 |
| `gaming-*` | `var(--gaming-*)` | 0 |
| `nh-*` | `var(--nh-*)` | 0 |
| `e-*` | `var(--e-*)` | 1488 |

## Violations by file

| File | Count |
|---|---|
| `admin-mockups/mockup-meeplecard/meeple-card-visual-test.html` | 273 |
| `admin-mockups/standalone/meeple-card-visual--multi-entity-grid.html` | 210 |
| `admin-mockups/standalone/meeple-card-visual--table-view.html` | 210 |
| `admin-mockups/standalone/meeple-card-visual--grid-view.html` | 204 |
| `admin-mockups/standalone/meeple-card-visual--flip-card.html` | 201 |
| `admin-mockups/standalone/meeple-card-visual--list-view.html` | 201 |
| `admin-mockups/standalone/meeple-card-visual--carousel-3d.html` | 189 |
| `admin-mockups/standalone/play-records--detail.html` | 2 |
| `admin-mockups/standalone/play-records--list.html` | 2 |
| `admin-mockups/standalone/play-records--new-step1-game.html` | 2 |
| `admin-mockups/standalone/play-records--new-step2-players.html` | 2 |
| `admin-mockups/standalone/play-records--new-step3-summary.html` | 2 |
| `admin-mockups/standalone/play-records--stats.html` | 2 |

## CI gate semantics

- Default (`pnpm lint:tokens:mockups`) = inventory only, exit 0.
- Strict (`pnpm lint:tokens:mockups --strict --max-baseline N`) = exit 1 when count > N. CI passes N as a frozen ceiling; introducing a NEW violation fails the gate.
- Whitelist is intentional (plan §7 R3): existing mockup literals carried over from the bridge era are tolerated until DS-16 unwinds the bridge upstream.

## Refs

- Sub-issue: [#2070](https://github.com/meepleAi-app/meepleai-monorepo/issues/2070)
- Umbrella: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)
- Plan: [`2026-06-09-ds-17-phase-1-implementation-plan.md`](../docs/superpowers/plans/2026-06-09-ds-17-phase-1-implementation-plan.md) §4.1
- Companion JSON: [`2026-06-09-mockup-token-violations.json`](./2026-06-09-mockup-token-violations.json)
