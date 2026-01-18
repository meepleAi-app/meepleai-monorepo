# Health Check Scripts Reference

Quick reference for all health check and secret validation scripts.

---

## Quick Start

### Test Everything
```bash
# Comprehensive test: services + secrets + OAuth
./scripts/test-all-health.sh
```

### Individual Tests
```bash
# Test all Docker services
./scripts/test-services.sh

# Validate OAuth secret files
python scripts/validate-oauth-secrets.py

# Test OAuth via API health check
./scripts/test-oauth-health.sh
```

---

## Available Scripts

### 1. test-all-health.sh
**Purpose**: Comprehensive health check of all systems

**Usage**:
```bash
./scripts/test-all-health.sh
```

**Tests**:
- Docker services (20 containers)
- OAuth secret files validation
- OAuth configuration via API health check

**Output**:
```
Main Health:     Degraded
Configuration:   Degraded
Health Checks:   6/7 Healthy, 1 Degraded
OAuth Providers: 3/3 configured
```

---

### 2. test-services.sh
**Purpose**: Check health of all Docker services

**Usage**:
```bash
./scripts/test-services.sh
```

**Tests**:
- Web services (Frontend, API)
- Monitoring (Grafana, Prometheus, Alertmanager, cAdvisor, Node Exporter)
- Development tools (Mailpit, n8n)
- AI services (Embedding, Unstructured, SmolDocling, Reranker, Ollama)
- Storage (Qdrant, PostgreSQL, Redis)

**Output**:
```
Frontend:          ✅ OK (HTTP 200)
API Health:        ✅ OK (HTTP 200)
PostgreSQL:        ✅ OK (Accepting connections)
Redis:             ✅ OK (PONG)
...
```

---

### 3. validate-oauth-secrets.py
**Purpose**: Validate OAuth secret files directly

**Usage**:
```bash
python scripts/validate-oauth-secrets.py
```

**Checks**:
- File existence
- No placeholder values (`your-`, `PLACEHOLDER`, `${...}`)
- Minimum length requirements
- No whitespace
- No empty files

**Output**:
```
Valid OAuth Providers: 3 / 3

[OK] Google Client ID: 9331....com (length: 72)
[OK] Google Client Secret: GOCS...y_t5 (length: 35)
[OK] Discord Client ID: 1436...2765 (length: 19)
[OK] Discord Client Secret: 2O_T...NT8U (length: 32)
[OK] GitHub Client ID: Ov23...YVmu (length: 20)
[OK] GitHub Client Secret: ba40...9197 (length: 40)
```

---

### 4. test-oauth-health.sh
**Purpose**: Test OAuth configuration via API health check

**Usage**:
```bash
# Basic test
./scripts/test-oauth-health.sh

# Verbose output with full JSON
./scripts/test-oauth-health.sh --verbose
```

**Checks**:
- `/health/config` endpoint availability
- OAuth provider configuration status
- Placeholder detection
- Misconfiguration detection
- Client ID masking

**Output**:
```
OAuth Providers:
✅ Configured: Google, Discord, GitHub (3/3)

Masked Client IDs:
- Google:  9331....com
- Discord: 1436...2765
- GitHub:  Ov23...YVmu

Summary:
✅ All OAuth providers (3/3) are properly configured
```

---

### 5. generate-env-from-secrets.sh
**Purpose**: Generate `.env` file from Docker secrets

**Usage**:
```bash
./scripts/generate-env-from-secrets.sh
```

**Actions**:
- Reads all secrets from `infra/secrets/`
- Generates `apps/api/src/Api/.env` with real values
- Sets proper environment variables for local development

**Output**:
```
🔐 Generating .env file from Docker secrets...
✅ Generated apps/api/src/Api/.env
```

---

## Health Check Endpoints

### Standard Endpoints

| Endpoint | Purpose | Response |
|----------|---------|----------|
| `/health` | All health checks | Summary of all checks |
| `/health/ready` | K8s readiness probe | DB, cache, vector DB only |
| `/health/live` | K8s liveness probe | Basic app running check |

**Example**:
```bash
curl http://localhost:8080/health | jq .
```

### Configuration Endpoint (NEW)

| Endpoint | Purpose | Response |
|----------|---------|----------|
| `/health/config` | Configuration validation | Detailed config data including OAuth |

**Example**:
```bash
curl http://localhost:8080/health/config | jq .
```

**Response Structure**:
```json
{
  "status": "Healthy" | "Degraded" | "Unhealthy",
  "checks": [{
    "name": "configuration",
    "data": {
      "oauth_configured_providers": ["Google", "Discord", "GitHub"],
      "oauth_misconfigured_providers": [],
      "oauth_placeholder_providers": [],
      "oauth_google_client_id": "9331....com",
      "redis_configured": true,
      "qdrant_configured": true,
      "secrets_found": ["postgres-password", "redis-password"]
    }
  }]
}
```

---

## Common Workflows

### Morning Startup Check
```bash
# 1. Start all services
cd infra && docker compose --profile full up -d

# 2. Wait for services to start
sleep 30

# 3. Run comprehensive health check
./scripts/test-all-health.sh
```

### After Updating Secrets
```bash
# 1. Update secret files
echo "new-value" > infra/secrets/google-oauth-client-id.txt

# 2. Validate secrets
python scripts/validate-oauth-secrets.py

# 3. Regenerate .env
./scripts/generate-env-from-secrets.sh

# 4. Restart services
cd infra && docker compose restart api

# 5. Verify changes
./scripts/test-oauth-health.sh
```

### Debug OAuth Issues
```bash
# 1. Validate secret files
python scripts/validate-oauth-secrets.py

# 2. Check API configuration
curl -s http://localhost:8080/health/config | jq '.checks[0].data'

# 3. Check container environment variables
docker exec meepleai-api printenv | grep OAUTH

# 4. Check secret loading logs
docker logs meepleai-api 2>&1 | grep "\[secrets\]"
```

---

## Exit Codes

| Script | Exit 0 | Exit 1 |
|--------|--------|--------|
| `test-all-health.sh` | All tests passed | Any test failed |
| `test-services.sh` | All services healthy | N/A (always 0) |
| `validate-oauth-secrets.py` | All valid or partial valid | No providers configured |
| `test-oauth-health.sh` | All configured or partial | No providers configured |

---

## Troubleshooting

### Script Permission Denied
```bash
chmod +x scripts/*.sh
```

### Python Script Not Found
```bash
# Ensure Python 3 is installed
python --version

# Or use python3
python3 scripts/validate-oauth-secrets.py
```

### jq Command Not Found
```bash
# Install jq
# Windows: choco install jq
# Mac: brew install jq
# Linux: apt-get install jq
```

### Health Check Returns 404
```bash
# API may not be running
docker compose up -d api

# Wait for startup
sleep 20

# Try again
curl http://localhost:8080/health
```

---

## Related Documentation

- [Docker Services Test URLs](../claudedocs/docker-services-test-urls.md)
- [Health Check OAuth Report](../claudedocs/health-check-oauth-report.md)
- [Secrets Audit Report](../claudedocs/secrets-audit-2026-01-15.md)
- [Local Secrets Setup Guide](../docs/02-development/local-secrets-setup.md)

---

**Last Updated**: 2026-01-15
**Maintainer**: MeepleAI DevOps Team
