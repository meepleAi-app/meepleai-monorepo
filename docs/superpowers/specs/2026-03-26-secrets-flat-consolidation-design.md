# Secrets Flat Consolidation

**Date**: 2026-03-26
**Status**: Approved
**Scope**: All local environments (dev, integration). Staging already flat. Prod excluded.

## Goal

Replace per-environment secret subdirectories (`secrets/dev/`, `secrets/integration/`, `secrets/staging/`) with a single flat `secrets/` directory. One set of secret files used by all environments — the compose file determines which services run (local Docker vs remote staging).

## Decisions

| Aspect | Decision |
|--------|----------|
| Structure | Flat `secrets/` — no subdirectories |
| Values | Identical across dev, integration, staging |
| Service selection | Compose file determines local vs remote |
| Credentials | Same everywhere (local postgres = staging password) |
| Optional files | Always exist with placeholders if not configured |
| Prod | Excluded from this change |
| Sync script simplification | Intentional — backup/push/diff/selective-restart capabilities of `sync-secrets.ps1` are removed. Push to staging is done by editing locally and running `scp` manually. Pull is the primary workflow. |

---

## 1. Target Directory Structure

```
infra/secrets/
├── database.secret
├── redis.secret
├── jwt.secret
├── admin.secret
├── openrouter.secret
├── oauth.secret
├── email.secret
├── embedding-service.secret
├── unstructured-service.secret
├── smoldocling-service.secret
├── reranker-service.secret
├── storage.secret
├── monitoring.secret
├── n8n.secret
├── bgg.secret
├── db-sync.secret
├── traefik.secret
├── services-auth.secret
├── qdrant.secret
├── *.secret.example       (templates, committed to git)
└── setup-secrets.ps1      (generates placeholders from .example)
```

Subdirectories `dev/`, `integration/`, `staging/` are deleted after migration.

---

## 2. Compose File Changes

All compose files point to `./secrets/X.secret` (matching what `compose.staging.yml` already does).

| File | Change |
|------|--------|
| `docker-compose.yml` (base) | `./secrets/dev/` → `./secrets/` in 2 fallback env_file refs |
| `compose.dev.yml` | `./secrets/dev/` → `./secrets/` (all occurrences — approx 17 refs including monitoring in orchestration/grafana/alertmanager) + volume mount `./secrets:/app/infra/secrets:ro` + update header comment (line 2) |
| `compose.integration.yml` | `./secrets/integration/` → `./secrets/` (6 refs) + volume mount `./secrets:/app/infra/secrets:ro` |
| `compose.staging.yml` | No change (already flat) |
| `compose.alpha.yml` | No change needed (no secret references, only sets ALPHA_MODE env vars) |
| `compose.prod.yml` | No change (out of scope) |

---

## 3. Script Changes

### setup-secrets.ps1 — Simplified

- Remove `-Environment` parameter and subdirectory creation logic.
- Generate directly in `secrets/` (same directory as the script).
- For each `.secret.example`: if corresponding `.secret` does not exist, copy as placeholder.
- If `.secret` already exists: do not overwrite (never clobber real values).
- **`services-auth.secret`**: currently generated dynamically in `setup-secrets.ps1` for the integration environment (lines 382-393). This generation logic must be preserved in the simplified script — create a `services-auth.secret.example` template AND retain the generation logic for when no `.secret` exists.

### sync-secrets.ps1 → sync-secrets.sh — Simplified

- Remove `staging-only` vs `sync` policy logic, backup, push, diff, selective restart, stateful warnings.
- Single operation: `scp staging:/opt/meepleai/repo/infra/secrets/*.secret → infra/secrets/`
- Overwrites local files with staging values (staging is the source of truth).
- **Push workflow**: To push a secret update to staging, edit the file locally and run `scp infra/secrets/<file>.secret staging:/opt/meepleai/repo/infra/secrets/` manually. This is intentionally simplified — the old script's backup/diff/selective-restart was rarely used and added complexity.

### secrets-sync.yml — Removed

No longer needed. The sync is a simple flat copy, no policy manifest required.

### Makefile — Simplified targets

Replace `secrets-dev`, `secrets-integration`, `secrets-staging`, `secrets-prod` with:

```makefile
secrets-setup: ## Generate placeholder secrets from .example files
	pwsh -File secrets/setup-secrets.ps1

secrets-sync: ## Sync secrets from staging server
	bash scripts/sync-secrets.sh
```

---

## 4. Migration Steps

1. Verify all `.secret` files referenced by compose files exist in `secrets/` root (staging files are already there).
2. For files only in subdirectories: copy to root. Specifically:
   - `services-auth.secret` from `integration/` → create `services-auth.secret.example` template + copy existing file to root
   - `qdrant.secret` — verify it exists in root (CI already writes it there)
3. Update compose files (section 2) — use find-and-replace `secrets/dev/` → `secrets/` and `secrets/integration/` → `secrets/`.
4. Update scripts (section 3).
5. Update shell scripts that hardcode subdirectory paths:
   - `infra/scripts/restore-local.sh` line 25: `secrets/dev/database.secret` → `secrets/database.secret`
   - `infra/scripts/seed-test-game.sh` line 22: `secrets/dev/admin.secret` → `secrets/admin.secret`
6. Delete subdirectories: `secrets/dev/`, `secrets/integration/`, `secrets/staging/`.
7. Do NOT touch `secrets/prod/` or `compose.prod.yml`.

---

## 5. Gitignore Update

Update `infra/.gitignore`:

```
# Secrets (real values, never committed)
secrets/*.secret
!secrets/*.secret.example
```

---

## 6. Documentation Update

Update `CLAUDE.md` Secret Management section:
- Remove references to per-environment subdirectories
- Document new workflow: `make secrets-setup` (placeholder) → `make secrets-sync` (from staging)
- Remove the priority table (Critical/Important/Optional) that references subdirectories

---

## 7. Files Changed

| File | Action |
|------|--------|
| `infra/docker-compose.yml` | MODIFY — update 2 env_file paths |
| `infra/compose.dev.yml` | MODIFY — update all `secrets/dev/` refs (~17) + volume mount + header comment |
| `infra/compose.integration.yml` | MODIFY — update 6 env_file paths + volume mount |
| `infra/secrets/setup-secrets.ps1` | REWRITE — simplified, no subdirectories, retain services-auth generation |
| `infra/scripts/sync-secrets.ps1` | DELETE — replaced by `sync-secrets.sh` |
| `infra/scripts/sync-secrets.sh` | **NEW** — simple scp pull from staging |
| `infra/secrets/secrets-sync.yml` | DELETE |
| `infra/secrets/services-auth.secret.example` | **NEW** — template for services-auth |
| `infra/Makefile` | MODIFY — simplify secrets targets |
| `infra/.gitignore` | MODIFY — update patterns |
| `infra/scripts/restore-local.sh` | MODIFY — update secret path (line 25) |
| `infra/scripts/seed-test-game.sh` | MODIFY — update secret path (line 22) |
| `CLAUDE.md` | MODIFY — update Secret Management section |
| `infra/secrets/dev/` | DELETE (after migration) |
| `infra/secrets/integration/` | DELETE (after migration) |
| `infra/secrets/staging/` | DELETE (after migration) |

## 8. Files NOT Changed

- `infra/compose.prod.yml` — prod excluded
- `infra/secrets/prod/` — prod excluded
- `infra/compose.staging.yml` — already flat
- `infra/compose.alpha.yml` — no secret references
- `infra/scripts/deploy-staging.sh` — already uses flat `secrets/*.secret`
- CI workflows (`ci.yml`, `deploy-staging.yml`, `rollback.yml`) — already write to flat `infra/secrets/`
- Any `.secret` file contents — values stay the same
