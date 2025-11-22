# Development Workflow Scripts

PowerShell scripts for managing Docker Compose development environment.

## Scripts

### 🚀 **dev-up.ps1**
**Purpose:** Start the entire development stack (Docker Compose services)

**What it does:**
1. Creates `.env` files from templates if missing (`api.env.dev`, `web.env.dev`, `n8n.env.dev`)
2. Builds Docker images (optionally with `--no-cache` if `-Rebuild` flag is used)
3. Starts all services in detached mode (`docker compose up -d`)
4. Shows service status

**Usage:**
```powershell
# Start all services
.\scripts\dev\dev-up.ps1

# Rebuild images from scratch and start
.\scripts\dev\dev-up.ps1 -Rebuild
```

**Who uses it:** Developers starting local development environment
**When:** Daily, before starting development work
**Requirements:** Docker Desktop, Docker Compose

---

### 🛑 **dev-down.ps1**
**Purpose:** Stop and remove all development containers and volumes

**What it does:**
- Runs `docker compose down -v` to stop services and delete volumes
- Cleans up database data, cache data, and temporary files

**Usage:**
```powershell
# Stop all services and remove volumes
.\scripts\dev\dev-down.ps1
```

**Who uses it:** Developers cleaning up development environment
**When:** End of day, troubleshooting, fresh start
**⚠️ Warning:** This deletes all Docker volumes (database data will be lost)

---

### 📋 **dev-logs.ps1**
**Purpose:** Stream logs from all Docker Compose services

**What it does:**
- Runs `docker compose logs -f` to follow logs in real-time
- Shows output from all 15 services (API, Web, PostgreSQL, Qdrant, Redis, etc.)

**Usage:**
```powershell
# Follow all service logs
.\scripts\dev\dev-logs.ps1
```

**Who uses it:** Developers debugging issues or monitoring service behavior
**When:** Troubleshooting errors, monitoring startup, watching API requests
**Tip:** Press `Ctrl+C` to stop following logs

---

## Cross-Platform Alternative

**Linux/macOS users:** Use Docker Compose directly:
```bash
# Start services
cd infra && docker compose up -d

# Stop services
cd infra && docker compose down -v

# View logs
cd infra && docker compose logs -f
```

Or use the comprehensive setup script:
```bash
bash tools/setup/setup-test-environment.sh
```

---

## Directory Structure

These scripts are located in `scripts/dev/` and work with the Docker Compose configuration in `infra/docker-compose.yml`.

**Environment files:** `infra/env/*.env.dev`
**Templates:** `infra/env/*.env.dev.example`

---

## Troubleshooting

**Services won't start:**
```powershell
# Rebuild from scratch
.\scripts\dev\dev-up.ps1 -Rebuild

# Check service status
cd ..\infra
docker compose ps
```

**Port conflicts:**
- Check if ports 3000 (Web), 8080 (API), 5432 (Postgres) are available
- Stop conflicting services or change ports in `docker-compose.yml`

**Missing .env files:**
- Script auto-creates them from `.example` templates
- Manually edit `infra/env/*.env.dev` for custom configuration

---

**Last Updated:** 2025-11-22
**Maintained by:** DevOps team
