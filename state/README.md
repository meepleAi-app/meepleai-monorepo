# `state/` — auto-generated workflow state

This directory contains JSON files written by recurring GitHub Actions
workflows. Files here are **committed to source control on purpose**: the diff
is the audit trail.

## Files

### `release-gate-digest.json`

Persistent state for `.github/workflows/release-gate-digest.yml` (issue #1446,
Phase 2c of #1016). Tracks `warning`-tier checks that have appeared in release
PRs week-over-week so the digest can compute deltas (`new` / `persisting` /
`resolved`) and trigger escalation flags after ≥ 4 consecutive weeks.

#### Schema (v1)

```jsonc
{
  "schemaVersion": 1,
  "lastWeekISO": "2026-W22",   // null on first run; ISO 8601 "YYYY-Www"
  "warningsByCheck": {
    "Frontend - A11y E2E": {
      "owner": "qa",
      "firstSeenWeek": "2026-W20",
      "weeksSeen": 3,
      "lastSeenWeek": "2026-W22"
    }
  }
}
```

#### Update flow

1. Cron fires at Monday 08:00 UTC (`scripts/release-gate/build-digest.mjs`).
2. CLI loads this file into memory.
3. CLI fetches release PRs from the last 7 days, parses bot comments, builds
   the digest via `lib/digest-builder.mjs`.
4. CLI posts the Slack message.
5. CLI opens a **state PR** on branch `chore/release-gate-digest-state-{weekISO}`
   that updates this file with the new state.
6. **A human maintainer merges the state PR** — no auto-merge.

The PR-not-direct-commit pattern is deliberate: it preserves an audit trail
(reviewer can spot drift) and avoids a race with concurrent release branches.

#### Schema drift policy

If `schemaVersion` here is older or newer than what the CLI expects, the CLI
treats the file as empty (graceful degrade) and emits a `[WARN]` log. Backfill
is not attempted; the next week's run reseeds the file with the current week's
data.

#### Backfill notes

This file was seeded empty on `2026-05-22` with PR #1446. Earlier weeks of
release PR warnings are not represented here.
