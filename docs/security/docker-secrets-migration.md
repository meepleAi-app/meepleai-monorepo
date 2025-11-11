# Docker Secrets Migration Guide

Migration guide for converting environment variable secrets to Docker Secrets (Issue #708 Phase 1).

## Overview

**Before**: Secrets stored as plain text in `.env` files
**After**: Secrets stored in secure files, mounted via Docker Secrets

**Benefits**:
- ✅ Encrypted in transit and at rest
- ✅ Git-ignored by default (infra/secrets/*.txt)
- ✅ Centralized secret management
- ✅ Easier rotation workflows
- ✅ Audit trail via git for secret file structure changes

## Prerequisites

- Docker Compose v2.17+ (supports `secrets:` without Swarm mode)
- `openssl` for key generation
- Bash shell for automation scripts

## Migration Steps

### Step 1: Backup Current Configuration (5 min)

```bash
# Backup existing environment files
cd infra/env
cp api.env.dev api.env.dev.backup
cp n8n.env.dev n8n.env.dev.backup
cp alertmanager.env alertmanager.env.backup

# Record current secret values (SECURE LOCATION ONLY!)
# DO NOT save to git or plaintext files
```

### Step 2: Initialize Docker Secrets (10 min)

```bash
# Run initialization script
cd tools/secrets
./init-secrets.sh

# Script will prompt for each secret:
# 1. postgres-password (PostgreSQL database)
# 2. openrouter-api-key (LLM API)
# 3. n8n-encryption-key (n8n data encryption)
# 4. n8n-basic-auth-password (n8n UI)
# 5. gmail-app-password (Alertmanager email)
# 6. grafana-admin-password (Grafana UI)
# 7. initial-admin-password (API bootstrap)
```

**Example initialization**:
```
📝 Setting up: postgres-password
PostgreSQL database password (used by postgres, api, n8n)
Default: meeplepass
Enter value (or press Enter for default): my-secure-db-password

✅ Created postgres-password.txt
```

### Step 3: Verify Secret Files (2 min)

```bash
# List created secrets
cd tools/secrets
./list-secrets.sh

# Expected output:
# Secret Name                    Status      Size
# ----------------------------------------------------------------
# postgres-password              ✅ Present  24 bytes
# openrouter-api-key             ✅ Present  51 bytes
# n8n-encryption-key             ✅ Present  24 bytes
# n8n-basic-auth-password        ✅ Present  12 bytes
# gmail-app-password             ✅ Present  16 bytes
# grafana-admin-password         ✅ Present  8 bytes
# initial-admin-password         ✅ Present  15 bytes
#
# Summary: 7/7 secrets present
# ✅ All secrets configured!
```

### Step 4: Update Environment Files (10 min)

Remove secret values from environment files (they're now read from Docker Secrets):

#### `infra/env/api.env.dev`

**REMOVE these lines** (replaced by Docker Secrets):
```bash
# ❌ DELETE - now in /run/secrets/postgres-password
POSTGRES_PASSWORD=meeplepass
CONNECTIONSTRINGS__POSTGRES=Host=postgres;...;Password=meeplepass

# ❌ DELETE - now in /run/secrets/openrouter-api-key
OPENROUTER_API_KEY=sk-or-...

# ❌ DELETE - now in /run/secrets/initial-admin-password
INITIAL_ADMIN_PASSWORD=Admin123!ChangeMe
```

**KEEP these lines** (non-sensitive config):
```bash
POSTGRES_USER=meeple
POSTGRES_DB=meepleai
EMBEDDING_PROVIDER=ollama
JWT_ISSUER=http://localhost:8080
ALLOW_ORIGIN=http://localhost:3000
# ... etc
```

#### `infra/env/n8n.env.dev`

**REMOVE**:
```bash
# ❌ DELETE - now in /run/secrets/postgres-password
DB_POSTGRESDB_PASSWORD=meeplepass

# ❌ DELETE - now in /run/secrets/n8n-encryption-key
N8N_ENCRYPTION_KEY=changeme-replace-with-32-byte-key

# ❌ DELETE - now in /run/secrets/n8n-basic-auth-password
N8N_BASIC_AUTH_PASSWORD=changeme-in-production
```

#### `infra/env/alertmanager.env`

**REMOVE**:
```bash
# ❌ DELETE - now in /run/secrets/gmail-app-password
GMAIL_APP_PASSWORD=abcdabcdabcdabcd
```

### Step 5: Test Docker Compose Configuration (5 min)

```bash
# Validate docker-compose.yml syntax
cd infra
docker compose config

# Should show secrets mounted at /run/secrets/*
# Check for errors in output
```

### Step 6: Start Services (10 min)

```bash
# Stop all running services
docker compose down

# Start with Docker Secrets
docker compose up -d

# Monitor logs for successful startup
docker compose logs -f

# Expected log entries:
# api: "✅ Loaded secret 'OPENROUTER_API_KEY' from file: /run/secrets/openrouter-api-key"
# api: "✅ Built PostgreSQL connection string: Host=postgres, Database=meepleai"
# postgres: Successfully using password from /run/secrets/postgres-password
```

### Step 7: Verify Services (10 min)

```bash
# Check all services are healthy
docker compose ps

# Should show all services as "healthy" or "running"

# Test database connection
docker compose exec api curl -f http://localhost:8080/health

# Test API authentication
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@meepleai.dev","password":"Admin123!ChangeMe"}'

# Test n8n UI
curl -u admin:admin http://localhost:5678/healthz

# Test Grafana
curl http://admin:admin@localhost:3001/api/health
```

### Step 8: Cleanup Backups (5 min)

After confirming everything works:

```bash
# Delete backup files containing old secrets
cd infra/env
rm -f *.backup

# Delete plaintext secret values from secure notes/password manager
```

## Troubleshooting

### "Secret file not found" Error

**Symptom**:
```
Error: Secret file not found: /run/secrets/postgres-password
```

**Solution**:
```bash
# Verify secret file exists
ls -la infra/secrets/postgres-password.txt

# If missing, run initialization
cd tools/secrets
./init-secrets.sh

# Restart services
cd infra
docker compose restart
```

### Service Won't Start After Migration

**Symptom**:
```
api exited with code 1
```

**Debug steps**:
```bash
# Check service logs
docker compose logs api

# Look for secret-related errors
docker compose logs api | grep -i "secret\|password\|file not found"

# Verify secret is mounted
docker compose exec api ls -la /run/secrets/

# Should show:
# -r--r--r-- 1 root root 24 Nov 11 14:00 openrouter-api-key
# -r--r--r-- 1 root root 24 Nov 11 14:00 postgres-password
# -r--r--r-- 1 root root 15 Nov 11 14:00 initial-admin-password
```

### Wrong Password in Secret File

**Symptom**:
```
PostgreSQL authentication failed
```

**Solution**:
```bash
# Rotate the secret with correct value
cd tools/secrets
./rotate-secret.sh postgres-password

# Enter correct password when prompted
# Restart affected services
docker compose restart postgres api n8n
```

### n8n/Alertmanager Not Reading Secrets

**Issue**: Some services don't support `_FILE` pattern natively.

**Workaround** (already implemented in docker-compose.yml):
- Services expose `_FILE` environment variables
- Application code reads files manually if service doesn't support it
- Fallback to direct env vars for backward compatibility

## Secret Rotation Workflow

### Rotate a Single Secret

```bash
cd tools/secrets
./rotate-secret.sh <secret-name>

# Examples:
./rotate-secret.sh postgres-password
./rotate-secret.sh openrouter-api-key
./rotate-secret.sh gmail-app-password
```

### Rotate All Secrets (Quarterly)

```bash
cd tools/secrets

# Rotate each secret one by one
for secret in postgres-password openrouter-api-key n8n-encryption-key \
              n8n-basic-auth-password gmail-app-password \
              grafana-admin-password initial-admin-password; do
    ./rotate-secret.sh "$secret"
    docker compose restart  # Restart all services after each rotation
    sleep 10  # Wait for services to stabilize
done
```

## Validation Checklist

After migration, verify:

- [ ] All secret files created in `infra/secrets/`
- [ ] Secret files are gitignored (check `git status`)
- [ ] All services start successfully (`docker compose ps`)
- [ ] Database connections work (`/health` endpoint returns healthy)
- [ ] API authentication works (login test)
- [ ] n8n UI accessible with basic auth
- [ ] Grafana UI accessible with admin password
- [ ] Alertmanager can send email (test alert)
- [ ] No plaintext passwords in env files
- [ ] Backup files deleted
- [ ] Documentation updated

## Rollback Procedure

If migration fails and you need to rollback:

```bash
# Stop services
docker compose down

# Restore backup env files
cd infra/env
cp api.env.dev.backup api.env.dev
cp n8n.env.dev.backup n8n.env.dev
cp alertmanager.env.backup alertmanager.env

# Revert docker-compose.yml changes
git checkout HEAD -- infra/docker-compose.yml

# Restart with old configuration
docker compose up -d
```

## Next Steps (Phase 2)

After Phase 1 stabilizes, migrate to Infisical for:
- ✅ Automatic secret rotation (30-day intervals)
- ✅ Unlimited secret versioning
- ✅ Web UI for secret management
- ✅ RBAC and audit logs
- ✅ Integration with CI/CD pipelines

See:
- Issue #936: POC Infisical rotation
- `infra/docker-compose.infisical.yml`
- `docs/guide/secrets-management.md` (Phase 2 section)

## References

- [Docker Secrets Documentation](https://docs.docker.com/engine/swarm/secrets/)
- [Docker Compose Secrets](https://docs.docker.com/compose/use-secrets/)
- [MeepleAI Secrets Management Guide](../guide/secrets-management.md)
- [OWASP Secrets Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html)

## Related Issues

- Issue #700: Remove hardcoded Gmail password (resolved by this migration)
- Issue #708: Secrets management strategy (parent issue)
- Issue #936: POC Infisical rotation (Phase 2)

---

**Last Updated**: 2025-11-11
**Issue**: #708 (Phase 1)
**Estimated Migration Time**: 60 minutes
