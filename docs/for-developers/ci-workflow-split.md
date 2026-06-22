# CI Workflow Split

> Status: Implemented (issue #846, ADR-054).

This document explains how GitHub Actions are organised across the three long-lived branches (`main-dev`, `main-staging`, `main`). For the strategic rationale see [ADR-054 — DevOps Multi-Branch Strategy](../for-claude/architecture/adr/adr-054-devops-multi-branch-strategy.md).

## Branch → Workflow mapping

| Branch event | Workflow | Scope | SLO | Blocking |
|---|---|---|---|---|
| PR → `main-dev` | `dev-fast.yml` | lint + typecheck + unit | < 3 min | Yes |
| Push → `main-dev` (post-merge) | `dev-async.yml` | integration + E2E smoke | < 15 min | No |
| PR → `main-staging` | `staging.yml` (+ legacy `ci.yml`) | full suite + a11y + visual | < 10 min | Yes |
| PR → `main` | `prod.yml` (+ legacy `ci.yml`) | full + security scan + load probe | < 30 min | Yes |

### What each workflow covers

- **`dev-fast.yml`** — hot path. Lint (`pnpm lint`), TypeScript typecheck (`pnpm typecheck`), Vitest unit tests with `--run`, and `dotnet build` + `Category=Unit` xUnit tests. Path-filtered: docs-only PRs short-circuit. Concurrency cancels in-progress runs for the same PR.
- **`dev-async.yml`** — non-blocking post-merge sweep. Integration tests against Testcontainers Postgres and Playwright E2E smoke specs tagged `@smoke`. Failures here do NOT block PR merges; once the auto-revert bot (#843) is in place, sustained red on `main-dev` will trigger a revert.
- **`staging.yml`** — staging gate, additive over `ci.yml`. Adds `axe-core` accessibility runs and Playwright visual-regression snapshots. Both `continue-on-error: true` during the baseline ramp-up (#807, #1015) — flipped to blocking when the baseline is established.
- **`prod.yml`** — production gate, additive over `ci.yml`. Adds `pnpm audit`, `dotnet list package --vulnerable`, a Semgrep OWASP scan, and a k6 smoke load (10 VUs × 30 s with `p95 < 1500 ms` and `<5%` error-rate thresholds). All `continue-on-error: true` until baselines are captured (#850).

## Concurrency policy

Every workflow declares:

```yaml
concurrency:
  group: <workflow>-${{ github.event.pull_request.number || github.ref }}
  cancel-in-progress: true
```

This kills stale runs when a new commit lands on the same PR/branch, saving GitHub minutes (target ~3.1 k min/month per #850).

## Composite actions

The new workflows reuse the existing composite actions in `.github/actions/`:

- `setup-frontend` — pnpm + Node + dependencies with cache-key including `runner.arch` to avoid x86/ARM64 collisions.
- `setup-backend` — .NET SDK + NuGet cache.
- `setup-playwright` — browsers + system deps for E2E.

If you add new composite actions, prefer extending `setup-frontend`/`setup-backend` over duplicating logic.

## Required status checks (branch protection)

The branch protection rules currently track `GitGuardian Security Checks` only on `main-dev`. As part of the rollout for this workflow split, branch protection should be updated to also require:

- On `main-dev`: `dev-fast / frontend-fast` AND `dev-fast / backend-fast` (added after a green run lands).
- On `main-staging`: existing `ci.yml` jobs + `staging / a11y` + `staging / visual-regression` (visual + a11y as warn-only until baseline lands).
- On `main`: existing `ci.yml` jobs + `prod / security-scan` + `prod / load-probe` (warn-only initially).

Updating branch protection is a manual GitHub admin action that intentionally is NOT done inside this PR — it requires a green run of each new workflow first so the check names are registered. The rollout sequence is documented in epic #842.

## Legacy `ci.yml` — co-existence and deprecation plan

`ci.yml` continues to trigger on `pull_request: [main, main-staging]` and `push: [main-staging]`. It overlaps with `staging.yml` and `prod.yml`. This co-existence is intentional during the rollout:

1. The new workflows ship without breaking the existing safety net.
2. After 1-2 weeks of green runs on the new workflows, the overlapping jobs in `ci.yml` are pruned.
3. `ci.yml` is renamed to `legacy-ci.yml` and scheduled for removal in a follow-up PR.

The follow-up PR is tracked under the [#842](https://github.com/meepleAi-app/meepleai-monorepo/issues/842) close-out criteria.

## Adding a new workflow

When introducing a new workflow file, decide:

1. **Which branch event?** PR → blocking, push → async, schedule → cron.
2. **Path filters?** Exclude docs-only / unrelated paths so the workflow doesn't burn minutes on irrelevant changes.
3. **Concurrency group?** Cancel in-progress for the same PR/branch.
4. **Timeout?** Default 30 min; lower is better.
5. **Required vs. warn-only?** New checks should be `continue-on-error: true` until a baseline exists.

If the workflow is required-by-branch-protection, update this document and the protection rule in the same rollout window.
