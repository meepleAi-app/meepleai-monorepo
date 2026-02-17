# Auto-Configuration System - Deployment Guide

> **Last Updated**: 2026-01-17
> **Related ADR**: [ADR-021 - Auto-Configuration System](../01-architecture/adr/adr-021-auto-configuration-system.md)
> **Related Issues**: [#2511](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2511), [#2522](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2522)

## Overview

MeepleAI's auto-configuration system provides **one-command setup** for all secret files, reducing setup time from 15-30 minutes to <1 minute for auto-generated secrets. This guide covers first-time setup, secret management, validation, and troubleshooting.

**Key Benefits**:
- ✅ **11 secrets auto-generated** with cryptographic strength (256-512 bits entropy)
- ✅ **3-level validation** (CRITICAL → IMPORTANT → OPTIONAL)
- ✅ **Health check system** for runtime monitoring
- ✅ **Zero weak passwords** (enforced complexity rules)

---

## Prerequisites

### System Requirements

**Operating Systems**:
- ✅ Windows 10/11 with PowerShell 5.1+
- ✅ Linux/macOS with PowerShell Core 7+
- ✅ Git Bash (fallback to manual setup)

**Software Dependencies**:
- Docker Desktop (for local development)
- .NET 9 SDK (for API)
- Node.js 20+ with pnpm (for frontend)
- Git

**Account Requirements** (for manual configuration):
- [BoardGameGeek](https://boardgamegeek.com) account (optional - IMPORTANT level)
- [OpenRouter](https://openrouter.ai/keys) API key (optional - IMPORTANT level)
- SMTP credentials (optional - OPTIONAL level)
- OAuth provider credentials (optional - OPTIONAL level)

### Check Prerequisites

```powershell
# PowerShell version
$PSVersionTable.PSVersion
# Should be >= 5.1 (Windows) or >= 7.0 (Linux/macOS)

# Docker
docker --version
# Should be >= 24.0

# .NET SDK
dotnet --version
# Should be >= 9.0

# Node.js
node --version
# Should be >= 20.0
```

---

## First-Time Setup

### Step 1: Clone Repository

```bash
git clone https://github.com/DegrassiAaron/meepleai-monorepo.git
cd meepleai-monorepo
```

### Step 2: Run Auto-Configuration Script

#### Option A: Quick Setup (Recommended for Development)

```powershell
cd infra/secrets
.\setup-secrets.ps1
```

**Output**:
```
=======================================
   MeepleAI Secrets Auto-Setup
=======================================

[STEP 1] Generating secure values...
  [OK] JWT_SECRET_KEY:           OwBQ90/g*** (64 bytes base64)
  [OK] POSTGRES_PASSWORD:        zNo%vL27*** (20 chars)
  [OK] REDIS_PASSWORD:           [d<9mynL*** (20 chars)
  [OK] EMBEDDING_SERVICE_API_KEY: C5CirwRq*** (32 bytes base64)
  [OK] QDRANT_API_KEY:           +7Ry8vXM*** (32 bytes base64)
  [OK] RERANKER_API_KEY:         v3KpWnQx*** (32 bytes base64)
  [OK] SMOLDOCLING_API_KEY:      8zFjTmBc*** (32 bytes base64)
  [OK] UNSTRUCTURED_API_KEY:     Lq2dYhNw*** (32 bytes base64)
  [OK] ADMIN_PASSWORD:           rgy&U,*0!%Ymfbhx (16 chars)
  [OK] GRAFANA_ADMIN_PASSWORD:   Kx7#vP2@qWnZtLm5 (16 chars)
  [OK] TRAEFIK_DASHBOARD_PASSWORD: Np4$wX9^cRyHjKf8 (16 chars)

[STEP 2] Creating and populating secret files...
  [OK] Created: admin.secret (1 auto-generated)
  [OK] Created: database.secret (1 auto-generated)
  [OK] Created: embedding-service.secret (1 auto-generated)
  [OK] Created: jwt.secret (1 auto-generated)
  [OK] Created: qdrant.secret (1 auto-generated)
  [OK] Created: redis.secret (1 auto-generated)
  [OK] Created: reranker-service.secret (1 auto-generated)
  [OK] Created: smoldocling-service.secret (1 auto-generated)
  [OK] Created: unstructured-service.secret (1 auto-generated)
  [OK] Created: monitoring.secret (2 auto-generated)
  [OK] Created: traefik.secret (1 auto-generated)
  [OK] Created: bgg.secret (manual config required)
  [OK] Created: openrouter.secret (manual config required)
  [OK] Created: email.secret (manual config required)
  [OK] Created: oauth.secret (manual config required)
  [OK] Created: storage.secret (manual config required)

[SUCCESS] All CRITICAL secrets configured!
          Application can start successfully.

[ACTION REQUIRED] Manual configuration needed for enhanced features:
  - bgg.secret: BoardGameGeek credentials (https://boardgamegeek.com)
  - openrouter.secret: OpenRouter API key (https://openrouter.ai/keys)
  - email.secret: SMTP credentials (for email notifications)
  - oauth.secret: Google/Discord OAuth (for social login)
  - storage.secret: S3 credentials (for cloud storage)
```

#### Option B: Setup with Backup (Recommended for Production)

```powershell
cd infra/secrets
.\setup-secrets.ps1 -SaveGenerated
```

**Additional Output**:
```
[BACKUP] Generated values saved to:
         .generated-values-20260117-151234.txt

⚠️  IMPORTANT: Copy these values to your password manager, then DELETE the file!
   This file contains sensitive credentials and should NEVER be committed to git.
```

**Backup File Contents** (`.generated-values-20260117-151234.txt`):
```
ADMIN_PASSWORD=rgy&U,*0!%Ymfbhx
EMBEDDING_SERVICE_API_KEY=C5CirwRqXcZJF3E8l+BqeLTqNRm2MwEMMpDsYRpUY5g=
JWT_SECRET_KEY=OwBQ90/gsq09hyKSJ6+2Hgl5GpTchLe3wz8E44D3wizvHRvT8KsUdWx1NQmYjLf2pZ4bVn7Gc0aX3rJwDkFtPg==
POSTGRES_PASSWORD=zNo%vL27Xp@K5wYhFjR3
QDRANT_API_KEY=+7Ry8vXMTcNq2dLpWfZhJk4sGbVnY9mQxUr3AwEi6oB=
REDIS_PASSWORD=[d<9mynL!Qz2@Xt7PkVh
RERANKER_API_KEY=v3KpWnQxZc8mLjYhTr2dNb5sGfVq9wUyEoK6AwPi4lA=
SMOLDOCLING_API_KEY=8zFjTmBcQx5nWr2dKp7vLy4sGhUq9wNyEoX6AwMi3lZ=
UNSTRUCTURED_API_KEY=Lq2dYhNwZc8mTjXr5vKp9bGfVq4sUyEoW7AwPi6lN=
GRAFANA_ADMIN_PASSWORD=Kx7#vP2@qWnZtLm5
TRAEFIK_DASHBOARD_PASSWORD=Np4$wX9^cRyHjKf8
```

⚠️ **CRITICAL**: After copying to password manager, **DELETE** this file:
```powershell
Remove-Item .generated-values-*.txt
```

### Step 3: Manual Configuration (Optional Services)

For enhanced features, configure external service credentials:

#### BoardGameGeek Integration (IMPORTANT)

Enables automatic game catalog synchronization.

```powershell
# Edit bgg.secret
notepad infra/secrets/bgg.secret

# Update values:
# BGG_USERNAME=your_bgg_username
# BGG_PASSWORD=your_bgg_password
```

**How to get credentials**:
1. Create account at https://boardgamegeek.com/register
2. Verify email address
3. Use username/password in `bgg.secret`

#### OpenRouter AI Gateway (IMPORTANT)

Enables cloud AI model fallback (when local Ollama unavailable).

```powershell
# Edit openrouter.secret
notepad infra/secrets/openrouter.secret

# Update values:
# OPENROUTER_API_KEY=sk-or-v1-your_key_here
# OPENROUTER_DEFAULT_MODEL=meta-llama/llama-3.3-70b-instruct:free
```

**How to get API key**:
1. Sign up at https://openrouter.ai
2. Navigate to https://openrouter.ai/keys
3. Create new API key
4. Copy key starting with `sk-or-v1-`

#### Email Notifications (OPTIONAL)

Enables password reset, account verification, and notification emails.

```powershell
# Edit email.secret
notepad infra/secrets/email.secret

# Update values:
# SMTP_HOST=smtp.gmail.com
# SMTP_PORT=587
# SMTP_USER=your_email@gmail.com
# SMTP_PASSWORD=your_app_password_here
# SMTP_FROM_EMAIL=noreply@meepleai.local
```

**Gmail Setup** (recommended):
1. Enable 2FA: https://myaccount.google.com/security
2. Create App Password: https://myaccount.google.com/apppasswords
3. Use generated password (16 chars without spaces)

#### OAuth Social Login (OPTIONAL)

Enables Google/Discord login.

```powershell
# Edit oauth.secret
notepad infra/secrets/oauth.secret

# Update values:
# GOOGLE_CLIENT_ID=your_google_client_id.apps.googleusercontent.com
# GOOGLE_CLIENT_SECRET=GOCSPX-your_google_secret
# DISCORD_CLIENT_ID=your_discord_client_id
# DISCORD_CLIENT_SECRET=your_discord_client_secret
```

**Provider Setup**:
- **Google**: https://console.cloud.google.com/apis/credentials
  - Create OAuth 2.0 Client ID
  - Authorized redirect URI: `http://localhost:8080/signin-google`
- **Discord**: https://discord.com/developers/applications
  - Create New Application
  - OAuth2 → Redirects: `http://localhost:8080/signin-discord`

### Step 4: Verify Configuration

```powershell
# Check all CRITICAL secrets exist
cd infra/secrets
$required = @('admin','database','jwt','qdrant','redis','embedding-service')
$required | ForEach-Object {
    if (!(Test-Path "$_.secret")) {
        Write-Host "❌ Missing: $_.secret" -ForegroundColor Red
    } else {
        Write-Host "✅ Found: $_.secret" -ForegroundColor Green
    }
}
```

**Expected Output**:
```
✅ Found: admin.secret
✅ Found: database.secret
✅ Found: jwt.secret
✅ Found: qdrant.secret
✅ Found: redis.secret
✅ Found: embedding-service.secret
```

### Step 5: Start Infrastructure

```bash
# Start core services (PostgreSQL, Redis, Qdrant)
cd ../../infra
docker compose up -d postgres qdrant redis

# Verify services are healthy
docker compose ps

# Check logs
docker compose logs -f postgres
# Look for: "database system is ready to accept connections"
```

### Step 6: Start Application

#### Backend API

```bash
cd ../apps/api/src/Api
dotnet run
```

**Expected Startup Logs**:
```
[10:00:00 INF] MeepleAI API starting...
[10:00:00 INF] Loading secrets from infra/secrets/*.secret
[10:00:00 INF] CRITICAL secret validation passed: admin.secret
[10:00:00 INF] CRITICAL secret validation passed: database.secret
[10:00:00 INF] CRITICAL secret validation passed: jwt.secret
[10:00:00 INF] CRITICAL secret validation passed: qdrant.secret
[10:00:00 INF] CRITICAL secret validation passed: redis.secret
[10:00:00 INF] CRITICAL secret validation passed: embedding-service.secret
[10:00:00 INF] All critical services passed startup health check.
[10:00:00 INF] Application started. Press Ctrl+C to shut down.
[10:00:00 INF] Listening on: http://localhost:8080
```

**Degraded Mode Example** (if OpenRouter not configured):
```
[10:00:00 WARN] IMPORTANT secret missing: infra/secrets/openrouter.secret
                OpenRouter integration disabled. Using local Ollama fallback.
[10:00:00 INF] Application started in degraded mode.
```

#### Frontend (New Terminal)

```bash
cd apps/web
pnpm install
pnpm dev
```

**Expected Output**:
```
  ▲ Next.js 14.2.3
  - Local:        http://localhost:3000
  - Ready in 2.1s
```

### Step 7: Validate Health Check

```bash
# Open browser to health check endpoint
curl http://localhost:8080/api/v1/health | jq
```

**Expected Response** (All Healthy):
```json
{
  "overallStatus": "Healthy",
  "checks": [
    {
      "serviceName": "postgres",
      "status": "Healthy",
      "description": "Connected to PostgreSQL",
      "isCritical": true,
      "timestamp": "2026-01-17T10:00:00Z"
    },
    {
      "serviceName": "redis",
      "status": "Healthy",
      "description": "Connected to Redis",
      "isCritical": true,
      "timestamp": "2026-01-17T10:00:00Z"
    },
    {
      "serviceName": "qdrant",
      "status": "Healthy",
      "description": "Connected to Qdrant",
      "isCritical": true,
      "timestamp": "2026-01-17T10:00:00Z"
    },
    {
      "serviceName": "embedding",
      "status": "Healthy",
      "description": "Embedding service ready",
      "isCritical": true,
      "timestamp": "2026-01-17T10:00:00Z"
    },
    {
      "serviceName": "openrouter",
      "status": "Degraded",
      "description": "OpenRouter API key missing (using fallback)",
      "isCritical": false,
      "timestamp": "2026-01-17T10:00:00Z"
    }
  ],
  "timestamp": "2026-01-17T10:00:00Z"
}
```

---

## Validation System

### 3-Level Priority

The auto-configuration system validates secrets at 3 priority levels:

| Priority | Behavior | Impact | Example |
|----------|----------|--------|---------|
| 🔴 **CRITICAL** | Block startup | Application won't start | `database.secret`, `jwt.secret` |
| 🟡 **IMPORTANT** | Warn, continue | Reduced functionality | `bgg.secret`, `openrouter.secret` |
| 🟢 **OPTIONAL** | Info, fallback | No impact on core features | `email.secret`, `oauth.secret` |

### CRITICAL Secrets (6 files)

Application **WILL NOT START** without these:

| File | Variables | Purpose |
|------|-----------|---------|
| `admin.secret` | ADMIN_EMAIL<br>ADMIN_PASSWORD<br>ADMIN_DISPLAY_NAME | Initial admin account |
| `database.secret` | POSTGRES_USER<br>POSTGRES_PASSWORD<br>POSTGRES_DB | PostgreSQL connection |
| `jwt.secret` | JWT_SECRET_KEY<br>JWT_ISSUER<br>JWT_AUDIENCE | JWT authentication |
| `qdrant.secret` | QDRANT_API_KEY | Vector database access |
| `redis.secret` | REDIS_PASSWORD | Cache/session storage |
| `embedding-service.secret` | EMBEDDING_SERVICE_API_KEY | AI embeddings for RAG |

**Error Example**:
```
ERROR: CRITICAL secret missing: infra/secrets/database.secret
Application startup blocked. Please run setup-secrets.ps1 or manually create the file.
```

### IMPORTANT Secrets (3 files)

Application **STARTS** with reduced functionality:

| File | Variables | Impact if Missing |
|------|-----------|-------------------|
| `bgg.secret` | BGG_USERNAME<br>BGG_PASSWORD | Game catalog limited to manual entries |
| `openrouter.secret` | OPENROUTER_API_KEY<br>OPENROUTER_DEFAULT_MODEL | Falls back to local Ollama (slower) |
| `unstructured-service.secret` | UNSTRUCTURED_API_KEY | PDF processing degraded (layout analysis disabled) |

**Warning Example**:
```
WARN: IMPORTANT secret missing: infra/secrets/bgg.secret
BoardGameGeek integration disabled. Game catalog limited to manual entries.
```

### OPTIONAL Secrets (8 files)

Application **STARTS** normally with fallback defaults:

| File | Variables | Fallback Behavior |
|------|-----------|-------------------|
| `email.secret` | SMTP_* | In-app notifications only |
| `oauth.secret` | GOOGLE_*, DISCORD_* | Email/password login only |
| `monitoring.secret` | GRAFANA_*, PROMETHEUS_* | Monitoring unavailable |
| `reranker-service.secret` | RERANKER_API_KEY | Skip reranking step |
| `smoldocling-service.secret` | SMOLDOCLING_API_KEY | Standard PDF extraction |
| `storage.secret` | S3_* | Local file storage |
| `traefik.secret` | TRAEFIK_* | Dashboard disabled |

**Info Example**:
```
INFO: OPTIONAL secret missing: infra/secrets/email.secret
Email notifications disabled. Using in-app notifications only.
```

---

## Troubleshooting

### Application Won't Start

#### Issue: CRITICAL secret missing

**Symptoms**:
```
ERROR: CRITICAL secret missing: infra/secrets/database.secret
Application startup blocked.
```

**Resolution**:
```powershell
# Option 1: Re-run auto-setup
cd infra/secrets
.\setup-secrets.ps1

# Option 2: Manual creation
cp database.secret.example database.secret
notepad database.secret
# Edit with real credentials
```

#### Issue: Secret validation failed

**Symptoms**:
```
ERROR: ADMIN_PASSWORD validation failed: Must be at least 8 characters
```

**Resolution**:
```powershell
# Edit secret file
notepad infra/secrets/admin.secret

# Ensure password meets requirements:
# - At least 8 characters
# - At least 1 uppercase letter
# - At least 1 digit
# Example: SecureP@ssw0rd123

# Restart application
cd ../../../apps/api/src/Api
dotnet run
```

### Service Unhealthy

#### Issue: PostgreSQL connection failed

**Symptoms**:
```bash
curl http://localhost:8080/api/v1/health | jq
# overallStatus: "Unhealthy"
# postgres: "Unhealthy"
```

**Resolution**:
```bash
# Check PostgreSQL is running
docker compose ps postgres

# Check logs
docker compose logs postgres
# Look for: "database system is ready to accept connections"

# Restart PostgreSQL
docker compose restart postgres

# Verify connection
docker compose exec postgres psql -U meepleai -c "\conninfo"
```

#### Issue: Redis connection failed

**Symptoms**:
```json
{
  "serviceName": "redis",
  "status": "Unhealthy",
  "description": "Connection refused"
}
```

**Resolution**:
```bash
# Check Redis is running
docker compose ps redis

# Check password matches secret file
cat infra/secrets/redis.secret | grep REDIS_PASSWORD

# Test connection
docker compose exec redis redis-cli -a "$(cat infra/secrets/redis.secret | grep REDIS_PASSWORD | cut -d= -f2)" ping
# Expected: PONG

# Restart Redis
docker compose restart redis
```

### Script Errors

#### Issue: PowerShell script won't run

**Symptoms**:
```
.\setup-secrets.ps1 : File cannot be loaded because running scripts is disabled
```

**Resolution**:
```powershell
# Check execution policy
Get-ExecutionPolicy

# Allow scripts (temporary)
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass

# Run script
.\setup-secrets.ps1

# Reset policy (optional)
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Restricted
```

#### Issue: Script fails mid-execution

**Symptoms**:
```
[ERROR] Failed to generate JWT_SECRET_KEY
```

**Resolution**:
```powershell
# Clean up partial files
Remove-Item *.secret

# Re-run script
.\setup-secrets.ps1

# If still fails, check PowerShell version
$PSVersionTable.PSVersion
# Should be >= 5.1

# Fallback to manual setup
Get-ChildItem -Filter "*.secret.example" | ForEach-Object {
    Copy-Item $_.FullName -Destination ($_.FullName -replace '\.example$','')
}
```

### Degraded Mode

#### Issue: Application in degraded mode

**Symptoms**:
```
[10:00:00 WARN] IMPORTANT secret missing: infra/secrets/openrouter.secret
[10:00:00 INF] Application started in degraded mode.
```

**Explanation**: Non-critical services unavailable, core functionality works.

**Resolution**:
```powershell
# Option 1: Accept degraded functionality (no action needed)

# Option 2: Configure missing service
cd infra/secrets
notepad openrouter.secret
# Add: OPENROUTER_API_KEY=sk-or-v1-your_key_here

# Restart application
cd ../../../apps/api/src/Api
dotnet run
```

---

## Advanced Configuration

### Secret Rotation

Rotate secrets periodically for security compliance.

#### JWT Secret Rotation (Every 90 days)

```powershell
# 1. Backup current secret
cd infra/secrets
Copy-Item jwt.secret jwt.secret.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')

# 2. Generate new secret
openssl rand -base64 64

# 3. Update jwt.secret
notepad jwt.secret
# Replace JWT_SECRET_KEY with new value from step 2

# 4. Restart API (invalidates all existing tokens)
cd ../../../apps/api/src/Api
dotnet run
```

⚠️ **Impact**: All users must log in again after rotation.

#### Database Password Rotation

```bash
# 1. Backup current config
cp infra/secrets/database.secret infra/secrets/database.secret.backup

# 2. Stop API (prevent connection errors)
docker compose stop api

# 3. Change PostgreSQL password
docker compose exec postgres psql -U postgres -c "ALTER USER meepleai WITH PASSWORD 'new_strong_password_here';"

# 4. Update secret file
notepad infra/secrets/database.secret
# POSTGRES_PASSWORD=new_strong_password_here

# 5. Restart all services
docker compose up -d

# 6. Verify health check
curl http://localhost:8080/api/v1/health | jq '.checks[] | select(.serviceName=="postgres")'
```

### Environment-Specific Configuration

#### Development

```powershell
# Use auto-generated secrets (weak passwords OK)
cd infra/secrets
.\setup-secrets.ps1

# Start with all services
docker compose up -d
```

#### Staging

```bash
# Use stronger passwords, external secrets manager
export JWT_SECRET_KEY="$(aws secretsmanager get-secret-value --secret-id staging/jwt-key --query SecretString --output text)"
export POSTGRES_PASSWORD="$(aws secretsmanager get-secret-value --secret-id staging/db-password --query SecretString --output text)"

# Start application (loads from environment)
dotnet run
```

#### Production

```bash
# Use cloud secrets manager (AWS/Azure/GCP)
export JWT_SECRET_KEY="$(aws secretsmanager get-secret-value --secret-id prod/jwt-key --query SecretString --output text)"
export POSTGRES_PASSWORD="$(aws secretsmanager get-secret-value --secret-id prod/db-password --query SecretString --output text)"

# Enable monitoring
export GRAFANA_ADMIN_PASSWORD="$(aws secretsmanager get-secret-value --secret-id prod/grafana-password --query SecretString --output text)"

# Start with observability
dotnet run
```

---

## Security Best Practices

### DO ✅

1. **Use auto-generated secrets** for cryptographic strength
2. **Backup generated values** with `-SaveGenerated` flag
3. **Store backups securely** in password manager (1Password, Bitwarden, etc.)
4. **Delete backup files** after storing in password manager
5. **Rotate secrets regularly** (JWT: 90 days, DB: 180 days)
6. **Use separate secrets per environment** (dev/staging/prod)
7. **Monitor health check endpoint** for service failures

### DON'T ❌

1. **Never commit `.secret` files** to git (only `.secret.example`)
2. **Never use weak passwords** for manually-configured secrets
3. **Never share secrets via insecure channels** (Slack, email, SMS)
4. **Never log secrets** (`console.log(apiKey)` forbidden)
5. **Never skip validation warnings** (degraded mode investigation required)
6. **Never use default passwords in production** (`change_me_*` forbidden)

---

## Related Documentation

- **Architecture Decision**: [ADR-021 - Auto-Configuration System](../01-architecture/adr/adr-021-auto-configuration-system.md)
- **Secrets Management**: [docs/04-deployment/secrets-management.md](./secrets-management.md)
- **Health Check System**: [docs/04-deployment/health-checks.md](./health-checks.md)
- **Health Check API Reference**: [docs/03-api/health-check-api.md](../03-api/health-check-api.md)
- **Secrets README**: [infra/secrets/README.md](../../infra/secrets/README.md)

---

**Maintained by**: MeepleAI DevOps Team
**Questions**: Open an issue on [GitHub](https://github.com/DegrassiAaron/meepleai-monorepo/issues)
