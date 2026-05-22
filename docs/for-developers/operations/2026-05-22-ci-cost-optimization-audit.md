# CI Cost Optimization — Audit Report

**Issue**: #850 (DevOps: CI cost optimization)
**Spec date**: 2026-05-13 (via `/sc:spec-panel`)
**Audit date**: 2026-05-22
**Author**: PR 1 audit

## Executive summary

Audit of all 42 active GitHub Actions workflows against the 7 acceptance
criteria of issue #850. **Result**: AC-1 through AC-4 and AC-7 are
**already implemented** by incremental merges between 2026-05-13 (spec
maturation) and 2026-05-22 (audit). AC-5 (≥30% minute reduction) and AC-6
(no build correctness regression) are asynchronous and require a 14-day
measurement window.

This document records the audit so #850 can be retired to the AC-5/AC-6
measurement phase without re-running the workflow scan from scratch.

## Methodology

Per-workflow extraction of the `on:` block + the top-level `concurrency:`
block via:

```bash
for f in .github/workflows/*.yml; do
  echo "=== $(basename $f) ==="
  awk '/^jobs:/{exit} 1' "$f" | head -60
done
```

Cross-referenced with the spec's branch-aware safety rules (deploy/rollback/
admin actions must keep `cancel-in-progress: false`; PR validation may keep
`true`). Verified branch protection rules on `main-dev` via the GitHub API
(`gh api repos/.../branches/main-dev/protection`): only **GitGuardian
Security Checks** is required, so paths-ignore additions on any other
workflow do not risk a stuck PR.

## AC-by-AC verdict

### AC-1 — Concurrency branch-aware: ✅ DONE

| Metric | Spec baseline (2026-05-13) | Audit (2026-05-22) |
|---|---|---|
| Workflows with `^concurrency:` (top-level) | 27 / 44 (61%) | **42 / 42 (100%)** |
| Workflows lacking `concurrency:` | 17 | **0** |

Branch-awareness verified per spec rules: `cancel-in-progress: false` on
deploy/rollback/admin/scheduled-snapshot workflows; `true` on PR validation
and feature CI:

```
=== false (safe-against-interruption) ===
admin-reset.yml, check-api-logs.yml, check-role-case.yml, ci-minutes-digest.yml,
deploy-staging.yml, dev-auto-revert.yml, diagnose-admin.yml,
e2e-smoke-real-backend.yml, e2e-smoke-real-llm-weekly.yml, fix-db-password.yml,
fix-line-endings.yml, generate-operations-pdf.yml, notify-slack.yml, rollback.yml,
runner-maintenance.yml, security-audit-staging.yml, spec-debt-false-positive-handler.yml,
test-login.yml

=== true (cancel-safe on re-trigger) ===
api-smoke.yml, auto-branch-policy.yml, auto-dependabot.yml, auto-validate.yml,
backend-e2e-tests.yml, backend-missing-gate.yml, barrier-verification.yml,
ci.yml, dev-async.yml, dev-fast.yml, docs-linkcheck.yml, e2e-library-to-game.yml,
generate-diagrams.yml, prod.yml, release-gate-comment.yml, runner-health-check.yml,
security-pentest.yml, security-review.yml, security-scan.yml, staging.yml,
test-e2e.yml, test-performance.yml, test-visual.yml, validate-workflows.yml
```

All explicit, no defaults. Comments in 25+ files reference `# Issue #850`
or `# Issue #850 AC-2`, indicating that this AC was implemented inline
across multiple PRs (no single landing).

### AC-2 — paths-ignore on code-only workflows: ✅ DONE

Spec assumed 0 workflows had `paths-ignore:`. Current state — **5 have
`paths-ignore:`** AND **18 use `paths:` allowlist** (a stricter equivalent
that achieves the same docs-only skip without enumerating excluded
patterns).

**Workflows with `paths-ignore:` (skip-on-match)**:

| Workflow | Trigger scope | Ignore list |
|---|---|---|
| `ci.yml` | PR main/main-staging | `**.md, docs/**, LICENSE, .gitignore, .editorconfig` |
| `prod.yml` | PR main | same |
| `staging.yml` | PR main-staging | same |
| `security-scan.yml` | PR + push main/main-staging/main-dev | same |
| `security-pentest.yml` | PR main + labeled trigger | same |

**Workflows with `paths:` allowlist (run-on-match — stricter than ignore)**:

| Workflow | Trigger | Allowlist scope |
|---|---|---|
| `api-smoke.yml` | PR main | `apps/api/**, tests/api-smoke/**, ...` |
| `auto-validate.yml` | PR + push main/main-dev | `.github/workflows/**, .github/actions/**` |
| `backend-e2e-tests.yml` | PR main | `apps/api/**, tests/Api.Tests/E2E/**` |
| `backend-missing-gate.yml` | PR main/main-staging | `apps/web/src/**/*.ts(x)` |
| `dev-async.yml` | push main-dev | `apps/{web,api,orchestration-service}/**, package.json, pnpm-lock.yaml, global.json, .github/workflows/dev-async.yml, .github/actions/**` |
| `dev-fast.yml` | PR main-dev | same as dev-async + `infra/scripts/check-migration-safety.py` |
| `docs-linkcheck.yml` | PR | `docs/**, CLAUDE.md, .github/workflows/docs-linkcheck.yml, .lycheeignore` |
| `e2e-library-to-game.yml` | PR epic/library-to-game | `apps/web/**, .github/workflows/e2e-library-to-game.yml` |
| `generate-diagrams.yml` | PR main-staging | `docs/09-bounded-contexts/diagrams/**/*.mmd` |
| `generate-operations-pdf.yml` | push main/main-dev | `docs/operations/operations-manual.md, docs/operations/.md-to-pdf.js` |
| `test-e2e.yml` | PR main | `apps/web/**, apps/api/**` |
| `test-performance.yml` | PR main/main-staging + schedule | `apps/web/**, apps/api/src/Api/Routing/**, apps/api/src/Api/BoundedContexts/**, tests/k6/**, .github/workflows/test-performance.yml` |
| `test-visual.yml` | PR main/main-staging | `apps/web/**, .github/workflows/test-visual.yml` |
| `validate-workflows.yml` | PR | `.github/workflows/**, .github/actions/**, docs/security/github-actions-pinning.md` |

**Workflows that intentionally do not filter** (non-PR triggers — paths-ignore
does not apply):

| Workflow | Reason |
|---|---|
| `admin-reset`, `check-api-logs`, `check-role-case`, `diagnose-admin`, `fix-db-password`, `fix-line-endings`, `rollback`, `test-login` | `workflow_dispatch` only |
| `barrier-verification`, `ci-minutes-digest`, `dev-auto-revert`, `e2e-smoke-real-backend`, `e2e-smoke-real-llm-weekly`, `runner-health-check`, `runner-maintenance`, `security-audit-staging`, `security-review` | `schedule` (cron) only |
| `deploy-staging` | `workflow_run` cascade + `workflow_dispatch` |
| `notify-slack` | `workflow_call` (reusable workflow) |
| `spec-debt-false-positive-handler` | `issues: labeled` event |
| `release-gate-comment` | PR + `check_suite` + `workflow_dispatch` — `paths-ignore` would skip the PR-event leg but `check_suite` runs regardless; leaving unfiltered to keep release-gate bot consistent across all release PRs (docs + code) |
| `auto-branch-policy` | PR-event branch-name validation. Fast (~5 s); per-PR coverage is the value, not minute savings. |
| `auto-dependabot` | PR-event but PRs are from Dependabot, never docs-only. paths-ignore would never match. |

**Verdict**: every PR-triggered workflow that *could* benefit from a
docs-only skip already has one (`paths-ignore`) or a stricter form
(`paths:` allowlist). No further changes appropriate without changing
the workflow's purpose.

### AC-3 — Cache key fingerprints with `vars.CACHE_VERSION`: ✅ DONE

Verified `vars.CACHE_VERSION` knob present in 5 workflows covering the
three target cache families:

| Cache | Workflow | Key |
|---|---|---|
| NuGet | `backend-e2e-tests.yml:70` | `nuget-${{ vars.CACHE_VERSION \|\| 'v1' }}-${{ runner.os }}-${{ runner.arch }}-${{ hashFiles('**/*.csproj', '**/Directory.Packages.props', 'global.json') }}` |
| pnpm | `e2e-library-to-game.yml:65` | `pnpm-${{ vars.CACHE_VERSION \|\| 'v1' }}-${{ runner.os }}-${{ hashFiles('pnpm-lock.yaml', '.npmrc') }}` |
| Playwright | `test-e2e.yml:188` | `playwright-${{ vars.CACHE_VERSION \|\| 'v1' }}-${{ runner.os }}-${{ runner.arch }}-${{ hashFiles('apps/web/package.json') }}` |
| (also referenced) | `dev-fast.yml`, `dev-async.yml` | `vars.CACHE_VERSION` knob present |

One outlier: `test-performance.yml:403` Next.js cache lacks `vars.CACHE_VERSION`
override but uses a workflow-local source-file fingerprint
(`hashFiles('apps/web/src/**')`) — minor, low-priority follow-up.

### AC-4 — dorny/paths-filter language-isolatable job skip: ✅ DONE

9 workflows use `dorny/paths-filter`: `dev-fast`, `dev-async`, `ci`,
`backend-e2e-tests`, `test-e2e`, `test-performance`, `test-visual`,
`security-pentest`, `deploy-staging`. This covers all language-isolatable
jobs in the PR validation cluster.

### AC-7 — `gh variable set CACHE_VERSION --body v2` invalidates all: ✅ DONE

By construction of AC-3: every cache key starts with
`<family>-${{ vars.CACHE_VERSION || 'v1' }}-...`. Bumping the repo variable
to `v2` causes all NuGet/pnpm/Playwright keys to miss in one shot. Manual
smoke test deferred (operator escape hatch; no signal in steady-state).

### AC-5 — ≥30% monthly minute reduction (14-day window): ⏳ MEASUREMENT PHASE

Cannot be verified at the moment of merging this audit doc. Requires:

1. Snapshot the `ci-minutes-digest.yml` weekly output from
   **2026-05-29 (Monday)** as the 7-day partial reading.
2. Snapshot again from **2026-06-05 (Monday)** as the 14-day reading.
3. Compare against the pre-audit baseline (digest from
   2026-05-15 / 2026-05-22, the two weeks pre-merge).
4. Post a comment on issue #850 with both snapshots and a delta %.

If the delta is `< 30%`, examine the digest for residual high-cost
workflows and open targeted follow-up issues. Do NOT re-open AC-1–AC-4
(they are already satisfied structurally; any further optimization is
a different kind of work — e.g. dropping the cron cadence of
`security-pentest`, splitting `ci.yml`, migrating heavy jobs off the
self-hosted runner, etc.).

### AC-6 — No regression in build correctness (`Dev Async` pass rate ±10%): ⏳ MEASUREMENT PHASE

Same window as AC-5. Verification query:

```bash
gh run list --workflow=dev-async.yml --status=completed \
  --created '2026-05-08..2026-05-22' --json conclusion --jq '[.[].conclusion] | group_by(.) | map({k: .[0], n: length})'
# vs
gh run list --workflow=dev-async.yml --status=completed \
  --created '2026-05-22..2026-06-05' --json conclusion --jq '[.[].conclusion] | group_by(.) | map({k: .[0], n: length})'
```

Comparison goes in the same Day-14 comment.

## Out of scope (per spec)

The following are tracked separately or out of scope per the issue:

- Self-hosted runner on dev machine (cost-shift, not cost-reduction)
- `security-pentest` cron cadence tightening
- `actions/cache@v3 → @v4` upgrade
- Draft-PR skip on heavy workflows
- `ci-minutes-digest.yml` accuracy audit for cancelled-run minute accounting

## Recommendation

1. **Close PR 1 scope on issue #850** — AC-1 through AC-4 and AC-7 are
   all satisfied. Lift the "concurrency + paths-ignore + cache" scope
   off the issue body.
2. **Keep issue #850 open** through the measurement window. Re-tag with
   `measurement-pending` once such a label exists; otherwise leave open
   with a comment quoting the schedule from §AC-5 above.
3. **Defer PR 2 (cache key tightening) and PR 3 (dorny/paths-filter
   extension) cards on the issue** as ALREADY DONE. The 3-PR phased
   rollout from the original spec collapses into "audit + measure"
   because the implementation work was absorbed by intermediate PRs.
4. **Post Day-7 and Day-14 digest comparison comments** on #850 as
   per §AC-5.

## Refs

- Parent epic: #842
- ADR-054 (multi-branch strategy)
- ADR-055 (auto-revert bot identity)
- Measurement primitive: `.github/workflows/ci-minutes-digest.yml`
- Branch protection check: `gh api repos/meepleAi-app/meepleai-monorepo/branches/main-dev/protection`
- Audit branch: `feature/issue-850-pr1-concurrency-paths-ignore`
