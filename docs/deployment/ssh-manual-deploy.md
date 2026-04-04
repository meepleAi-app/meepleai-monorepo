# Manual SSH Deploy to Staging

Quick reference for deploying to `meepleai.app` when GitHub Actions is unavailable.

## Prerequisites

- SSH key: `D:\Repositories\SSH Keys\meepleai-staging`
- Server: `deploy@204.168.135.69`
- Server path: `/opt/meepleai/repo/infra`

## Deploy Commands

### 1. Full Deploy (git pull + restart services)

```bash
SSH_KEY="D:/Repositories/SSH Keys/meepleai-staging"

ssh -i "$SSH_KEY" deploy@204.168.135.69 "cd /opt/meepleai/repo && git pull origin main-staging && cd infra && set -a && for f in secrets/*.secret; do source \"\$f\" 2>/dev/null; done && set +a && docker compose -f docker-compose.yml -f compose.traefik.yml -f compose.staging.yml -f compose.staging-traefik.yml --profile minimal up -d --no-deps api web"
```

### 2. Health Check

```bash
ssh -i "$SSH_KEY" deploy@204.168.135.69 "curl -s http://localhost:8080/health/live && echo '' && curl -s -o /dev/null -w '%{http_code}' https://meepleai.app"
```

### 3. View Logs

```bash
# API logs (last 50 lines)
ssh -i "$SSH_KEY" deploy@204.168.135.69 "docker logs meepleai-api --tail=50"

# Web logs
ssh -i "$SSH_KEY" deploy@204.168.135.69 "docker logs meepleai-web --tail=50"

# Follow live
ssh -i "$SSH_KEY" deploy@204.168.135.69 "docker logs -f meepleai-api"
```

### 4. Restart Single Service

```bash
ssh -i "$SSH_KEY" deploy@204.168.135.69 "cd /opt/meepleai/repo/infra && set -a && for f in secrets/*.secret; do source \"\$f\" 2>/dev/null; done && set +a && docker compose -f docker-compose.yml -f compose.traefik.yml -f compose.staging.yml -f compose.staging-traefik.yml --profile minimal restart api"
```

### 5. Check Running Containers

```bash
ssh -i "$SSH_KEY" deploy@204.168.135.69 "docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'"
```

### 6. Database Query

```bash
ssh -i "$SSH_KEY" deploy@204.168.135.69 "docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -c 'SELECT COUNT(*) FROM users;'"
```

## Compose File Chain

The staging environment uses 4 layered compose files:

| File | Purpose |
|------|---------|
| `docker-compose.yml` | Base services (postgres, redis, qdrant, api, web) |
| `compose.traefik.yml` | Traefik reverse proxy (image + base config) |
| `compose.staging.yml` | Staging overrides (DB names, env vars, monitoring) |
| `compose.staging-traefik.yml` | Staging TLS (Let's Encrypt for meepleai.app) |

## Important Notes

- Always source secrets before running `docker compose`
- Use `--no-deps` to avoid restarting dependent services
- The `--profile minimal` includes: postgres, qdrant, redis, api, web, traefik
- Traefik handles HTTPS termination — services listen on HTTP internally
