# Issue #2565 - Secret Management Consolidation Analysis

**Date**: 2026-01-17
**Analyst**: Claude Code
**Context**: Verification task revealed duplicate secret management systems

---

## Problem Statement

The codebase has **TWO parallel secret management systems** with significant duplication:

1. **Multi-variable `.secret` files** (backend C# validation system)
2. **Single-value `.txt` files** (Docker secrets system)

This creates:
- ❌ Maintenance burden (update same value in 2 files)
- ❌ Consistency risks (files can drift out of sync)
- ❌ Confusion for developers

---

## Current State Analysis

### File Duplications Identified

| Purpose | `.secret` Multi-var | `.txt` Single-var | Status |
|---------|-------------------|------------------|--------|
| **PostgreSQL** | `database.secret` (USER, PASSWORD, DB) | `postgres-password.txt` | ✅ **FIXED** (using env_file) |
| **Redis** | `redis.secret` (PASSWORD) | `redis-password.txt` | ❌ DUPLICATE |
| **JWT** | `jwt.secret` (KEY, ISSUER, AUDIENCE) | `jwt-secret.txt` | ❌ DUPLICATE |
| **OpenRouter** | `openrouter.secret` (KEY, DEFAULT_MODEL) | `openrouter-api-key.txt` | ❌ DUPLICATE |
| **Admin** | `admin.secret` (EMAIL, PASSWORD, DISPLAY_NAME) | `initial-admin-password.txt` | ❌ DUPLICATE |
| **OAuth Google** | `oauth.secret` (GOOGLE_CLIENT_ID, SECRET) | `google-oauth-client-id.txt`, `google-oauth-client-secret.txt` | ❌ DUPLICATE |
| **OAuth Discord** | `oauth.secret` (DISCORD_CLIENT_ID, SECRET) | `discord-oauth-client-id.txt`, `discord-oauth-client-secret.txt` | ❌ DUPLICATE |
| **OAuth GitHub** | `oauth.secret` (GITHUB_CLIENT_ID, SECRET) | `github-oauth-client-id.txt`, `github-oauth-client-secret.txt` | ❌ DUPLICATE |
| **Grafana** | `monitoring.secret` (GRAFANA_PASSWORD, PROMETHEUS_PASSWORD) | `grafana-admin-password.txt` | ❌ DUPLICATE |

### Architecture Overview

```
┌─────────────────────────────────────────────────┐
│ Backend C# (SecretLoader.cs)                    │
│ ✅ Reads: *.secret multi-var files              │
│ ✅ Location: /app/infra/secrets/*.secret        │
│ ✅ Format: KEY=VALUE                            │
│ ✅ Validation: 3-level (Critical/Important/Opt) │
└─────────────────────────────────────────────────┘
                    ↓
         ┌──────────────────────┐
         │ docker-compose.yml   │
         │ Volume mount:        │
         │ ./secrets → /app/... │
         └──────────────────────┘

┌─────────────────────────────────────────────────┐
│ Docker Secrets (load-secrets-env.sh)            │
│ ⚠️ Reads: *.txt single-value files              │
│ ⚠️ Location: /run/secrets/*.txt                 │
│ ⚠️ Format: raw value only                       │
│ ⚠️ Mapped to env vars for compatibility         │
└─────────────────────────────────────────────────┘
```

---

## Recommended Solution

### Strategy: Consolidate on `.secret` files

**Rationale**:
1. ✅ Backend C# already has robust validation system (`SecretDefinitions.cs`)
2. ✅ Multi-variable files reduce file count (9 vs 20+ files)
3. ✅ Better documentation (comments supported in `.secret`)
4. ✅ Single source of truth

### Implementation Plan

#### Phase 1: Core Services (CRITICAL - Required for Issue #2565)
- [x] `database.secret` → Remove `postgres-password.txt` reference
- [ ] `redis.secret` → Remove `redis-password.txt` reference
- [ ] `jwt.secret` → Remove `jwt-secret.txt` reference

**Impact**: Unblocks database migration and API startup

#### Phase 2: External Services (IMPORTANT)
- [ ] `openrouter.secret` → Remove `openrouter-api-key.txt` reference
- [ ] `admin.secret` → Remove `initial-admin-password.txt` reference

**Impact**: Unblocks AI features and admin bootstrap

#### Phase 3: Optional Services (OPTIONAL)
- [ ] `oauth.secret` → Remove 6x `*-oauth-*.txt` files
- [ ] `monitoring.secret` → Remove `grafana-admin-password.txt`

**Impact**: Cleaner secret management, reduced file count

---

## Migration Steps (Per Secret)

### Example: Redis Consolidation

**Before**:
```yaml
# docker-compose.yml
redis:
  secrets:
    - redis-password  # → ./secrets/redis-password.txt

api:
  secrets:
    - redis-password  # → ./secrets/redis-password.txt
```

**After**:
```yaml
# docker-compose.yml
redis:
  env_file:
    - ./secrets/redis.secret  # REDIS_PASSWORD=value

api:
  env_file:
    - ./secrets/redis.secret  # Backend C# validates this
```

**load-secrets-env.sh** (update):
```bash
# Before
load_secret "redis-password" "REDIS_PASSWORD"

# After (REDIS_PASSWORD already loaded from redis.secret via env_file)
# No change needed - env var already set
```

---

## Risk Assessment

### Low Risk
- ✅ Backend C# already validates `.secret` files
- ✅ `env_file` natively supported by Docker Compose
- ✅ Gradual migration possible (service-by-service)

### Medium Risk
- ⚠️ Need to update `load-secrets-env.sh` to avoid overwriting env vars
- ⚠️ Service restarts required for each migration
- ⚠️ N8n uses custom env var names (DB_POSTGRESDB_*) - needs mapping

### Mitigation
1. Test each service after migration
2. Keep `.txt` files temporarily as backup
3. Document migration in CHANGELOG
4. Add validation to CI/CD

---

## Files to Deprecate (After Full Migration)

```bash
infra/secrets/postgres-password.txt          # → database.secret
infra/secrets/redis-password.txt             # → redis.secret
infra/secrets/jwt-secret.txt                 # → jwt.secret
infra/secrets/openrouter-api-key.txt         # → openrouter.secret
infra/secrets/initial-admin-password.txt     # → admin.secret
infra/secrets/google-oauth-client-id.txt     # → oauth.secret
infra/secrets/google-oauth-client-secret.txt # → oauth.secret
infra/secrets/discord-oauth-client-id.txt    # → oauth.secret
infra/secrets/discord-oauth-client-secret.txt # → oauth.secret
infra/secrets/github-oauth-client-id.txt     # → oauth.secret
infra/secrets/github-oauth-client-secret.txt # → oauth.secret
infra/secrets/grafana-admin-password.txt     # → monitoring.secret
infra/secrets/gmail-app-password.txt         # → email.secret (if created)
infra/secrets/bgg-api-token.txt              # → bgg.secret
```

**Result**: 14 files → 9 files (36% reduction)

---

## Current Issue #2565 Blocker

**Problem**: PostgreSQL uses `database.secret` (meepleai_db) but Docker created database named `meepleai`

**Root Cause**: Previously used hardcoded defaults in docker-compose.yml instead of `database.secret`

**Fix Applied**:
```yaml
# OLD (WRONG)
postgres:
  environment:
    POSTGRES_USER: ${POSTGRES_USER:-postgres}  # Hardcoded default
    POSTGRES_PASSWORD_FILE: /run/secrets/postgres-password
    POSTGRES_DB: ${POSTGRES_DB:-meepleai}  # Hardcoded default

# NEW (CORRECT)
postgres:
  env_file:
    - ./secrets/database.secret  # Single source of truth
```

**Next Step**: Recreate PostgreSQL container with correct database name from `database.secret`

---

## Recommendations for Issue #2565

### Immediate Actions (Unblock Testing)
1. ✅ Use `database.secret` for PostgreSQL (done)
2. ⏳ Recreate PostgreSQL volume with correct database name
3. ⏳ Apply EF Core migrations to `meepleai_db`
4. ⏳ Continue with endpoint testing

### Follow-up Issue (Post-#2565)
Create **Issue #2566**: "Consolidate all secret management on .secret files"
- Migrate redis, jwt, openrouter, admin, oauth, monitoring
- Remove deprecated `.txt` files
- Update documentation
- Add CI validation for secret file format

---

## Files Modified (Issue #2565 Scope)

- [x] `infra/docker-compose.yml` - postgres service uses database.secret
- [x] `infra/docker-compose.yml` - api service uses database.secret
- [x] `infra/docker-compose.yml` - n8n service uses database.secret
- [x] `infra/docker-compose.yml` - removed postgres-password from secrets list
- [x] `infra/scripts/load-secrets-env.sh` - map POSTGRES_* to DB_POSTGRESDB_*
- [x] `infra/env/n8n.env.dev` - removed hardcoded DB values
- [x] `apps/api/src/Api/appsettings.Development.json` - updated connection string

---

**Conclusion**: Secret management consolidation is feasible and recommended. Phase 1 (database) is complete for Issue #2565. Full migration should be tracked in a separate issue.
