# CI/CD Pipeline — MeepleAI Monorepo

> **Last updated**: 2026-04-09
> **Owner**: DevOps
> **Spec**: docs/superpowers/specs/2026-04-09-ci-cd-optimization (TBD)
> **Implemented by**: PR #335 (Sprint 1), PR #337 (Sprint 2a), PR #338 (Sprint 2b)

## TL;DR

The MeepleAI monorepo runs a **3-stage deployment pipeline** modeled on the Humble/Farley *Continuous Delivery* pattern:

```
[ Stage 1: Commit ]   →   [ Stage 2: Acceptance ]   →   [ Stage 3: Deploy ]
   feature → main-dev      main-dev → main-staging       main-staging → meepleai.app
   ~5-10 min wall-clock     ~60-90 min wall-clock          ~90 min wall-clock
   cheap, fast, frequent    expensive, thorough            gated on green CI
```

Each stage is **strictly gated** on the previous one. The deploy stage will not fire if CI on `main-staging` fails — this is a structural invariant enforced via `workflow_run` triggers, not a developer responsibility.

## Stage 1 — Commit (PR feature/* → main-dev)

**Goal**: fast feedback on every commit. Catch lint, type, unit, and build errors before they reach `main-dev`.

**Triggers**:
- `pull_request: { branches: [main-dev, frontend-dev, backend-dev] }` on `ci.yml`
- `pull_request: { branches: [main-dev] }` on `auto-branch-policy.yml`

**Jobs that run** (typical PR with mixed changes):
| Job | Tool | Wall-clock |
|-----|------|-----------|
| `Detect Changes` | dorny/paths-filter | <30s |
| `Frontend - Build & Test` | pnpm + vitest | ~10 min (only if `apps/web/**` changed) |
| `Backend - Unit Tests` | dotnet test (Category=Unit) | ~5 min (only if `apps/api/**` changed) |
| `Python - Orchestration Tests` | pytest | <2 min (only if `apps/orchestration-service/**` changed) |
| `CI Success` | aggregator gate | <10s |
| `validate-source-branch` | branch policy enforcer | <5s |
| `GitGuardian Security Checks` | GitGuardian app | <30s |

**Jobs that DO NOT run on main-dev PRs** (deferred to Stage 2):
- `Backend - Integration` (3-shard matrix × Postgres+Redis service containers, ~30 min) — runs only when `base_ref == main-staging || main || workflow_dispatch || workflow_call`
- `E2E - Critical Paths` — only on PRs targeting `main-staging` or `main`
- `Backend E2E Tests` (Testcontainers) — only on PRs targeting `main-staging` or `main`
- `Security Scan` (CodeQL) — only on PRs targeting `main-staging` or `main`
- `Performance Tests` (K6 + Lighthouse) — only on PRs targeting `main-staging` or `main`
- `Auto-merge Dependabot PRs` — runs only on PRs targeting `[main, main-dev, main-staging]`

**Branch protection** (main-dev required checks):
1. `CI Success` — aggregate gate of stage 1 jobs
2. `validate-source-branch` — branch policy enforcer
3. `GitGuardian Security Checks` — secret scanning

`enforce_admins: false` means admins can `gh pr merge --admin` for emergencies; this should be a rare exception.

**Typical lead time for a feature PR**: ~5-10 minutes wall-clock.

## Stage 2 — Acceptance (PR main-dev → main-staging)

**Goal**: thorough validation of the integrated state before it becomes a release candidate. Catch integration regressions, security issues, performance regressions, and visual regressions.

**Triggers**:
- `pull_request: { branches: [main-staging] }` on `ci.yml`, `test-e2e.yml`, `backend-e2e-tests.yml`, `security-scan.yml`, `test-performance.yml`, `test-visual.yml`

**Jobs that run** (all enabled at this stage):
| Job | Tool | Wall-clock |
|-----|------|-----------|
| All Stage 1 jobs | (see above) | ~10 min |
| `Backend - Integration (KnowledgeBase)` | dotnet test + Postgres + Redis | ~30 min |
| `Backend - Integration (Games)` | dotnet test + Postgres + Redis | ~30 min |
| `Backend - Integration (Core)` | dotnet test + Postgres + Redis | ~30 min |
| `E2E - Critical Paths` | Playwright (6-shard matrix) | ~30 min |
| `Backend E2E Tests` | xUnit + Testcontainers | ~20 min |
| `Security Scan` | CodeQL + dependency scan | ~10 min |
| `Performance Tests` | K6 + Lighthouse CI | ~30 min |
| `Visual Tests` | Chromatic | ~5 min |

**Total wall-clock**: ~60-90 min (most jobs run in parallel).

**Concurrency**: each PR has its own concurrency group with `cancel-in-progress: true`, so force-push to a release PR cancels in-flight runs.

## Stage 3 — Deploy (push to main-staging → meepleai.app)

**Goal**: deploy the validated commit to the staging environment with full post-deploy verification.

**Triggers** (Spec R2 — Sprint 2 implementation):
- `workflow_run: { workflows: [CI], types: [completed], branches: [main-staging] }` on `deploy-staging.yml`
- `workflow_dispatch` (manual escape hatch)

**Critical invariant**: `deploy-staging.yml` fires **if and only if**:
- `ci.yml` on `main-staging` completes with `conclusion == 'success'`, OR
- An operator manually triggers `workflow_dispatch` (escape hatch)

The gating is enforced by the `gate` job at the entry point of `deploy-staging.yml`:

```yaml
gate:
  name: CI Quality Gate
  if: |
    github.event_name == 'workflow_dispatch'
    || (
      github.event_name == 'workflow_run'
      && github.event.workflow_run.conclusion == 'success'
      && github.event.workflow_run.head_branch == 'main-staging'
    )
```

When `ci.yml` on `main-staging` fails, the gate stays in `skipped` state and every downstream job (`notify-start`, `detect-changes`, `build`, `migrate-db`, `snapshot-baseline`, `deploy`, `validate`, `e2e-staging`) inherits the skip via `needs: gate`. **Result**: zero deploy attempts on red CI.

### `github.sha` gotcha under `workflow_run`

Under `workflow_run` trigger, GitHub executes the workflow in the **default branch context**. `github.sha`, `github.ref`, and `github.head_ref` all point to the default branch (`main-dev`), NOT the commit on `main-staging` that triggered CI.

Two global env vars route around this:

```yaml
env:
  DEPLOY_SHA: ${{ github.event.workflow_run.head_sha || github.sha }}
  DEPLOY_BRANCH: ${{ github.event.workflow_run.head_branch || github.head_ref || github.ref_name }}
```

Every `actions/checkout` step pins `ref: ${{ env.DEPLOY_SHA }}` and every shell reference to "the commit being deployed" uses `${{ env.DEPLOY_SHA }}`. The fallback to `github.sha` preserves correct behavior under `workflow_dispatch`.

### Deploy jobs

| Job | Purpose | Wall-clock |
|-----|---------|-----------|
| `gate` | CI quality gate (Spec R2) | <5s |
| `notify-start` | Slack notification (start) | <10s |
| `detect-changes` | dorny/paths-filter — what to deploy | <30s |
| `pre-deploy-check` | Build verification + CI status sanity check | ~5 min |
| `build` | Docker image build + push to ghcr.io | ~30 min |
| `migrate-db` | EF Core migrations on staging Postgres | ~5 min |
| `snapshot-baseline` | Capture pre-deploy performance baseline | ~2 min |
| `deploy` | SSH + docker compose pull/up on the staging VPS | ~5 min |
| `validate` | Deep health check + smoke tests | ~3 min |
| `e2e-staging` | Playwright E2E against live staging | ~5 min |
| `notify-end` | Slack notification (success/failure) | <10s |

**Total wall-clock**: ~60-90 min depending on what changed.

**Deploy mechanism**: SSH to the staging VPS, `docker pull` from `ghcr.io/meepleAi-app/meepleai-monorepo/{api,web}`, `docker compose up -d --force-recreate`. No Kubernetes, no cloud orchestrator.

**Concurrency**: `group: deploy-staging, cancel-in-progress: false` — staging deploys are serialized to prevent overlapping image swaps.

## Stage 3.5 — Production (manual)

**Triggers**: `workflow_dispatch` only.

**File**: `deploy-production.yml.disabled` (currently disabled, awaiting Production Readiness sign-off).

When enabled, the production deploy pipeline will mirror the staging structure with stricter gates: required approval, OIDC auth, immutable image tags, blue/green deployment.

## Quick reference — workflow inventory

| Workflow | Stage | Trigger | Required check on main-dev? |
|----------|-------|---------|---------------------------|
| `ci.yml` | 1+2 | `pull_request`, `push: main-staging`, `workflow_dispatch`, `workflow_call` | ✅ via `CI Success` |
| `auto-branch-policy.yml` | 1+2 | `pull_request: [main, main-staging, main-dev]` | ✅ via `validate-source-branch` |
| `auto-dependabot.yml` | 1+2 | `pull_request: [main, main-dev, main-staging]` | ❌ |
| `test-e2e.yml` | 2 | `pull_request: [main, main-staging]` | ❌ |
| `backend-e2e-tests.yml` | 2 | `pull_request: [main, main-staging]` | ❌ |
| `security-scan.yml` | 2 | `pull_request: [main, main-staging]`, `schedule` | ❌ |
| `test-performance.yml` | 2 | `pull_request: [main, main-staging]`, `schedule` | ❌ |
| `test-visual.yml` | 2 | `pull_request: [main, main-staging]` | ❌ |
| `deploy-staging.yml` | 3 | `workflow_run: ci.yml on main-staging`, `workflow_dispatch` | n/a (post-merge) |
| `deploy-production.yml.disabled` | 3.5 | `workflow_dispatch` (when enabled) | n/a |
| `rollback.yml` | 3 | `workflow_dispatch` | n/a (manual escape) |

## Common operator tasks

### Open a feature PR

```bash
git checkout main-dev && git pull
git checkout -b feature/issue-XXX-short-description
git config branch.feature/issue-XXX-short-description.parent main-dev
# ... work ...
git push -u origin feature/issue-XXX-short-description
gh pr create --base main-dev --title "..." --body "..."
```

The PR will trigger Stage 1 jobs only. Wait for the 3 required checks (`CI Success`, `validate-source-branch`, `GitGuardian Security Checks`) to go green, then merge.

### Open a release PR

```bash
git checkout main-dev && git pull
gh pr create --base main-staging --head main-dev --title "release: $(date +%Y-%m-%d)" --body "..."
```

The PR will trigger ALL Stage 1 + Stage 2 jobs. Wait ~60-90 min for full validation. After merge, `ci.yml` runs on the resulting push commit on `main-staging`, then `deploy-staging.yml` fires automatically via `workflow_run` if CI is green.

### Emergency deploy (manual)

```bash
gh workflow run deploy-staging.yml --ref main-staging
```

This bypasses the `workflow_run` gate via `workflow_dispatch`. Use only for:
- Recovering from a failed deploy after an infrastructure incident
- Forcing a redeploy of an already-merged commit (e.g., after manual config change on the VPS)
- Hotfix paths that bypass the normal release flow

### Rollback a bad staging deploy

```bash
gh workflow run rollback.yml --ref main-staging --field target_image=ghcr.io/...:previous-tag
```

See `.github/workflows/rollback.yml` for input details.

### Bypass branch protection (admin only)

```bash
gh pr merge <PR-NUMBER> --squash --admin
```

Reserved for emergencies. **Always document the reason in the PR description and Slack.** Each bypass should trigger a follow-up PR to fix the underlying CI issue.

## Cost optimization (Spec R1, Sprint 1)

Before Sprint 1, every PR `feature/* → main-dev` fired:
- ~13 status checks visible in the GitHub UI
- 3-shard backend-integration jobs (~30 min) on every backend touch
- `auto-dependabot` job on every PR (no branch filter)

After Sprint 1 (PR #335):
- ~8-9 status checks visible (most non-required SKIPPED)
- backend-integration deferred to release-candidate PRs (saves ~25 min × N feature PRs/day)
- `auto-dependabot` filtered to release branches only (saves ~30s × N feature PRs/day)
- Branch protection on main-dev marks 3 checks as required (Spec A4)

Net wall-clock improvement: **~15-25 min faster per feature PR with backend changes**.

## Observability (Sprint 3)

- **Weekly CI minutes digest**: posted to Slack `#ci-cost` channel via cron workflow (Spec A6)
- **Per-PR runtime summary**: published as a GitHub step summary at the end of `ci.yml` (TBD)

See `infra/observability/ci-runner-minutes.md` for the dashboard spec (TBD).

## References

- Spec doc: `docs/superpowers/specs/2026-04-09-ci-cd-optimization.md` (TBD)
- Decision log: PR #335 (Sprint 1), PR #337 (Sprint 2a), PR #338 (Sprint 2b)
- Panel synthesis: this session's spec-panel run (Humble + Farley + Forsgren + Nygard + Meadows + Crispin + Beck + Doumont + Wiegers)
- Operations manual: `docs/operations/operations-manual.md`
- Runbook for autosave job stale alert: `docs/operations/runbooks/session-autosave-stale.md`
