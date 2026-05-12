# Visual State Coverage

> **Issue:** #1070 (WS-D Mockup Conformity Roadmap, umbrella #1066)
> **Status:** Foundation shipped 2026-05-12

## Overview

Each mockup in `admin-mockups/design_files/*.html` declares canonical states in a header comment (e.g. `Stati: default · loading · error · not-found`). The Foundation tooling extracts those states into a machine-readable matrix and provides:

- `apps/web/scripts/extract-mockup-states.ts` — parser (jsdom)
- `apps/web/e2e/state-coverage/state-matrix.json` — inventory
- `apps/web/e2e/state-coverage/state-matrix.schema.json` — JSON Schema sidecar (IDE validation)
- `apps/web/src/lib/visual-test/state-override.ts` — `?state=` URL override helper
- `.github/workflows/state-coverage-check.yml` — CI gate

## Workflow

### When a mockup is edited

1. Edit `admin-mockups/design_files/<mockup>.html`. Update `Stati:` line if states change.
2. Regenerate matrix locally:
   ```bash
   cd apps/web
   pnpm tsx scripts/extract-mockup-states.ts --write
   ```
3. Commit both the mockup change and the regenerated `state-matrix.json`:
   ```bash
   git add admin-mockups/design_files/<mockup>.html apps/web/e2e/state-coverage/state-matrix.json
   git commit -m "feat(mockup): update <feature> states"
   ```

If you forget step 2, CI will fail at the `state-coverage` job with a guidance comment on the PR.

### When a state gets test coverage

After implementing a Playwright test that exercises a specific declared state for a route:

1. Open `apps/web/e2e/state-coverage/state-matrix.json`.
2. Find the entry by `mockup_path`.
3. Add the state name to `covered_states`:
   ```json
   {
     "mockup_path": "admin-mockups/design_files/nanolith-runthrough-game-detail.html",
     "route": "/library/{gameId}",
     "declared_states": ["default", "loading", "error", "not-found"],
     "covered_states": ["loading"],
     "missing": ["default", "error", "not-found"],
     "enforced": false
   }
   ```
4. Or run `pnpm tsx scripts/extract-mockup-states.ts --write` to let the parser recompute `missing` for you.

### When a route is ready for enforcement

Once all declared states have test coverage and the team wants to block regression:

1. Set `enforced: true` on the matrix entry.
2. Increment `enforced_count` accordingly.
3. CI `--enforced-only` check now blocks PRs that leave declared states uncovered.

## CLI reference

```bash
# Dry-run: exit 1 if matrix.json out-of-sync with mockup HTML
pnpm tsx scripts/extract-mockup-states.ts --check

# Regenerate matrix.json (preserves covered_states + enforced)
pnpm tsx scripts/extract-mockup-states.ts --write

# Validate enforced entries have no missing states
pnpm tsx scripts/extract-mockup-states.ts --enforced-only
```

## Using `state-override.ts` in a route component (PR2 Exemplar onward)

```tsx
'use client';
import { useStateOverride } from '@/lib/visual-test/state-override';

const LIBRARY_GAME_DETAIL_STATES = ['default', 'loading', 'error', 'not-found'] as const;

export default function LibraryGameDetailPage() {
  const stateOverride = useStateOverride(LIBRARY_GAME_DETAIL_STATES);
  // stateOverride is `null` in production (early-return after Rules-of-Hooks call).
  // In `?state=loading` visual test mode, returns 'loading' for branching.

  if (stateOverride === 'loading') return <SkeletonView />;
  if (stateOverride === 'error') return <ErrorBoundary />;
  // ... real data fetch + render
}
```

**Production guarantee**: `NEXT_PUBLIC_VISUAL_TEST_BUILD=1` is set **only** by `playwright.config.ts:webServer.env`. Production `pnpm build` never sets the flag → `useStateOverride` early-returns null after the unconditional `useSearchParams()` call. `next/navigation` is already part of the bundle, so no incremental cost.

## Distinction vs other visual workflows

| Workflow | Scope |
|---|---|
| `visual-regression-mockups.yml` | Mockup HTML → baseline PNG (visual stability of mockups) |
| `visual-regression-migrated.yml` | Route live → baseline PNG (implementation stability) |
| **`state-coverage-check.yml`** | Mockup `Stati:` ↔ matrix.json consistency (declarative gate) |
| `visual-regression-conformity.yml` (WS-C future) | Route ↔ mockup pixel diff (visual gate) |

No overlap. The state coverage gate is **declarative** (state names match), not **visual** (pixel diff).

## Known limitations

- Mockups with hybrid `A · state-name` declaration format (e.g. `nanolith-runthrough-error-states.html`) may extract the prefix letter as a state name; manual cleanup of `state-matrix.json` after first run is acceptable.
- State proliferation cap: keep canonical states to 4-6 per route. Document timing-dependent states (SSE, real-time) as exceptions in PR description and cover via unit tests rather than visual.

## References

- Spec: [`docs/for-developers/specs/2026-05-12-ws-d-state-coverage-design.md`](../../specs/2026-05-12-ws-d-state-coverage-design.md)
- Roadmap: [`docs/for-developers/specs/2026-05-12-mockup-conformity-roadmap.md`](../../specs/2026-05-12-mockup-conformity-roadmap.md) §3 WS-D
- Plan: [`docs/for-developers/plans/2026-05-12-ws-d-foundation.md`](../../plans/2026-05-12-ws-d-foundation.md)
- Umbrella issue: #1066
- Tracking issue: #1070
