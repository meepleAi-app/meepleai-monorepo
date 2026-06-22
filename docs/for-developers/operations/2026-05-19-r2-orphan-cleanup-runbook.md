# R2 Orphan PDF Cleanup Runbook

**Date**: 2026-05-19 · **Issue**: [#520](https://github.com/meepleAi-app/meepleai-monorepo/issues/520) · **Scope**: Opt. C cleanup-only

## Context

Post-merge audit (PR #517, 2026-04-22) revealed ~82 orphan top-level prefixes under
`pdf_uploads/` on `meepleai-uploads` (R2 staging) with no matching row in
`pdf_documents.Id`, `PrivateGameId`, or `SharedGameId`. These are leftovers from
games removed from the catalog without storage cleanup.

The original issue body also proposed a `{gameId}` → `{pdfId}` layout migration,
but `S3BlobStorageService.cs:271` (codice live) still keys objects as
`pdf_uploads/{gameId}/{fileId}_{filename}`. That refactor is a separate epic
(tracked in the spawned follow-up issue) and is **out of scope** for this runbook.

This runbook removes the orphans via a reversible `_trash/` move (NOT a hard
delete) with a 7-day grace window before permanent purge.

## Pre-requisites

- Access to staging server (SSH key `~/.ssh/meepleai-staging`).
- R2 credentials present in `infra/secrets/storage.secret` (`S3_ENDPOINT`,
  `S3_ACCESS_KEY`, `S3_SECRET_KEY`, `S3_BUCKET_NAME`).
- Postgres readonly DSN to staging (`PG_DSN`).
- `aws` CLI v2 + `psql` available locally OR run from staging.
- 30-min maintenance window — uploads during cleanup are race-safe (script
  operates on classified orphans only, which by definition have no live row;
  in-flight uploads cannot target an orphan prefix), but lower-traffic windows
  reduce overall blast radius.

## Step 1 — Pre-flight audit (snapshot)

```bash
export BUCKET=meepleai-uploads
export S3_ENDPOINT="<r2-endpoint-from-storage.secret>"
export AWS_ACCESS_KEY_ID="<from-storage.secret>"
export AWS_SECRET_ACCESS_KEY="<from-storage.secret>"
export PG_DSN="<staging-readonly-dsn>"

./scripts/audit-pdf-storage.sh > docs/for-developers/operations/2026-05-19-r2-orphan-audit-pre.txt
echo "exit=$?"
```

Expected:
- Exit code `2` (orphans found) OR `0` (already clean).
- Summary line `unknown: N` is the orphan count to cleanup.
- Cross-check against issue #520 body baseline (`unknown: 82` at 2026-04-22).
  Drift >5% is OK but flag it in the PR description for traceability.

## Step 2 — Dry-run

```bash
./scripts/cleanup-orphan-pdfs.sh \
    --manifest docs/for-developers/operations/2026-05-19-r2-orphan-manifest-dryrun.tsv
```

Expected:
- Exit code `2` (orphans listed, no S3 writes).
- Manifest TSV lists each orphan prefix with `object_count`, `total_size_bytes`,
  status `dry-run`.
- Each `[DRY] mv …` line targets `_trash/2026-05-19/pdf_uploads/<prefix>/`.

Review the manifest. If any prefix looks load-bearing (e.g. matches a recent
upload that the audit missed because of replication lag), abort and
re-investigate.

## Step 3 — Apply

```bash
./scripts/cleanup-orphan-pdfs.sh --apply \
    --manifest docs/for-developers/operations/2026-05-19-r2-orphan-manifest-apply.tsv
```

Expected:
- Exit code `0`.
- Each line in manifest now has status `moved`.
- `aws s3 ls s3://meepleai-uploads/_trash/2026-05-19/pdf_uploads/` shows the
  rescued orphans.

If any prefix fails (status `ERROR`):
- Re-run the same command; `aws s3 mv` is idempotent — already-moved prefixes
  return cleanly because the source is now empty.
- Inspect logs for the failing prefix (likely transient R2 throttle or
  permission issue).

## Step 4 — Post-cleanup verification

```bash
./scripts/audit-pdf-storage.sh > docs/for-developers/operations/2026-05-19-r2-orphan-audit-post.txt
echo "exit=$?"
```

Expected:
- Exit code `0` (`unknown: 0`).
- `matched_pdf` and `matched_game` totals match the pre-audit snapshot exactly
  (no live data should have moved).

Smoke test (optional but recommended): retrieve a random PDF via the staging
API and verify download works.

```bash
curl -sSf -H "Cookie: <admin-session>" \
    "https://staging.meepleai.example.com/api/v1/pdfs/<some-fileId>/download" \
    -o /tmp/smoke.pdf
```

## Step 5 — Restore (only if needed)

If a prefix turns out to be live (e.g. user reports a missing PDF):

```bash
PREFIX_TO_RESTORE="<orphan-prefix>"
aws --endpoint-url "$S3_ENDPOINT" s3 mv \
    "s3://$BUCKET/_trash/2026-05-19/pdf_uploads/$PREFIX_TO_RESTORE/" \
    "s3://$BUCKET/pdf_uploads/$PREFIX_TO_RESTORE/" \
    --recursive
```

Document the restore in the PR / incident report.

## Step 6 — Permanent purge (after grace window)

After 7 days (default; adjust per the change board if needed) AND only after
confirmation from the operations channel that no restore requests came in:

```bash
PURGE_DATE="2026-05-19"
aws --endpoint-url "$S3_ENDPOINT" s3 rm \
    "s3://$BUCKET/_trash/$PURGE_DATE/" \
    --recursive
```

Re-run the audit one last time to confirm storage clean state, then archive the
manifest files alongside this runbook for traceability.

## Observability / artifacts

| File | Purpose |
|---|---|
| `2026-05-19-r2-orphan-audit-pre.txt` | Audit snapshot BEFORE cleanup |
| `2026-05-19-r2-orphan-manifest-dryrun.tsv` | Planned moves (review gate) |
| `2026-05-19-r2-orphan-manifest-apply.tsv` | Executed moves (per-prefix status) |
| `2026-05-19-r2-orphan-audit-post.txt` | Audit AFTER cleanup (`unknown: 0`) |

## Related

- Issue #520 (this runbook) — Opt. C orphan cleanup.
- Spawned follow-up: refactor `IBlobStorageService` layout `{gameId}` → `{pdfId}`
  (deferred epic — see the linked issue in the PR closing #520).
- `scripts/audit-pdf-storage.sh` — read-only classification.
- `scripts/cleanup-orphan-pdfs.sh` — this runbook's executor.
