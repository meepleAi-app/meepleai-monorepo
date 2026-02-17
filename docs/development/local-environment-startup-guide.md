# Local Environment Startup Guide

**Version**: 1.0 | **Last Updated**: 2026-01-22

---

## Environment Overview

| Environment | Database | HTTPS | Log Retention | RAM Required |
|------------|----------|-------|---------------|--------------|
| **Development** | `meepleai` | HTTP only | Default | ~6 GB |
| **Staging** | `meepleai_staging` | HTTP | 10MB × 3 | ~8 GB |
| **Production** | `meepleai_prod` | HTTPS + HTTP | 50MB × 10 | ~16 GB |

---

## Quick Start (Development)

### 1. Clone & Setup Secrets (15-30min saved)

```bash
git clone <repo> && cd meepleai-monorepo-dev
cd infra/secrets && pwsh setup-secrets.ps1 -SaveGenerated
cd ../../apps/web && cp .env.development.example .env.local
```

**Secrets Generated** (10 files):
- ✅ **CRITICAL**: database, redis, qdrant, jwt, admin, embedding-service
- ⚠️ **IMPORTANT**: openrouter, oauth, bgg
- 🟢 **OPTIONAL**: email

### 2. Start Development (3 Terminals)

**Terminal 1 - Infrastructure**:
```bash
cd infra && docker compose up -d postgres qdrant redis
```

**Terminal 2 - Backend**:
```bash
cd apps/api/src/Api && dotnet run  # :8080
```

**Terminal 3 - Frontend**:
```bash
cd apps/web && pnpm dev  # :3000
```

---

## Docker Profiles

| Profile | Services | RAM | Use Case |
|---------|----------|-----|----------|
| **minimal** | postgres, qdrant, redis, api, web | ~4 GB | Core dev |
| **dev** | minimal + prometheus, grafana, mailpit | ~6 GB | Debug + monitor |
| **ai** | minimal + ollama, embedding, unstructured, smoldocling, reranker | ~12 GB | ML dev |
| **automation** | minimal + n8n | ~5 GB | Workflow dev |
| **observability** | dev + alertmanager, cadvisor, node-exporter | ~8 GB | Full monitor |
| **full** | All services | ~18 GB | Complete stack |

```bash
cd infra
docker compose --profile minimal up -d
docker compose --profile ai up -d
```

---

## Service URLs

### Core
- **Frontend**: http://localhost:3000
- **API**: http://localhost:8080
- **API Docs**: http://localhost:8080/scalar/v1

### Database
- **PostgreSQL**: localhost:5432 (postgres/meeplepass)
- **Redis**: localhost:6379 (password in redis.secret)
- **Qdrant**: http://localhost:6333

### AI Services
- **Embedding**: :8000/health
- **Reranker**: :8003/health
- **Unstructured**: :8001/health
- **SmolDocling**: :8002/health

### Monitoring
- **Grafana**: http://localhost:3001 (admin/admin)
- **Prometheus**: http://localhost:9090
- **Mailpit**: http://localhost:8025

---

## Common Commands

### Backend (.NET)

```bash
cd apps/api/src/Api

# Run
dotnet run                          # :8080
dotnet watch run                    # Hot reload

# Build
dotnet build
dotnet clean && dotnet build

# Test
dotnet test
dotnet test --filter "Category=Unit"
dotnet test /p:CollectCoverage=true

# Migrations
dotnet ef migrations add MigrationName
dotnet ef database update
dotnet ef migrations list
```

### Frontend (Next.js)

```bash
cd apps/web

# Run
pnpm dev                            # :3000
pnpm build && pnpm start            # Production

# Test
pnpm test                           # Vitest
pnpm test:coverage
pnpm test:e2e                       # Playwright
pnpm test:e2e:ui                    # Playwright UI Mode

# Quality
pnpm typecheck
pnpm lint
pnpm lint --fix
```

### Docker

```bash
cd infra

# Start/Stop
docker compose up -d
docker compose down
docker compose down -v              # ⚠️ Data loss!

# Logs
docker compose logs -f api
docker compose logs --tail=100 api

# Restart
docker compose restart api
docker compose up -d --force-recreate

# Status
docker compose ps
docker stats
```

---

## Staging Environment

### Setup Secrets

```bash
cd infra/secrets && mkdir -p staging
pwsh -Command "[Convert]::ToBase64String((1..32 | % { Get-Random -Max 256 }))" > staging/redis-password.txt
```

### Start

```bash
cd infra
docker compose -f docker-compose.yml -f compose.staging.yml up -d
```

**Variables**: `POSTGRES_DB=meepleai_staging`, `STAGING_API_URL=http://api:8080`

---

## Production Environment

### TLS Certificate

```bash
cd infra/secrets/prod
openssl req -x509 -newkey rsa:4096 -keyout api-key.pem -out api-cert.pem -days 365 -nodes
openssl pkcs12 -export -out api-cert.pfx -inkey api-key.pem -in api-cert.pem -password pass:YourPass
echo "YourPass" > api-cert-password.txt
```

### Required Variables

```bash
export PRODUCTION_API_URL=https://your-domain.com
export GRAFANA_ROOT_URL=https://grafana.your-domain.com
export SEQ_PASSWORD_HASH=$(echo -n 'YourSeqPassword' | sha256sum | cut -d' ' -f1)
```

### Start

```bash
cd infra
docker compose -f docker-compose.yml -f compose.prod.yml up -d
```

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| **Unhealthy service** | `docker compose logs service --tail=100` → `docker compose restart service` |
| **DB connection failed** | `docker compose ps postgres` → Check `database.secret` |
| **Port in use** | Windows: `netstat -ano \| findstr :8080` → `taskkill /PID <PID> /F` |
| **Secrets missing** | `cd infra/secrets && pwsh setup-secrets.ps1 -SaveGenerated` |
| **JWT invalid** | Regenerate: `pwsh -Command "[Convert]::ToBase64String((1..64 | % { Get-Random -Max 256 }))" > jwt.secret` |
| **Frontend proxy error** | Check `apps/web/.env.local`: `NEXT_PUBLIC_API_BASE=http://localhost:8080` |

### Performance

**High memory**:
```bash
docker stats                        # Check usage
docker compose --profile minimal up -d  # Reduce services
```

**Slow startup** (normal: 3-5min for full stack):
- Use specific profiles vs `full`
- Pre-download: `docker compose pull`

---

## Development Workflow

### Initial Setup (Once)

```bash
git clone <repo> && cd meepleai-monorepo-dev
cd infra/secrets && pwsh setup-secrets.ps1 -SaveGenerated
cd ../../apps/web && cp .env.development.example .env.local
cd ../api/src/Api && dotnet restore
cd ../../../web && pnpm install
```

### Daily Development

```bash
# T1: Infrastructure
cd infra && docker compose --profile minimal up -d

# T2: Backend
cd apps/api/src/Api && dotnet watch run

# T3: Frontend
cd apps/web && pnpm dev

# T4: Tests (optional)
cd apps/web && pnpm test:watch
```

### Pre-Commit

```bash
# Backend
cd apps/api && dotnet build && dotnet test && dotnet format --verify-no-changes

# Frontend
cd apps/web && pnpm typecheck && pnpm lint && pnpm test && pnpm build
```

### Feature Flow

```bash
git checkout -b feature/issue-123-desc
# ... develop ...
cd apps/api && dotnet test
cd apps/web && pnpm typecheck && pnpm lint && pnpm test && pnpm test:e2e
git commit -m "feat(scope): description"
git push -u origin feature/issue-123-desc
```

---

## Playwright Screenshot Automation

### Codegen (Interactive)

```bash
cd apps/web
pnpm playwright codegen http://localhost:3000
# Navigate UI → Auto-generates code → Add screenshot commands → Save script
```

### Automated Script

```typescript
// scripts/generate-docs-screenshots.ts
import { chromium } from '@playwright/test';

async function captureScreenshots() {
  const browser = await chromium.launch();
  const page = await browser.newPage();

  await page.goto('http://localhost:3000/admin/dashboard');
  await page.screenshot({ path: 'docs/screenshots/admin-dashboard.png', fullPage: true });

  await browser.close();
}
```

**Run**: `pnpm tsx scripts/generate-docs-screenshots.ts`

---

## Best Practices

1. **Development**: Native API + Web + Docker infra only (faster, lower RAM)
2. **Secrets**: NEVER commit `.secret` files (use `.gitignore`)
3. **Backups**: Export volumes before `down -v`
4. **Logs**: Use `--tail` to avoid console overload
5. **Profiles**: Use specific profiles vs `full` to save resources

---

**References**: [Development Docs](./README.md) | [Secrets Setup](./local-secrets-setup.md) | [Docker Services](./docker/service-endpoints.md) | [Troubleshooting](./troubleshooting/)
