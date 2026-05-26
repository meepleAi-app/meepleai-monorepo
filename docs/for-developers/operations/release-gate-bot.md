# Release-gate Bot — Operator Manual

> **Status**: Phase 1 MVP (issue [#1016](https://github.com/meepleAi-app/meepleai-monorepo/issues/1016))
> **Design doc**: [`docs/for-developers/specs/2026-05-22-release-gate-spreadsheet.md`](../specs/2026-05-22-release-gate-spreadsheet.md)
> **Last updated**: 2026-05-22

## What it does

When a release PR (`main-dev → main-staging` or `main-staging → main`) is opened or updated, the bot:

1. Fetches all check_runs for the PR's head commit.
2. Filters failures (`failure`, `cancelled`, `timed_out`, `action_required`).
3. Classifies each failure against `.github/release-gates.yml` (severity + owner + override path).
4. Posts a single bot comment on the PR with a classification table and verdict.
5. On re-runs (synchronize event, check_suite re-completion), **edits the same comment in-place** instead of duplicating.

Triage decision happens in < 1 min instead of manual classification per release.

## Files

| Path | Purpose |
|---|---|
| `.github/release-gates.yml` | Authoritative classification table (schema v1) |
| `.github/workflows/release-gate-comment.yml` | GitHub Actions workflow (PR + check_suite + manual triggers) |
| `scripts/release-gate/comment.mjs` | Bot entry script (octokit + classification + idempotent comment) |
| `scripts/release-gate/validate.mjs` | Schema validator CLI |
| `scripts/release-gate/lib/classify.mjs` | Pure classification logic (exact match → fallback regex → unknown) |
| `scripts/release-gate/lib/format.mjs` | Markdown comment + GH Actions summary formatters |
| `scripts/release-gate/lib/validate.mjs` | Schema validation logic |

## How to edit `.github/release-gates.yml`

### Add a new check

1. Edit `.github/release-gates.yml`. Add an entry under `checks:`:
   ```yaml
   - check_name: "Exact check name as it appears in the PR"
     severity: blocker | warning | informational
     owner: backend-dev | frontend-dev | qa | devops
     override_path: fix-forward | revert | exception-comment | baseline-update
     pre_existing_in_main_dev: true | false
     notes: "Free-form context for release reviewer."
   ```
2. Validate locally:
   ```bash
   cd scripts/release-gate
   node validate.mjs
   ```
3. Open a PR. The release-gate-comment workflow itself runs `validate.mjs` as a pre-step, so invalid yaml blocks the workflow.

### Update an existing check

Edit the entry inline. Keep `notes:` current — it appears in the bot comment column.

### Remove a check (workflow deletion or rename)

**Schema evolution policy (AC-9)**:
- When a workflow is renamed: keep the **old** `check_name` entry for **2 release cycles** as a grace period (covers in-flight release PRs whose check_runs still reference the old name).
- Add the **new** `check_name` entry alongside. Both classify identically.
- After 2 release cycles, delete the old entry.

For full deletion (workflow removed permanently), set a removal date in the `notes:` and revisit after 2 release cycles.

### Use a fallback regex (`fallback_patterns:`)

If a new family of checks appears (e.g. all jobs starting with `e2e-`), add a fallback pattern instead of N individual entries:

```yaml
fallback_patterns:
  - pattern: "^e2e-"
    severity: warning
    owner: qa
    override_path: exception-comment
    notes: "Catch-all for E2E job matrix."
```

Patterns are evaluated in declaration order; **first match wins**. Case-insensitive matching is supported via the `(?i)` prefix (e.g. `"(?i)security|codeql"`).

## How to interpret the bot comment

The bot posts a comment with this structure:

```markdown
<!-- release-gate-bot:v1 -->

## Release-gate Classification

Commit `abc1234` · Generated 2026-05-22T10:00:00Z

**Verdict**: ❌ BLOCKER

1 blocker · 1 warning · 1 informational

| Check | Severity | Owner | Override path | Notes |
|---|---|---|---|---|
| `Backend - Unit Tests` | ❌ blocker | backend-dev | fix-forward | xUnit suite. |
| `Frontend - A11y E2E` | ⚠️ warning | qa | baseline-update | #1094 baseline. |
| `Lychee Link Check` | ℹ️ informational | devops | exception-comment | External link rot. |

🆕 1 new check detected — please update .github/release-gates.yml.
```

**Verdict rules**:
- `BLOCKER` if at least one failure is `blocker` severity → manual triage required, do not merge.
- `WARNING` if no blockers but at least one `warning` → reviewer judgement (often safe with `baseline-update` or `exception-comment` rationale).
- `GREEN` if only `informational` failures → merge safely.

**Override path** is the suggested action:
- `fix-forward`: fix in this PR or a follow-up before merge.
- `revert`: revert the offending commit (security only).
- `exception-comment`: add a PR comment justifying the bypass.
- `baseline-update`: update the test baseline / snapshot.

**🆕 footer**: if any unknown check appears, `.github/release-gates.yml` is missing an entry. Add it (see above) so the next release benefits from explicit classification.

## How to handle bot failure

If the bot crashes (yaml parse error, GH API rate-limit, network outage), it posts a fallback comment instead of silently failing:

```markdown
<!-- release-gate-bot:v1 -->

## Release-gate Classification

Commit `unknown` · Generated 2026-05-22T10:00:00Z

⚠️ **release-gate bot failed — manual triage required.**

- Phase: `yaml-parse` | `fetch-check-runs` | …
- Error: `<the actual error message>`
- Action: review `.github/release-gates.yml` syntax and bot logs.
```

**Steps**:
1. Click the workflow run link in the Actions tab → see the bot's stack trace + GitHub step summary.
2. If it's a yaml syntax error, fix `.github/release-gates.yml` and re-trigger via `workflow_dispatch`.
3. If it's a GitHub API outage / rate-limit, wait 5 min and re-trigger (idempotency means the comment will be edited, not duplicated).
4. If neither: open an issue and ping `devops`.

**Why this design?** Silent failures are worse than visible failures. The fallback comment is the audit trail — release reviewer sees that classification failed and reverts to manual triage. The workflow status stays neutral (not red) so the PR is not artificially blocked.

## Local dry-run (no real comment posted)

To preview the comment before shipping a yaml change:

```bash
cd scripts/release-gate
DRY_RUN=1 node comment.mjs
```

By default, dry-run classifies 3 sample failures (`Backend - Unit Tests`, `Frontend - A11y E2E`, `Lychee Link Check`). To customize:

```bash
DRY_RUN=1 DRY_RUN_FAILURES="MyCheck1,MyCheck2,MyCheck3" node comment.mjs
```

The script prints the would-be comment body + GH Actions summary to stdout. No GitHub API calls are made (no token required).

## Running tests

```bash
cd scripts/release-gate
pnpm install
pnpm test
```

Vitest runs 4 test files (56 tests):
- `__tests__/classify.test.mjs` — pure classification logic + fallback regex + unknown handling
- `__tests__/format.test.mjs` — markdown comment + GH Actions summary formatters
- `__tests__/validate.test.mjs` — schema validator + I/O wrapper + canonical `.github/release-gates.yml` validation
- `__tests__/integration.test.mjs` — `runBot` integration with octokit mocks (first-run, idempotency, failure-mode, observability)

## Manual workflow dispatch

To run the bot on a historical PR (for debugging or retroactive classification):

```bash
gh workflow run release-gate-comment.yml -f pr_number=1234
```

The bot will post (or edit) a comment on PR #1234. Useful when:
- Investigating a missed classification.
- Testing a yaml change without waiting for the next release PR.
- Re-classifying after a yaml update.

## Limits

**Phase 1 MVP (this issue)**:
- Single-repo only.
- Triggers only on PRs targeting `main-staging` / `main` (release PRs).
- No auto-bypass, no auto-revert, no Slack/email digest.

**Phase 2 (separate sub-issues #1016-2a / 2b / 2c)**:
- Auto-bypass `informational` checks (modify branch protection via API).
- Auto-revert on `blocker` (workflow + safety gate).
- Weekly digest for `warning` triage (Slack / email).

See [design doc § Phase plan](../specs/2026-05-22-release-gate-spreadsheet.md#phase-plan).

## Schema reference

`.github/release-gates.yml` schema v1:

| Field | Type | Required | Values |
|---|---|---|---|
| `version` | int | yes | `1` |
| `checks[]` | array | yes | (see below) |
| `checks[].check_name` | string | yes | Exact check name |
| `checks[].severity` | enum | yes | `blocker` \| `warning` \| `informational` |
| `checks[].owner` | enum | yes | `backend-dev` \| `frontend-dev` \| `qa` \| `devops` \| `unknown` |
| `checks[].override_path` | enum | yes | `fix-forward` \| `revert` \| `exception-comment` \| `baseline-update` |
| `checks[].pre_existing_in_main_dev` | bool | yes | `true` \| `false` |
| `checks[].notes` | string | no | Free-form context |
| `fallback_patterns[]` | array | no | Regex fallback (first match wins) |
| `fallback_patterns[].pattern` | string | yes | JS-compatible regex, optional `(?i)` prefix |
| `fallback_patterns[].severity` | enum | yes | (same as `checks[].severity`) |
| `fallback_patterns[].owner` | enum | yes | (same as `checks[].owner`) |
| `fallback_patterns[].override_path` | enum | yes | (same as `checks[].override_path`) |
| `bot.signature_header` | string | yes | Must start with `<!--` |
| `bot.verdict_emoji.{blocker,warning,informational,unknown}` | string | yes | Non-empty |
| `bot.fallback_unknown.{severity,owner,override_path}` | enum | yes | Default classification for unknowns |

## Related

- Parent epic: [#842](https://github.com/meepleAi-app/meepleai-monorepo/issues/842) — DevOps Multi-Branch Strategy
- Sibling: [#850](https://github.com/meepleAi-app/meepleai-monorepo/issues/850) — CI cost optimization (pattern reuse: branch-aware concurrency, idempotency)
- Post-mortem template: [#1088](https://github.com/meepleAi-app/meepleai-monorepo/issues/1088)
