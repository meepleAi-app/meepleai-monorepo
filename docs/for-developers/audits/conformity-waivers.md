# Conformity Waivers — Audit Log

> **WS-C** (#1069, umbrella #1066) — AC-C.5.5
> **Status**: append-only by convention (git-as-audit-log). All entries are
> appended by the `conformity-debt-issue.yml` workflow via auto-PR. Manual
> edits other than appending a new row are reviewed and flagged at PR review
> time. Tampering is visible in `git log` / `git blame` of this file.

## Purpose

Every time a `conformity-waiver` label is applied to a PR, this log records:

- the **date** the waiver was registered
- the **origin PR** that triggered it
- the affected **routes** (from `mockup-ownership.bootstrap.json`)
- the **expiry date** after which `conformity-debt-gate.yml` will fail on PRs
  targeting `main-staging` / `main`
- the **waiver key** (dedup hash, also embedded in the matching debt-issue
  header)
- an **excerpt of the rationale** (first ~120 chars, raw text)

The full rationale and current status live in the corresponding `conformity-debt`
issue (linked via the waiver key marker `<!-- waiver-key: {key} -->`).

## How a row gets added

1. Author applies the `conformity-waiver` label to a PR.
2. Author posts a comment with the structured rationale block (see
   [`docs/for-developers/testing/frontend/mockup-conformity.md`](../testing/frontend/mockup-conformity.md)
   for the format and validator rules — AC-C.5.1).
3. `conformity-debt-issue.yml` parses the rationale, creates/updates the
   `conformity-debt` issue (AC-C.5.2), appends a row to this file under the
   current `YYYY-MM` section, and opens an auto-PR for human review.
4. A reviewer (typically `area/frontend`) approves and merges the auto-PR.

## How a row gets resolved

Rows are **not removed**. The debt-issue referenced via `waiver-key` carries
the state:

- closed via `Closes #<debt-issue>` on a remediation PR → status: remediated
- closed via `waiver-revalidated` label on a manual approval PR → status:
  manually-cleared
- still open past `Expiry` → `conformity-debt-gate.yml` blocks `main-staging` /
  `main` merges; reviewer manually decides to extend (new waiver) or remediate

For aggregate stats (active count, oldest expiring, top routes by debt) see
the AC-C.5.7 weekly summary at
`docs/for-developers/audits/conformity-waivers-summary.md` (lands in Phase 4c).

## Log

<!-- New entries appended below by conformity-debt-issue.yml. The most recent
     month appears first by reading order once a few months accumulate. -->

## 2026-05

| Date | PR | Routes | Expiry | Waiver key | Reason (excerpt) |
|------|-----|--------|--------|------------|-------------------|
| 2026-05-18 | [#1277](https://github.com/meepleAi-app/meepleai-monorepo/pull/1277) | `library,library-game-detail,player-detail,game-nights-index` | 2026-05-25 | `973242cdbd23bee8` | Refactor framework conformity baseline pattern — single-screen markers replace gallery-style fullPage capture. Baselin |
