# Docker Quick Start - 5 Minutes

**Last Updated**: 2026-02-02

Start MeepleAI locally in under 5 minutes with minimal setup.

---

## Prerequisites (1 minute)

- **Docker Desktop**: ≥ 4.20 running
- **PowerShell**: ≥ 7.0 (Windows) or bash (Linux/Mac)
- **Git**: Repository cloned

```bash
# Verify Docker
docker --version  # Should show ≥ 24.0
docker compose version  # Should show ≥ 2.20
```

---

## Setup (2 minutes)

### 1. Generate Secrets (Auto)

```powershell
cd infra/secrets
pwsh setup-secrets.ps1 -SaveGenerated
```

**Output**: Auto-generates 10 `.secret` files with secure random passwords, JWT keys, API keys

**⏱️ Time Saved**: 15-30 minutes of manual configuration

### 2. Configure Frontend

```bash
cd apps/web
cp .env.development.example .env.local
```

### 3. Build Images (Optional but Recommended)

⚠️ **IMPORTANT**: If this is your first time, or after code changes, build images with a profile:

```bash
cd infra

# Build minimal stack (api + web)
docker compose --profile minimal build --no-cache

# Or build everything
docker compose --profile full build --no-cache

# ❌ WRONG - Will fail with "No services to build"
docker compose build --no-cache
```

**Why?** Services with `build:` are assigned to profiles. No profile = Docker can't see them.

---

## Start Services (2 minutes)

### Option A: Minimal Stack (Recommended for Development)

**Services**: Postgres, Redis, Qdrant, API, Web (5 services)
**RAM**: ~4 GB
**Startup Time**: ~30 seconds

```bash
cd infra
docker compose --profile minimal up -d
```

### Option B: Full Stack (All Features)

**Services**: All 17+ services including AI, monitoring, automation
**RAM**: ~18 GB
**Startup Time**: ~3 minutes

```bash
cd infra
docker compose --profile full up -d
```

### Option C: Native Development (Fastest Hot Reload)

**Best for**: Active development with instant hot reload

**Terminal 1 - Infrastructure**:
```bash
cd infra
docker compose up -d postgres qdrant redis
```

**Terminal 2 - Backend API**:
```bash
cd apps/api/src/Api
dotnet watch run  # http://localhost:8080
```

**Terminal 3 - Frontend**:
```bash
cd apps/web
pnpm dev  # http://localhost:3000
```

---

## Verify (30 seconds)

### Check Running Services

```bash
cd infra
docker compose ps
```

**Expected Output**:
```
NAME                   STATUS           PORTS
meepleai-postgres      Up (healthy)     127.0.0.1:5432->5432/tcp
meepleai-qdrant        Up               127.0.0.1:6333-6334->6333-6334/tcp
meepleai-redis         Up (healthy)     127.0.0.1:6379->6379/tcp
meepleai-api           Up (healthy)     127.0.0.1:8080->8080/tcp
meepleai-web           Up (healthy)     127.0.0.1:3000->3000/tcp
```

### Test Endpoints

```bash
# Web UI
curl http://localhost:3000/

# API Health
curl http://localhost:8080/health

# API Documentation
# Open in browser: http://localhost:8080/scalar/v1
```

---

## Access Services

**Core Services**:
- **Web UI**: http://localhost:3000
- **API Documentation**: http://localhost:8080/scalar/v1
- **API Health**: http://localhost:8080/health

**See full endpoint list**: [service-endpoints.md](./service-endpoints.md)

---

## Next Steps

### Development Workflow

1. **Make code changes** (hot reload active with `dotnet watch` and `pnpm dev`)
2. **View logs**: `docker compose logs -f api web`
3. **Run tests**: `cd apps/api && dotnet test` | `cd apps/web && pnpm test`
4. **Stop services**: `docker compose down`

### Expand Services

```bash
# Add AI services (embedding, reranker, etc.)
docker compose --profile ai up -d

# Add monitoring (Grafana, Prometheus)
docker compose --profile observability up -d

# Add automation (n8n workflows)
docker compose --profile automation up -d
```

**See profiles details**: [docker-profiles.md](./docker-profiles.md)

---

## Quick Commands

```bash
# Stop all
docker compose down

# View logs (all services)
docker compose logs -f

# View logs (specific service)
docker compose logs -f api

# Restart service
docker compose restart api

# Remove everything + data (⚠️ DATA LOSS!)
docker compose down -v
```

**See full command reference**: [common-commands.md](./common-commands.md)

---

## Troubleshooting

| Issue | Quick Fix |
|-------|-----------|
| **Port 8080 in use** | `netstat -ano \| findstr :8080` → `taskkill /PID <PID> /F` |
| **Secrets not found** | `cd infra/secrets && pwsh setup-secrets.ps1` |
| **Service unhealthy** | `docker compose logs <service>` → `docker compose restart <service>` |
| **High memory usage** | Use `--profile minimal` instead of `--profile full` |

**See detailed troubleshooting**: [troubleshooting.md](./troubleshooting.md)

---

## Learning Resources

- **Service Endpoints**: [service-endpoints.md](./service-endpoints.md)
- **Clean Builds**: [clean-builds.md](./clean-builds.md)
- **Docker Profiles**: [docker-profiles.md](./docker-profiles.md)
- **Advanced Features**: [advanced-features.md](./advanced-features.md)
- **Main Guide**: [../local-environment-startup-guide.md](../local-environment-startup-guide.md)

---

**⏱️ Total Time**: ~5 minutes (including secret generation)
**💾 Disk Usage**: ~10 GB (minimal) | ~30 GB (full)
**🧠 RAM Usage**: ~4 GB (minimal) | ~18 GB (full)
