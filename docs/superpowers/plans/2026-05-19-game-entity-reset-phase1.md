# Game Entity Reset — Phase 1: Backup Infrastructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the safety net (backup scripts + rollback rehearsal harness) so Phase 3 can execute the reset against dev/staging/prod confidently. No schema change. No code refactor. Output is a set of bash scripts, a gitignore update, and a verified rollback procedure.

**Architecture:** Three bash scripts under `infra/scripts/game-reset/` driven by environment-specific config (`.env`-style). Each script idempotent and dry-runnable. Backup output goes to gitignored `infra/backups/`. Rollback procedure rehearsed against a disposable Docker postgres before Phase 3 ever runs against real DBs.

**Tech Stack:** Bash · `pg_dump` / `pg_restore` (PostgreSQL 16) · `psql` · Docker Compose · GNU Make · spec doc `docs/for-developers/specs/2026-05-19-game-entity-reset.md` (source of truth for SQL)

**Tracking issue**: [#1320](https://github.com/meepleAi-app/meepleai-monorepo/issues/1320)

**Parent spec**: [`docs/for-developers/specs/2026-05-19-game-entity-reset.md`](../../for-developers/specs/2026-05-19-game-entity-reset.md) — sections §4 Step 0, Step 1, Step 6

---

## File structure

| File | Responsibility |
|---|---|
| `infra/backups/.gitkeep` | Marker file so empty directory is tracked |
| `infra/backups/README.md` | Documents structure, naming convention, retention policy |
| `.gitignore` (modify) | Exclude `infra/backups/*.dump`, `*.csv`, `*.txt` |
| `infra/scripts/game-reset/.env.example` | Template for env-specific config (DATABASE_URL, ENV_NAME) |
| `infra/scripts/game-reset/01-backup.sh` | Pre-flight backup: pg_dump + vector count snapshot |
| `infra/scripts/game-reset/02-export-mapping.sh` | Mapping CSV export (§4 Step 1 of spec) |
| `infra/scripts/game-reset/99-rollback.sh` | pg_restore from snapshot (§4 Step 6 of spec) |
| `infra/scripts/game-reset/lib/common.sh` | Shared helpers: env loading, logging, safety checks |
| `infra/scripts/game-reset/README.md` | How-to: load env, run scripts, sequence, troubleshooting |
| `infra/Makefile` (modify) | Targets `game-reset-backup`, `game-reset-rollback-rehearse` |
| `tests/scripts/game-reset/rehearse-rollback.sh` | End-to-end rollback test against disposable docker postgres |

---

## Task 1: Create backups directory + gitignore

**Files:**
- Create: `infra/backups/.gitkeep`
- Create: `infra/backups/README.md`
- Modify: `.gitignore`

- [ ] **Step 1: Create the directory marker**

```bash
mkdir -p infra/backups
touch infra/backups/.gitkeep
```

- [ ] **Step 2: Create `infra/backups/README.md`**

```markdown
# Backups directory

Local backup artifacts for database reset operations (issue #1320).

## Contents (all gitignored)

| Pattern | Source | Retention |
|---|---|---|
| `*.dump` | `pg_dump -Fc` snapshots | 30 days local; off-machine archive for prod |
| `*.csv` | Mapping exports for vector re-link | 30 days local |
| `*.txt` | Verification artefacts (vector counts, gate outputs) | 30 days local |

## Naming convention

`<YYYY-MM-DD>-<env>-<purpose>.<ext>`

Examples:
- `2026-05-19-dev-pre-game-reset.dump`
- `2026-05-19-dev-game-mapping.csv`
- `2026-05-19-dev-vector-count-pre.txt`

## NEVER commit dump files

Dumps may contain PII, secrets, or unreleased product data. The `.gitkeep` marker is the only committed file in this directory.
```

- [ ] **Step 3: Modify `.gitignore`**

Append to `.gitignore`:

```gitignore
# Backups directory (issue #1320 — game entity reset)
infra/backups/*.dump
infra/backups/*.csv
infra/backups/*.txt
!infra/backups/.gitkeep
!infra/backups/README.md
```

- [ ] **Step 4: Verify gitignore works**

Run:

```bash
touch infra/backups/dummy.dump
git status --short infra/backups/
```

Expected: only `infra/backups/.gitkeep` and `infra/backups/README.md` shown as new files (when staged); `dummy.dump` not listed.

Cleanup:

```bash
rm infra/backups/dummy.dump
```

- [ ] **Step 5: Commit**

```bash
git add infra/backups/.gitkeep infra/backups/README.md .gitignore
git commit -m "chore(infra): scaffold infra/backups/ with gitignore (refs #1320)"
```

---

## Task 2: Environment config template

**Files:**
- Create: `infra/scripts/game-reset/.env.example`
- Create: `infra/scripts/game-reset/lib/common.sh`

- [ ] **Step 1: Create directory tree**

```bash
mkdir -p infra/scripts/game-reset/lib
```

- [ ] **Step 2: Write `infra/scripts/game-reset/.env.example`**

```bash
# Copy this to .env.<env-name> (e.g. .env.dev, .env.staging, .env.prod)
# and fill in the values. NEVER commit the filled .env.* files.

# Friendly name for the environment (used in backup filenames and log output)
ENV_NAME="dev"

# Full PostgreSQL connection string for the target environment
# Format: postgresql://user:pass@host:port/dbname
DATABASE_URL="postgresql://meepleai:meepleai@localhost:5432/meepleai_dev"

# Admin connection string (for DROP DATABASE during rollback rehearsal only)
# Must connect to a database OTHER than DATABASE_URL's dbname (e.g. 'postgres')
DATABASE_URL_ADMIN="postgresql://meepleai:meepleai@localhost:5432/postgres"

# Target dbname (must match the dbname in DATABASE_URL above)
DATABASE_NAME="meepleai_dev"

# Output directory for dump/CSV/txt artefacts (defaults to ../../backups relative to script dir)
BACKUP_DIR="${BACKUP_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/../../backups" && pwd)}"
```

- [ ] **Step 3: Add `.env.*` files to gitignore**

Append to `.gitignore`:

```gitignore
# Per-environment config for game-reset scripts (never commit actual values)
infra/scripts/game-reset/.env.*
!infra/scripts/game-reset/.env.example
```

- [ ] **Step 4: Write `infra/scripts/game-reset/lib/common.sh`**

```bash
#!/usr/bin/env bash
# Shared helpers for game-reset scripts.
# Source this file at the top of every script: source "$(dirname "$0")/lib/common.sh"

set -euo pipefail

# Logging helpers
log_info()  { printf '\033[0;36m[INFO]\033[0m  %s\n' "$*" >&2; }
log_warn()  { printf '\033[1;33m[WARN]\033[0m  %s\n' "$*" >&2; }
log_error() { printf '\033[0;31m[ERROR]\033[0m %s\n' "$*" >&2; }
log_ok()    { printf '\033[0;32m[OK]\033[0m    %s\n' "$*" >&2; }

# Load env file passed as first argument; exit if missing
load_env() {
  local env_file="${1:-}"
  if [[ -z "$env_file" ]]; then
    log_error "Usage: $0 <path-to-env-file>"
    log_error "Example: $0 infra/scripts/game-reset/.env.dev"
    exit 64  # EX_USAGE
  fi
  if [[ ! -f "$env_file" ]]; then
    log_error "Env file not found: $env_file"
    exit 66  # EX_NOINPUT
  fi
  # shellcheck disable=SC1090
  source "$env_file"

  : "${ENV_NAME:?ENV_NAME must be set in $env_file}"
  : "${DATABASE_URL:?DATABASE_URL must be set in $env_file}"
  : "${DATABASE_NAME:?DATABASE_NAME must be set in $env_file}"
  : "${BACKUP_DIR:?BACKUP_DIR must be set in $env_file}"

  mkdir -p "$BACKUP_DIR"
  log_info "Loaded env: ENV_NAME=$ENV_NAME DATABASE=$DATABASE_NAME"
}

# Refuse to run against production unless --i-mean-it is passed
guard_prod() {
  if [[ "${ENV_NAME:-}" == "prod" ]]; then
    if [[ "${1:-}" != "--i-mean-it" ]]; then
      log_error "ENV_NAME=prod detected. Refusing to run without --i-mean-it flag."
      log_error "Re-run as: $0 <env-file> --i-mean-it"
      exit 77  # EX_NOPERM
    fi
    log_warn "Running against PROD with --i-mean-it. Last chance to ctrl-c (sleeping 5s)..."
    sleep 5
  fi
}

# Standard timestamped artefact path
artefact_path() {
  local purpose="$1"  # e.g. pre-game-reset
  local ext="$2"      # e.g. dump
  local date_str
  date_str="$(date +%Y-%m-%d)"
  echo "$BACKUP_DIR/${date_str}-${ENV_NAME}-${purpose}.${ext}"
}
```

- [ ] **Step 5: Verify common.sh syntax**

Run:

```bash
bash -n infra/scripts/game-reset/lib/common.sh
```

Expected: no output, exit code 0.

- [ ] **Step 6: Commit**

```bash
git add infra/scripts/game-reset/.env.example infra/scripts/game-reset/lib/common.sh .gitignore
git commit -m "feat(infra): game-reset env template and shared helpers (refs #1320)"
```

---

## Task 3: Backup script `01-backup.sh`

**Files:**
- Create: `infra/scripts/game-reset/01-backup.sh`
- Test: manual run against local docker postgres

- [ ] **Step 1: Write the script**

```bash
#!/usr/bin/env bash
# Phase 1 / Step 0 of spec: pre-flight backup before game entity reset.
# Produces:
#   - <date>-<env>-pre-game-reset.dump  (pg_dump -Fc full snapshot)
#   - <date>-<env>-vector-count-pre.txt (baseline vector count for verification gate)
#
# Usage: ./01-backup.sh <env-file> [--i-mean-it]

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

load_env "${1:-}"
guard_prod "${2:-}"

dump_file=$(artefact_path "pre-game-reset" "dump")
count_file=$(artefact_path "vector-count-pre" "txt")

if [[ -f "$dump_file" ]]; then
  log_error "Refusing to overwrite existing dump: $dump_file"
  log_error "Move or delete the old file first."
  exit 73  # EX_CANTCREAT
fi

log_info "Dumping $DATABASE_NAME → $dump_file"
pg_dump "$DATABASE_URL" -Fc --no-owner --no-acl -f "$dump_file"

dump_size=$(stat -c '%s' "$dump_file" 2>/dev/null || stat -f '%z' "$dump_file")
if [[ "$dump_size" -lt 1024 ]]; then
  log_error "Dump file is suspiciously small ($dump_size bytes). Aborting."
  exit 70  # EX_SOFTWARE
fi
log_ok "Dump created: $dump_file ($(numfmt --to=iec --suffix=B "$dump_size" 2>/dev/null || echo "${dump_size}B"))"

log_info "Recording baseline vector count → $count_file"
psql "$DATABASE_URL" -t -A -c "SELECT COUNT(*) FROM pgvector_embeddings;" > "$count_file"

count=$(cat "$count_file")
log_ok "Baseline vector count: $count"

log_info "Backup complete. Artefacts:"
log_info "  $dump_file"
log_info "  $count_file"
```

- [ ] **Step 2: Make executable**

```bash
chmod +x infra/scripts/game-reset/01-backup.sh
```

- [ ] **Step 3: Verify syntax**

```bash
bash -n infra/scripts/game-reset/01-backup.sh
```

Expected: no output, exit 0.

- [ ] **Step 4: Create local dev env file (not committed)**

```bash
cp infra/scripts/game-reset/.env.example infra/scripts/game-reset/.env.dev
# Edit .env.dev to match your local docker postgres credentials
```

- [ ] **Step 5: Test against local dev DB**

Ensure local dev stack is running (`make dev-core` from `infra/`). Then:

```bash
./infra/scripts/game-reset/01-backup.sh infra/scripts/game-reset/.env.dev
```

Expected:
- Two files appear in `infra/backups/`: `<date>-dev-pre-game-reset.dump` and `<date>-dev-vector-count-pre.txt`
- Log lines `[OK] Dump created: ...` and `[OK] Baseline vector count: <N>`
- Exit code 0

- [ ] **Step 6: Verify re-run is idempotent-safe (refuses overwrite)**

Run the same command again:

```bash
./infra/scripts/game-reset/01-backup.sh infra/scripts/game-reset/.env.dev
```

Expected: `[ERROR] Refusing to overwrite existing dump: ...`, exit 73.

- [ ] **Step 7: Cleanup test artefacts**

```bash
rm infra/backups/*.dump infra/backups/*.txt
```

- [ ] **Step 8: Commit**

```bash
git add infra/scripts/game-reset/01-backup.sh
git commit -m "feat(infra): game-reset 01-backup.sh pre-flight snapshot (refs #1320)"
```

---

## Task 4: Mapping CSV export script `02-export-mapping.sh`

**Files:**
- Create: `infra/scripts/game-reset/02-export-mapping.sh`
- Reference: spec §4 Step 1 (UNION ALL query)

- [ ] **Step 1: Write the script**

```bash
#!/usr/bin/env bash
# Phase 1 / Step 1 of spec: export mapping CSV bridging old game IDs → PDF identity.
# Produces:
#   - <date>-<env>-game-mapping.csv (one row per game-pdf pair, UNION over 3 sources)
#
# Pre-condition: 01-backup.sh has been run (so we have a recovery point).
#
# Usage: ./02-export-mapping.sh <env-file> [--i-mean-it]

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

load_env "${1:-}"
guard_prod "${2:-}"

csv_file=$(artefact_path "game-mapping" "csv")
sanity_file=$(artefact_path "game-mapping-sanity" "txt")

# Verify pre-flight backup was run today for this env (presence of dump file)
expected_dump=$(artefact_path "pre-game-reset" "dump")
if [[ ! -f "$expected_dump" ]]; then
  log_error "No backup dump found for today/$ENV_NAME. Run 01-backup.sh first."
  log_error "Expected: $expected_dump"
  exit 72  # EX_OSFILE
fi
log_ok "Pre-flight backup present: $expected_dump"

if [[ -f "$csv_file" ]]; then
  log_error "Refusing to overwrite existing CSV: $csv_file"
  exit 73
fi

log_info "Running pre-export sanity check (vectors without re-link source)..."
orphans=$(psql "$DATABASE_URL" -t -A -c "
  SELECT COUNT(DISTINCT pe.game_id)
  FROM pgvector_embeddings pe
  WHERE pe.game_id NOT IN (
    SELECT vd.game_id FROM vector_documents vd WHERE vd.game_id IS NOT NULL
    UNION
    SELECT vd.shared_game_id FROM vector_documents vd WHERE vd.shared_game_id IS NOT NULL
  );
")

echo "orphan_game_ids_in_vectors=$orphans" > "$sanity_file"
if [[ "$orphans" -gt 0 ]]; then
  log_warn "$orphans distinct game_id values in pgvector_embeddings cannot be re-linked via vector_documents."
  log_warn "They will be captured under 'orphan' source_kind in the CSV."
fi
log_ok "Sanity check recorded: $sanity_file"

log_info "Exporting mapping CSV → $csv_file"
psql "$DATABASE_URL" -c "\copy (
  SELECT
    'game'                       AS source_kind,
    vd.game_id                   AS old_game_id,
    g.title                      AS game_title,
    g.bgg_id,
    vd.id                        AS vector_document_id,
    vd.pdf_document_id,
    pd.file_name                 AS pdf_filename,
    pd.file_path                 AS pdf_path,
    pd.metadata->>'sha256'       AS pdf_sha256
  FROM vector_documents vd
  LEFT JOIN games g          ON g.id = vd.game_id
  LEFT JOIN pdf_documents pd ON pd.id = vd.pdf_document_id
  WHERE vd.game_id IS NOT NULL

  UNION ALL

  SELECT
    'shared'                     AS source_kind,
    vd.shared_game_id            AS old_game_id,
    sg.title,
    sg.bgg_id,
    vd.id,
    vd.pdf_document_id,
    pd.file_name,
    pd.file_path,
    pd.metadata->>'sha256'
  FROM vector_documents vd
  LEFT JOIN shared_games sg  ON sg.id = vd.shared_game_id
  LEFT JOIN pdf_documents pd ON pd.id = vd.pdf_document_id
  WHERE vd.shared_game_id IS NOT NULL

  UNION ALL

  SELECT
    'orphan'                     AS source_kind,
    pe.game_id                   AS old_game_id,
    NULL                         AS game_title,
    NULL                         AS bgg_id,
    NULL                         AS vector_document_id,
    NULL                         AS pdf_document_id,
    NULL, NULL, NULL
  FROM pgvector_embeddings pe
  LEFT JOIN vector_documents vd ON vd.game_id = pe.game_id OR vd.shared_game_id = pe.game_id
  WHERE vd.id IS NULL
  GROUP BY pe.game_id
) TO STDOUT WITH CSV HEADER" > "$csv_file"

rows=$(wc -l < "$csv_file")
log_ok "CSV exported: $csv_file ($rows lines including header)"
```

- [ ] **Step 2: Make executable + syntax check**

```bash
chmod +x infra/scripts/game-reset/02-export-mapping.sh
bash -n infra/scripts/game-reset/02-export-mapping.sh
```

Expected: exit 0.

- [ ] **Step 3: Test pre-condition guard (no backup → must fail)**

```bash
rm -f infra/backups/*.dump
./infra/scripts/game-reset/02-export-mapping.sh infra/scripts/game-reset/.env.dev
```

Expected: `[ERROR] No backup dump found for today/dev. Run 01-backup.sh first.`, exit 72.

- [ ] **Step 4: Test happy path**

```bash
./infra/scripts/game-reset/01-backup.sh infra/scripts/game-reset/.env.dev
./infra/scripts/game-reset/02-export-mapping.sh infra/scripts/game-reset/.env.dev
```

Expected:
- CSV file created
- Sanity file shows `orphan_game_ids_in_vectors=<N>` (likely 0 on fresh dev DB)
- Exit 0

- [ ] **Step 5: Inspect CSV header**

```bash
head -1 infra/backups/*-dev-game-mapping.csv
```

Expected: `source_kind,old_game_id,game_title,bgg_id,vector_document_id,pdf_document_id,pdf_filename,pdf_path,pdf_sha256`

- [ ] **Step 6: Cleanup test artefacts**

```bash
rm infra/backups/*.dump infra/backups/*.csv infra/backups/*.txt
```

- [ ] **Step 7: Commit**

```bash
git add infra/scripts/game-reset/02-export-mapping.sh
git commit -m "feat(infra): game-reset 02-export-mapping.sh with UNION over 3 sources (refs #1320)"
```

---

## Task 5: Rollback script `99-rollback.sh`

**Files:**
- Create: `infra/scripts/game-reset/99-rollback.sh`
- Reference: spec §4 Step 6

- [ ] **Step 1: Write the script**

```bash
#!/usr/bin/env bash
# Phase 1 / Step 6 of spec: restore DB from a pre-flight backup.
# Drops and recreates the target database, then pg_restore from the dump.
#
# Usage: ./99-rollback.sh <env-file> <dump-file> [--i-mean-it]

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

load_env "${1:-}"
dump_file="${2:-}"
guard_prod "${3:-}"

if [[ -z "$dump_file" ]]; then
  log_error "Usage: $0 <env-file> <dump-file> [--i-mean-it]"
  exit 64
fi
if [[ ! -f "$dump_file" ]]; then
  log_error "Dump file not found: $dump_file"
  exit 66
fi

: "${DATABASE_URL_ADMIN:?DATABASE_URL_ADMIN must be set in env file for rollback}"

log_warn "About to DROP DATABASE $DATABASE_NAME and restore from $dump_file"
log_warn "All current data in $DATABASE_NAME will be lost."

# Confirmation prompt (skipped if --i-mean-it passed, see guard_prod)
if [[ "${3:-}" != "--i-mean-it" && "${ENV_NAME:-}" != "prod" ]]; then
  read -r -p "Type the database name ($DATABASE_NAME) to confirm: " confirm
  if [[ "$confirm" != "$DATABASE_NAME" ]]; then
    log_error "Confirmation mismatch. Aborting."
    exit 65  # EX_DATAERR
  fi
fi

log_info "Terminating active connections to $DATABASE_NAME..."
psql "$DATABASE_URL_ADMIN" -c "
  SELECT pg_terminate_backend(pid)
  FROM pg_stat_activity
  WHERE datname = '$DATABASE_NAME' AND pid <> pg_backend_pid();
" > /dev/null

log_info "Dropping database $DATABASE_NAME..."
psql "$DATABASE_URL_ADMIN" -c "DROP DATABASE IF EXISTS \"$DATABASE_NAME\";"

log_info "Recreating database $DATABASE_NAME..."
psql "$DATABASE_URL_ADMIN" -c "CREATE DATABASE \"$DATABASE_NAME\";"

log_info "Restoring from $dump_file..."
pg_restore -d "$DATABASE_URL" --no-owner --no-acl "$dump_file"

restored_count=$(psql "$DATABASE_URL" -t -A -c "SELECT COUNT(*) FROM pgvector_embeddings;" 2>/dev/null || echo "?")
log_ok "Restore complete. pgvector_embeddings rows: $restored_count"

table_count=$(psql "$DATABASE_URL" -t -A -c "
  SELECT COUNT(*) FROM pg_tables WHERE schemaname='public';
")
log_ok "Public tables in restored DB: $table_count"
```

- [ ] **Step 2: Make executable + syntax check**

```bash
chmod +x infra/scripts/game-reset/99-rollback.sh
bash -n infra/scripts/game-reset/99-rollback.sh
```

Expected: exit 0.

- [ ] **Step 3: Commit (testing happens in Task 7)**

```bash
git add infra/scripts/game-reset/99-rollback.sh
git commit -m "feat(infra): game-reset 99-rollback.sh restore from dump (refs #1320)"
```

---

## Task 6: Scripts README

**Files:**
- Create: `infra/scripts/game-reset/README.md`

- [ ] **Step 1: Write the README**

```markdown
# game-reset scripts

Helpers for issue #1320 (game entity reset). See parent spec:
`docs/for-developers/specs/2026-05-19-game-entity-reset.md`.

## Sequence

| Phase | Script | When |
|---|---|---|
| Pre-flight | `01-backup.sh` | Before any destructive operation |
| Mapping export | `02-export-mapping.sh` | After backup, before DROP |
| Drop + reset | (in Phase 3 implementation plan) | After Phase 2 code is merged |
| Re-link vectors | (in Phase 3 implementation plan) | After EF migration applied |
| Rollback | `99-rollback.sh` | Emergency only |

## Setup

```bash
# 1. Copy env template
cp .env.example .env.dev
# Edit .env.dev: set DATABASE_URL, DATABASE_URL_ADMIN, DATABASE_NAME

# 2. Repeat for staging / prod as needed
cp .env.example .env.staging
cp .env.example .env.prod
```

`.env.*` files are gitignored. Each developer / operator maintains their own.

## Run order (per environment)

```bash
./01-backup.sh .env.dev
./02-export-mapping.sh .env.dev
# ... Phase 3 scripts ...
```

## Production safety

Scripts targeting `ENV_NAME=prod` require `--i-mean-it` as last argument. Example:

```bash
./01-backup.sh .env.prod --i-mean-it
```

Without the flag, scripts abort with exit 77.

## Rollback rehearsal

Before running any reset against staging or prod, rehearse the rollback against a disposable docker postgres:

```bash
./tests/scripts/game-reset/rehearse-rollback.sh
```

This script:
1. Spins up a temporary postgres container
2. Loads a synthetic dump
3. Mutates the DB
4. Runs `99-rollback.sh`
5. Verifies the DB matches the original state
6. Tears down the container

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Success |
| 64 | Usage error (bad arguments) |
| 65 | Confirmation mismatch (DB name typed wrong) |
| 66 | Missing input file (env file or dump file) |
| 70 | Software error (dump too small, etc.) |
| 72 | Missing pre-condition artefact (e.g. no backup before mapping export) |
| 73 | Refusing to overwrite existing artefact |
| 77 | Production safety guard: `--i-mean-it` not passed |

## Troubleshooting

**"connection refused"**: docker postgres not running. Run `make dev-core` from `infra/`.

**"role does not exist"**: check `DATABASE_URL` credentials match your docker compose env.

**"permission denied"**: scripts not executable. Run `chmod +x *.sh lib/*.sh`.
```

- [ ] **Step 2: Commit**

```bash
git add infra/scripts/game-reset/README.md
git commit -m "docs(infra): game-reset scripts README (refs #1320)"
```

---

## Task 7: Rollback rehearsal harness

**Files:**
- Create: `tests/scripts/game-reset/rehearse-rollback.sh`
- Create: `tests/scripts/game-reset/fixtures/synthetic-dump.sql`

- [ ] **Step 1: Create directory tree**

```bash
mkdir -p tests/scripts/game-reset/fixtures
```

- [ ] **Step 2: Write `tests/scripts/game-reset/fixtures/synthetic-dump.sql`**

This is the minimal schema + data that simulates the parts we care about (vectors + games).

```sql
-- Minimal synthetic schema for rollback rehearsal.
-- Mirrors pgvector_embeddings + games surface enough to verify restore.

CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE games (
    id           uuid PRIMARY KEY,
    title        text NOT NULL,
    created_at   timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE pgvector_embeddings (
    id           uuid PRIMARY KEY,
    game_id      uuid NOT NULL,
    text_content text NOT NULL,
    vector       vector(768) NOT NULL,
    created_at   timestamptz NOT NULL DEFAULT now()
);

-- Seed: 3 games, 5 vectors
INSERT INTO games (id, title) VALUES
  ('00000000-0000-0000-0000-000000000001', 'Catan'),
  ('00000000-0000-0000-0000-000000000002', 'Wingspan'),
  ('00000000-0000-0000-0000-000000000003', 'Terraforming Mars');

-- Use a zero-vector for simplicity (HNSW search isn't tested here)
INSERT INTO pgvector_embeddings (id, game_id, text_content, vector)
SELECT
  gen_random_uuid(),
  ('00000000-0000-0000-0000-00000000000' || ((i % 3) + 1))::uuid,
  'chunk ' || i,
  array_fill(0::real, ARRAY[768])::vector
FROM generate_series(1, 5) AS s(i);
```

- [ ] **Step 3: Write `tests/scripts/game-reset/rehearse-rollback.sh`**

```bash
#!/usr/bin/env bash
# End-to-end rollback rehearsal:
# 1. Spin up disposable postgres container
# 2. Apply synthetic schema + seed
# 3. Backup → mutate → rollback
# 4. Verify state matches original
# 5. Teardown

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
FIXTURES="$SCRIPT_DIR/fixtures"

CONTAINER_NAME="meepleai-rollback-rehearse"
DB_USER="rehearse"
DB_PASS="rehearse"
DB_NAME="rehearse_db"
DB_PORT="55432"
DB_HOST="localhost"

cleanup() {
  docker rm -f "$CONTAINER_NAME" > /dev/null 2>&1 || true
  rm -rf "$REPO_ROOT/infra/backups/rehearse-"*.dump 2>/dev/null || true
}
trap cleanup EXIT

echo "[1/7] Spinning up disposable postgres..."
docker run -d --rm \
  --name "$CONTAINER_NAME" \
  -e POSTGRES_USER="$DB_USER" \
  -e POSTGRES_PASSWORD="$DB_PASS" \
  -e POSTGRES_DB="$DB_NAME" \
  -p "$DB_PORT:5432" \
  pgvector/pgvector:pg16 > /dev/null

echo "[2/7] Waiting for postgres to be ready..."
for i in {1..30}; do
  if docker exec "$CONTAINER_NAME" pg_isready -U "$DB_USER" > /dev/null 2>&1; then
    break
  fi
  sleep 1
done

DB_URL="postgresql://$DB_USER:$DB_PASS@$DB_HOST:$DB_PORT/$DB_NAME"
DB_URL_ADMIN="postgresql://$DB_USER:$DB_PASS@$DB_HOST:$DB_PORT/postgres"

echo "[3/7] Loading synthetic schema and seed data..."
PGPASSWORD="$DB_PASS" psql "$DB_URL" -f "$FIXTURES/synthetic-dump.sql" > /dev/null

original_count=$(PGPASSWORD="$DB_PASS" psql "$DB_URL" -t -A -c "SELECT COUNT(*) FROM pgvector_embeddings;")
original_games=$(PGPASSWORD="$DB_PASS" psql "$DB_URL" -t -A -c "SELECT COUNT(*) FROM games;")
echo "    Original state: $original_games games, $original_count vectors"

echo "[4/7] Creating temp env file and running 01-backup.sh..."
TMP_ENV="$(mktemp)"
cat > "$TMP_ENV" <<EOF
ENV_NAME="rehearse"
DATABASE_URL="$DB_URL"
DATABASE_URL_ADMIN="$DB_URL_ADMIN"
DATABASE_NAME="$DB_NAME"
BACKUP_DIR="$REPO_ROOT/infra/backups"
EOF

PGPASSWORD="$DB_PASS" "$REPO_ROOT/infra/scripts/game-reset/01-backup.sh" "$TMP_ENV"
dump_file=$(ls -t "$REPO_ROOT/infra/backups/"*-rehearse-pre-game-reset.dump | head -1)
echo "    Dump: $dump_file"

echo "[5/7] Mutating DB (delete half the vectors, all the games)..."
PGPASSWORD="$DB_PASS" psql "$DB_URL" -c "DELETE FROM pgvector_embeddings WHERE ctid IN (SELECT ctid FROM pgvector_embeddings LIMIT 3);" > /dev/null
PGPASSWORD="$DB_PASS" psql "$DB_URL" -c "DELETE FROM games;" > /dev/null
mutated_count=$(PGPASSWORD="$DB_PASS" psql "$DB_URL" -t -A -c "SELECT COUNT(*) FROM pgvector_embeddings;")
mutated_games=$(PGPASSWORD="$DB_PASS" psql "$DB_URL" -t -A -c "SELECT COUNT(*) FROM games;")
echo "    Mutated state: $mutated_games games, $mutated_count vectors"

if [[ "$mutated_count" == "$original_count" ]] || [[ "$mutated_games" == "$original_games" ]]; then
  echo "[FAIL] Mutation didn't take effect — rehearsal is invalid."
  exit 1
fi

echo "[6/7] Running 99-rollback.sh..."
PGPASSWORD="$DB_PASS" "$REPO_ROOT/infra/scripts/game-reset/99-rollback.sh" "$TMP_ENV" "$dump_file" --i-mean-it

echo "[7/7] Verifying restored state matches original..."
restored_count=$(PGPASSWORD="$DB_PASS" psql "$DB_URL" -t -A -c "SELECT COUNT(*) FROM pgvector_embeddings;")
restored_games=$(PGPASSWORD="$DB_PASS" psql "$DB_URL" -t -A -c "SELECT COUNT(*) FROM games;")
echo "    Restored state: $restored_games games, $restored_count vectors"

rm -f "$TMP_ENV"

if [[ "$restored_count" != "$original_count" ]]; then
  echo "[FAIL] Vector count mismatch: expected $original_count, got $restored_count"
  exit 1
fi
if [[ "$restored_games" != "$original_games" ]]; then
  echo "[FAIL] Game count mismatch: expected $original_games, got $restored_games"
  exit 1
fi

echo ""
echo "[PASS] Rollback rehearsal succeeded:"
echo "  - Backup created from clean state"
echo "  - DB mutated then rolled back"
echo "  - Final state matches original ($restored_games games, $restored_count vectors)"
```

- [ ] **Step 4: Make executable + syntax check**

```bash
chmod +x tests/scripts/game-reset/rehearse-rollback.sh
bash -n tests/scripts/game-reset/rehearse-rollback.sh
```

Expected: exit 0.

- [ ] **Step 5: Run the rehearsal (requires Docker)**

```bash
./tests/scripts/game-reset/rehearse-rollback.sh
```

Expected output ends with:

```
[PASS] Rollback rehearsal succeeded:
  - Backup created from clean state
  - DB mutated then rolled back
  - Final state matches original (3 games, 5 vectors)
```

Exit code: 0.

- [ ] **Step 6: Verify cleanup happened**

```bash
docker ps -a --filter "name=meepleai-rollback-rehearse" --format '{{.Names}}'
ls infra/backups/rehearse-* 2>/dev/null || echo "no leftover artefacts"
```

Expected: container removed, no leftover dump files.

- [ ] **Step 7: Commit**

```bash
git add tests/scripts/game-reset/rehearse-rollback.sh tests/scripts/game-reset/fixtures/synthetic-dump.sql
git commit -m "test(infra): rollback rehearsal harness for game-reset (refs #1320)"
```

---

## Task 8: Makefile targets

**Files:**
- Modify: `infra/Makefile`

- [ ] **Step 1: Read current Makefile to find right insertion point**

Run:

```bash
grep -n "^[a-z].*:" infra/Makefile | head -20
```

Note the section structure (likely has section headers like `## Backup commands` or similar).

- [ ] **Step 2: Append the new targets**

Append to `infra/Makefile`:

```makefile

# === Game entity reset (issue #1320) ===
# Scripts live in infra/scripts/game-reset/

.PHONY: game-reset-backup game-reset-mapping game-reset-rollback-rehearse game-reset-help

game-reset-help:  ## Show game-reset workflow help
	@echo "Game entity reset (#1320) — usage:"
	@echo "  make game-reset-backup ENV=dev              # pg_dump + vector count baseline"
	@echo "  make game-reset-mapping ENV=dev             # export bridge CSV (requires backup first)"
	@echo "  make game-reset-rollback-rehearse           # disposable docker test of rollback"
	@echo ""
	@echo "Per env, create infra/scripts/game-reset/.env.<ENV> from .env.example."

game-reset-backup:  ## Pre-flight DB backup (ENV=dev|staging|prod)
	@test -n "$(ENV)" || (echo "ENV required: make game-reset-backup ENV=dev" && exit 64)
	@test -f scripts/game-reset/.env.$(ENV) || (echo "Missing scripts/game-reset/.env.$(ENV) — copy from .env.example" && exit 66)
	./scripts/game-reset/01-backup.sh scripts/game-reset/.env.$(ENV) $(IMEANIT)

game-reset-mapping:  ## Export mapping CSV (ENV=dev|staging|prod, requires backup first)
	@test -n "$(ENV)" || (echo "ENV required: make game-reset-mapping ENV=dev" && exit 64)
	@test -f scripts/game-reset/.env.$(ENV) || (echo "Missing scripts/game-reset/.env.$(ENV) — copy from .env.example" && exit 66)
	./scripts/game-reset/02-export-mapping.sh scripts/game-reset/.env.$(ENV) $(IMEANIT)

game-reset-rollback-rehearse:  ## Run disposable docker rollback rehearsal
	../tests/scripts/game-reset/rehearse-rollback.sh
```

(Note: `IMEANIT` is empty by default. For prod, invoke as `make game-reset-backup ENV=prod IMEANIT=--i-mean-it`.)

- [ ] **Step 3: Verify Makefile parses**

```bash
cd infra && make game-reset-help
```

Expected:

```
Game entity reset (#1320) — usage:
  make game-reset-backup ENV=dev              # pg_dump + vector count baseline
  ...
```

- [ ] **Step 4: Verify ENV guard works**

```bash
cd infra && make game-reset-backup
```

Expected: `ENV required: make game-reset-backup ENV=dev`, exit 64.

- [ ] **Step 5: Verify env-file guard works**

```bash
cd infra && make game-reset-backup ENV=nonexistent
```

Expected: `Missing scripts/game-reset/.env.nonexistent — copy from .env.example`, exit 66.

- [ ] **Step 6: Commit**

```bash
cd .. && git add infra/Makefile
git commit -m "feat(infra): Makefile targets for game-reset workflow (refs #1320)"
```

---

## Task 9: CLAUDE.md quick reference

**Files:**
- Modify: `CLAUDE.md` (Quick Reference table)

- [ ] **Step 1: Locate Quick Reference table**

Run:

```bash
grep -n "Quick Reference" CLAUDE.md | head -3
```

- [ ] **Step 2: Add new row after "Deploy Staging" row**

Add this row to the table:

```markdown
| Game Reset (#1320) | `make game-reset-help` | `infra/` — workflow help, see [spec](./docs/for-developers/specs/2026-05-19-game-entity-reset.md) |
```

Use Edit tool to insert immediately after the `| Deploy Staging | ... |` row.

- [ ] **Step 3: Verify**

```bash
grep "Game Reset" CLAUDE.md
```

Expected: one match showing the new row.

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md
git commit -m "docs(claude): add game-reset to Quick Reference (refs #1320)"
```

---

## Task 10: PR and close-out

- [ ] **Step 1: Push the branch**

```bash
git push -u origin feature/issue-1320-game-entity-reset-phase1
```

(Branch should have been created at start per `superpowers:using-git-worktrees` or per CLAUDE.md branch hygiene.)

- [ ] **Step 2: Open PR**

```bash
gh pr create --title "feat(infra): game-reset Phase 1 — backup & rollback infrastructure (#1320)" \
  --base main-dev \
  --body "$(cat <<'EOF'
## Summary

Phase 1 of issue #1320 — pure infrastructure for the upcoming game entity reset. **No schema change, no code refactor.** This PR ships:

- `infra/backups/` directory + gitignore policy
- `infra/scripts/game-reset/` with three scripts (`01-backup.sh`, `02-export-mapping.sh`, `99-rollback.sh`) + shared `lib/common.sh`
- Per-environment config via `.env.<env>` (template + gitignored actuals)
- Disposable docker rollback rehearsal in `tests/scripts/game-reset/`
- Makefile targets and CLAUDE.md quick reference entry

Reference: spec sections §4 Step 0, Step 1, Step 6.

## Test plan

- [ ] `make game-reset-help` shows usage
- [ ] `bash -n` passes on all `.sh` files
- [ ] `./tests/scripts/game-reset/rehearse-rollback.sh` exits 0 with PASS message
- [ ] Production safety guard: `ENV_NAME=prod` without `--i-mean-it` exits 77
- [ ] No `infra/backups/*.dump` files committed (gitignored)

## What this does NOT do

- Does not drop any tables
- Does not touch the `Game` entity or any consumer code
- Does not run on staging or prod (only local dev verified by rehearsal)

Phase 2 (code refactor) and Phase 3 (execute reset) are tracked separately under #1320.

🤖 Implemented per `docs/superpowers/plans/2026-05-19-game-entity-reset-phase1.md`
EOF
)"
```

- [ ] **Step 3: Update issue #1320 with Phase 1 PR link**

```bash
gh issue comment 1320 --body "Phase 1 PR opened: ${pr_url:-<link>} — backup infrastructure, no schema change."
```

(Replace `${pr_url}` with the actual URL printed by `gh pr create`.)

- [ ] **Step 4: Wait for CI green + merge approval**

After CI is green and review approved, merge via GitHub UI (squash). Branch auto-deletes per repo settings.

---

## Out of scope for Phase 1

- Phase 2 (code refactor — delete `Game`, add `GameCoreData`/`GameRef`, update 40+ consumer files)
- Phase 3 (execute reset on dev → staging → prod, re-link vectors, verification gates)
- Re-link SQL (`Step 4` of spec — lands in Phase 3 plan)
- DROP TABLE SQL (`Step 2` of spec — lands in Phase 3 plan)
- Any frontend changes (separate follow-up issue)
- Modifying `pgvector_embeddings` schema (would force re-embedding)

---

## Self-review checklist (post-write)

✅ **Spec coverage**: Phase 1 covers spec sections §4 Step 0 (backup), Step 1 (mapping CSV), Step 6 (rollback). Sections §4 Step 2, 3, 4, 5 are deferred to Phase 3 plan as intended.

✅ **Placeholder scan**: All steps contain concrete commands/code. No "TBD" or "implement later" left. The two `${pr_url}` and `IMEANIT` substitutions are user-supplied at runtime, not placeholders in the plan content.

✅ **Type/name consistency**: Script names (`01-backup.sh`, `02-export-mapping.sh`, `99-rollback.sh`) used consistently across Tasks 3-8 and README. Exit codes (64/66/70/72/73/77) consistent across `common.sh`, scripts, and README troubleshooting table.

✅ **Frequent commits**: One commit per task (10 commits total).

✅ **TDD where applicable**: Tasks 3, 4, 7 include a test/verification step before commit. Task 7 (rehearsal) is itself the test harness for the whole script set.
