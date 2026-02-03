# Common Docker Commands Cheatsheet

**Last Updated**: 2026-02-02

Comprehensive command reference for daily Docker development with MeepleAI.

---

## Table of Contents

1. [Service Management](#service-management)
2. [Logs & Debugging](#logs--debugging)
3. [Container Operations](#container-operations)
4. [Volume Management](#volume-management)
5. [Network Operations](#network-operations)
6. [Image Management](#image-management)
7. [Resource Monitoring](#resource-monitoring)
8. [Database Operations](#database-operations)
9. [Development Workflows](#development-workflows)
10. [Production Operations](#production-operations)

---

## Service Management

### Start/Stop Services

```bash
cd infra

# Start all services (default profile)
docker compose up -d

# Start with specific profile
docker compose --profile minimal up -d
docker compose --profile ai up -d
docker compose --profile observability up -d
docker compose --profile full up -d

# Start specific services
docker compose up -d postgres qdrant redis
docker compose up -d api web

# Start without detached mode (see logs in terminal)
docker compose up

# Start with rebuild
docker compose up -d --build

# Stop all services
docker compose stop

# Stop specific services
docker compose stop api web

# Stop and remove containers
docker compose down

# Stop and remove containers + volumes (⚠️ DATA LOSS!)
docker compose down -v

# Stop and remove everything (⚠️ COMPLETE RESET!)
docker compose down -v --rmi all --remove-orphans
```

### Restart Services

```bash
# Restart all
docker compose restart

# Restart specific service
docker compose restart api
docker compose restart web

# Restart multiple services
docker compose restart api web postgres

# Restart with force-recreate
docker compose up -d --force-recreate api
```

### Service Status

```bash
# List running services
docker compose ps

# List all services (including stopped)
docker compose ps -a

# Check service health
docker compose ps | grep "(healthy)"

# Show running containers
docker ps

# Show all containers (including stopped)
docker ps -a

# Filter by name
docker ps -a | grep meepleai

# Format output
docker compose ps --format "table {{.Name}}\t{{.Status}}\t{{.Ports}}"
```

---

## Logs & Debugging

### View Logs

```bash
# All services (follow mode)
docker compose logs -f

# Specific service (follow mode)
docker compose logs -f api
docker compose logs -f web

# Multiple services
docker compose logs -f api web postgres

# Last N lines
docker compose logs --tail=50 api
docker compose logs --tail=100 web

# With timestamps
docker compose logs -f --timestamps api

# Since specific time
docker compose logs --since 2024-01-01T10:00:00 api
docker compose logs --since 30m api  # Last 30 minutes
docker compose logs --since 2h api   # Last 2 hours

# Until specific time
docker compose logs --until 2024-01-01T12:00:00 api
```

### Save Logs to File

```bash
# Save all logs
docker compose logs > logs-all-$(date +%Y%m%d-%H%M%S).log

# Save specific service logs
docker compose logs api > api-logs.log
docker compose logs web > web-logs.log

# Save with timestamps
docker compose logs --timestamps > logs-with-time.log

# Save last 1000 lines
docker compose logs --tail=1000 > logs-recent.log
```

### Search Logs

```bash
# Using PowerShell
pwsh -c "docker compose logs api | Select-String 'ERROR'"
pwsh -c "docker compose logs web | Select-String 'Exception'"

# Using grep (Git Bash/Linux/Mac)
docker compose logs api | grep ERROR
docker compose logs web | grep -i exception

# Multiple patterns
docker compose logs api | grep -E "ERROR|WARNING|FATAL"

# Show context (3 lines before and after match)
docker compose logs api | grep -B 3 -A 3 "ERROR"
```

### Live Log Filtering

```bash
# Follow logs and filter
docker compose logs -f api | grep ERROR

# Multiple services with filter
docker compose logs -f api web | grep -i exception

# Colorize output (using ccze)
docker compose logs -f api | ccze -A
```

---

## Container Operations

### Execute Commands in Containers

```bash
# Run shell in container
docker compose exec api bash
docker compose exec web sh

# Run specific command
docker compose exec postgres psql -U postgres -d meepleai
docker compose exec redis redis-cli -a PASSWORD PING

# Run as specific user
docker compose exec -u root api bash

# Execute without TTY (for scripts)
docker compose exec -T postgres psql -U postgres -d meepleai < script.sql

# Run command in new container (one-off)
docker compose run --rm api dotnet --version
docker compose run --rm web npm --version
```

### Copy Files To/From Containers

```bash
# Copy FROM container TO host
docker compose cp api:/app/logs/app.log ./local-logs/
docker compose cp web:/app/.next ./next-build/

# Copy FROM host TO container
docker compose cp ./backup.sql postgres:/tmp/backup.sql
docker compose cp ./config.json api:/app/config.json

# Copy directory
docker compose cp api:/app/pdf_uploads ./pdf_backup/
```

### Container Inspection

```bash
# Inspect container configuration
docker compose inspect api

# Show container processes
docker compose top api

# Show resource usage
docker compose stats

# Show resource usage (no stream)
docker compose stats --no-stream

# Port mapping
docker compose port api 8080
docker compose port web 3000

# Network inspection
docker network inspect meepleai
```

---

## Volume Management

### List Volumes

```bash
# List all volumes
docker volume ls

# List MeepleAI volumes only
docker volume ls | grep meepleai
# Or PowerShell
pwsh -c "docker volume ls | Select-String 'meepleai'"

# Inspect volume
docker volume inspect meepleai_postgres_data
docker volume inspect meepleai_qdrant_data
```

### Backup Volumes

```bash
# Backup PostgreSQL volume
docker run --rm \
  -v meepleai_postgres_data:/data \
  -v $(pwd):/backup \
  alpine tar czf /backup/postgres-backup-$(date +%Y%m%d).tar.gz /data

# Backup Qdrant volume
docker run --rm \
  -v meepleai_qdrant_data:/data \
  -v $(pwd):/backup \
  alpine tar czf /backup/qdrant-backup-$(date +%Y%m%d).tar.gz /data

# PowerShell equivalent
pwsh -c "docker run --rm -v meepleai_postgres_data:/data -v ${PWD}:/backup alpine tar czf /backup/postgres-backup.tar.gz /data"
```

### Restore Volumes

```bash
# Restore PostgreSQL volume (⚠️ overwrites existing data)
docker run --rm \
  -v meepleai_postgres_data:/data \
  -v $(pwd):/backup \
  alpine sh -c "cd /data && tar xzf /backup/postgres-backup-20260202.tar.gz --strip 1"

# Restore Qdrant volume
docker run --rm \
  -v meepleai_qdrant_data:/data \
  -v $(pwd):/backup \
  alpine sh -c "cd /data && tar xzf /backup/qdrant-backup-20260202.tar.gz --strip 1"
```

### Clean Volumes

```bash
# Remove specific volume (⚠️ DATA LOSS!)
docker volume rm meepleai_postgres_data

# Remove all project volumes (⚠️ DATA LOSS!)
docker volume ls | grep meepleai | awk '{print $2}' | xargs docker volume rm

# Remove dangling volumes (not attached to any container)
docker volume prune

# Remove all unused volumes (⚠️ DANGEROUS!)
docker volume prune -a
```

---

## Network Operations

### Network Information

```bash
# List networks
docker network ls

# Inspect MeepleAI network
docker network inspect meepleai

# Show containers on network
docker network inspect meepleai --format='{{range .Containers}}{{.Name}} {{end}}'

# Container network settings
docker inspect meepleai-api --format='{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}'
```

### Network Troubleshooting

```bash
# Test connectivity between containers
docker compose exec api ping -c 3 postgres
docker compose exec api curl http://web:3000
docker compose exec web curl http://api:8080/health

# DNS resolution test
docker compose exec api nslookup postgres
docker compose exec web nslookup api

# Port connectivity test
docker compose exec api nc -zv postgres 5432
docker compose exec web nc -zv api 8080
```

### Network Cleanup

```bash
# Remove network
docker network rm meepleai

# Recreate network
docker compose down
docker compose up -d

# Prune unused networks
docker network prune
```

---

## Image Management

### List Images

```bash
# List all images
docker images

# List MeepleAI images
docker images | grep meepleai

# List with digests
docker images --digests

# List with size
docker images --format "table {{.Repository}}\t{{.Tag}}\t{{.Size}}"
```

### Build Images

⚠️ **IMPORTANT**: Services with `build:` are assigned to profiles. You must specify a profile to build them!

```bash
cd infra

# ❌ WRONG - Will show "No services to build" if no profile active
docker compose build --no-cache

# ✅ CORRECT - Build with profile
docker compose --profile minimal build --no-cache   # Builds: api, web
docker compose --profile ai build --no-cache        # Builds: api, web, AI services
docker compose --profile full build --no-cache      # Builds: everything

# Build specific service (works without profile)
docker compose build --no-cache api
docker compose build --no-cache web

# Build multiple specific services
docker compose build --no-cache api web embedding-service

# Build with pull (updates base images)
docker compose --profile minimal build --pull

# Build with parallel (faster)
docker compose --profile full build --parallel

# Build with BuildKit (faster, better caching)
DOCKER_BUILDKIT=1 docker compose --profile full build

# Build all services across all profiles (comprehensive)
docker compose --profile minimal --profile ai --profile full build --no-cache
```

**Services Requiring Build**:
- `api`, `web` → in profiles: minimal, dev, ai, observability, automation, full
- `embedding-service`, `reranker-service`, `unstructured-service`, `smoldocling-service` → in profiles: ai, full

### Pull Images

```bash
# Pull all images defined in compose
docker compose pull

# Pull specific service images
docker compose pull postgres
docker compose pull qdrant

# Pull specific image directly
docker pull postgres:16.4-alpine3.20
docker pull qdrant/qdrant:v1.12.4
```

### Clean Images

```bash
# Remove specific image
docker rmi meepleai-api
docker rmi meepleai-web

# Remove dangling images (untagged)
docker image prune

# Remove all unused images (⚠️ will re-download on next build)
docker image prune -a

# Remove specific images matching pattern
docker images | grep meepleai | awk '{print $3}' | xargs docker rmi
```

### Image Information

```bash
# Inspect image
docker inspect meepleai-api

# Show image history (layers)
docker history meepleai-api

# Show image size breakdown
docker history meepleai-api --no-trunc --format "table {{.CreatedBy}}\t{{.Size}}"
```

---

## Resource Monitoring

### Real-Time Stats

```bash
# All containers (live)
docker stats

# All containers (one-shot)
docker stats --no-stream

# Specific containers
docker stats meepleai-api meepleai-web

# Format output
docker stats --format "table {{.Container}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.NetIO}}"

# Using docker compose
docker compose stats
```

### Disk Usage

```bash
# Overall Docker disk usage
docker system df

# Detailed breakdown
docker system df -v

# Show volumes size
docker system df -v | grep -A 20 "Local Volumes"

# Show images size
docker system df -v | grep -A 20 "Images"
```

### Resource Limits

```bash
# Show container resource limits
docker inspect meepleai-api --format='{{.HostConfig.Memory}}'
docker inspect meepleai-api --format='{{.HostConfig.NanoCpus}}'

# Check if limits are enforced
docker inspect meepleai-api | grep -A 10 Resources
```

---

## Database Operations

### PostgreSQL

```bash
# Connect to database
docker compose exec postgres psql -U postgres -d meepleai

# Run SQL command
docker compose exec postgres psql -U postgres -d meepleai -c "SELECT COUNT(*) FROM games;"

# Run SQL file
docker compose exec -T postgres psql -U postgres -d meepleai < script.sql

# Backup database
docker compose exec postgres pg_dump -U postgres meepleai > backup-$(date +%Y%m%d).sql

# Restore database
cat backup-20260202.sql | docker compose exec -T postgres psql -U postgres -d meepleai

# List databases
docker compose exec postgres psql -U postgres -c "\l"

# List tables
docker compose exec postgres psql -U postgres -d meepleai -c "\dt"

# Show table schema
docker compose exec postgres psql -U postgres -d meepleai -c "\d games"

# Check database size
docker compose exec postgres psql -U postgres -d meepleai -c "SELECT pg_size_pretty(pg_database_size('meepleai'));"

# Vacuum database (optimize)
docker compose exec postgres psql -U postgres -d meepleai -c "VACUUM ANALYZE;"
```

### Redis

```bash
# Connect to Redis
REDIS_PASSWORD=$(pwsh -c "cat infra/secrets/redis.secret | Select-String 'REDIS_PASSWORD' | ForEach-Object { $_.Line.Split('=')[1] }")
docker compose exec redis redis-cli -a $REDIS_PASSWORD

# Once inside redis-cli:
> PING                    # Test connection
> INFO                    # Server info
> KEYS *                  # List all keys (⚠️ slow on large DBs)
> GET key_name            # Get value
> SET key_name value      # Set value
> DEL key_name            # Delete key
> FLUSHDB                 # Clear current database (⚠️ DATA LOSS!)
> FLUSHALL                # Clear all databases (⚠️ DATA LOSS!)

# One-liner commands
docker compose exec redis redis-cli -a $REDIS_PASSWORD PING
docker compose exec redis redis-cli -a $REDIS_PASSWORD INFO
docker compose exec redis redis-cli -a $REDIS_PASSWORD DBSIZE
```

### Qdrant

```bash
# List collections
curl http://localhost:6333/collections

# Get collection info
curl http://localhost:6333/collections/games

# Create collection (example)
curl -X PUT http://localhost:6333/collections/games \
  -H "Content-Type: application/json" \
  -d '{
    "vectors": {
      "size": 1024,
      "distance": "Cosine"
    }
  }'

# Delete collection (⚠️ DATA LOSS!)
curl -X DELETE http://localhost:6333/collections/games

# Search vectors (example)
curl -X POST http://localhost:6333/collections/games/points/search \
  -H "Content-Type: application/json" \
  -d '{
    "vector": [0.1, 0.2, ..., 0.9],
    "limit": 10
  }'

# Backup collection (export points)
curl http://localhost:6333/collections/games/points > qdrant-games-backup.json
```

---

## Development Workflows

### Hot Reload Development

```bash
# Terminal 1 - Infrastructure
cd infra
docker compose up -d postgres qdrant redis

# Terminal 2 - Backend (hot reload)
cd apps/api/src/Api
dotnet watch run  # Auto-reloads on code changes

# Terminal 3 - Frontend (hot reload)
cd apps/web
pnpm dev  # Auto-reloads on code changes

# Terminal 4 - Logs (optional)
cd infra
docker compose logs -f postgres qdrant redis
```

### Rebuild After Code Changes

```bash
# Backend changes
cd infra
docker compose build --no-cache api
docker compose up -d api

# Frontend changes
docker compose build --no-cache web
docker compose up -d web

# Both
docker compose build --no-cache api web
docker compose up -d api web
```

### Run Tests in Docker

```bash
# Backend tests (using Docker)
cd infra
docker compose run --rm api dotnet test

# Frontend tests
docker compose run --rm web pnpm test

# E2E tests
docker compose run --rm web pnpm test:e2e
```

### Database Migrations

```bash
# Create migration
cd apps/api/src/Api
dotnet ef migrations add MigrationName

# Apply migrations (local DB)
dotnet ef database update

# Apply migrations (Docker DB)
docker compose exec api dotnet ef database update --project /app

# List migrations
dotnet ef migrations list

# Rollback migration
dotnet ef database update PreviousMigrationName

# Generate SQL script
dotnet ef migrations script > migration.sql
```

---

## Production Operations

### Health Checks

```bash
# Check all services health
docker compose ps | grep "(healthy)"

# Monitor health continuously
watch -n 5 'docker compose ps | grep -E "(healthy|unhealthy)"'

# PowerShell equivalent
while ($true) { docker compose ps | Select-String "(healthy|unhealthy)"; Start-Sleep -Seconds 5; Clear-Host }
```

### Graceful Shutdown

```bash
# Stop with grace period (10s default)
docker compose stop

# Stop with custom grace period
docker compose stop -t 30

# Force kill (no grace period)
docker compose kill

# Stop and remove (graceful)
docker compose down

# Stop and remove (force)
docker compose kill && docker compose down
```

### Update Services

```bash
# Pull latest images
docker compose pull

# Rebuild and restart
docker compose build
docker compose up -d

# Rolling update (one service at a time)
docker compose up -d --no-deps --build api
docker compose up -d --no-deps --build web

# Scale service (if supported)
docker compose up -d --scale api=3
```

---

## System Cleanup

### Comprehensive Cleanup

```bash
# Clean stopped containers
docker container prune

# Clean dangling images
docker image prune

# Clean unused volumes
docker volume prune

# Clean unused networks
docker network prune

# Clean everything (⚠️ CAREFUL!)
docker system prune -a --volumes

# Clean with force (no confirmation)
docker system prune -a --volumes -f
```

### Reclaim Disk Space

```bash
# Check current usage
docker system df

# Remove unused everything
docker system prune -a --volumes

# Check space reclaimed
docker system df
```

---

## Troubleshooting Commands

### Container Not Starting

```bash
# Check logs
docker compose logs api --tail=100

# Inspect container
docker inspect meepleai-api

# Check exit code
docker inspect meepleai-api --format='{{.State.ExitCode}}'

# Try running manually
docker compose run --rm api bash
```

### Port Conflicts

```bash
# Find process using port (Windows)
netstat -ano | findstr :8080
taskkill /PID <PID> /F

# Find process using port (Linux/Mac)
lsof -i :8080
kill -9 <PID>

# Change port in docker-compose.yml
# "8081:8080" instead of "8080:8080"
```

### Permission Issues

```bash
# Fix volume permissions (Linux)
docker compose exec -u root api chown -R appuser:appuser /app

# Fix file permissions
docker compose exec -u root postgres chown -R postgres:postgres /var/lib/postgresql/data
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
| Start full | `docker compose --profile full up -d` |
| Stop all | `docker compose down` |
| View logs | `docker compose logs -f <service>` |
| Restart service | `docker compose restart <service>` |
| Rebuild service | `docker compose build --no-cache <service>` |
| Execute command | `docker compose exec <service> <command>` |
| Run shell | `docker compose exec <service> bash` |
| Check status | `docker compose ps` |
| Resource usage | `docker stats` |
| Clean volumes | `docker compose down -v` |
| Backup DB | `pg_dump > backup.sql` |
| Restore DB | `psql < backup.sql` |

---

## Additional Resources

- **Quick Start**: [quick-start.md](./quick-start.md)
- **Service Endpoints**: [service-endpoints.md](./service-endpoints.md)
- **Clean Builds**: [clean-builds.md](./clean-builds.md)
- **Troubleshooting**: [troubleshooting.md](./troubleshooting.md)
- **Docker Profiles**: [docker-profiles.md](./docker-profiles.md)

---

**Last Updated**: 2026-02-02
**Maintainer**: MeepleAI Development Team
