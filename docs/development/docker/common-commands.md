# Docker Commands Cheatsheet

**Last Updated**: 2026-02-02

Quick reference for daily Docker operations with MeepleAI.

---

## Service Management

### Start/Stop
```bash
cd infra

# Start with profile
docker compose --profile minimal up -d      # Core only
docker compose --profile ai up -d           # + AI services
docker compose --profile full up -d         # Everything

# Start specific
docker compose up -d postgres qdrant redis
docker compose up -d api web

# Stop
docker compose stop                         # Graceful
docker compose stop api web                 # Specific
docker compose down                         # Remove containers
docker compose down -v                      # ⚠️ Remove + volumes
```

### Restart
```bash
docker compose restart                      # All
docker compose restart api                  # Specific
docker compose up -d --force-recreate api   # Full recreate
```

### Status
```bash
docker compose ps                           # Running services
docker compose ps -a                        # All (including stopped)
docker compose ps | grep "(healthy)"        # Health check
docker ps                                   # All containers
```

---

## Logs & Debugging

### View Logs
```bash
# Follow mode
docker compose logs -f api
docker compose logs -f api web postgres

# Last N lines
docker compose logs --tail=50 api
docker compose logs --tail=100 --timestamps api

# Time range
docker compose logs --since 30m api         # Last 30min
docker compose logs --since 2h api          # Last 2h
```

### Search Logs
```bash
# PowerShell
pwsh -c "docker compose logs api | Select-String 'ERROR'"

# Grep (Git Bash/Linux/Mac)
docker compose logs api | grep -i exception
docker compose logs api | grep -E "ERROR|WARNING"
docker compose logs api | grep -B 3 -A 3 "ERROR"
```

---

## Container Operations

### Execute Commands
```bash
# Shell
docker compose exec api bash
docker compose exec web sh

# Specific command
docker compose exec postgres psql -U postgres -d meepleai
docker compose exec redis redis-cli -a PASSWORD PING

# As root
docker compose exec -u root api bash

# One-off
docker compose run --rm api dotnet --version
```

### Copy Files
```bash
# Container → Host
docker compose cp api:/app/logs/app.log ./local-logs/

# Host → Container
docker compose cp ./backup.sql postgres:/tmp/backup.sql
```

---

## Volume Management

### List & Inspect
```bash
docker volume ls
docker volume ls --filter label=com.docker.compose.project=meepleai
docker volume inspect meepleai_postgres_data
```

### Backup
```bash
# PostgreSQL
docker compose exec postgres pg_dump -U postgres meepleai | \
  gzip > backup-$(date +%Y%m%d).sql.gz

# Other volumes
docker run --rm \
  -v meepleai_qdrant_data:/data \
  -v $(pwd):/backup \
  alpine tar czf /backup/qdrant-$(date +%Y%m%d).tar.gz /data
```

### Restore
```bash
# PostgreSQL
gunzip -c backup.sql.gz | \
  docker compose exec -T postgres psql -U postgres meepleai

# Volumes
docker run --rm \
  -v meepleai_qdrant_data:/data \
  -v $(pwd):/backup \
  alpine tar xzf /backup/qdrant-20260202.tar.gz -C /data
```

---

## Image Management

### Build (⚠️ Profile Required)
```bash
# ❌ WRONG (no profile active)
docker compose build

# ✅ CORRECT (with profile)
docker compose --profile minimal build --no-cache   # api, web
docker compose --profile ai build --no-cache       # + AI services
docker compose --profile full build --no-cache     # all

# Specific service (no profile needed)
docker compose build --no-cache api web
```

### Pull & Clean
```bash
# Pull
docker compose pull
docker compose pull postgres qdrant

# Clean
docker image prune                  # Dangling
docker image prune -a               # All unused
docker system prune -a --volumes    # Everything ⚠️
```

---

## Database Operations

### PostgreSQL
```bash
# Connect
docker compose exec postgres psql -U postgres -d meepleai

# Run SQL
docker compose exec postgres psql -U postgres -d meepleai \
  -c "SELECT COUNT(*) FROM games;"

# Tables
docker compose exec postgres psql -U postgres -d meepleai -c "\dt"
docker compose exec postgres psql -U postgres -d meepleai -c "\d games"

# Size
docker compose exec postgres psql -U postgres -d meepleai \
  -c "SELECT pg_size_pretty(pg_database_size('meepleai'));"
```

### Redis
```bash
REDIS_PWD=$(pwsh -c "cat infra/secrets/redis.secret | Select-String 'REDIS_PASSWORD' | % { \$_.Line.Split('=')[1] }")
docker compose exec redis redis-cli -a $REDIS_PWD

# Inside redis-cli
> PING
> INFO
> DBSIZE
> FLUSHDB  # ⚠️ Clear DB
```

### Qdrant
```bash
# List collections
curl http://localhost:6333/collections

# Collection info
curl http://localhost:6333/collections/games

# Search
curl -X POST http://localhost:6333/collections/games/points/search \
  -H "Content-Type: application/json" \
  -d '{"vector": [...], "limit": 10}'
```

---

## Development Workflows

### Hot Reload (3 Terminals)
```bash
# Terminal 1: Infrastructure
docker compose up -d postgres qdrant redis

# Terminal 2: Backend
cd apps/api/src/Api && dotnet watch run

# Terminal 3: Frontend
cd apps/web && pnpm dev
```

### Rebuild After Changes
```bash
docker compose build --no-cache api web
docker compose up -d api web
```

### Migrations
```bash
cd apps/api/src/Api
dotnet ef migrations add MigrationName
dotnet ef database update
dotnet ef migrations list
```

---

## Monitoring

### Resource Stats
```bash
docker stats                                # Live
docker stats --no-stream                    # Snapshot
docker compose stats

# Disk usage
docker system df
docker system df -v                         # Detailed
```

### Health Checks
```bash
docker compose ps | grep "(healthy)"

# Continuous monitoring
watch -n 5 'docker compose ps | grep -E "(healthy|unhealthy)"'
```

---

## Production Operations

### Update Services
```bash
docker compose pull                         # Latest images
docker compose build && docker compose up -d

# Rolling update
docker compose up -d --no-deps --build api
docker compose up -d --no-deps --build web
```

### Graceful Shutdown
```bash
docker compose stop                         # 10s grace
docker compose stop -t 30                   # 30s grace
docker compose kill                         # Force
```

---

## System Cleanup

```bash
# Individual
docker container prune                      # Stopped containers
docker image prune                          # Dangling images
docker volume prune                         # Dangling volumes
docker network prune                        # Unused networks

# Everything ⚠️
docker system prune -a --volumes -f
```

---

## Troubleshooting

### Container Won't Start
```bash
docker compose logs api --tail=100
docker inspect meepleai-api
docker inspect meepleai-api --format='{{.State.ExitCode}}'
```

### Port Conflicts
```bash
# Windows
netstat -ano | findstr :8080
taskkill /PID <PID> /F

# Linux/Mac
lsof -i :8080
kill -9 <PID>
```

### Network Issues
```bash
# Recreate network
docker compose down
docker network rm meepleai
docker compose up -d

# Test connectivity
docker compose exec api ping postgres
docker compose exec api curl http://web:3000
```

---

## Quick Reference

| Task | Command |
|------|---------|
| Start minimal | `docker compose --profile minimal up -d` |
| Stop all | `docker compose down` |
| View logs | `docker compose logs -f <service>` |
| Restart | `docker compose restart <service>` |
| Rebuild | `docker compose build --no-cache <service>` |
| Shell | `docker compose exec <service> bash` |
| Stats | `docker stats` |
| Backup DB | `pg_dump > backup.sql` |
| Restore DB | `psql < backup.sql` |

---

**See Also**: [Quick Start](./quick-start.md) | [Service Endpoints](./service-endpoints.md) | [Troubleshooting](./troubleshooting.md) | [Profiles](./docker-profiles.md)
