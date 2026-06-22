# Release-gate Spreadsheet + Automation

> **Issue**: [#1016](https://github.com/meepleAi-app/meepleai-monorepo/issues/1016)
> **Spec mature**: 2026-05-22 post `/sc:spec-panel #1016` (Wiegers · Cockburn · Fowler · Nygard · Newman · Adzic · Crispin · Hightower)
> **Score**: 8.5/10 ✅ READY TO DISPATCH

## Problem

Release PRs (`main-dev → main-staging`) currently surface a long tail of CI failures that mix:

- **Real release blockers** (new test failures, new CVEs)
- **Pre-existing debt** (legacy a11y violations, doc cleanup cascade)
- **Environment-specific issues** (Docker cache, Playwright runner quirks, flake)

Each release becomes a manual triage of the same recurring failures without classification. PR #979 surfaced 7 fail categories that have been seen on prior releases too. Burst evidence 2026-05-20 → 2026-05-21 = 4 release PR (#1353/#1359/#1361/#1375) in 36h with similar fail-cluster shapes.

## Solution

Authoritative `.github/release-gates.yml` mapping each known CI check to severity + owner + override-path, plus an idempotent bot (`scripts/release-gate-comment.mjs`) that auto-classifies failures on every release PR and posts/edits a single triage comment.

## Stakeholders

| Role | Goal | Pain |
|---|---|---|
| Release reviewer (badsworm) | Decide merge / revert in < 5 min | Manual reclassification on every PR |
| Backend / Frontend / DevOps dev | Know if their check is a blocker | Ambiguous failures across releases |
| CI maintainer | Schema evolution as workflows rename | Drift between yaml & reality |

## Acceptance Criteria (SMART)

| ID | Requirement | Validation |
|----|-------------|------------|
| **AC-1** | `.github/release-gates.yml` exists with **≥ 15 check_name entries** covering all checks that have failed on the last 3 release PRs (#1353, #1359, #1361, #1375). Each entry has: `check_name`, `severity` (`blocker`/`warning`/`informational`), `owner` (`backend-dev`/`frontend-dev`/`qa`/`devops`), `override_path` (`fix-forward`/`revert`/`exception-comment`/`baseline-update`), `pre_existing_in_main_dev` (bool). | `pnpm release-gate:validate` parses yaml + asserts schema |
| **AC-2** | Bot script `scripts/release-gate-comment.mjs` (Node + `@octokit/rest`) reads `.github/release-gates.yml`, fetches PR check runs via GitHub API, posts a single comment classifying each failure. Comment has signature header `<!-- release-gate-bot:v1 -->` for idempotent edit-in-place on re-run. | Unit + integration tests (see AC-7) |
| **AC-3** | Workflow `.github/workflows/release-gate-comment.yml` triggers on `pull_request` to `main-staging` (opened, synchronize, reopened) + on `check_suite.completed`. Posts/edits comment within 2min of check_suite completion. | Manual smoke test on 1 dry-run PR + 3 real release PRs |
| **AC-4** | **Idempotency**: re-running the workflow on the same PR with same check state produces **0 new comments** (edits existing comment by signature header). | Integration test: invoke twice, assert single comment |
| **AC-5** | **Failure-mode**: if bot script crashes (yaml parse error, GH API down, rate-limit), workflow posts `⚠️ release-gate bot failed — manual triage required` fallback comment + sets workflow status to neutral (not red). Counter `release_gate_bot_failures_total` emitted in GitHub Actions summary. | Inject yaml syntax error → assert fallback comment posted |
| **AC-6** | **Unknown-check fallback**: check_name not present in yaml → classified as `warning` with `owner: unknown` + appended to comment "🆕 New checks detected, please update .github/release-gates.yml". | Add fake check name to PR → assert warning classification |
| **AC-7** | **Test strategy**: unit tests (Vitest) covering classification logic + 15+ fixtures, integration test mocking GH API with synthetic PR, E2E dry-run mode (`DRY_RUN=1` env → dump to stdout, no real comment posted) verified on 1 closed historical PR. | `pnpm test:release-gate` passes |
| **AC-8** | **Validation on 3 real release PRs**: after merge, next 3 main-staging release PRs receive bot comment within 2min. False-positive rate (`blocker` classification on pre-existing failure) ≤ 5%. Coverage (% of failed checks classified) ≥ 90%. | Manual audit comment posted on this issue after 3 PRs |
| **AC-9** | **Schema evolution policy**: when workflow renamed, fallback regex pattern matched against `check_name` prefix (e.g. `dev-async/*`). Add new entries via PR review. Removal grace period: 2 release cycles after workflow deletion. | Documented in `.github/release-gates.yml` header comment |
| **AC-10** | **Observability**: GitHub Actions summary table shows: total checks, blocker count, warning count, informational count, unknown count, classification duration. | Visible in every workflow run |

## Given/When/Then scenarios

### Scenario 1: First-run on new release PR

```gherkin
Given release PR #N is opened from main-dev to main-staging
  And check_suite completed with 3 failures (Backend E2E, Frontend A11y, Lychee)
  And .github/release-gates.yml has entries for all 3 checks
When release-gate-comment.yml workflow runs
Then a single comment is posted on PR #N with:
  - Header: <!-- release-gate-bot:v1 --> + commit ref + timestamp
  - Table: 3 rows (one per failure) with severity badge + owner + override_path
  - Summary: "1 blocker, 1 warning, 1 informational"
  - GitHub Actions summary populated with same data
```

### Scenario 2: Re-run idempotency (synchronize event)

```gherkin
Given release PR #N has existing release-gate-bot comment (signature v1)
  And new commit pushed → check_suite re-runs with 2 failures (1 fixed)
When release-gate-comment.yml workflow re-runs
Then the existing comment is EDITED in-place (not duplicated)
  And updated timestamp + updated commit ref + updated table
  And total comment count on PR remains 1 (only one bot comment)
```

### Scenario 3: Bot crash (yaml parse error)

```gherkin
Given .github/release-gates.yml has been edited with invalid yaml syntax
  And release PR #N triggers release-gate-comment.yml
When the bot script attempts to parse the yaml
Then yaml.parse throws
  And the workflow's catch handler posts fallback comment:
    "⚠️ release-gate bot failed — manual triage required.
     Error: <error message>
     Action: check .github/release-gates.yml syntax"
  And workflow conclusion = "neutral" (not failure, not success)
  And GitHub Actions summary shows release_gate_bot_failures_total: 1
```

### Scenario 4: Unknown check (new workflow added)

```gherkin
Given .github/release-gates.yml has NO entry for "new-feature-test"
  And release PR #N has a check_run "new-feature-test" with conclusion=failure
When release-gate-comment.yml workflow runs
Then comment includes row:
  | check_name | severity | owner | override_path |
  | new-feature-test | warning | unknown | exception-comment |
  And comment has footer:
    "🆕 1 new check detected. Please update .github/release-gates.yml."
  And GitHub Actions summary unknown_count: 1
```

## Phase plan

### Phase 1 (MVP) — this spec scope

- [ ] `.github/release-gates.yml` authored with ≥ 15 entries (snapshot from last 4 release PRs)
- [ ] `scripts/release-gate-comment.mjs` Node implementation (octokit + yaml + classification logic)
- [ ] `.github/workflows/release-gate-comment.yml` workflow (PR + check_suite triggers)
- [ ] `scripts/release-gate-validate.mjs` schema validator (lint mode)
- [ ] `pnpm release-gate:validate` + `pnpm test:release-gate` scripts in root package.json
- [ ] Unit tests (Vitest, 15+ fixtures) + integration test (mocked GH API) + E2E dry-run
- [ ] Documentation: README section in `docs/for-developers/operations/release-gate-bot.md`
- [ ] Validation on 3 real release PRs (AC-8) — issue stays OPEN until comment posted

### Phase 2 (automation) — separate sub-issues

- **#1016-2a** Auto-bypass `informational` checks (modify branch protection via API)
- **#1016-2b** Auto-revert on `blocker` triggers (workflow + safety gate)
- **#1016-2c** Slack/email digest for `warning` triage (weekly cadence)

## Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Schema location | `.github/release-gates.yml` | Co-located with workflows; conventional GH discovery |
| Bot language | Node.js (`scripts/release-gate-comment.mjs`) | Reuses existing `@octokit/rest` toolchain; consistent with codebase |
| Comment idempotency | Signature header `<!-- release-gate-bot:v1 -->` | Standard GH bot pattern (cf dependabot, codecov) |
| Failure fallback | Neutral conclusion + fallback comment | Avoid blocking release on bot infrastructure failure |
| Unknown check default | `warning` (not `blocker`) | Fail-soft to avoid stuck PRs on new workflow additions |
| Test infra | Vitest + nock (octokit mock) | Existing FE infra; no new test framework |
| Trigger | `pull_request` + `check_suite.completed` | Catches both PR open and async check completion |

## Schema example `.github/release-gates.yml`

```yaml
# Release-gate bot — classification table for main-dev → main-staging PRs
# Schema v1. Bot script: scripts/release-gate-comment.mjs
# Validator: pnpm release-gate:validate

version: 1

# Schema evolution policy:
# - When workflow renamed: fallback regex pattern matches check_name prefix.
# - When workflow deleted: keep entry for 2 release cycles (grace period).
# - New checks default to severity=warning, owner=unknown (see AC-6).

checks:
  - check_name: "Backend E2E"
    severity: blocker
    owner: backend-dev
    override_path: fix-forward
    pre_existing_in_main_dev: false
    notes: "Real DB integration tests. Failure indicates regression."

  - check_name: "Frontend A11y"
    severity: warning
    owner: frontend-dev
    override_path: baseline-update
    pre_existing_in_main_dev: true
    notes: "~159 known violations tracked in #1094. Strict mode pending Phase D."

  - check_name: "Lychee Link Check"
    severity: informational
    owner: devops
    override_path: exception-comment
    pre_existing_in_main_dev: false
    notes: "External link rot. Non-blocking unless docs-critical."

  # ... 12+ more entries from last 4 release PRs
```

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Bot misclassifies blocker as warning → release ships regression | Medium | HIGH | False-positive rate ≤ 5% AC + manual audit on 3 PRs |
| yaml schema drift as workflows rename | High | MED | Schema evolution policy + fallback regex |
| Bot infra failure blocks release | Low | HIGH | Failure-mode AC: neutral conclusion + fallback comment |
| Comment spam (idempotency bug) | Medium | LOW | Signature header + integration test |
| GH API rate-limit on busy day | Low | MED | Single API call per workflow run; reuse existing token |

## Out of scope

- Auto-revert (Phase 2a)
- Auto-bypass informational (Phase 2b)
- Slack/email digest (Phase 2c)
- Cross-repo gate (single repo only)
- Visual UI / dashboard (text comment is sufficient)

## Observability

GitHub Actions summary table per run:

```markdown
## Release-gate Classification Summary

| Metric | Value |
|---|---:|
| Total checks evaluated | 24 |
| ❌ Blocker failures | 1 |
| ⚠️ Warning failures | 3 |
| ℹ️ Informational failures | 2 |
| 🆕 Unknown checks | 0 |
| ⏱️ Classification duration (ms) | 142 |

**Verdict**: ❌ BLOCKER — manual intervention required
```

## Verification timeline

| Day | Action |
|-----|--------|
| 0 | Merge Phase 1 MVP PR |
| 0 + 1 | First real release PR receives bot comment (AC-3) |
| 0 + 3 | 3 release PRs validated → false-positive/coverage audit (AC-8) |
| 0 + 7 | Issue closure decision; spawn Phase 2 sub-issues if green |

## References

- Parent epic: #842 (DevOps Multi-Branch Strategy)
- Sibling: #850 (CI cost optimization, in Day-14 measurement window)
- Related: #1088 (post-mortem template), #979 (release-PR triage origin)
- ADR pattern: signature-header idempotent bot comments
- Octokit docs: https://octokit.github.io/rest.js/v21/
- Spec maturation: `/sc:spec-panel #1016` session 2026-05-22

## Sign-off checklist

- [ ] Spec body update applied to GitHub issue
- [ ] Spec-panel critique comment posted (audit trail)
- [ ] User approves dispatch
- [ ] Branch `feature/issue-1016-release-gate-phase1` created from `main-dev`
- [ ] TDD plan written (RED → GREEN per AC)
- [ ] Phase 1 MVP shipped
- [ ] 3-PR validation window complete (AC-8)
- [ ] Issue closed; Phase 2 sub-issues spawned if needed
