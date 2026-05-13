# Mockup-to-route Conformity Gate

> **Issue:** #1069 (WS-C Mockup Conformity Roadmap, umbrella #1066)
> **Status:** WS-C complete (Phase 1–4b) + WS-F.1 (ownership headers + allowlist enforcement) shipped 2026-05-13. WS-F.2 (drift detection workflow) + WS-F.3 (dashboard) ship next.

## Overview

The conformity gate answers a question that the two existing visual-regression workflows do **not**:

| Workflow | Compares | Catches |
|----------|----------|---------|
| `visual-regression-mockups.yml` | mockup HTML vs committed PNG | mockup HTML drift |
| `visual-regression-migrated.yml` | live route vs committed PNG | route implementation drift |
| **`visual-regression-conformity.yml`** (Phase 3) | **live route vs mockup PNG** | **route deviation from canonical mockup** |

Without this gate, an implementation can drift 75–85% from its canonical mockup and every check stays green (this is exactly what the 2026-05-12 audit measured).

## Deliverables shipped

### Phase 1 (PR #1105)

- `apps/web/e2e/visual-conformity/mockup-ownership.schema.json` — JSON Schema for the ownership map (versioned)
- `apps/web/e2e/visual-conformity/mockup-ownership.bootstrap.json` — minimal bootstrap with **2 routes** (AC-C.7)
- `apps/web/scripts/conformity-ownership.ts` — loader, validator, default merger
- `apps/web/scripts/__tests__/conformity-ownership.test.ts` — unit tests

### Phase 2 (this PR)

- `apps/web/playwright.config.ts` — 4 new projects: `conformity-bootstrap-{desktop,mobile}` (generate baselines) + `conformity-verify-{desktop,mobile}` (assert route vs baseline)
- `apps/web/e2e/visual-conformity/bootstrap.spec.ts` — iterates ownership, captures mockup screenshot at `__mockup__/{id}.{viewport}.png`
- `apps/web/e2e/visual-conformity/conformity.spec.ts` — scaffold spec, `test.fixme()`'d until Phase 3 data-mocking parity (rationale in commit)
- `apps/web/scripts/mockup-pin-policy.json` — declarative pin policy (React UMD versions + SRI + preconnects)
- `apps/web/scripts/verify-mockup-pins.ts` — verifier CLI (`pnpm verify:mockup-pins`)
- `apps/web/scripts/__tests__/verify-mockup-pins.test.ts` — 13 unit tests
- `apps/web/package.json` — 4 new scripts (`test:visual:conformity{,:bootstrap{,:update}}`, `verify:mockup-pins`)

The workflows (gate + bootstrap) and per-route `page.route()` data stubs arrive in Phase 3.

## AC-C.8 implementation note (spec reconciliation)

The spec said _"serve-mockups.cjs injects Inter font stack with SHA-384 integrity"_. Implementation diverges deliberately:

| Spec claim | Reality | Phase 2 resolution |
|------------|---------|--------------------|
| Inject Inter font | App uses **Quicksand + Nunito + JetBrains Mono** (via `next/font/google`); mockups already load these from Google Fonts CDN | No injection. Existing setup is already cross-platform deterministic. |
| SHA-384 on font preload | Font binaries don't carry SRI; Google Fonts CSS is dynamically generated and SRI-incompatible | N/A — accepted. |
| React 18.3.1 SRI pin in `serve-mockups.cjs` | Already pinned **in each mockup HTML file** (verified) | Pin policy externalized to `mockup-pin-policy.json`; verifier enforces consistency |

Determinism guarantee: Playwright runs the same Chromium build on Linux (`ubuntu-22.04`, per AC-C.8) for both mockup and route renders, and `document.fonts.ready` waits ensure font binaries are loaded before screenshot. The verifier catches the only remaining drift vector: accidental React version bumps.

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

### Phase 3 (this PR)

- `.github/workflows/bootstrap-mockup-baselines.yml` — workflow_dispatch (with `reason` input) + auto-trigger on `admin-mockups/design_files/**` push to `main-dev`. Runs `pnpm test:visual:conformity:bootstrap:update` on `ubuntu-22.04`, uploads artifact, opens auto-PR with regenerated `__mockup__/*.png` for human review (peter-evans/create-pull-request)
- `.github/workflows/visual-regression-conformity.yml` — PR gate triggered on routes in `mockup-ownership.bootstrap.json` triggerPaths OR shared design surface (tokens / Tailwind / mockup HTML). Runs `pnpm test:visual:conformity`, posts/updates sticky PR comment on failure (AC-C.4), uploads diff triplets with 14-day retention (AC-C.3), honors `conformity-waiver` label (AC-C.5), `concurrency: cancel-in-progress` per PR (AC-C.9)
- `bootstrap.spec.ts` — adaptive `waitForMockupReady` that handles both React-bootstrapped mockups (`#root`/`#desktop-root`/`#mobile-root`) and pure-CSS mockups (Nanolith)

The conformity gate **runs** in CI but every spec is still `test.fixme()`'d pending Phase 3b data mocks. The wiring is present so the next PR that lands `page.route()` stubs can remove fixme guards route-by-route.

### Phase 3b (this PR)

The original Phase 3b plan was to add `page.route()` API stubs per route. That turned out unnecessary: both routes already have deterministic visual-test paths from previous waves.

| Route | Determinism strategy | Source |
|-------|---------------------|--------|
| `/library` | `IS_VISUAL_TEST_BUILD` constant set via `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` at build time → `LibraryHubV2` swaps to curated 12-entry fixture | Wave B.3 (PR #638) |
| `/library/{gameId}` | `?state=default` URL override consumed by `useStateOverride()` → fixture detail, no API call | WS-D Exemplar (PR #1093) |

- `conformity.spec.ts` rewrites the spec body: `seedAuthSession` + `seedCookieConsent` + `mockAuthEndpoints` triple-helper (auth bypass), per-route `RUNBOOKS` table declares `readySelector` + optional `?state=` URL suffix, `mask: [data-dynamic]` for flake-prone zones
- The only remaining `test.fixme()` is the **baseline-missing** guard — self-resolves once `bootstrap-mockup-baselines.yml` dispatches and the auto-PR lands committed `__mockup__/*.png`
- `visual-regression-conformity.yml`: build step now passes `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` so the fixture short-circuit is active in CI

### Phase 4a (this PR)

- `.github/workflows/conformity-waiver-validator.yml` — required check (post-merge branch protection update) that parses PR comments for the structured rationale marker when `conformity-waiver` label is present (AC-C.5.1)
- `.github/workflows/conformity-debt-issue.yml` — on label-add: parses rationale, computes `sha256(PR, sorted(routes), iso_week)` dedup key (AC-C.5.2), creates/updates a `conformity-debt` issue (AC-C.5.3 machine-readable header), appends row to audit log via auto-PR (AC-C.5.5). On PR-closed-unmerged: auto-closes matching debt-issues.
- `docs/for-developers/audits/conformity-waivers.md` — audit log scaffold

### Waiver rationale format (AC-C.5.1)

Apply the `conformity-waiver` label, then post a PR comment containing:

```
> Conformity waiver rationale:
> Reason: <free text, min 40 char — describe why the diff is acceptable to merge>
> Expiry: 2026-06-12
> Routes: library library-game-detail
```

- `Reason` must be ≥ 40 characters.
- `Expiry` is optional. Default = now + 30 days. Maximum = now + 90 days.
- `Routes` must list one or more route ids present in `mockup-ownership.bootstrap.json`.
- The comment author must be the PR author OR a member with `OWNER` / `MEMBER` /
  `COLLABORATOR` association.

The validator fails with a guidance link if the block is missing, malformed, or
the expiry is out of range.

### Branch protection update (manual, post-Phase-4a)

To make the waiver validator block merges, add it to the required-status-check
list on `main-dev`, `main-staging`, and `main`:

```bash
gh api repos/meepleAi-app/meepleai-monorepo/branches/main-dev/protection \
  --method PUT \
  --raw-field required_status_checks='{"strict":false,"contexts":["GitGuardian Security Checks","Conformity Waiver Rationale"]}'
```

(Repeat for `main-staging` and `main`.)

### Phase 4b (this PR)

- `.github/workflows/conformity-debt-gate.yml` — required check on PR `main-dev → main-staging` and `main-staging → main`. Enumerates open `conformity-debt` issues, parses the `<!-- conformity-debt: ...; expiry=...; ... -->` header, fails if ≥1 has `expiry < now`. Posts sticky PR comment summarizing expired and active waivers, with remediation runbook (AC-C.5.4 + AC-C.5.6).

### Branch protection update (manual, post-Phase-4b)

```bash
# main-staging
gh api repos/meepleAi-app/meepleai-monorepo/branches/main-staging/protection \
  --method PUT \
  --raw-field required_status_checks='{"strict":false,"contexts":["GitGuardian Security Checks","Conformity Debt Gate"]}'

# main
gh api repos/meepleAi-app/meepleai-monorepo/branches/main/protection \
  --method PUT \
  --raw-field required_status_checks='{"strict":false,"contexts":["GitGuardian Security Checks","Conformity Debt Gate"]}'
```

### WS-F.1 — Ownership header standard (this PR for WS-F)

Each mockup in the allowlist carries a structured ownership header. Spec: §3 WS-F, AC-F.1b + AC-F.5.

```html
<!--
  @route /library/{gameId}
  @last-verified 2026-05-13
  @verified-by maintainer
  @status canonical
-->
```

Field semantics (AC-F.5 enum normalization):

| Field | Required | Format / Enum |
|-------|----------|---------------|
| `@route` | yes | One or more space-separated route paths starting with `/`. Use `{placeholder}` for dynamic segments. |
| `@last-verified` | yes | `YYYY-MM-DD` ISO date. |
| `@verified-by` | yes | `designer` · `maintainer` · `spec-panel-review` · `auto-bootstrap`. `spec-panel-review` requires `[spec-panel-validated]` in the commit message. |
| `@status` | yes | `canonical` (green, verified ≤30d) · `verified` (yellow, verified >30d) · `drifted` (orange, gate failing) · `pending-implementation` (blue, orphan) · `archived` (gray, retired). |

Enforcement (AC-F.1b progressive):

- 5 mockup core in `ALLOWLIST` (sp4-library-desktop, nanolith-runthrough-game-detail, sp4-game-detail, sp4-agents-index, sp4-sessions-index): missing/invalid header → CI error.
- Other mockups: warning only. Allowlist expands by explicit PR edit to `apps/web/scripts/mockup-ownership-check.ts`.

Local validation:

```bash
cd apps/web
pnpm verify:mockup-ownership
```

CI workflow: `.github/workflows/mockup-ownership-validator.yml` (required check on PR touching `admin-mockups/design_files/**`).

## What ships next

| Phase | Deliverable |
|-------|-------------|
| **F.2** | `mockup-drift-detect.yml` workflow — PR-triggered (SLO ≤5min) + cron daily fallback. Generates companion issue when a mockup change touches a route in the WS-C conformity gate ownership map (AC-F.4) |
| **F.3** | Dashboard generator `mockup-ownership-summary.yml` weekly cron, publishes `docs/for-developers/audits/mockup-ownership-status.md` with status enum coloring + age (AC-F.5 dashboard) |
| **4c** (opt) | Weekly observability summary `conformity-waivers-summary.md` (AC-C.5.7) |
| **post-merge** | Dispatch `bootstrap-mockup-baselines.yml` workflow → auto-PR lands `__mockup__/*.png` → conformity gate produces real diff (expected: large, documenting the 75-85% pre-remediation gap) |

## Running locally

```bash
cd apps/web

# Generate / regenerate mockup baselines under __mockup__/ (committed to repo)
pnpm test:visual:conformity:bootstrap:update

# Verify route vs committed baseline (after Phase 3 mocks land)
pnpm test:visual:conformity

# Sanity-check the pin policy across ownership-mapped mockups
pnpm verify:mockup-pins
```

## References

- Umbrella: [#1066](https://github.com/meepleAi-app/meepleai-monorepo/issues/1066)
- WS-C issue: [#1069](https://github.com/meepleAi-app/meepleai-monorepo/issues/1069)
- Spec: `docs/for-developers/specs/2026-05-12-mockup-conformity-roadmap.md` §3 WS-C
- Sibling: `docs/for-developers/testing/frontend/visual-state-coverage.md` (WS-D Foundation)
