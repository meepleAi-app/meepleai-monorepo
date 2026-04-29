# Bundle-size budget enforcement

> Wave A closeout — Step 4 (Issue #629).
> CI gate: `frontend-bundle-size` job in `.github/workflows/ci.yml`.

The frontend has a per-route **First Load JS** budget gate that fails any PR
exceeding the budget by more than the configured tolerance (default **5%**).
This document explains what is measured, how to operate the gate, and how to
adjust budgets when a justified bundle increase needs to land.

## What is measured

Next.js 16 (Turbopack) emits `.next/diagnostics/route-bundle-stats.json` during
`pnpm build`. For each app-router route the file lists:

- `route` — route path (e.g. `/shared-games/[id]`).
- `firstLoadUncompressedJsBytes` — uncompressed total JS the browser must
  download on first visit (vendor + framework + page-specific).
- `firstLoadChunkPaths` — concrete `.next/static/chunks/*.js` files that make
  up that First Load.

The check script gzips each chunk (level 9) and sums the compressed bytes —
this is the canonical user-perceived load size and matches what most CDNs
serve. The gate compares the measured gzipped sum to the budget in
`apps/web/.bundle-budgets.json`.

## Source of truth

`apps/web/.bundle-budgets.json` is the **only** place where budgets live.
Schema:

```json
{
  "description": "...",
  "updatedAt": "YYYY-MM-DD",
  "tolerance": 0.05,
  "routes": {
    "/route/path": {
      "gzippedBytes": 123456,
      "note": "free-form context for reviewers"
    }
  }
}
```

- `tolerance`: fraction over budget that is still allowed (e.g. `0.05` = 5%).
- `gzippedBytes`: hard target. The gate fails when measured size exceeds
  `floor(gzippedBytes * (1 + tolerance))`.
- `note`: short context (baseline date, aspirational target, optimization
  notes). Reviewers read this first when a budget update lands.

## Budgeted routes (initial seed, 2026-04-29)

The seed reflects the **measured baseline** at the time #629 landed, not an
aspirational ideal — the issue explicitly excludes "editing route code to fit
budgets" from this work. Budgets give us a regression floor; future
optimization passes will tighten them.

| Route | Baseline (gzipped) | Notes |
|-------|--------------------|-------|
| `/shared-games` | ~557 KB | Wave A.3b catalog (PR #600). |
| `/shared-games/[id]` | ~592 KB | Wave A.4 detail (PR #605). Aspirational target **45 KB** documented in #629; deferred to a future optimization pass. |
| `/discover` | ~605 KB | — |
| `/library` | ~645 KB | — |
| `/chat/[threadId]` | ~617 KB | — |

> **Note on the 45 KB target.** Issue #629 references "45 KB gzipped JS per
> Wave A.4 spec §SLO" for `/shared-games/[id]`. The Wave A.4 spec
> ([2026-04-28-v2-migration-wave-a-4-shared-game-detail.md](../superpowers/specs/2026-04-28-v2-migration-wave-a-4-shared-game-detail.md))
> actually defines a **+30 KB delta** ceiling vs the prior baseline, not a
> 45 KB absolute. The 45 KB number is an aspirational target — keep the floor
> at the measured baseline so the gate enforces "no regression", and reduce
> the budget in dedicated optimization PRs.

## Local workflow

```bash
cd apps/web
pnpm build           # produces .next/diagnostics/route-bundle-stats.json
pnpm bundle:check    # validates against .bundle-budgets.json
```

Sample output:

```
✅ /shared-games                   556.9 KB (budget 556.9 KB, Δ +0.00%, max 584.7 KB)
✅ /shared-games/[id]              592.1 KB (budget 592.1 KB, Δ +0.00%, max 621.7 KB)
...
✅ All 5 routes within budget (tolerance 5%).
```

Glyphs:

- ✅ — measured ≤ budget.
- ⚠️ — measured > budget but ≤ `budget × (1 + tolerance)` (passes; consider
  trimming).
- ❌ — measured > `budget × (1 + tolerance)` → CI fails.

The script also writes `apps/web/.next/diagnostics/bundle-budget-report.json`
with machine-readable per-route results — CI uploads this as the
`bundle-budget-report-<run>` artifact (30-day retention).

## CI behaviour

`frontend-bundle-size` is a **blocking** job:

- Triggered when `dorny/paths-filter` detects changes under `apps/web/**`,
  `apps/web/package.json`, or `pnpm-lock.yaml` (same scope as the other
  frontend jobs).
- Builds the app, runs `pnpm bundle:check`.
- Aggregated into `ci-success` so a budget violation blocks the PR.
- Always uploads the JSON report so reviewers can inspect deltas even on
  failure.

## When the gate fails

A failure means: a route now ships more First Load JS than its budget × 1.05
allows. The default response is **investigate, do not bump the budget**.

1. **Read the per-route line** — the offender is the route with `❌`.
2. **Check the diff** for new dependencies, new client components, or new
   barrel-file re-exports landing on a server route.
3. **Use `pnpm build:analyze`** (`@next/bundle-analyzer`) to inspect what
   landed in the chunk: opens an interactive treemap.
4. **Fix the regression** by code-splitting (`next/dynamic`, lazy imports),
   removing unused dependencies, or moving client logic behind a server
   boundary.

A budget bump is only acceptable when:

- The growth is **intentional** (a feature explicitly authorized to enlarge
  the bundle, e.g. a new mandatory editor on a route).
- The diff is reviewed by someone other than the author.
- The `note` field is updated with a one-line rationale + linking PR/issue.
- Ideally, the bump goes in its own PR titled `chore(bundle): bump <route>
  budget …`, kept small for git-blame visibility.

## Bumping a budget

```bash
cd apps/web
pnpm build
pnpm bundle:check    # confirms the new measurement
# edit .bundle-budgets.json:
#   - update "gzippedBytes" to the new measured value (or measured + small
#     headroom)
#   - update "updatedAt" to today
#   - update "note" with rationale + PR/issue link
git add .bundle-budgets.json
git commit -m "chore(bundle): bump <route> budget — <rationale>"
```

## Adding a new budgeted route

When a new public route ships, add it to the seed:

1. Build the app: `pnpm build`.
2. Look up the measured gzipped size in
   `.next/diagnostics/bundle-budget-report.json` (the script reports the
   measured value alongside the budget when the route is already listed; for
   a new route, run the parser inline or just inspect the chunk paths in
   `route-bundle-stats.json`).
3. Add an entry to `.bundle-budgets.json` with `gzippedBytes` =
   measured-current and a `note` documenting the baseline date.
4. Commit alongside the route landing PR.

The script fails with a configuration error (exit 2) when a route in the
budgets file is missing from the build output — this catches renames and
removals.

## Related infra

- `apps/web/__tests__/bundle-size.test.ts` — global `.next/static/chunks/*.js`
  total budget (Vitest, `apps/web/bundle-size-baseline.json`). Complements the
  per-route gate: the global test catches background bloat across all chunks
  even when no individual route changes; the per-route gate catches bloat
  isolated to a budgeted route.
- `pnpm build:analyze` — manual treemap via `@next/bundle-analyzer`.

## References

- Issue #629 — *FE: bundle-size tracker (size-limit) for public routes*.
- Issue #617 / Task #73 — Wave A.4 polish (the original 45 KB ask, deferred
  here).
- Wave A umbrella #579.
- ADR-053 — Wave A.4 spec.
