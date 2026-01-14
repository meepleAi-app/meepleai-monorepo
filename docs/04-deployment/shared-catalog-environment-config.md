# SharedGameCatalog Environment Configuration

**Issue**: #2427 (Parent: #2374 Phase 5)
**Date**: 2026-01-14

---

## Required Environment Variables

### Database (PostgreSQL)
```bash
CONNECTIONSTRINGS__POSTGRES="Host=localhost;Port=5432;Database=meepleai;Username=postgres;Password=***"
```
- **Used for**: All SharedGameCatalog persistence (games, categories, mechanics, FAQs, errata)
- **Required indexes**: Run `scripts/db/apply-shared-catalog-indexes.sh` after first deploy
- **Validation**: Health check at GET /health (postgres + shared-catalog-fts)

### Cache (Redis)
```bash
CONNECTIONSTRINGS__REDIS="localhost:6379,abortConnect=false"
```
- **Used for**: HybridCache L2 (search results, game details, taxonomy)
- **Target hit rate**: > 80%
- **Monitoring**: Prometheus `meepleai_cache_hits_total`, `meepleai_cache_misses_total`

---

## Optional Environment Variables

### Cache Expiration (Tuning)
```bash
# Categories cache (default: 24h = 86400s)
CACHE_EXPIRATION_CATEGORIES=86400

# Mechanics cache (default: 24h = 86400s)
CACHE_EXPIRATION_MECHANICS=86400

# Search results cache L2 (default: 1h = 3600s)
CACHE_EXPIRATION_SEARCH=3600

# Game details cache L1 Memory (default: 30min = 1800s)
CACHE_EXPIRATION_GETBYID_L1=1800

# Game details cache L2 Redis (default: 2h = 7200s)
CACHE_EXPIRATION_GETBYID_L2=7200
```

**When to tune**:
- **Increase** if cache hit rate < 70% (stale data acceptable)
- **Decrease** if data freshness critical (catalog updated frequently)

### BGG Integration
```bash
# BGG API URL (default: https://boardgamegeek.com/xmlapi2)
BGG_API_URL=https://boardgamegeek.com/xmlapi2

# BGG request timeout (default: 30s)
BGG_API_TIMEOUT_SECONDS=30
```

**Rate limiting**: BGG has no official rate limit but throttles aggressive usage. Implement exponential backoff if bulk imports fail.

---

## Docker Compose Configuration

### Local Development
```yaml
# infra/docker-compose.yml
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-dev-password}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    ports:
      - "5432:5432"

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
```

### Production (via Environment Variables)
```bash
# Set in deployment platform (Railway, Render, Fly.io, etc.)
CONNECTIONSTRINGS__POSTGRES="postgresql://..."  # Managed PostgreSQL
CONNECTIONSTRINGS__REDIS="redis://..."          # Managed Redis
```

---

## Pre-Deployment Checklist

### 1. Database Preparation
```bash
# Apply migrations
cd apps/api/src/Api
dotnet ef database update

# Verify indexes created
psql -c "SELECT indexname FROM pg_indexes WHERE tablename = 'shared_games';"

# Expected: 13 indexes (3 existing + 10 from migration)
```

### 2. Seed Initial Data
```bash
# Categories (Strategy, Family, Party, Abstract, Thematic, etc.)
# Mechanics (Deck Building, Worker Placement, Dice Rolling, etc.)
# 100+ games via bulk import from BGG

POST /api/v1/admin/shared-games/bulk-import
{
  "bggIds": [13, 68448, 169786, ...]  # Top 100 games
}
```

### 3. Performance Validation
```bash
# Run k6 load test
cd tests/k6
k6 run shared-catalog-load-test.js

# Expected:
# - http_req_duration p(95) < 200ms ✓
# - shared_catalog_cache_hit_rate > 0.80 ✓
# - http_req_failed rate < 0.01 ✓
```

### 4. Health Check Verification
```bash
curl http://localhost:8080/health | jq '.entries."shared-catalog-fts"'

# Expected:
# {
#   "status": "Healthy",
#   "description": "SharedGameCatalog FTS operational. Latency: <200ms",
#   "data": {
#     "fts_latency_ms": <200,
#     "performance_status": "optimal"
#   }
# }
```

### 5. Prometheus Metrics Validation
```bash
curl http://localhost:8080/metrics | grep meepleai_cache

# Expected metrics:
# meepleai_cache_hits_total{operation="search",cache_type="shared_games"}
# meepleai_cache_misses_total{operation="search",cache_type="shared_games"}
```

---

## Rollback Plan

### If Deployment Fails
```bash
# 1. Rollback database migration
cd apps/api/src/Api
dotnet ef database update 20260113212945  # Previous migration

# 2. Restart application (loads previous code)
docker compose restart api

# 3. Verify rollback
curl http://localhost:8080/health
```

### If Performance Degrades
```sql
-- Check index usage
SELECT schemaname, tablename, indexname, idx_scan
FROM pg_stat_user_indexes
WHERE tablename = 'shared_games'
ORDER BY idx_scan DESC;

-- If ix_shared_games_fts has low scans, rebuild
REINDEX INDEX ix_shared_games_fts;

-- If performance still poor, run maintenance
VACUUM ANALYZE shared_games;
```

---

## Monitoring Setup

### Grafana Dashboard Import
```bash
# 1. Open Grafana: http://localhost:3000
# 2. Navigate to Dashboards → Import
# 3. Upload: infra/monitoring/grafana/dashboards/shared-catalog-performance.json
# 4. Verify panels load (requires Prometheus datasource)
```

### Alert Configuration
Alerts configured in dashboard JSON:
- **P95 > 200ms**: Warning (notify dev team)
- **Cache < 80%**: Warning (investigate cache misses)

---

## Security Checklist

### Secrets Management
```bash
# Run detect-secrets scan
cd D:\Repositories\meepleai-monorepo-frontend
detect-secrets scan --baseline .secrets.baseline

# Expected: No new secrets detected
# If secrets found: Review and add to .gitignore or encrypt
```

### CORS Origins
```json
// appsettings.Production.json
{
  "Cors": {
    "AllowedOrigins": [
      "https://meepleai.app",
      "https://www.meepleai.app"
    ]
  }
}
```

### Rate Limiting Verification
```bash
# Test rate limiting
for i in {1..350}; do
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/api/v1/shared-games
done

# Expected: First 300 return 200, then 429 (rate limited)
```

---

## Production Readiness Matrix

| Category | Criteria | Status | Evidence |
|----------|----------|--------|----------|
| **Performance** | P95 < 200ms | ✅ | k6 load test passed |
| **Performance** | Cache > 80% | ✅ | Prometheus metrics |
| **Security** | Rate limiting | ✅ | 300 req/min public, 100 admin |
| **Security** | Authorization | ✅ | 21 protected endpoints |
| **Security** | Input validation | ✅ | FluentValidation audit passed |
| **Security** | No secrets | ✅ | detect-secrets scan |
| **Monitoring** | Health checks | ✅ | /health operational |
| **Monitoring** | Prometheus | ✅ | /metrics exposed |
| **Monitoring** | Grafana | ✅ | Dashboard created |
| **Monitoring** | Alerts | ✅ | P95 + cache alerts |
| **Documentation** | ADRs | ✅ | 3 ADRs complete |
| **Documentation** | README | ✅ | API surface documented |
| **Documentation** | OpenAPI | ✅ | Scalar UI complete |
| **Quality** | Tests passing | ⏳ | Run before deploy |
| **Quality** | Zero warnings | ⏳ | dotnet build check |
| **Stakeholder** | Approval | ⏳ | Product owner + QA sign-off |

---

## Deployment Commands

### Railway / Render / Fly.io
```bash
# Build and deploy via platform CLI
railway up  # or: render deploy, fly deploy

# Verify deployment
curl https://api.meepleai.app/health
```

### Docker Compose (Self-Hosted)
```bash
# Build production image
docker compose -f docker-compose.prod.yml build api

# Run migrations (one-time)
docker compose -f docker-compose.prod.yml run --rm api \
  dotnet ef database update

# Start services
docker compose -f docker-compose.prod.yml up -d

# Check logs
docker compose -f docker-compose.prod.yml logs -f api
```

---

## Post-Deployment Validation

### Functional Tests
- [ ] Search "strategia" returns results
- [ ] Category filter works (Strategy, Family, etc.)
- [ ] Game details page loads
- [ ] Admin can create game
- [ ] Editor can request deletion
- [ ] Admin can approve deletion

### Performance Tests
- [ ] Grafana dashboard shows P95 < 200ms
- [ ] Cache hit rate > 80%
- [ ] Health check returns Healthy status

### Security Tests
- [ ] Unauthenticated user cannot access /admin/* (403)
- [ ] Rate limiting triggers after 300 requests (429)
- [ ] CORS allows only configured origins

---

**Last Updated**: 2026-01-14
**Contact**: MeepleAI DevOps Team
