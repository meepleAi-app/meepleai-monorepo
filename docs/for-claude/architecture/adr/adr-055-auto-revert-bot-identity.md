# ADR-055 — Auto-Revert Bot Identity & Push Mechanism

**Status**: Proposed
**Date**: 2026-05-13
**Deciders**: @badsworm
**Tracking**: Issue [#843](https://github.com/meepleAi-app/meepleai-monorepo/issues/843); extends [ADR-054](./adr-054-devops-multi-branch-strategy.md) (multi-branch strategy).
**Supersedes**: —

## Context

ADR-054 introduced the three-branch strategy. The `main-dev` async pipeline (`dev-async.yml`) runs post-merge and is **non-blocking** for PR merges. Without compensating control, a regression on `main-dev` can sit red for hours: developers keep merging on top of broken state, and the red signal becomes noise rather than action.

Epic [#842](https://github.com/meepleAi-app/meepleai-monorepo/issues/842) committed to an auto-revert bot that watches `dev-async` and reverts the offending commit when it has been continuously red for ≥ 30 minutes. The bot must:

- Authenticate against GitHub with write access to `main-dev`
- Read workflow run state (`actions:read`)
- Create commits and push them (`contents:write`)
- Open pull requests OR push directly (decision below)
- Be revocable without changing repository settings

## Constraints

- **Single-operator project**: any credential model must be maintainable by one person without team handoff.
- **Branch protection on `main-dev`** (verified 2026-05-13): required check is `GitGuardian Security Checks` only; `enforce_admins=false`; `allow_force_pushes=false`; **no required PR review**. So both direct-push and auto-PR are technically possible with `contents:write`.
- **Audit trail**: every automated change to `main-dev` must be traceable to a workflow run, with the revert SHA cross-referenced in the original commit's PR.
- **No additional Anthropic/3rd-party hosting**: bot runs inside GitHub Actions, not on an external service.
- **Cost-aware**: GitHub Actions cron `*/5 * * * *` = 288 runs/day. We must respect the ~3,100 min/month soft cap from #850.

## Decision

The bot will use **GitHub Actions' built-in `GITHUB_TOKEN`** as its identity, with the **`contents:write` + `actions:read` + `pull-requests:write`** permission set scoped to a single workflow file (`dev-auto-revert.yml`).

Push mechanism: **auto-PR (created + immediately auto-merged with `--admin` via `gh`)**, NOT direct push.

Bot commit author: `github-actions[bot] <41898282+github-actions[bot]@users.noreply.github.com>` (the default for `GITHUB_TOKEN`-authored commits) — recognisable everywhere, no PII, no credential rotation needed.

## Alternatives considered

### A. Personal Access Token (classic PAT)

| Aspect | Outcome |
|--------|---------|
| Setup | One repo secret (e.g. `AUTO_REVERT_PAT`). |
| Expiry | PATs expire (max 1 year, or no-expiry for legacy classic). Rotation is on the operator. |
| Identity | Pushes appear as the PAT owner (e.g. @badsworm). **Audit confusion**: indistinguishable from a real commit by that human. |
| Bypass risk | Inherits full owner permissions — much broader than the bot needs. |

**Rejected**: rotation overhead + identity ambiguity outweigh the simplicity. The PAT would also bypass any future requirement around "must be a non-human author for automated reverts".

### B. Dedicated GitHub App

| Aspect | Outcome |
|--------|---------|
| Setup | Create App, generate private key, install on the repo, store `APP_ID` + `APP_PRIVATE_KEY` secrets, exchange for short-lived install token at job start. |
| Expiry | Install tokens are 1-hour, minted per run via `actions/create-github-app-token` or similar. No human rotation needed. |
| Identity | Commits appear as `meepleai-auto-revert[bot]` — unambiguous. |
| Bypass risk | Permissions scoped at App level, narrower than PAT. |
| Cost | Free for our scale. |

**Considered** the cleanest long-term solution. **Deferred**: introducing a dedicated App for a single workflow adds ceremony (key storage, lifecycle docs, recovery plan) that the current single-operator setup does not need yet. We will migrate to a GitHub App if/when we add a second automation that also needs write to `main-dev`, or if commit-author confusion turns out to be a real audit pain point.

### C. Built-in `GITHUB_TOKEN` + direct push

| Aspect | Outcome |
|--------|---------|
| Setup | Zero — token is auto-provisioned per workflow run. |
| Expiry | Auto-managed by Actions. |
| Identity | `github-actions[bot]`. |
| Bypass risk | Token is scoped to the workflow, expires on job end. |
| Audit | Push lands as a bare commit on `main-dev` — no PR record. |

**Rejected for push, accepted for identity**: the token is fine, but direct push hides the revert in commit history with no associated discussion thread. Auto-PR is preferred even though branch protection technically allows direct push (see option D).

### D. Built-in `GITHUB_TOKEN` + auto-PR (chosen)

| Aspect | Outcome |
|--------|---------|
| Setup | Zero credentials, just `permissions:` block in the workflow. |
| Expiry | Per-run token; no rotation. |
| Identity | `github-actions[bot]`. |
| Audit | Revert PR is a first-class GitHub object: searchable, linkable, can carry a body explaining "Auto-reverted SHA X because dev-async run Y was red for Z minutes". |
| Merge | `gh pr merge <n> --admin --squash --delete-branch` after creation. |

**Chosen**. The 1-2 min cost of opening + auto-merging a PR is worth the audit trail and the human-readable revert history.

## Permission set

The workflow declares the minimum scope:

```yaml
permissions:
  contents: write          # commit revert + push branch
  actions: read            # query dev-async runs
  pull-requests: write     # open + merge revert PR
  issues: write            # post status comment on original PR (optional, future)
```

No org-level secrets needed. No environment protection needed for the bot itself (the bot does not deploy).

## Kill switch & feature flag

Two layers, both required:

1. **Repository variable** `vars.AUTO_REVERT_ENABLED` (`true` | `false`). Default `false` for the first 1-2 weeks (shadow mode — logs intent, does not act). The workflow short-circuits when `false`.
2. **PR label `no-auto-revert`** on the original PR that introduced the regression. The bot must inspect the PR associated with the offending commit (via `gh api repos/{owner}/{repo}/commits/{sha}/pulls`) and skip the revert if the label is present.

Both are needed: the variable is the global circuit breaker; the label is the targeted opt-out.

## Loop safety / self-reference guard

If the revert commit itself produces a red `dev-async`, the bot must NOT revert the revert. Detection:

- The revert commit message follows the convention `Revert "..."` (Git's default for `git revert`) — easy regex match
- Additionally, commit author is `github-actions[bot]` for any bot-authored work

If both conditions hold for the candidate-to-revert SHA, the bot aborts and emits a `critical` Slack alert (different routing than the routine notify-on-revert) so a human pages in.

## Consequences

### Positive

- Zero credential lifecycle for the operator
- Native audit trail via revert PRs
- Permission scope is minimal and visible in-repo (workflow file)
- Migration path to a dedicated App is clean if scope grows

### Negative

- `github-actions[bot]` is a shared identity — every workflow uses it. Audit must disambiguate by workflow source, not author.
- Auto-PR adds ~30-60s vs direct push. Acceptable: the 30-min trigger window dwarfs this.
- If `actions/create-github-app-token` ever becomes the org policy, we will need to migrate (foreseeable, not urgent).

### Neutral / deferred

- The kill-switch and label-bypass mechanisms are bot-logic concerns, not identity concerns; documented here for completeness but tested via the bot's own implementation (issue #843 acceptance criteria).

## Refs

- ADR-054 — DevOps Multi-Branch Strategy (parent)
- Epic #842 — DevOps Multi-Branch Strategy
- Issue #843 — Auto-revert bot (implementation tracking)
- Issue #850 — CI cost optimization (cost cap context)
- `.github/workflows/dev-async.yml` — monitored workflow
- `.github/workflows/notify-slack.yml` — reusable notification primitive
