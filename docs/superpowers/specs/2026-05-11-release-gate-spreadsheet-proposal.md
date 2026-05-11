# ADR Draft — release-gates.yml: classification of CI checks per release PR

**Status**: Draft — proposal post #979 release-PR triage
**Date**: 2026-05-11
**Context**: Release PR #979 had 7 CI fails mixing real blockers (test failures, CVEs) with pre-existing debt (a11y, doc cleanup) and env-specific issues. Manual triage burned ~3h. Tracking: #1016.

## Problem

CI check failures on release PRs (`main-dev → main-staging`) have no severity classification. Every fail is currently treated as a release blocker, but reality:
- Some are **real blockers** (new test failures, new CVEs) → MUST fix
- Some are **pre-existing debt** tolerated upstream (a11y, doc deadlinks) → SHOULD fix eventually
- Some are **environment-specific** (Docker cache, transient flake) → ignore + retry

Without classification, every release becomes a manual triage of the same 7-10 candidates.

## Proposal

Add `.github/release-gates.yml`:

```yaml
version: 1
gates:
  - name: Backend - Unit Tests
    severity: blocker
    owner: backend-dev
    override_path: fix-forward
    pre_existing_in_main_dev: false

  - name: Frontend - A11y E2E
    severity: warning
    owner: frontend-dev
    override_path: baseline-update OR exception-comment
    pre_existing_in_main_dev: true   # known #636 family debt
    notes: "Compare diff vs main-staging baseline; only NEW violations gate"

  - name: codecov/patch
    severity: informational
    owner: author
    override_path: exception-comment-required-if-below-80%
    pre_existing_in_main_dev: variable

  - name: Lychee link check
    severity: warning
    owner: docs-editor
    override_path: exclude + tracked-issue
    pre_existing_in_main_dev: true   # cascading debt from reorgs

  - name: Dependency Vulnerabilities
    severity: blocker
    owner: backend-dev
    override_path: fix-forward OR cherry-revert
    pre_existing_in_main_dev: false  # bumping policy: never carry HIGH/CRITICAL

  - name: smoke (ubuntu-latest)
    severity: blocker
    owner: qa
    override_path: investigate + workflow_dispatch retry
    pre_existing_in_main_dev: false

  ...
```

GitHub Action on `pull_request` for `base=main-staging` reads the file + current check states, posts a comment classifying each:

```markdown
## Release Gate Triage — PR #N

| Check | Status | Severity | Action |
|-------|--------|----------|--------|
| Backend - Unit Tests | ❌ fail | blocker | **fix-forward** |
| Lychee link check | ❌ fail | warning | exclude path #X (auto-issue filed) |
| codecov/patch | ❌ fail | informational | exception comment required |

**Release blockers**: 1 → MUST resolve
**Warnings**: 2 → SHOULD address (won't block merge if owner approves)
**Informational**: 1 → REVIEW
```

## Phase 1 (MVP)

- [ ] Author `.github/release-gates.yml` with current 30+ check names + severity
- [ ] Bot comment generator (gh-script in workflow)
- [ ] Validate on 3 release PRs (manual approve)

## Phase 2 (automation)

- [ ] Auto-bypass `informational` checks (post comment, allow merge)
- [ ] Auto-revert PR if `blocker` triggers (with grace period)
- [ ] Slack/email digest for `warning` triage

## Trade-offs

| Pro | Con |
|-----|-----|
| Reduces release triage from 3h to 10min | Initial classification cost |
| Documents which fails are debt vs regression | Requires maintenance as CI evolves |
| Bot comment = audit trail | False sense of safety if classification wrong |

## Decision

TBD — pending discussion. Tracked as #1016.

## References

- #979 (release PR that motivated this)
- #1004, #1014, #1015 (per-domain followups)
