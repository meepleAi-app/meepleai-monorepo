# Release-gate Bot

Bot that auto-classifies CI failures on release PRs (`main-dev → main-staging`).

**Operator manual**: [`docs/for-developers/operations/release-gate-bot.md`](../../docs/for-developers/operations/release-gate-bot.md)
**Design doc**: [`docs/for-developers/specs/2026-05-22-release-gate-spreadsheet.md`](../../docs/for-developers/specs/2026-05-22-release-gate-spreadsheet.md)
**Phase 2 overview**: [`docs/for-developers/specs/2026-05-22-release-gate-phase2-overview.md`](../../docs/for-developers/specs/2026-05-22-release-gate-phase2-overview.md)
**Issue**: [#1016](https://github.com/meepleAi-app/meepleai-monorepo/issues/1016) (Phase 1) · [#1444](https://github.com/meepleAi-app/meepleai-monorepo/issues/1444) (Phase 2a — verdict check) · [#1446](https://github.com/meepleAi-app/meepleai-monorepo/issues/1446) (Phase 2c — weekly digest)

## Phases at a glance

| Phase | Status | What it does | Toggle |
|---|---|---|---|
| **1** — Bot comment | ✅ live | Posts/edits a single comment on release PRs with the per-check classification table | always on |
| **2a** — Verdict check | 🚦 dispatch-gated until 2026-06-08 | Synthesizes a single `Release-Gate Verdict` check-run (success / neutral / failure) via the Checks API so reviewers gate on one row instead of N | `bot.phase2a.enabled` in `.github/release-gates.yml` (default: `false` until dispatch gate opens, then flip to `true`) |
| **2c** — Weekly digest | ✅ live | Monday cron posts a Slack digest of warning-tier classifications + escalation suggestions | `bot.phase2c.enabled` |

## Quick commands

```bash
pnpm install                                  # install deps
pnpm test                                     # run 93-test Vitest suite
pnpm validate                                 # validate .github/release-gates.yml schema
DRY_RUN=1 pnpm comment                        # preview classification comment body
DRY_RUN=1 pnpm digest                         # preview weekly digest Slack payload
```

## Files

```
scripts/release-gate/
├── package.json              # @meepleai/release-gate (private)
├── vitest.config.mjs
├── README.md                 # this file
├── validate.mjs              # CLI entry: schema validator (exit 0/1/2)
├── comment.mjs               # CLI entry: bot (Phase 1)
├── build-digest.mjs          # CLI entry: weekly digest (Phase 2c, #1446)
├── lib/
│   ├── classify.mjs            # pure: classify(check_name, gates)
│   ├── format.mjs              # pure: formatComment + formatActionsSummary (Phase 1)
│   ├── validate.mjs            # pure: validateGates + validateGatesFile
│   ├── conclusion-override.mjs # pure: computeVerdict (Phase 2a, #1444)
│   ├── parse-bot-comment.mjs   # pure: parseBotComment + pickLatestBotComment (Phase 2c)
│   └── digest-builder.mjs      # pure: buildDigest + isoWeek (Phase 2c)
└── __tests__/
    ├── classify.test.mjs              # 16 tests
    ├── format.test.mjs                # 18 tests
    ├── validate.test.mjs              # 22 tests (Phase 1 + 2c + 2a schemas)
    ├── integration.test.mjs           # 20 tests (Phase 1 + Phase 2a publishVerdict)
    ├── conclusion-override.test.mjs   # 22 tests (Phase 2a pure synthesizer)
    ├── parse-bot-comment.test.mjs     # 15 tests (Phase 2c)
    ├── digest-builder.test.mjs        # 15 tests (Phase 2c)
    └── fixtures/
        ├── release-gates-valid.yml
        ├── release-gates-invalid-version.yml
        ├── release-gates-invalid-severity.yml
        ├── release-gates-missing-field.yml
        └── sample-bot-comment.md    # Phase 2c parser fixture
```

## Env

| Var | Required | Notes |
|---|---|---|
| `GITHUB_TOKEN` | yes (unless `DRY_RUN=1`) | Provided automatically in GH Actions |
| `GITHUB_REPOSITORY` | yes | e.g. `meepleAi-app/meepleai-monorepo` |
| `PR_NUMBER` | yes | Release PR number |
| `GITHUB_SHA` | no | Head commit SHA; falls back to `pulls.get` if missing |
| `RELEASE_GATES_YAML` | no | Override default `.github/release-gates.yml` |
| `GITHUB_STEP_SUMMARY` | no | Auto-set by GH Actions runner |
| `DRY_RUN` | no | If `1`, print to stdout, no API calls |
| `DRY_RUN_FAILURES` | no | Comma-separated check names for dry-run sample |
