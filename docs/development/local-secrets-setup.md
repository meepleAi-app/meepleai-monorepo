# Local Development Secrets Setup

**Last Updated**: 2026-01-15
**Security Level**: ⚠️ **CRITICAL** - Never commit real secrets to git

---

## Overview

MeepleAI uses **Docker Secrets** for production deployments and **environment variables** for local development. This guide explains how to configure credentials for local development without exposing them in git.

---

## Security Rules

### ✅ DO
- Store real credentials in `infra/secrets/` directory (gitignored)
- Use `.env` files for local development (gitignored)
- Use placeholder values in committed files
- Regenerate `.env` from secrets when they change

### ❌ DON'T
- Commit real passwords or API keys to git
- Share secrets via chat, email, or screenshots
- Use production secrets for local development
- Hardcode credentials in source code

---

## Quick Start

### Option 1: Automated Setup (Recommended)

```bash
# 1. Ensure secrets are initialized in infra/secrets/
ls infra/secrets/*.txt

# 2. Generate .env file from secrets
chmod +x scripts/generate-env-from-secrets.sh
./scripts/generate-env-from-secrets.sh

# 3. Verify generated .env
cat apps/api/src/Api/.env

# 4. Start API (will load .env automatically)
cd apps/api/src/Api
dotnet run
```

### Option 2: Manual Setup

```bash
# 1. Copy example file
cp apps/api/src/Api/.env.example apps/api/src/Api/.env

# 2. Edit .env with real values from infra/secrets/
nano apps/api/src/Api/.env

# 3. Update credentials:
#    - POSTGRES_PASSWORD (from infra/secrets/postgres-password.txt)
#    - REDIS_PASSWORD (from infra/secrets/redis-password.txt)
#    - OPENROUTER_API_KEY (from infra/secrets/openrouter-api-key.txt)
#    - OAuth credentials (optional)

# 4. Start API
cd apps/api/src/Api
dotnet run
```

---

## Required Secrets

### Core Services (Required)

| Secret | File | Used By | Default Value (Dev) |
|--------|------|---------|---------------------|
| **PostgreSQL Password** | `infra/secrets/postgres-password.txt` | Database | `meeplepass` |
| **Redis Password** | `infra/secrets/redis-password.txt` | Cache | `z1x22ROGjbSha7HQ8UE6KOL3` |

### AI Services (Optional)

| Secret | File | Used By | Purpose |
|--------|------|---------|---------|
| **OpenRouter API Key** | `infra/secrets/openrouter-api-key.txt` | LLM fallback | External LLM access |
| **BGG API Token** | `infra/secrets/bgg-api-token.txt` | Game scraper | BoardGameGeek API |

### OAuth Providers (Optional)

| Secret | File | Used By | Purpose |
|--------|------|---------|---------|
| **Google OAuth Client ID** | `infra/secrets/google-oauth-client-id.txt` | Authentication | Google login |
| **Google OAuth Client Secret** | `infra/secrets/google-oauth-client-secret.txt` | Authentication | Google login |
| **Discord OAuth Client ID** | `infra/secrets/discord-oauth-client-id.txt` | Authentication | Discord login |
| **Discord OAuth Client Secret** | `infra/secrets/discord-oauth-client-secret.txt` | Authentication | Discord login |
| **GitHub OAuth Client ID** | `infra/secrets/github-oauth-client-id.txt` | Authentication | GitHub login |
| **GitHub OAuth Client Secret** | `infra/secrets/github-oauth-client-secret.txt` | Authentication | GitHub login |

### Bootstrap Credentials (Optional)

| Secret | File | Used By | Purpose |
|--------|------|---------|---------|
| **Initial Admin Password** | `infra/secrets/initial-admin-password.txt` | Bootstrap | First admin user |

---

## File Locations

### Development Configuration Files

```
apps/api/src/Api/
├── .env                  # ❌ GITIGNORED - Your local credentials
├── .env.example          # ✅ COMMITTED - Template with placeholders
└── Properties/
    └── launchSettings.json  # ✅ COMMITTED - Placeholder references
```

### Secret Files (Gitignored)

```
infra/secrets/
├── postgres-password.txt           # Database password
├── redis-password.txt              # Cache password
├── openrouter-api-key.txt          # OpenRouter API key
├── bgg-api-token.txt               # BoardGameGeek token
├── initial-admin-password.txt      # Bootstrap admin password
├── google-oauth-client-id.txt      # Google OAuth
├── google-oauth-client-secret.txt  # Google OAuth
├── discord-oauth-client-id.txt     # Discord OAuth
├── discord-oauth-client-secret.txt # Discord OAuth
├── github-oauth-client-id.txt      # GitHub OAuth
└── github-oauth-client-secret.txt  # GitHub OAuth
```

---

## How It Works

### Docker Compose (Production)

```yaml
services:
  api:
    environment:
      OPENROUTER_API_KEY_FILE: /run/secrets/openrouter-api-key
    secrets:
      - openrouter-api-key
    entrypoint: ["/scripts/load-secrets-env.sh"]
    command: ["dotnet", "Api.dll"]

secrets:
  openrouter-api-key:
    file: ./secrets/openrouter-api-key.txt
```

**Flow**:
1. Docker mounts secret files to `/run/secrets/`
2. `load-secrets-env.sh` reads files and exports as env vars
3. Application reads from environment variables

### Local Development (.NET)

```bash
# .env file in apps/api/src/Api/.env
POSTGRES_PASSWORD=meeplepass
REDIS_PASSWORD=z1x22ROGjbSha7HQ8UE6KOL3
```

**Flow**:
1. `.env` file loads automatically with `dotnet run`
2. ASP.NET Core configuration system reads env vars
3. Application uses values from configuration

---

## Verification

### Check Secrets Are Loaded

```bash
# 1. View .env file (should have real values, not placeholders)
cat apps/api/src/Api/.env

# 2. Start API and check logs
cd apps/api/src/Api
dotnet run

# Look for successful connections:
# ✅ info: Microsoft.EntityFrameworkCore.Database.Command[20101]
#    Executed DbCommand (123ms) [Parameters=[], CommandType='Text']

# 3. Test database connection
curl http://localhost:8080/health

# 4. Test Redis connection (check logs for cache hits)
curl http://localhost:8080/api/v1/shared-games?search=catan
```

### Common Issues

#### "Password authentication failed for user postgres"
```bash
# Check password matches
cat infra/secrets/postgres-password.txt
cat apps/api/src/Api/.env | grep POSTGRES_PASSWORD

# Regenerate .env if mismatched
./scripts/generate-env-from-secrets.sh
```

#### "Connection refused" for Redis
```bash
# Check Redis password
cat infra/secrets/redis-password.txt
cat apps/api/src/Api/.env | grep REDIS_PASSWORD

# Test Redis connection
docker exec -it meepleai-redis redis-cli -a $(cat infra/secrets/redis-password.txt) PING
# Should return: PONG
```

#### "PLACEHOLDER" values in .env
```bash
# Run generation script
./scripts/generate-env-from-secrets.sh

# Or manually copy from secrets
POSTGRES_PASS=$(cat infra/secrets/postgres-password.txt)
echo "POSTGRES_PASSWORD=$POSTGRES_PASS" >> apps/api/src/Api/.env
```

---

## Updating Secrets

### Rotate Credentials

```bash
# 1. Update secret file
echo "new-secure-password-123" > infra/secrets/postgres-password.txt

# 2. Regenerate .env
./scripts/generate-env-from-secrets.sh

# 3. Restart services
docker compose restart postgres
cd apps/api/src/Api && dotnet run
```

### Add New Secret

```bash
# 1. Add to infra/secrets/
echo "your-new-api-key" > infra/secrets/new-service-api-key.txt

# 2. Update docker-compose.yml
# Add to secrets section and service environment

# 3. Update generation script
nano scripts/generate-env-from-secrets.sh
# Add new secret reading logic

# 4. Regenerate .env
./scripts/generate-env-from-secrets.sh
```

---

## Security Best Practices

### For Developers

1. **Never commit `.env` file** - Verify `.gitignore` includes `.env`
2. **Use strong passwords** - Minimum 16 characters, random
3. **Rotate regularly** - Change passwords every 90 days
4. **Separate environments** - Different secrets for dev/staging/prod
5. **Use secret managers** - Consider 1Password, Bitwarden for team secrets

### For Teams

1. **Share via secure channels** - Use 1Password Shared Vaults, not email
2. **Document access** - Track who has access to which secrets
3. **Audit regularly** - Review secret usage and access logs
4. **Revoke on departure** - Rotate secrets when team members leave
5. **Test secret rotation** - Ensure systems work after credential changes

### CI/CD Integration

```yaml
# GitHub Actions example (use repository secrets)
- name: Setup secrets
  env:
    POSTGRES_PASSWORD: ${{ secrets.POSTGRES_PASSWORD }}
    REDIS_PASSWORD: ${{ secrets.REDIS_PASSWORD }}
  run: |
    echo "$POSTGRES_PASSWORD" > infra/secrets/postgres-password.txt
    echo "$REDIS_PASSWORD" > infra/secrets/redis-password.txt
    ./scripts/generate-env-from-secrets.sh
```

---

## Related Documentation

- [Docker Compose Configuration](../04-deployment/docker-compose.md)
- [Security Guidelines](../04-deployment/security.md)
- [CI/CD Setup](../04-deployment/ci-cd.md)
- [Production Deployment](../04-deployment/production.md)

---

**Last Updated**: 2026-01-15
**Maintainer**: MeepleAI Security Team
**Security Contact**: security@meepleai.dev (DO NOT share secrets via this channel)
