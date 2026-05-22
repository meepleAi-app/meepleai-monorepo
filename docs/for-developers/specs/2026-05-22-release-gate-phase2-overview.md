# Release-Gate Phase 2 — Overview, Sequence, Sub-Issue Specs

**Issue**: [#1016](https://github.com/meepleAi-app/meepleai-monorepo/issues/1016) Phase 2 (automation)
**Parent epic**: [#842](https://github.com/meepleAi-app/meepleai-monorepo/issues/842) — DevOps Multi-Branch Strategy
**Phase 1 design**: [`2026-05-22-release-gate-spreadsheet.md`](./2026-05-22-release-gate-spreadsheet.md)
**Spec-panel**: 2026-05-22 — Hightower · Nygard · Newman · Wiegers · Adzic · Crispin · Fowler · Cockburn (mode: discussion, focus: architecture+requirements)
**Status**: 🔵 OVERVIEW (panoramica strategica; le 3 sub-issue saranno spec-panellate singolarmente prima del dispatch)

---

## Context

Phase 1 MVP ([PR #1435](https://github.com/meepleAi-app/meepleai-monorepo/pull/1435), merged 2026-05-22) ships a classification-only bot: it reads each release-PR's `check_runs`, looks them up in `.github/release-gates.yml`, and posts a single edit-in-place comment with severity (`blocker | warning | informational`), owner, and override path. **No automation.** The human still decides what to do.

Phase 2 closes the loop by acting on each severity tier automatically. The original issue body proposes 3 sub-issues. This overview decides the sequence, names the prerequisites, draws the dependency edges, and produces ready-to-spawn stubs.

## Sub-issue summary

| ID | Title | Severity acted on | Action | Blast radius | Complexity |
|---|---|---|---|---|---|
| **#1016-2a** | Auto-bypass `informational` checks | `informational` | Checks API conclusion override → green check on PR | LOW (advisory only) | S |
| **#1016-2b** | Auto-revert release PR on `blocker` | `blocker` | Open auto-revert PR on `main-staging` after merge if a blocker surfaces post-merge | HIGH (touches release branch) | L |
| **#1016-2c** | Slack digest for `warning` triage | `warning` | Weekly cron aggregates classified warnings across recent release PRs; posts digest to `#meepleai-release` Slack | MED (notification noise risk) | M |

## Expert panel discussion

### Round 1 — Sequence

**Kelsey HIGHTOWER**: "We don't have branch protection on `main-staging` (just verified: `required_status_checks.contexts == null`). 2a in its full form needs branch protection to be meaningful — without it, an `informational` check is already non-blocking. The lighter path is `Checks API` conclusion override: the bot publishes a synthesized check named `Release-Gate Verdict` with `conclusion=success` when all blockers are clean, regardless of how the underlying informational checks resolved. That's installable in days, doesn't require touching branch-protection state, and gives a single gate humans can wire into protection later. I'd start there."

**Sam NEWMAN**: "Evolutionary delivery says: ship the thing that lowers risk for the next ship. 2c (digest) is pure observability — it surfaces classification quality without acting. Phase 1 already declares forward AC-8 (3 live release PRs, FP ≤ 5 %, coverage ≥ 90 %). 2c gives us those metrics across rolling windows. Building 2a or 2b before 2c means trusting `classify.mjs` blind — we want a feedback loop first."

**Michael NYGARD**: "I agree with Sam on order — 2c first — but for a different reason: blast radius. 2b reverts on `main-staging`. If `classify.mjs` mis-tags a known flake as `blocker`, 2b reverts a clean release. That's a stop-the-line event for the whole team. We need at least 14 days of stable classification metrics before granting the bot a write keystroke on a release branch. 2c gives that telemetry."

**Karl WIEGERS**: "Sequence aside — each of these needs SMART AC. The original 1-line bullets in the issue body fail testability. For 2b especially: 'auto-revert on blocker triggers' is not a requirement, it's a category. We need: which `check_run.conclusion` values count as a trigger, at what cumulative threshold, with what cooldown, with what manual-override path."

**Lisa CRISPIN**: "And acceptance must be 'no false revert in N consecutive releases' — not 'the code handles the revert case'. The latter is implementation. The former is what protects the team."

**Alistair COCKBURN**: "Primary actor for each:
- 2a: the release-PR author. Goal: 'I want my PR to not look red because of Lychee link rot.'
- 2b: the release manager (currently @badsworm). Goal: 'I want a broken release reverted before I notice.'
- 2c: the release manager + tech leads. Goal: 'I want to triage warnings before they accumulate into next-release blockers.'

Three different actors. 2c serves the broadest set, 2a serves the narrowest. That's another argument for 2c → 2a → 2b."

**Martin FOWLER**: "Architecturally the three share three concerns: read `release-gates.yml`, classify a check, take an action. We already have `scripts/release-gate/lib/{classify,format,validate}.mjs`. Phase 2 should add `lib/conclusion-override.mjs` (2a), `lib/digest-builder.mjs` (2c), `lib/auto-revert.mjs` (2b). New workflows wrap these. No new toolchain. Keeps the single-responsibility line clean and the test surface uniform — extend the existing Vitest pattern."

**Gojko ADZIC**: "Concrete scenarios per sub-issue go in the stubs below. For 2b I want one explicit Given/When/Then for the dangerous case: 'Given a release PR is merged AND a post-merge blocker surfaces, When the bot opens a revert PR, Then the revert PR is auto-merged only if a human has not opened a fix-forward PR within 15 minutes.' That cooldown is the safety gate."

### Round 2 — Dependencies

```
Phase 1 MVP (merged 2026-05-22, PR #1435)
   │
   ├─→ Forward AC-8 validation gate (3 live release PRs, ~7 days)
   │       │
   │       └─→ 2c (digest) ────────────────────┐
   │              │                            │
   │              │ 14d telemetry              │
   │              ↓                            │
   │           classification quality          │
   │           confidence ≥ 95 %               │
   │              │                            │
   │              ↓                            │
   │           2a (informational bypass)       │ (parallel after 2c metrics)
   │              │                            │
   │              ↓                            │
   │           2b (auto-revert blocker)        │
   │              │                            │
   │              ↓                            │
   │       Optional: tighten main-staging      │
   │       branch protection to require        │
   │       Release-Gate Verdict check          │
```

### Round 3 — Risks

| Risk | Owner | Sub-issue | Mitigation |
|---|---|---|---|
| `classify.mjs` mis-classifies → bot acts on wrong tier | release manager | 2a + 2b | 14-day telemetry gate (2c output); manual override path documented; `bot:` config flag to disable per-sub-issue |
| Auto-revert clobbers operator's fix-forward in flight | release manager | 2b | 15-min cooldown after blocker detection (G/W/T below); SHA-pinned revert PR (refuses if main-staging HEAD moved) |
| Digest noise → people mute the channel | tech leads | 2c | Threshold-based (only fire when ≥ N warnings cumulative); empty-week silence; opt-in mention list, not @channel |
| Branch-protection drift if 2a writes via API | devops | 2a | 2a does NOT modify branch-protection. It publishes a synthesized `Release-Gate Verdict` check. Branch protection rule is a separate, manual, documented step (operator chooses when to make it required). |
| Permission scope creep | devops | 2b | Per-workflow `permissions:` block, `GITHUB_TOKEN` only (per ADR-055); no PAT, no App for Phase 2. |
| Cost (Actions minutes) | devops | 2c | Weekly cron `0 9 * * 1` = 4 runs/month, ~5 min each. Cheap. |

### Round 4 — Hand-off conditions

Each sub-issue may begin only when its predecessor's exit criteria are met:

- **2c may start** when Phase 1 forward AC-8 reports PASS (3 live release PRs evaluated, FP ≤ 5 %, coverage ≥ 90 %).
- **2a may start** when 2c has produced ≥ 14 days of telemetry AND informational-tier classification has zero false-blocker / false-warning crossovers in that window.
- **2b may start** when 2a has been live ≥ 14 days AND classification confidence across all severities is ≥ 95 % (measured as `1 - (overrides_by_human / total_classifications)`).

## Recommended sequence (consensus)

**`2c → 2a → 2b`**, with telemetry gates between each transition. Estimated calendar time end-to-end: ~6-8 weeks from 2026-05-22.

## Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Shared classification layer | Reuse `scripts/release-gate/lib/classify.mjs` | Single source of truth; no fork |
| Bot identity (2a + 2b) | `GITHUB_TOKEN` (per [ADR-055](../../for-claude/architecture/adr/adr-055-auto-revert-bot-identity.md)) | Zero rotation, audit clear |
| 2a action mechanism | Synthesized check via `POST /repos/{owner}/{repo}/check-runs` | Avoids touching branch protection |
| 2b auto-revert mechanism | Auto-PR (NOT direct push), per ADR-055 §D | Audit trail in PR thread |
| 2c notification channel | Slack webhook (`SLACK_WEBHOOK_URL` exists) | No new secret, no new infra |
| Config location | `bot:` section in `.github/release-gates.yml` (extended) | Single config file |
| Kill switch | `bot.phase2.{2a,2b,2c}.enabled = false` config flag | Per-feature disable without revert |
| Test infra | Extend Vitest pattern from Phase 1 | Uniform test surface |

## Open questions for sub-issue spec-panels

These are deferred to the per-sub-issue `/sc:spec-panel` sessions. Listed here so the overview captures them rather than dropping them.

- **2a**: Does the synthesized `Release-Gate Verdict` check appear on the PR as a separate row, or replace the existing per-check rows visually? (UI placement matters for reviewer confusion.)
- **2a**: Should `informational` fail-states still surface in the bot's comment (just not in the verdict check), or be suppressed entirely?
- **2b**: Cooldown duration — 15 minutes feels right for Adzic's scenario, but is it long enough for an operator on PST who picks up the alert at 9 AM after a 6 AM merge?
- **2b**: How does the bot detect that the blocker is "new" vs "pre-existing" — i.e. distinguish a real regression from a blocker the human already accepted via override path?
- **2c**: Digest scope — per release PR, or rolling 7-day window across all release PRs? Newman favors rolling; Cockburn favors per-PR.
- **2c**: Slack channel — `#meepleai-release` exists? Or create new? Or use existing `SLACK_GITNOTIFY_WEBHOOK_URL`?

## Sub-issue stubs (ready to spawn)

The 3 stubs below are issue bodies pre-drafted to be posted via `gh issue create`. Each carries the spec-panel score from this overview (`6.0/10` — needs per-issue panel before dispatch) and a Phase 1 cross-link.

### Stub: #1016-2a — Auto-bypass `informational` checks via synthesized verdict check

```yaml
title: "[#1016-2a] Release-gate: synthesize 'Release-Gate Verdict' check (Phase 2a)"
labels: ["enhancement", "devops", "tech-debt", "P2"]
body: |
  **Parent**: #1016 Phase 2 ([overview doc](../blob/main-dev/docs/for-developers/specs/2026-05-22-release-gate-phase2-overview.md))
  **Spec maturity**: 6.0/10 — needs `/sc:spec-panel #1016-2a` before dispatch
  **Predecessors**: 2c (#1016-2c) must produce 14d telemetry first

  ## Problem
  Release PRs collect red rows for `Lychee Link Check`, `Conformity gate`, and similar
  `informational` checks. These rows visually distract from real blockers without ever
  needing action. Phase 1 classifies them; Phase 2a should make them go away from the
  reviewer's checklist.

  ## Solution
  Add `scripts/release-gate/lib/conclusion-override.mjs` that, after `classify.mjs` runs,
  publishes a synthesized check `Release-Gate Verdict` to the PR head SHA via
  `POST /repos/{owner}/{repo}/check-runs` with:
    - conclusion = `success` if no `blocker` failed
    - conclusion = `failure` if any `blocker` failed
    - conclusion = `neutral` if only `warning` failed
    - text body links to the bot comment

  Reviewers gate on `Release-Gate Verdict`, not on individual rows. Branch protection
  remains UNCHANGED in this issue — making the verdict check required is a separate
  operator decision (documented as next step).

  ## Given/When/Then (initial — refine in spec-panel)
  Scenario: Release PR with Lychee failure
    Given a release PR with `Lychee Link Check = failure`
      AND all `blocker`-tier checks = success
      AND no `warning`-tier checks failed
     When the release-gate bot runs
     Then a `Release-Gate Verdict` check is published with `conclusion=success`
      AND the bot comment shows Lychee under "informational (auto-bypassed)"

  ## SMART Acceptance Criteria (initial)
  - **AC-1**: `lib/conclusion-override.mjs` published verdict check appears within 60s
    of last `check_run.completed` on release PR.
  - **AC-2**: Verdict check name is exactly `Release-Gate Verdict` (consumed by future
    branch protection rule).
  - **AC-3**: Bot kill switch — when `bot.phase2.2a.enabled = false` in
    `.github/release-gates.yml`, no verdict check is published.
  - **AC-4**: Idempotency — re-run on same SHA edits the existing verdict check, does
    not create a duplicate.
  - **AC-5**: 14 days of zero false-blocker / false-warning crossovers in 2c telemetry
    BEFORE this issue may begin (gate, not AC).

  ## Out of scope
  - Modifying branch-protection rules (separate doc/runbook task).
  - Suppressing informational rows from the bot comment (still shown under "auto-bypassed").

  ## References
  - Parent: #1016 (Phase 2 overview)
  - Phase 1: PR #1435, design doc `2026-05-22-release-gate-spreadsheet.md`
  - ADR-055: GITHUB_TOKEN identity
  - GitHub Checks API: https://docs.github.com/en/rest/checks/runs#create-a-check-run

  ## Estimated effort
  S (~2 days incl. tests + docs)
```

### Stub: #1016-2b — Auto-revert release PR on post-merge `blocker`

```yaml
title: "[#1016-2b] Release-gate: auto-revert on post-merge blocker (Phase 2b)"
labels: ["enhancement", "devops", "P2"]
body: |
  **Parent**: #1016 Phase 2 ([overview doc](../blob/main-dev/docs/for-developers/specs/2026-05-22-release-gate-phase2-overview.md))
  **Spec maturity**: 6.0/10 — needs `/sc:spec-panel #1016-2b` before dispatch (Nygard lead)
  **Predecessors**: 2a (#1016-2a) live ≥ 14 days; classification confidence ≥ 95 %

  ## Problem
  When a release PR merges and a post-merge check classified as `blocker` surfaces
  on `main-staging`, the broken state stays until someone notices. Echoes ADR-055
  for `main-dev`, but with HIGHER blast radius (release branch feeds prod).

  ## Solution
  Add `scripts/release-gate/lib/auto-revert.mjs` + workflow
  `.github/workflows/release-gate-auto-revert.yml` that:
    1. Polls release-gate bot output on `main-staging` HEAD
    2. If a `blocker`-tier check is failing AND ≥ 15 min have passed since merge
       AND no human has opened a fix-forward PR targeting `main-staging` in that window
    3. Opens a revert PR via `gh pr create` with SHA-pinned diff (refuses if HEAD moved)
    4. Auto-merges the revert PR via `gh pr merge --admin --squash` (per ADR-055)
    5. Notifies `#meepleai-release` Slack with revert PR link + original PR link

  ## Given/When/Then (initial — Adzic scenario from overview)
  Scenario: Post-merge blocker triggers cooldown, no fix-forward, revert fires
    Given release PR #N was merged to `main-staging` at T
      AND at T+5min a `Backend - Integration (Core)` check completes with `failure`
      AND release-gate classifies it as `blocker`
     When the auto-revert workflow polls at T+15min
      AND no fix-forward PR has been opened targeting `main-staging` after T
      AND `main-staging` HEAD == merge commit of PR #N
     Then a revert PR is opened reverting the merge commit of PR #N
      AND the revert PR is auto-merged via `--admin --squash`
      AND a Slack message is sent to `#meepleai-release` linking both PRs

  Scenario: Fix-forward already in flight — no revert
    Given release PR #N was merged to `main-staging` at T
      AND at T+5min a `blocker` check surfaces
      AND at T+10min an operator opens PR #M targeting `main-staging` with a fix
     When the auto-revert workflow polls at T+15min
     Then NO revert PR is opened
      AND a Slack message is sent acknowledging fix-forward in progress

  ## SMART Acceptance Criteria (initial)
  - **AC-1**: 15-min cooldown enforced before any revert action.
  - **AC-2**: Revert PR is SHA-pinned: workflow aborts if `main-staging` HEAD moved
    during cooldown.
  - **AC-3**: Fix-forward detection — workflow queries open PRs targeting
    `main-staging` opened after merge time; non-empty result aborts revert.
  - **AC-4**: Kill switch — `bot.phase2.2b.enabled = false` disables workflow.
  - **AC-5**: 14-day dry-run period (workflow runs, opens DRAFT PRs, never merges)
    BEFORE flipping to live mode.
  - **AC-6**: Audit trail — every revert PR body links the workflow run + original PR
    + classification snapshot.
  - **AC-7**: False-revert rate ≤ 2 % across 30-day rolling window post-launch.

  ## Out of scope
  - Auto-revert on `warning`-tier checks (warnings are by definition acceptable).
  - Reverting commits older than the current `main-staging` HEAD's parent (only
    revert the most recent merge).
  - Notifying anything other than Slack `#meepleai-release` (email/SMS deferred).

  ## References
  - Parent: #1016 (Phase 2 overview)
  - Pattern source: ADR-055 (auto-revert identity & push mechanism) for `main-dev`
  - Phase 1: PR #1435
  - Sibling: #843 (auto-revert on `main-dev`, currently SHADOW MODE)

  ## Estimated effort
  L (~5-7 days incl. 14-day dry-run + tests + Slack template + ops runbook)
```

### Stub: #1016-2c — Weekly digest of `warning`-tier classifications to Slack

```yaml
title: "[#1016-2c] Release-gate: weekly warning digest to Slack (Phase 2c)"
labels: ["enhancement", "devops", "observability", "P2"]
body: |
  **Parent**: #1016 Phase 2 ([overview doc](../blob/main-dev/docs/for-developers/specs/2026-05-22-release-gate-phase2-overview.md))
  **Spec maturity**: 6.0/10 — needs `/sc:spec-panel #1016-2c` before dispatch
  **Predecessors**: Phase 1 forward AC-8 PASS (3 live release PRs evaluated)
  **First in Phase 2 sequence** — produces the telemetry that gates 2a + 2b.

  ## Problem
  `warning`-tier checks (a11y violations #1094, codecov drops, Lighthouse flake)
  accumulate silently between releases. By the time they surface in a release-PR
  triage, they've grown into a blocker-worthy backlog. Need proactive visibility.

  ## Solution
  Add `scripts/release-gate/lib/digest-builder.mjs` + workflow
  `.github/workflows/release-gate-digest.yml` (cron `0 9 * * 1` Europe/Rome) that:
    1. Lists release PRs (`base = main-staging` OR `base = main`) merged or opened in the
       last 7 days.
    2. For each, fetches the latest release-gate bot comment.
    3. Aggregates `warning`-tier failures by check name + owner.
    4. Computes deltas vs prior week (new, persisting, resolved).
    5. Posts digest to Slack via webhook (existing `SLACK_GITNOTIFY_WEBHOOK_URL`).
    6. If `warning` count == 0 in the window, posts a one-line "all green" message.

  ## Given/When/Then (initial)
  Scenario: Standard weekly run with warnings
    Given 3 release PRs merged to `main-staging` in the past 7 days
      AND release-gate bot classified 5 `warning`-tier failures across them
     When the digest workflow runs at Monday 09:00 Europe/Rome
     Then a single Slack message is posted listing:
          - top 3 warning checks by frequency
          - per-check owner (`@frontend-dev`, `@qa`, etc.)
          - delta vs last week's digest (new / persisting / resolved counts)
      AND the message is threaded under a pinned "Release-Gate Digest" header

  Scenario: Empty window
    Given 0 release PRs merged AND 0 warning failures in the past 7 days
     When the digest workflow runs
     Then a single line `:white_check_mark: No release-gate warnings this week` is posted

  ## SMART Acceptance Criteria (initial)
  - **AC-1**: Digest runs weekly on Monday 09:00 Europe/Rome (`0 8 * * 1` UTC).
  - **AC-2**: Posts to `#meepleai-release` (or channel of existing `SLACK_GITNOTIFY_WEBHOOK_URL`,
    verify in spec-panel).
  - **AC-3**: Aggregates `warning`-tier ONLY; ignores `blocker` (those have their own
    alerts) and `informational` (those are auto-bypassed by 2a).
  - **AC-4**: Per-check counters include `new this week`, `persisting >=2 weeks`,
    `resolved this week`.
  - **AC-5**: Persisting >= 4 weeks of the same warning triggers a `:warning:` escalation
    flag in the digest (suggests the check should be reclassified to `blocker`).
  - **AC-6**: Kill switch — `bot.phase2.2c.enabled = false` no-ops the workflow.
  - **AC-7**: 14 days of digest output AFTER launch becomes the telemetry that gates 2a.

  ## Out of scope
  - Email digest (Slack only for Phase 2).
  - Per-developer @-mentions (use owner group handles only).
  - Historical backfill (digest starts from week of merge).

  ## References
  - Parent: #1016 (Phase 2 overview)
  - Phase 1: PR #1435
  - Existing Slack webhooks: `SLACK_WEBHOOK_URL`, `SLACK_GITNOTIFY_WEBHOOK_URL`, `SLACK_CRITICAL_WEBHOOK_URL`
  - A11y backlog example: #1094

  ## Estimated effort
  M (~3 days incl. tests + Slack template iteration with team)
```

## Sign-off checklist

- [ ] Overview doc reviewed by user (this document)
- [ ] User decides whether to spawn all 3 stubs now or after Phase 1 forward AC-8 PASS
- [ ] 3 sub-issues created via `gh issue create` with the bodies above
- [ ] Cross-link comment on #1016 parent referencing the 3 new sub-issues
- [ ] When predecessors clear: run `/sc:spec-panel #1016-2X` per sub-issue to mature 6.0 → ≥ 8.5
- [ ] Per-sub-issue TDD plan written before dispatching to implementer

## References

- Parent issue: [#1016](https://github.com/meepleAi-app/meepleai-monorepo/issues/1016)
- Parent epic: [#842](https://github.com/meepleAi-app/meepleai-monorepo/issues/842)
- Phase 1 design: [`2026-05-22-release-gate-spreadsheet.md`](./2026-05-22-release-gate-spreadsheet.md)
- Phase 1 PR: [#1435](https://github.com/meepleAi-app/meepleai-monorepo/pull/1435)
- ADR-055 (auto-revert identity): [`adr-055-auto-revert-bot-identity.md`](../../for-claude/architecture/adr/adr-055-auto-revert-bot-identity.md)
- ADR-054 (multi-branch strategy): [`adr-054-devops-multi-branch-strategy.md`](../../for-claude/architecture/adr/adr-054-devops-multi-branch-strategy.md)
- Sibling: #843 (auto-revert `main-dev` — different scope, same pattern)
- Sibling: #850 (CI cost optimization, window 2026-06-01)
- Bot operator manual (Phase 1): [`release-gate-bot.md`](../operations/release-gate-bot.md)
