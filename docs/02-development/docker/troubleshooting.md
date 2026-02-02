# Docker Troubleshooting Guide

**Last Updated**: 2026-02-02

Solutions for common Docker issues with MeepleAI, focusing on port conflicts and memory/CPU problems.

---

## ⚡ Quick Fixes (Most Common Issues)

### "No services to build" Error

**Problem**: Running `docker compose build` shows "No services to build"

**Cause**: Services with `build:` are assigned to profiles. Without profile = not visible.

**Solution**:
```bash
# ❌ WRONG
docker compose build --no-cache
# Output: No services to build

# ✅ CORRECT - Specify profile
docker compose --profile minimal build --no-cache  # Builds: api, web
docker compose --profile ai build --no-cache       # Builds: all AI services too
docker compose --profile full build --no-cache     # Builds: everything

# Or build specific services (works without profile)
docker compose build --no-cache api web
```

### Port Already in Use

**Quick fix**:
```powershell
# Windows
netstat -ano | findstr :8080
taskkill /PID <PID> /F

# Then retry
docker compose --profile minimal up -d
```

### High Memory Usage

**Quick fix**:
```bash
# Stop heavy services
docker compose stop ollama smoldocling-service

# Or use lighter profile
docker compose down
docker compose --profile minimal up -d  # Uses ~4 GB instead of ~18 GB
```

---

## Table of Contents

1. [Port Conflicts](#port-conflicts)
2. [Memory & CPU Issues](#memory--cpu-issues)
3. [Service Won't Start](#service-wont-start)
4. [Network Problems](#network-problems)
5. [Database Issues](#database-issues)
6. [Volume & Permission Issues](#volume--permission-issues)
7. [Performance Problems](#performance-problems)
8. [General Debugging](#general-debugging)

---

## Port Conflicts

### Symptoms

- `Error starting userland proxy: listen tcp 0.0.0.0:8080: bind: address already in use`
- `ERROR: for meepleai-api Cannot start service api: Ports are not available`
- `docker compose up` fails immediately after starting

### Quick Fix

**Windows**:
```powershell
# Find process using port 8080
netstat -ano | findstr :8080
# Output: TCP 0.0.0.0:8080 0.0.0.0:0 LISTENING 12345

# Kill process by PID
taskkill /PID 12345 /F

# Verify port is free
netstat -ano | findstr :8080
# Should return nothing
```

**Linux/Mac**:
```bash
# Find process using port 8080
lsof -i :8080
# Or
sudo netstat -tulpn | grep :8080

# Kill process
kill -9 <PID>

# Verify port is free
lsof -i :8080
```

### Common Port Conflicts

| Port | Service | Common Conflict |
|------|---------|-----------------|
| **3000** | Web (Next.js) | React dev server, other Next.js apps |
| **8080** | API (.NET) | Jenkins, Tomcat, other APIs, test servers |
| **5432** | PostgreSQL | Local Postgres installation |
| **6379** | Redis | Local Redis installation |
| **6333** | Qdrant | Rare (custom services) |
| **3001** | Grafana | Other Grafana instances |
| **9090** | Prometheus | Other Prometheus instances |

### Solution 1: Stop Conflicting Process

```bash
# Windows - Common culprits
tasklist | findstr "node.exe"      # Node.js dev servers
tasklist | findstr "dotnet.exe"    # .NET apps
tasklist | findstr "postgres.exe"  # Local PostgreSQL
tasklist | findstr "redis-server"  # Local Redis

# Kill all Node processes (⚠️ closes ALL Node apps)
taskkill /F /IM node.exe

# Kill all .NET processes
taskkill /F /IM dotnet.exe

# Linux/Mac
ps aux | grep node
ps aux | grep dotnet
killall node
killall dotnet
```

### Solution 2: Change Docker Port Mapping

**Edit `docker-compose.yml`**:
```yaml
services:
  api:
    ports:
      - "8081:8080"  # Map host 8081 to container 8080
      # Or use alternative port:
      - "127.0.0.1:8081:8080"  # Bind to localhost only

  web:
    ports:
      - "3001:3000"  # Map host 3001 to container 3000
```

**Then update frontend configuration**:
```bash
# apps/web/.env.local
NEXT_PUBLIC_API_BASE=http://localhost:8081  # Match new port
```

### Solution 3: Use Dynamic Port Allocation

```yaml
# Let Docker choose available ports
services:
  api:
    ports:
      - "8080"  # Docker assigns random host port
```

**Find assigned port**:
```bash
docker compose port api 8080
# Output: 0.0.0.0:54321 (example random port)
```

### Prevention

```bash
# Check ports before starting Docker
netstat -ano | findstr ":3000 :8080 :5432 :6379"

# Or use PowerShell script
pwsh -c "3000,8080,5432,6379 | ForEach-Object { netstat -ano | Select-String \":$_\" }"

# Start only needed services to reduce conflicts
docker compose --profile minimal up -d  # Fewer services = fewer ports
```

---

## Memory & CPU Issues

### Symptoms

- Docker containers running slow
- Host system unresponsive
- Docker Desktop shows high CPU/RAM usage
- `docker stats` shows containers at memory limits
- Out of Memory (OOM) errors in logs
- Services randomly crashing

### Diagnosis

```bash
# Check current resource usage
docker stats

# Check specific services
docker stats meepleai-api meepleai-web meepleai-postgres

# One-time snapshot
docker stats --no-stream

# Check system-wide Docker resource usage
docker system df

# Identify memory-hungry services
docker stats --format "table {{.Container}}\t{{.MemUsage}}" --no-stream | sort -k2 -h
```

### Quick Fixes

#### 1. Increase Docker Desktop Resources

**Windows/Mac**:
1. Open Docker Desktop
2. Settings → Resources
3. Increase:
   - **Memory**: 8 GB → 16 GB (for full stack)
   - **CPU**: 4 cores → 8 cores
4. Apply & Restart

**Recommended allocations**:
- **Minimal profile**: 6 GB RAM, 4 CPU
- **AI profile**: 12 GB RAM, 6 CPU
- **Full profile**: 16 GB RAM, 8 CPU

#### 2. Use Lighter Profiles

```bash
# Instead of full stack
docker compose --profile full up -d

# Use minimal stack
docker compose --profile minimal up -d

# Or selective services
docker compose up -d postgres qdrant redis api web
# Skip: ollama, embedding-service, monitoring stack
```

#### 3. Stop Unused Services

```bash
# Stop AI services if not needed
docker compose stop ollama embedding-service reranker-service smoldocling-service unstructured-service

# Stop monitoring if not debugging
docker compose stop grafana prometheus alertmanager cadvisor node-exporter

# Stop automation if not needed
docker compose stop n8n
```

#### 4. Adjust Service Resource Limits

**Edit `docker-compose.yml`** (example for API):
```yaml
services:
  api:
    deploy:
      resources:
        limits:
          cpus: '1.0'     # Reduce from 2.0
          memory: 2G      # Reduce from 4G
        reservations:
          cpus: '0.5'     # Reduce from 1.0
          memory: 1G      # Reduce from 2G
```

**Apply changes**:
```bash
docker compose down
docker compose up -d
```

### Memory Leak Detection

```bash
# Monitor memory over time
watch -n 5 'docker stats --no-stream'

# Log memory usage
while true; do
  docker stats --no-stream --format "{{.Container}}: {{.MemUsage}}" >> memory-log.txt
  sleep 60
done

# Analyze logs
cat memory-log.txt | grep meepleai-api
```

### CPU Usage Spikes

**Identify CPU-intensive services**:
```bash
# Sort by CPU usage
docker stats --format "table {{.Container}}\t{{.CPUPerc}}" --no-stream | sort -k2 -h

# Monitor specific service
docker stats meepleai-api --no-stream
```

**Common CPU culprits**:
1. **SmolDocling**: Model inference (CPU mode)
   - Solution: Reduce `MAX_PAGES_PER_REQUEST` in docker-compose.yml
   - Or: Use GPU acceleration (if available)

2. **Embedding Service**: Large batch processing
   - Solution: Reduce batch sizes in API calls
   - Or: Disable if using OpenRouter embeddings

3. **Postgres**: Unoptimized queries
   - Solution: Add indexes, optimize queries
   - Check: `docker compose logs postgres | grep "duration:"`

4. **Ollama**: Model loading/inference
   - Solution: Use smaller models or disable Ollama
   - Or: Limit `OLLAMA_MAX_LOADED_MODELS`

### Out of Memory (OOM) Errors

**Symptoms in logs**:
```
docker compose logs api | grep -i "out of memory"
docker compose logs api | grep -i "OOM"
docker compose logs | grep -i "killed"
```

**Solutions**:
1. **Increase container memory limit**:
   ```yaml
   services:
     api:
       deploy:
         resources:
           limits:
             memory: 8G  # Increase from 4G
   ```

2. **Increase Docker Desktop memory**:
   - Settings → Resources → Memory → 16 GB

3. **Optimize application code**:
   - Reduce batch sizes for embeddings
   - Implement streaming for large responses
   - Clear caches periodically

4. **Use pagination**:
   - API queries should use `pageSize` limits
   - Avoid loading large datasets in memory

### Cleanup to Free Resources

```bash
# Stop all Docker services
docker compose down

# Clean up containers
docker container prune -f

# Clean up images (⚠️ will need to rebuild/pull)
docker image prune -a -f

# Clean up volumes (⚠️ DATA LOSS!)
docker volume prune -f

# Complete cleanup (⚠️ RESETS EVERYTHING!)
docker system prune -a --volumes -f

# Check reclaimed space
docker system df
```

---

## Service Won't Start

### Symptoms

- Service shows as "Restarting" or "Exited"
- Health check failing continuously
- Container exits immediately after starting

### Diagnosis

```bash
# Check service status
docker compose ps

# View logs (last 100 lines)
docker compose logs <service> --tail=100

# Follow logs in real-time
docker compose logs -f <service>

# Check container exit code
docker inspect meepleai-<service> --format='{{.State.ExitCode}}'
# Exit codes: 0=success, 1=error, 137=OOM killed, 139=segfault
```

### Common Causes & Fixes

#### 1. Missing Secrets

**Symptoms**: "Secret not found", "Environment variable required"

```bash
# Check if secrets exist
ls -la infra/secrets/*.secret

# Regenerate secrets
cd infra/secrets
pwsh setup-secrets.ps1 -SaveGenerated

# Restart service
cd ..
docker compose restart <service>
```

#### 2. Database Connection Failed

**Symptoms**: "Connection refused", "Can't connect to PostgreSQL"

```bash
# Check if PostgreSQL is running and healthy
docker compose ps postgres

# Check PostgreSQL logs
docker compose logs postgres --tail=50

# Test connection
docker compose exec postgres psql -U postgres -c "SELECT version();"

# If PostgreSQL not healthy, restart it
docker compose restart postgres

# Wait for health check
watch docker compose ps postgres
```

#### 3. Port Already in Use

**Solution**: See [Port Conflicts](#port-conflicts) section above

#### 4. Volume Permission Issues

**Symptoms**: "Permission denied", "Cannot write to directory"

```bash
# Linux/Mac - Fix volume ownership
docker compose exec -u root <service> chown -R appuser:appuser /app

# Or rebuild with correct permissions
docker compose down -v
docker compose up -d
```

#### 5. Health Check Timeout

**Symptoms**: Service marked as "unhealthy"

```bash
# Check health check configuration
docker inspect meepleai-<service> | grep -A 10 Healthcheck

# Increase timeout in docker-compose.yml
services:
  api:
    healthcheck:
      timeout: 10s      # Increase from 5s
      start_period: 60s  # Increase from 20s

# Apply changes
docker compose down
docker compose up -d
```

---

## Network Problems

### Symptoms

- Services can't communicate with each other
- "Connection refused" between containers
- DNS resolution fails
- Network timeouts

### Diagnosis

```bash
# Check network exists
docker network ls | grep meepleai

# Inspect network
docker network inspect meepleai

# Test DNS resolution
docker compose exec api nslookup postgres
docker compose exec web nslookup api

# Test connectivity (ping)
docker compose exec api ping -c 3 postgres
docker compose exec web ping -c 3 api

# Test port connectivity
docker compose exec api nc -zv postgres 5432
docker compose exec web nc -zv api 8080
```

### Solutions

#### 1. Recreate Network

```bash
# Stop services
docker compose down

# Remove network
docker network rm meepleai

# Restart (recreates network)
docker compose up -d
```

#### 2. Check Service Names

**Correct container-to-container communication**:
- Use service name: `http://api:8080` (not `localhost:8080`)
- Use service name: `postgres:5432` (not `localhost:5432`)

**Example fix in appsettings.json**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres;Port=5432;..."  // Not "Host=localhost"
  }
}
```

#### 3. Verify Network Mode

```bash
# Check if services are on same network
docker inspect meepleai-api --format='{{range .NetworkSettings.Networks}}{{.NetworkID}}{{end}}'
docker inspect meepleai-web --format='{{range .NetworkSettings.Networks}}{{.NetworkID}}{{end}}'

# Should return same network ID
```

---

## Database Issues

### PostgreSQL Won't Start

```bash
# Check logs
docker compose logs postgres --tail=100

# Common issues:
# 1. Data corruption - requires volume reset (⚠️ DATA LOSS!)
docker compose stop postgres
docker volume rm meepleai_postgres_data
docker compose up -d postgres

# 2. Port conflict (5432)
netstat -ano | findstr :5432
# Kill conflicting process or change port
```

### Redis Connection Issues

```bash
# Test connection
REDIS_PASSWORD=$(pwsh -c "cat infra/secrets/redis.secret | Select-String 'REDIS_PASSWORD' | ForEach-Object { $_.Line.Split('=')[1] }")
docker compose exec redis redis-cli -a $REDIS_PASSWORD PING

# If fails, check logs
docker compose logs redis --tail=50

# Restart Redis
docker compose restart redis
```

### Qdrant Not Accessible

```bash
# Test endpoint
curl http://localhost:6333/collections

# Check logs
docker compose logs qdrant --tail=50

# Restart Qdrant
docker compose restart qdrant
```

---

## Volume & Permission Issues

### Volume Won't Delete

```bash
# Error: volume is in use

# Force stop and remove
docker compose down
docker volume rm -f meepleai_postgres_data

# If still fails, check what's using it
docker ps -a --filter volume=meepleai_postgres_data
```

### Permission Denied Errors

```bash
# Linux/Mac - Fix ownership
docker compose exec -u root api chown -R $(id -u):$(id -g) /app

# Windows - Not usually an issue due to volume mounting
```

---

## Performance Problems

### Slow Startup

**Causes**:
1. Model downloads (SmolDocling, Ollama) - first time only
2. Many services starting simultaneously
3. Limited system resources

**Solutions**:
```bash
# Start services in stages
docker compose up -d postgres qdrant redis
sleep 10
docker compose up -d api
sleep 5
docker compose up -d web

# Disable model warmup (faster startup, slower first request)
# In docker-compose.yml:
# ENABLE_MODEL_WARMUP=false

# Use minimal profile for faster startup
docker compose --profile minimal up -d
```

### Slow API Responses

**Diagnosis**:
```bash
# Check API logs for slow queries
docker compose logs api | grep "duration:"

# Check database performance
docker compose exec postgres psql -U postgres -d meepleai -c "SELECT * FROM pg_stat_activity WHERE state = 'active';"

# Monitor resource usage
docker stats meepleai-api meepleai-postgres
```

**Solutions**:
- Add database indexes
- Optimize queries
- Increase container resources
- Enable Redis caching

---

## General Debugging

### Enable Verbose Logging

```yaml
# docker-compose.yml
services:
  api:
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      Logging__LogLevel__Default: Debug  # More verbose

  embedding-service:
    environment:
      LOG_LEVEL: DEBUG  # More verbose
```

### Interactive Container Shell

```bash
# Enter container for debugging
docker compose exec api bash

# Once inside:
cd /app
ls -la
cat appsettings.json
curl http://localhost:8080/health
exit
```

### Compare Working vs Broken States

```bash
# Save current config
docker compose config > current-config.yml

# Save logs
docker compose logs > current-logs.txt

# Save stats
docker stats --no-stream > current-stats.txt

# Compare with working backup
```

### Reset to Known Good State

```bash
# 1. Backup current state
docker compose logs > logs-before-reset.txt

# 2. Full clean (⚠️ DATA LOSS!)
docker compose down -v

# 3. Reset to main branch
git stash
git checkout main-dev
git pull

# 4. Regenerate secrets
cd infra/secrets
pwsh setup-secrets.ps1 -SaveGenerated

# 5. Fresh start
cd ..
docker compose --profile minimal up -d
```

---

## Emergency Procedures

### Complete Docker Reset

⚠️ **WARNING**: Destroys all Docker data (all projects, not just MeepleAI)

```bash
# Stop all Docker processes
docker compose down

# Stop Docker Desktop
# Windows: Exit from system tray
# Mac: Quit Docker Desktop

# Remove Docker data directory (Windows)
# C:\Users\<User>\AppData\Local\Docker

# Remove Docker data directory (Mac)
# ~/Library/Containers/com.docker.docker/Data

# Restart Docker Desktop

# Rebuild MeepleAI
cd infra
docker compose --profile minimal up -d
```

### Get Help

1. **Check logs first**: `docker compose logs <service> --tail=200`
2. **Search GitHub Issues**: Check if others have same problem
3. **Create Issue**: Include logs, docker-compose.yml, system info
4. **Ask in Discord/Slack**: MeepleAI community channels

---

## Diagnostic Checklist

When troubleshooting, work through this checklist:

- [ ] Check `docker compose ps` - Are services running?
- [ ] Check `docker compose logs <service>` - Any errors?
- [ ] Check `docker stats` - Memory/CPU issues?
- [ ] Check `netstat -ano | findstr :<port>` - Port conflicts?
- [ ] Check `docker system df` - Disk space available?
- [ ] Check `docker volume ls` - Volumes exist?
- [ ] Check `docker network ls` - Network exists?
- [ ] Test connectivity: `docker compose exec api ping postgres`
- [ ] Verify secrets: `ls infra/secrets/*.secret`
- [ ] Try restart: `docker compose restart <service>`
- [ ] Try rebuild: `docker compose build --no-cache <service>`
- [ ] Try clean: `docker compose down && docker compose up -d`

---

## Additional Resources

- **Quick Start**: [quick-start.md](./quick-start.md)
- **Service Endpoints**: [service-endpoints.md](./service-endpoints.md)
- **Clean Builds**: [clean-builds.md](./clean-builds.md)
- **Common Commands**: [common-commands.md](./common-commands.md)
- **Docker Documentation**: https://docs.docker.com/
- **Docker Compose Reference**: https://docs.docker.com/compose/compose-file/

---

**Last Updated**: 2026-02-02
**Maintainer**: MeepleAI Development Team
