# Release-gate Bot

Bot that auto-classifies CI failures on release PRs (`main-dev → main-staging`).

**Operator manual**: [`docs/for-developers/operations/release-gate-bot.md`](../../docs/for-developers/operations/release-gate-bot.md)
**Design doc**: [`docs/for-developers/specs/2026-05-22-release-gate-spreadsheet.md`](../../docs/for-developers/specs/2026-05-22-release-gate-spreadsheet.md)
**Issue**: [#1016](https://github.com/meepleAi-app/meepleai-monorepo/issues/1016)

## Quick commands

```bash
pnpm install                      # install deps
pnpm test                         # run 56-test Vitest suite
pnpm validate                     # validate .github/release-gates.yml schema
DRY_RUN=1 pnpm comment            # preview comment body + summary
```

## Files

```
scripts/release-gate/
├── package.json          # @meepleai/release-gate (private)
├── vitest.config.mjs
├── README.md             # this file
├── validate.mjs          # CLI entry: schema validator (exit 0/1/2)
├── comment.mjs           # CLI entry: bot (octokit + classification + comment)
├── lib/
│   ├── classify.mjs      # pure: classify(check_name, gates) → {severity, owner, override_path, ...}
│   ├── format.mjs        # pure: formatComment + formatActionsSummary
│   └── validate.mjs      # pure: validateGates + validateGatesFile
└── __tests__/
    ├── classify.test.mjs
    ├── format.test.mjs
    ├── validate.test.mjs
    ├── integration.test.mjs
    └── fixtures/
        ├── release-gates-valid.yml
        ├── release-gates-invalid-version.yml
        ├── release-gates-invalid-severity.yml
        └── release-gates-missing-field.yml
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
