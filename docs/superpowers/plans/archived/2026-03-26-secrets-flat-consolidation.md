# Secrets Flat Consolidation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace per-environment secret subdirectories with a single flat `secrets/` directory, using staging values as the source of truth.

**Architecture:** All compose files point to `./secrets/X.secret` (what staging already does). Scripts simplified to remove environment switching. Shell scripts updated to remove `secrets/dev/` paths. Subdirectories deleted last.

**Tech Stack:** Docker Compose, PowerShell, Bash, Make

**Spec:** `docs/superpowers/specs/2026-03-26-secrets-flat-consolidation-design.md`

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `infra/docker-compose.yml` | MODIFY | Update 2 fallback env_file paths |
| `infra/compose.dev.yml` | MODIFY | Update all `secrets/dev/` → `secrets/` (~17 refs + volume mount + comment) |
| `infra/compose.integration.yml` | MODIFY | Update 6 `secrets/integration/` → `secrets/` refs + volume mount + comment |
| `infra/Makefile` | MODIFY | Replace 4 secrets targets with 2 |
| `infra/scripts/restore-local.sh` | MODIFY | Update secret path (line 25) |
| `infra/scripts/seed-test-game.sh` | MODIFY | Update secret path (line 22) |
| `infra/scripts/sync-secrets.sh` | **NEW** | Simple scp pull from staging |
| `.gitignore` | MODIFY | Simplify secret patterns |
| `CLAUDE.md` | MODIFY | Update Secret Management section |
| `infra/secrets/setup-secrets.ps1` | MODIFY | Remove -Environment, generate in flat dir |
| `infra/scripts/sync-secrets.ps1` | DELETE | Replaced by sync-secrets.sh |
| `infra/secrets/secrets-sync.yml` | DELETE | No longer needed |
| `infra/secrets/dev/` | DELETE | After all updates |
| `infra/secrets/integration/` | DELETE | After all updates |
| `infra/secrets/staging/` | DELETE | After all updates |

---

### Task 1: Update compose.dev.yml

**Files:**
- Modify: `infra/compose.dev.yml`

- [ ] **Step 1: Replace all `secrets/dev/` with `secrets/`**

Use find-and-replace: `secrets/dev/` → `secrets/` for all occurrences in the file. This covers:
- Line 2: header comment
- Line 15: postgres env_file
- Line 25: redis env_file
- Lines 35-44: api env_file (10 entries)
- Line 66: api volume mount `./secrets/dev:/app/infra/secrets:ro` → `./secrets:/app/infra/secrets:ro`
- Line 94: orchestration-service env_file (2 entries)
- Line 114: grafana env_file
- Line 126: alertmanager env_file
- Line 140: n8n env_file (2 entries)
- Line 151: minio env_file
- Line 170: ssh-tunnel-sidecar env_file

- [ ] **Step 2: Verify no `secrets/dev` references remain**

Run: `cd D:/Repositories/meepleai-monorepo-backend && grep -n "secrets/dev" infra/compose.dev.yml`
Expected: No matches.

- [ ] **Step 3: Commit**

```bash
git add infra/compose.dev.yml
git commit -m "refactor(infra): compose.dev.yml use flat secrets/ directory"
```

---

### Task 2: Update docker-compose.yml and compose.integration.yml

**Files:**
- Modify: `infra/docker-compose.yml`
- Modify: `infra/compose.integration.yml`

- [ ] **Step 1: Update docker-compose.yml**

Replace `secrets/dev/` → `secrets/` on lines 15 and 40:
- Line 15: `env_file: [./secrets/dev/database.secret]` → `env_file: [./secrets/database.secret]`
- Line 40: `env_file: [./secrets/dev/redis.secret]` → `env_file: [./secrets/redis.secret]`

- [ ] **Step 2: Update compose.integration.yml**

Replace all `secrets/integration/` → `secrets/`:
- Line 10: comment update
- Lines 38-43: api env_file (6 entries)
- Line 65: volume mount `./secrets/integration:/app/infra/secrets:ro` → `./secrets:/app/infra/secrets:ro`

- [ ] **Step 3: Verify no subdirectory references remain**

Run: `cd D:/Repositories/meepleai-monorepo-backend && grep -n "secrets/dev\|secrets/integration\|secrets/staging" infra/docker-compose.yml infra/compose.integration.yml`
Expected: No matches.

- [ ] **Step 4: Commit**

```bash
git add infra/docker-compose.yml infra/compose.integration.yml
git commit -m "refactor(infra): docker-compose.yml and compose.integration.yml use flat secrets/"
```

---

### Task 3: Update shell scripts

**Files:**
- Modify: `infra/scripts/restore-local.sh` (line 25)
- Modify: `infra/scripts/seed-test-game.sh` (line 22)

- [ ] **Step 1: Update restore-local.sh**

Line 25: change `secrets/dev/database.secret` → `secrets/database.secret`

- [ ] **Step 2: Update seed-test-game.sh**

Line 22: change `secrets/dev/admin.secret` → `secrets/admin.secret`
Also update the error message on line 24-25 to reference `make secrets-setup` instead of `make secrets-dev`.

- [ ] **Step 3: Commit**

```bash
git add infra/scripts/restore-local.sh infra/scripts/seed-test-game.sh
git commit -m "refactor(infra): update shell scripts to use flat secrets/ path"
```

---

### Task 4: Simplify Makefile

**Files:**
- Modify: `infra/Makefile`

- [ ] **Step 1: Replace secrets targets**

Replace lines 52-64:
```makefile
# === Secrets ===

secrets-dev: ## Generate dev secrets (auto-generated)
	pwsh -File secrets/setup-secrets.ps1 -Environment dev -SaveGenerated

secrets-integration: ## Setup integration secrets
	pwsh -File secrets/setup-secrets.ps1 -Environment integration

secrets-staging: ## Setup staging secrets
	pwsh -File secrets/setup-secrets.ps1 -Environment staging

secrets-prod: ## Setup production secrets
	pwsh -File secrets/setup-secrets.ps1 -Environment prod
```

With:
```makefile
# === Secrets ===

secrets-setup: ## Generate placeholder secrets from .example files
	pwsh -File secrets/setup-secrets.ps1

secrets-sync: ## Sync secrets from staging server (requires SSH access)
	bash scripts/sync-secrets.sh
```

- [ ] **Step 2: Update .PHONY line**

Replace `secrets-dev secrets-integration secrets-staging secrets-prod` with `secrets-setup secrets-sync` in the .PHONY declaration (line 185).

- [ ] **Step 3: Commit**

```bash
git add infra/Makefile
git commit -m "refactor(infra): simplify Makefile secrets targets to setup + sync"
```

---

### Task 5: Create sync-secrets.sh and delete old sync script

**Files:**
- Create: `infra/scripts/sync-secrets.sh`
- Delete: `infra/scripts/sync-secrets.ps1`
- Delete: `infra/secrets/secrets-sync.yml`

- [ ] **Step 1: Create sync-secrets.sh**

```bash
#!/usr/bin/env bash
# sync-secrets.sh — Pull secrets from staging server to local
# Usage: ./sync-secrets.sh
# Requires: SSH access to meepleai.app

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SECRETS_DIR="$SCRIPT_DIR/../secrets"
REMOTE="meepleai.app"
REMOTE_PATH="/opt/meepleai/repo/infra/secrets"

echo "Syncing secrets from $REMOTE..."
scp "$REMOTE:$REMOTE_PATH/*.secret" "$SECRETS_DIR/"
echo "Done. Secrets synced to $SECRETS_DIR/"
```

- [ ] **Step 2: Make it executable**

Run: `chmod +x D:/Repositories/meepleai-monorepo-backend/infra/scripts/sync-secrets.sh`

- [ ] **Step 3: Delete old files**

Run:
```bash
cd D:/Repositories/meepleai-monorepo-backend
git rm infra/scripts/sync-secrets.ps1
git rm infra/secrets/secrets-sync.yml
```

- [ ] **Step 4: Commit**

```bash
git add infra/scripts/sync-secrets.sh
git commit -m "refactor(infra): replace sync-secrets.ps1 with simple sync-secrets.sh"
```

---

### Task 6: Simplify setup-secrets.ps1

**Files:**
- Modify: `infra/secrets/setup-secrets.ps1`

- [ ] **Step 1: Rewrite setup-secrets.ps1**

Read the current file first, then rewrite it to:
- Remove the `-Environment` parameter
- Remove all subdirectory creation logic
- Generate `.secret` files directly in the script's own directory (`infra/secrets/`)
- For each `.secret.example` file found: if corresponding `.secret` does not exist, copy it as placeholder
- If `.secret` already exists: skip (never overwrite)
- Retain the `services-auth.secret` generation logic (dynamic content)

The script should be approximately 30-50 lines, not 400+.

- [ ] **Step 2: Commit**

```bash
git add infra/secrets/setup-secrets.ps1
git commit -m "refactor(infra): simplify setup-secrets.ps1 for flat secrets directory"
```

---

### Task 7: Update .gitignore

**Files:**
- Modify: `.gitignore` (root)

- [ ] **Step 1: Simplify secret patterns**

Replace lines 53-68:
```
# Exclude actual secret files but keep templates
infra/secrets/*.secret
!infra/secrets/*.secret.example

# Per-environment secrets
infra/secrets/dev/*.secret
infra/secrets/integration/*.secret
infra/secrets/staging/*.secret
infra/secrets/prod/*.secret

# Staging/Production secrets (Docker secrets format - plain text files)
infra/secrets/staging/*.txt
!infra/secrets/staging/*.txt.example
infra/secrets/prod/*.txt
infra/secrets/prod/*.pfx
!infra/secrets/prod/*.txt.example
```

With:
```
# Exclude actual secret files but keep templates
infra/secrets/*.secret
!infra/secrets/*.secret.example

# Production secrets (separate management)
infra/secrets/prod/*.secret
infra/secrets/prod/*.txt
infra/secrets/prod/*.pfx
!infra/secrets/prod/*.txt.example
```

- [ ] **Step 2: Commit**

```bash
git add .gitignore
git commit -m "refactor(infra): simplify .gitignore secret patterns for flat directory"
```

---

### Task 8: Update CLAUDE.md

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update Secret Management section**

Find the Secret Management section and replace it with:

```markdown
### Secret Management

**System**: `.secret` files in `infra/secrets/` — single flat directory for all environments.

Staging server is the source of truth. All environments use the same secret values.

| Command | Purpose |
|---------|---------|
| `make secrets-setup` | Generate placeholder `.secret` files from `.example` templates |
| `make secrets-sync` | Pull real secrets from staging server (requires SSH) |

**Workflow**:
```bash
cd infra
make secrets-setup    # First time: creates placeholder files
make secrets-sync     # Pull real values from staging (requires SSH access to meepleai.app)
```

**Rules**: Do not commit `.secret` files. Templates (`.secret.example`) are committed.
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: update CLAUDE.md secret management for flat directory"
```

---

### Task 9: Delete subdirectories

**Files:**
- Delete: `infra/secrets/dev/`
- Delete: `infra/secrets/integration/`
- Delete: `infra/secrets/staging/`

- [ ] **Step 1: Verify all secret files exist in root**

Run: `ls D:/Repositories/meepleai-monorepo-backend/infra/secrets/*.secret`
Verify `database.secret`, `redis.secret`, `jwt.secret`, `admin.secret`, `openrouter.secret` at minimum exist.

- [ ] **Step 2: Copy any missing files from subdirectories to root**

Check each subdirectory for files not already in root:
```bash
cd D:/Repositories/meepleai-monorepo-backend/infra/secrets
for f in dev/*.secret integration/*.secret staging/*.secret; do
  base=$(basename "$f")
  [ ! -f "$base" ] && echo "MISSING: $base" && cp "$f" "./$base"
done
```

- [ ] **Step 3: Delete subdirectories**

```bash
cd D:/Repositories/meepleai-monorepo-backend
rm -rf infra/secrets/dev/ infra/secrets/integration/ infra/secrets/staging/
```

Note: These directories contain only `.secret` files (gitignored), so `git rm` won't work. Use plain `rm -rf`. If there are tracked files (like `.gitkeep`), use `git rm` for those first.

- [ ] **Step 4: Commit any tracked files that were deleted**

```bash
git add -u infra/secrets/
git commit -m "refactor(infra): delete per-environment secret subdirectories"
```

---

### Task 10: Verify everything works

- [ ] **Step 1: Test dev compose parses correctly**

Run: `cd D:/Repositories/meepleai-monorepo-backend/infra && pwsh -c "docker compose -f docker-compose.yml -f compose.dev.yml config --services"`
Expected: Lists services (postgres, redis, api, web, etc.) without errors.

- [ ] **Step 2: Test integration compose parses correctly**

Run: `cd D:/Repositories/meepleai-monorepo-backend/infra && pwsh -c "docker compose -f docker-compose.yml -f compose.integration.yml config --services"`
Expected: Lists services without errors.

- [ ] **Step 3: Verify no remaining subdirectory references**

Run: `cd D:/Repositories/meepleai-monorepo-backend && grep -rn "secrets/dev/\|secrets/integration/\|secrets/staging/" infra/ --include="*.yml" --include="*.sh" --include="*.ps1" --include="Makefile" | grep -v "secrets/prod/"`
Expected: No matches (or only in compose.prod.yml which is out of scope).

- [ ] **Step 4: Final commit if any fixes needed**

```bash
git add -u
git commit -m "fix(infra): address remaining secret path references"
```
