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
