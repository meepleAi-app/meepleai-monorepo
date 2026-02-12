# Epic #4068: Deployment Guide

**Production deployment strategy, rollout plan, monitoring setup**

---

## Pre-Deployment Checklist

**Code Quality**:
- [ ] All 10 issues merged to main-dev
- [ ] CI/CD pipeline passing (all tests green)
- [ ] Code review approved (2+ approvers)
- [ ] No TypeScript errors (`pnpm typecheck`)
- [ ] No ESLint errors (`pnpm lint`)
- [ ] Backend builds (`dotnet build`)
- [ ] Frontend builds (`pnpm build`)

**Testing**:
- [ ] Backend coverage ≥90% (`dotnet test /p:CollectCoverage=true`)
- [ ] Frontend coverage ≥85% (`pnpm test:coverage`)
- [ ] E2E tests passing (`pnpm test:e2e`)
- [ ] Accessibility audit clean (0 axe violations)
- [ ] Performance benchmarks met (tooltip <16ms, render <100ms)
- [ ] Visual regression approved (Chromatic)

**Database**:
- [ ] Migration script reviewed (`dotnet ef migrations list`)
- [ ] Migration tested on staging
- [ ] Rollback script prepared
- [ ] Backward compatible (old clients work during deploy)

**Documentation**:
- [ ] CHANGELOG.md updated
- [ ] API docs updated (Scalar)
- [ ] Component docs updated
- [ ] Migration guide reviewed

**Security**:
- [ ] No secrets committed
- [ ] Environment variables configured
- [ ] Permission endpoints rate-limited
- [ ] HTTPS enforced
- [ ] CORS configured correctly

---

## Deployment Strategy: Blue-Green

**Architecture**:
```
                   Load Balancer
                        |
          +-------------+-------------+
          |                           |
    Blue (Current)              Green (New)
    v1.4 (no Epic)              v1.5 (Epic #4068)
```

**Steps**:

1. **Deploy Green** (new version with Epic #4068)
   ```bash
   # Build Docker images
   docker build -t meepleai-api:v1.5-epic4068 apps/api
   docker build -t meepleai-web:v1.5-epic4068 apps/web

   # Tag as green
   docker tag meepleai-api:v1.5-epic4068 meepleai-api:green
   docker tag meepleai-web:v1.5-epic4068 meepleai-web:green

   # Deploy to green environment
   docker compose -f docker-compose.green.yml up -d
   ```

2. **Smoke Test Green**
   ```bash
   # Health check
   curl https://green.meepleai.com/health
   # Expected: 200 OK

   # Permission endpoint
   curl -H "Authorization: Bearer $TOKEN" https://green.meepleai.com/api/v1/permissions/me
   # Expected: 200 OK, JSON response

   # Frontend loads
   curl https://green-web.meepleai.com
   # Expected: 200 OK, HTML
   ```

3. **Apply Database Migration** (before switching traffic)
   ```bash
   # Connect to production database
   docker exec -it meepleai-api-green dotnet ef database update
   # Applies AddUserAccountStatus migration
   # Adds Status column (backward compatible)
   ```

4. **Switch Load Balancer** (gradually)
   ```nginx
   # nginx.conf: Canary rollout (10% traffic to green)
   upstream meepleai_api {
       server blue:8080 weight=9;
       server green:8080 weight=1;
   }

   # Monitor for 10 minutes, check metrics
   # If OK, increase to 50%
   upstream meepleai_api {
       server blue:8080 weight=1;
       server green:8080 weight=1;
   }

   # Monitor for 30 minutes
   # If OK, switch 100% to green
   upstream meepleai_api {
       server green:8080;
   }
   ```

5. **Monitor Green** (1 hour)
   - Error rate < 1%
   - Permission API p95 latency < 100ms
   - No critical bugs reported
   - User feedback positive

6. **Decommission Blue** (after 24 hours stable)
   ```bash
   docker compose -f docker-compose.blue.yml down
   docker tag meepleai-api:green meepleai-api:latest
   docker tag meepleai-web:green meepleai-web:latest
   ```

**Rollback** (if issues):
```bash
# Instant rollback: Switch load balancer back to blue
upstream meepleai_api {
    server blue:8080;
}

# Blue still running (not decommissioned), instant switch
```

---

## Database Migration Deployment

### Migration Script Review

```bash
# Review migration before applying
cd apps/api/src/Api
dotnet ef migrations script AddUserAccountStatus --output migration.sql

# Review SQL
cat migration.sql
```

**Expected SQL**:
```sql
-- Add Status column (default: Active = 0)
ALTER TABLE "Users" ADD COLUMN "Status" integer NOT NULL DEFAULT 0;

-- Create index for performance
CREATE INDEX "IX_Users_Status" ON "Users" ("Status");

-- Composite index for permission queries
CREATE INDEX "IX_Users_Tier_Role_Status" ON "Users" ("Tier", "Role", "Status");
```

**Safety Checks**:
- ✅ `DEFAULT 0` ensures existing rows get Active status
- ✅ `NOT NULL` enforced (all rows will have value)
- ✅ Indexes created for performance
- ✅ No destructive changes (no DROP, no DELETE)

### Apply Migration (Production)

```bash
# Backup database first
docker exec meepleai-postgres pg_dump -U postgres meepleai > backup-before-epic4068.sql

# Apply migration
cd apps/api/src/Api
dotnet ef database update --connection "$PROD_CONNECTION_STRING"

# Verify columns added
docker exec meepleai-postgres psql -U postgres -d meepleai -c "\\d \"Users\""
# Expected: Status column present
```

**Rollback Migration** (if needed):
```bash
# Revert to previous migration
dotnet ef database update PreviousMigrationName --connection "$PROD_CONNECTION_STRING"

# Or manual SQL
docker exec -it meepleai-postgres psql -U postgres -d meepleai
DROP INDEX IF EXISTS "IX_Users_Tier_Role_Status";
DROP INDEX IF EXISTS "IX_Users_Status";
ALTER TABLE "Users" DROP COLUMN IF EXISTS "Status";
```

---

## Environment Configuration

### Backend Environment Variables

```bash
# .env.production
DATABASE_URL=postgres://user:pass@db.example.com:5432/meepleai
REDIS_URL=redis://cache.example.com:6379
JWT_SECRET=<256-bit-secret>
JWT_EXPIRATION_MINUTES=15
CORS_ORIGINS=https://app.meepleai.com,https://admin.meepleai.com

# Epic #4068: Permission system
PERMISSION_CACHE_DURATION_MINUTES=5
PERMISSION_RATE_LIMIT_PER_MINUTE=100

# Feature flags (gradual rollout)
FEATURE_FLAG_EPIC_4068_ENABLED=true
FEATURE_FLAG_EPIC_4068_ROLLOUT_PERCENTAGE=10 # Start with 10% of users
```

### Frontend Environment Variables

```bash
# .env.production
NEXT_PUBLIC_API_URL=https://api.meepleai.com
NEXT_PUBLIC_WS_URL=wss://api.meepleai.com
NEXT_PUBLIC_ENVIRONMENT=production

# Epic #4068
NEXT_PUBLIC_PERMISSION_CACHE_TIME_MS=300000 # 5 minutes
NEXT_PUBLIC_PERMISSION_STALE_TIME_MS=300000

# Feature flags
NEXT_PUBLIC_EPIC_4068_ENABLED=true
NEXT_PUBLIC_TAGS_ENABLED=true
NEXT_PUBLIC_AGENT_METADATA_ENABLED=true
```

---

## Docker Configuration

### Dockerfile.api (Backend)

```dockerfile
# apps/api/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY apps/api/src/Api/Api.csproj .
RUN dotnet restore

# Copy source and build
COPY apps/api/src/Api/ .
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

# Epic #4068: Set environment for permission system
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PERMISSION_REGISTRY_CACHE_ENABLED=true

EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
```

### Dockerfile.web (Frontend)

```dockerfile
# apps/web/Dockerfile
FROM node:20-alpine AS base
RUN npm install -g pnpm

FROM base AS deps
WORKDIR /app
COPY apps/web/package.json apps/web/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

FROM base AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY apps/web/ .
RUN pnpm build

FROM base AS runner
WORKDIR /app
ENV NODE_ENV=production

# Epic #4068: Permission system config
ENV NEXT_PUBLIC_EPIC_4068_ENABLED=true

COPY --from=builder /app/public ./public
COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static ./.next/static

EXPOSE 3000
CMD ["node", "server.js"]
```

### docker-compose.production.yml

```yaml
version: '3.9'

services:
  api:
    image: meepleai-api:v1.5-epic4068
    environment:
      - DATABASE_URL=${DATABASE_URL}
      - REDIS_URL=${REDIS_URL}
      - JWT_SECRET=${JWT_SECRET}
      - PERMISSION_CACHE_DURATION_MINUTES=5
    ports:
      - "8080:8080"
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

  web:
    image: meepleai-web:v1.5-epic4068
    environment:
      - NEXT_PUBLIC_API_URL=https://api.meepleai.com
      - NEXT_PUBLIC_EPIC_4068_ENABLED=true
    ports:
      - "3000:3000"
    restart: unless-stopped
    depends_on:
      api:
        condition: service_healthy

  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: meepleai
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - pgdata:/var/lib/postgresql/data
    restart: unless-stopped

  redis:
    image: redis:7-alpine
    command: redis-server --requirepass ${REDIS_PASSWORD}
    restart: unless-stopped

volumes:
  pgdata:
```

---

## Kubernetes Deployment (Optional)

### Deployment Manifest

```yaml
# k8s/epic-4068/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: meepleai-api-epic4068
  labels:
    app: meepleai-api
    version: v1.5
    epic: "4068"
spec:
  replicas: 3
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  selector:
    matchLabels:
      app: meepleai-api
      version: v1.5
  template:
    metadata:
      labels:
        app: meepleai-api
        version: v1.5
        epic: "4068"
    spec:
      containers:
      - name: api
        image: ghcr.io/meepleai/api:v1.5-epic4068
        ports:
        - containerPort: 8080
        env:
        - name: DATABASE_URL
          valueFrom:
            secretKeyRef:
              name: meepleai-secrets
              key: database-url
        - name: PERMISSION_CACHE_DURATION_MINUTES
          value: "5"
        resources:
          requests:
            cpu: 500m
            memory: 512Mi
          limits:
            cpu: 1000m
            memory: 1Gi
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
```

---

## Monitoring Setup

### Prometheus Metrics

```yaml
# prometheus/epic-4068-alerts.yml
groups:
  - name: epic-4068-alerts
    interval: 30s
    rules:
      # Permission API latency
      - alert: PermissionAPISlowResponses
        expr: histogram_quantile(0.95, rate(http_request_duration_seconds_bucket{endpoint="/api/v1/permissions/me"}[5m])) > 0.1
        for: 5m
        labels:
          severity: warning
          epic: "4068"
        annotations:
          summary: "Permission API p95 latency > 100ms"
          description: "Permission endpoint responding slowly (p95: {{ $value | humanizeDuration }})"

      # Permission denied rate (possible attack)
      - alert: HighPermissionDenialRate
        expr: rate(permission_denied_total[5m]) / rate(permission_check_total[5m]) > 0.1
        for: 10m
        labels:
          severity: critical
          epic: "4068"
        annotations:
          summary: "High permission denial rate (>10%)"
          description: "{{ $value | humanizePercentage }} of permission checks denied - possible attack or misconfiguration"

      # Permission cache hit rate low
      - alert: LowPermissionCacheHitRate
        expr: rate(permission_cache_hit_total[5m]) / rate(permission_check_total[5m]) < 0.8
        for: 15m
        labels:
          severity: warning
        annotations:
          summary: "Permission cache hit rate < 80%"
          description: "Cache not effective, check staleTime configuration"

      # Tooltip positioning slow
      - alert: SlowTooltipPositioning
        expr: histogram_quantile(0.95, rate(tooltip_position_duration_ms_bucket[5m])) > 16
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Tooltip positioning slow (p95 > 16ms)"
          description: "Tooltip positioning not meeting 60fps target"
```

### Grafana Dashboard

```json
{
  "dashboard": {
    "title": "Epic #4068: Permission System & MeepleCard Enhancements",
    "panels": [
      {
        "title": "Permission API Request Rate",
        "targets": [
          { "expr": "rate(http_requests_total{endpoint=~'/api/v1/permissions/.*'}[5m])" }
        ]
      },
      {
        "title": "Permission Denied Rate",
        "targets": [
          { "expr": "rate(permission_denied_total[5m]) / rate(permission_check_total[5m])" }
        ],
        "thresholds": [
          { "value": 0.05, "color": "green" },
          { "value": 0.1, "color": "red" }
        ]
      },
      {
        "title": "Top Denied Features",
        "targets": [
          { "expr": "topk(10, sum by (feature) (rate(permission_denied_total[1h])))" }
        ],
        "type": "table"
      },
      {
        "title": "User Tier Distribution",
        "targets": [
          { "expr": "count by (tier) (user_tier_gauge)" }
        ],
        "type": "pie"
      },
      {
        "title": "Permission Cache Hit Rate",
        "targets": [
          { "expr": "rate(permission_cache_hit_total[5m]) / rate(permission_check_total[5m])" }
        ]
      },
      {
        "title": "Tooltip Positioning Performance (p95)",
        "targets": [
          { "expr": "histogram_quantile(0.95, rate(tooltip_position_duration_ms_bucket[5m]))" }
        ],
        "thresholds": [{ "value": 16, "color": "red" }]
      },
      {
        "title": "MeepleCard Render Time (p95)",
        "targets": [
          { "expr": "histogram_quantile(0.95, rate(meeplecard_render_duration_ms_bucket[5m]))" }
        ]
      },
      {
        "title": "Tag Rendering Performance",
        "targets": [
          { "expr": "rate(tag_strip_render_duration_ms_sum[5m]) / rate(tag_strip_render_duration_ms_count[5m])" }
        ]
      }
    ]
  }
}
```

---

## Gradual Rollout Plan

### Week 1: Internal (Admins Only)

```csharp
// Feature flag: Enable only for admins
public bool IsEpic4068Enabled(User user)
{
    return user.Role.IsAdmin() || user.Email.EndsWith("@meepleai.com");
}

// Conditional feature rendering
if (IsEpic4068Enabled(currentUser))
{
    // Show Epic #4068 features
}
else
{
    // Show legacy UI
}
```

**Monitoring**: Watch for errors, collect admin feedback

---

### Week 2: Beta Users (10%)

```csharp
// Feature flag: Gradual rollout
public bool IsEpic4068Enabled(User user)
{
    if (user.Role.IsAdmin()) return true;
    if (user.IsBetaTester) return true;

    // 10% random rollout
    return user.Id.GetHashCode() % 10 == 0;
}
```

**Metrics to Watch**:
- Error rate (alert if > 1%)
- Permission API latency (alert if p95 > 100ms)
- User feedback (track via support tickets)
- Tier upgrade conversion (compare to baseline)

---

### Week 3: General Availability (100%)

```csharp
// Remove feature flag
public bool IsEpic4068Enabled(User user) => true;

// Or remove conditional entirely
// Always show Epic #4068 features
```

**Announcement**:
- Blog post: "Introducing Enhanced MeepleCard System"
- Email campaign: "New Features Available - Upgrade to Pro"
- In-app notification: "Discover new tag system and permissions"

---

## Post-Deployment Monitoring

### Key Metrics (First 48 Hours)

**API Performance**:
```promql
# Permission endpoint latency (target: p95 < 100ms)
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket{endpoint="/api/v1/permissions/me"}[5m]))

# Permission endpoint error rate (target: < 1%)
rate(http_requests_total{endpoint="/api/v1/permissions/me",status=~"5.."}[5m]) / rate(http_requests_total{endpoint="/api/v1/permissions/me"}[5m])

# Database query performance (target: < 50ms)
rate(sql_query_duration_ms_sum{query="GetUserPermissions"}[5m]) / rate(sql_query_duration_ms_count{query="GetUserPermissions"}[5m])
```

**Frontend Performance**:
```javascript
// Web Vitals
onLCP(metric => {
  // Target: < 2.5s
  if (metric.value > 2500) {
    analytics.track('performance_issue', { metric: 'LCP', value: metric.value });
  }
});

onFID(metric => {
  // Target: < 100ms
  if (metric.value > 100) {
    analytics.track('performance_issue', { metric: 'FID', value: metric.value });
  }
});

onCLS(metric => {
  // Target: < 0.1
  if (metric.value > 0.1) {
    analytics.track('performance_issue', { metric: 'CLS', value: metric.value });
  }
});
```

**Business Metrics**:
- Tier upgrade conversion rate (baseline vs post-deploy)
- Feature adoption rate (% users using new tags, agent metadata)
- Permission-denied UI interactions (clicks on upgrade prompts)
- Support ticket volume (permissions-related)

---

## Incident Response

### Scenario 1: Permission API Overload

**Symptoms**:
- Permission endpoint p95 > 500ms
- Database CPU > 80%
- Slow page loads

**Response**:
1. **Immediate**: Add backend caching (HybridCache)
   ```csharp
   app.MapGet("/permissions/me", async (IHybridCacheService cache, Guid userId) =>
   {
       return await cache.GetOrCreateAsync($"perm:{userId}", async ct =>
           await mediator.Send(new GetUserPermissionsQuery(userId), ct),
           new HybridCacheEntryOptions { Duration = TimeSpan.FromMinutes(5) });
   });
   ```

2. **Short-term**: Increase frontend cache (5min → 15min)
3. **Long-term**: Add CDN edge caching, replicate read database

---

### Scenario 2: Migration Failure

**Symptoms**:
- Deployment fails during migration
- "Column already exists" error

**Response**:
1. **Rollback deployment** (blue-green makes this instant)
2. **Fix migration script** (idempotent SQL)
   ```sql
   -- Idempotent version
   DO $$
   BEGIN
       IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Users' AND column_name='Status') THEN
           ALTER TABLE "Users" ADD COLUMN "Status" integer NOT NULL DEFAULT 0;
       END IF;
   END$$;
   ```
3. **Redeploy** with fixed migration

---

### Scenario 3: Permission Cache Poisoning

**Symptoms**:
- Users see wrong tier/role
- Permission checks inconsistent

**Response**:
1. **Clear all caches** (Redis + frontend)
   ```bash
   docker exec meepleai-redis redis-cli FLUSHDB
   # Frontend: Invalidate React Query cache via force refresh
   ```
2. **Investigate**: Check audit logs for tier/role changes
3. **Fix**: Ensure cache invalidation on permission changes

---

## Health Checks

### Backend Health Endpoint

```csharp
// Routing/HealthEndpoints.cs (extend existing)
app.MapGet("/health/epic-4068", async (PermissionRegistry registry, MeepleAiDbContext db) =>
{
    var checks = new Dictionary<string, string>();

    // Check PermissionRegistry initialized
    try
    {
        var testPerm = registry.GetPermission("wishlist");
        checks["permission_registry"] = testPerm != null ? "healthy" : "unhealthy";
    }
    catch (Exception ex)
    {
        checks["permission_registry"] = $"error: {ex.Message}";
    }

    // Check User.Status column exists
    try
    {
        var hasStatusColumn = await db.Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM information_schema.columns WHERE table_name='Users' AND column_name='Status'");

        checks["database_schema"] = hasStatusColumn > 0 ? "healthy" : "migration_pending";
    }
    catch
    {
        checks["database_schema"] = "error";
    }

    var allHealthy = checks.Values.All(v => v == "healthy");

    return allHealthy ? Results.Ok(checks) : Results.Json(checks, statusCode: 503);
})
.WithTags("Health");
```

**Monitoring**:
```bash
# Kubernetes liveness probe
livenessProbe:
  httpGet:
    path: /health/epic-4068
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10
```

---

## Rollback Procedures

### Quick Rollback (< 5 minutes)

```bash
# 1. Switch load balancer to blue (old version)
# nginx.conf
upstream meepleai_api {
    server blue:8080; # Old version (no Epic #4068)
}

# Reload nginx
docker exec nginx nginx -s reload

# 2. Users immediately see old UI (no Epic #4068 features)
# 3. Database unchanged (migration is backward compatible)
```

### Full Rollback (if migration issues)

```bash
# 1. Restore database backup
docker exec -i meepleai-postgres psql -U postgres -d meepleai < backup-before-epic4068.sql

# 2. Deploy old version
docker tag meepleai-api:v1.4 meepleai-api:latest
docker tag meepleai-web:v1.4 meepleai-web:latest
docker compose up -d

# 3. Verify health
curl http://localhost:8080/health
```

**Data Loss**: Users who upgraded tiers during rollout lose tier assignment (reverted to backup)

**Prevention**: Keep migration, rollback code only (safer)

---

## Performance Tuning Post-Deploy

### Database Query Optimization

```sql
-- Verify indexes used
EXPLAIN ANALYZE SELECT "Tier", "Role", "Status" FROM "Users" WHERE "Id" = 'xxx';

-- Expected: Index Scan on IX_Users_Id (or PK)
-- NOT: Seq Scan

-- If slow, add covering index
CREATE INDEX IX_Users_Permissions_Covering ON "Users" ("Id") INCLUDE ("Tier", "Role", "Status");
```

### Redis Caching (Future Enhancement)

```csharp
// Add Redis caching for permission checks
public async Task<UserPermissions> GetUserPermissions(Guid userId)
{
    var cacheKey = $"permissions:{userId}";

    // Try cache first
    var cached = await _redis.StringGetAsync(cacheKey);
    if (cached.HasValue)
    {
        return JsonSerializer.Deserialize<UserPermissions>(cached);
    }

    // Cache miss, query database
    var permissions = await _db.Users
        .Where(u => u.Id == userId)
        .Select(u => new UserPermissions { /* ... */ })
        .FirstAsync();

    // Cache for 5 minutes
    await _redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(permissions), TimeSpan.FromMinutes(5));

    return permissions;
}
```

**Benchmark**:
- With Redis: ~2ms (cache hit)
- Without Redis: ~20ms (database query)

---

## Security Hardening (Production)

### Rate Limiting

```csharp
// Add ASP.NET Core rate limiting
builder.Services.AddRateLimiter(options =>
{
    // Permission endpoints: 100 req/min per user
    options.AddFixedWindowLimiter("permission", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });

    // Global: 1000 req/min
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            ctx.User.GetUserId().ToString(),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1000,
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

// Apply to permission endpoints
app.MapPermissionEndpoints()
   .RequireRateLimiting("permission");
```

### HTTPS Enforcement

```csharp
// Program.cs
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
    app.UseHsts(); // Strict-Transport-Security header
}
```

### CORS Lockdown

```csharp
// Production CORS: Only specific origins
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
            "https://app.meepleai.com",
            "https://admin.meepleai.com"
        ) // No wildcards in production
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});
```

---

## Deployment Commands

### Deploy to Production (Manual)

```bash
# 1. Pull latest
git checkout main
git pull origin main

# 2. Build Docker images
docker build -t ghcr.io/meepleai/api:v1.5-epic4068 apps/api
docker build -t ghcr.io/meepleai/web:v1.5-epic4068 apps/web

# 3. Push to registry
docker push ghcr.io/meepleai/api:v1.5-epic4068
docker push ghcr.io/meepleai/web:v1.5-epic4068

# 4. Deploy to servers
ssh production-server << 'EOF'
  cd /opt/meepleai
  docker compose pull
  docker compose up -d --no-deps api web
  docker compose logs -f api # Watch for errors
EOF

# 5. Apply migration
ssh production-server << 'EOF'
  docker exec meepleai-api dotnet ef database update
EOF

# 6. Verify deployment
curl https://api.meepleai.com/health/epic-4068
curl https://app.meepleai.com
```

---

### Deploy via GitHub Actions (Automated)

```yaml
# .github/workflows/deploy-production.yml
on:
  push:
    branches: [main]
    tags: ['v*']

jobs:
  deploy:
    runs-on: ubuntu-latest
    environment: production

    steps:
      - uses: actions/checkout@v4

      - name: Build & Push Docker Images
        run: |
          echo "${{ secrets.GHCR_TOKEN }}" | docker login ghcr.io -u ${{ github.actor }} --password-stdin

          docker build -t ghcr.io/meepleai/api:${{ github.sha }} apps/api
          docker push ghcr.io/meepleai/api:${{ github.sha }}

          docker build -t ghcr.io/meepleai/web:${{ github.sha }} apps/web
          docker push ghcr.io/meepleai/web:${{ github.sha }}

      - name: Deploy to Production
        uses: appleboy/ssh-action@master
        with:
          host: ${{ secrets.PROD_HOST }}
          username: ${{ secrets.PROD_USER }}
          key: ${{ secrets.PROD_SSH_KEY }}
          script: |
            cd /opt/meepleai
            docker compose pull
            docker compose up -d --no-deps api web

      - name: Run Migration
        uses: appleboy/ssh-action@master
        with:
          host: ${{ secrets.PROD_HOST }}
          username: ${{ secrets.PROD_USER }}
          key: ${{ secrets.PROD_SSH_KEY }}
          script: docker exec meepleai-api dotnet ef database update

      - name: Health Check
        run: |
          sleep 30 # Wait for containers to start

          curl --fail https://api.meepleai.com/health/epic-4068 || exit 1
          curl --fail https://app.meepleai.com || exit 1

      - name: Notify Slack
        uses: slackapi/slack-github-action@v1
        with:
          payload: |
            {
              "text": "✅ Epic #4068 deployed to production",
              "blocks": [
                {
                  "type": "section",
                  "text": {
                    "type": "mrkdwn",
                    "text": "*Deployment Complete*\n\n• Epic: #4068 MeepleCard Enhancements\n• Version: ${{ github.sha }}\n• Environment: Production\n• Deployer: ${{ github.actor }}\n\n<https://app.meepleai.com|View App>"
                  }
                }
              ]
            }
        env:
          SLACK_WEBHOOK_URL: ${{ secrets.SLACK_WEBHOOK_URL }}
```

---

## Backup & Disaster Recovery

### Pre-Deployment Backup

```bash
# Automated backup script
#!/bin/bash
# backup-before-deploy.sh

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="/backups/epic-4068"

mkdir -p $BACKUP_DIR

# Database backup
docker exec meepleai-postgres pg_dump -U postgres meepleai | gzip > "$BACKUP_DIR/db-$TIMESTAMP.sql.gz"

# Redis backup (if caching permissions)
docker exec meepleai-redis redis-cli SAVE
docker cp meepleai-redis:/data/dump.rdb "$BACKUP_DIR/redis-$TIMESTAMP.rdb"

# Application files
tar -czf "$BACKUP_DIR/app-$TIMESTAMP.tar.gz" /opt/meepleai/

echo "✅ Backup complete: $BACKUP_DIR"
ls -lh $BACKUP_DIR
```

**Run before deployment**:
```bash
chmod +x backup-before-deploy.sh
./backup-before-deploy.sh
# Verify backup files created
```

---

### Disaster Recovery Procedure

**If catastrophic failure** (data loss, corruption):

```bash
# 1. Stop all services
docker compose down

# 2. Restore database from backup
gunzip -c /backups/epic-4068/db-20260212_100000.sql.gz | \
  docker exec -i meepleai-postgres psql -U postgres -d meepleai

# 3. Restore Redis (if cached)
docker cp /backups/epic-4068/redis-20260212_100000.rdb meepleai-redis:/data/dump.rdb
docker restart meepleai-redis

# 4. Restore application files
tar -xzf /backups/epic-4068/app-20260212_100000.tar.gz -C /

# 5. Restart services
docker compose up -d

# 6. Verify health
curl http://localhost:8080/health
```

**RTO** (Recovery Time Objective): < 30 minutes
**RPO** (Recovery Point Objective): Last backup (hourly backups recommended)

---

## Load Testing

### Pre-Deployment Load Test

```bash
# Install k6
docker pull grafana/k6

# Run load test
docker run -v $(pwd)/tests/load:/scripts grafana/k6 run /scripts/epic-4068-load-test.js
```

```javascript
// tests/load/epic-4068-load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '2m', target: 100 },  // Ramp up to 100 users
    { duration: '5m', target: 100 },  // Stay at 100 for 5 minutes
    { duration: '2m', target: 200 },  // Ramp to 200
    { duration: '5m', target: 200 },  // Stay at 200
    { duration: '2m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<100'], // 95% < 100ms
    http_req_failed: ['rate<0.01'],   // < 1% errors
  },
};

export default function () {
  const token = 'Bearer xxx'; // Test token

  // Permission check
  const res1 = http.get('https://api.meepleai.com/api/v1/permissions/me', {
    headers: { 'Authorization': token },
  });

  check(res1, {
    'permissions/me status 200': (r) => r.status === 200,
    'permissions/me < 100ms': (r) => r.timings.duration < 100,
  });

  sleep(1);

  // Feature check
  const res2 = http.get('https://api.meepleai.com/api/v1/permissions/check?feature=bulk-select', {
    headers: { 'Authorization': token },
  });

  check(res2, {
    'permissions/check status 200': (r) => r.status === 200,
    'permissions/check < 50ms': (r) => r.timings.duration < 50,
  });

  sleep(1);
}
```

**Expected Results**:
- p95 latency: <100ms ✓
- Error rate: <1% ✓
- 200 concurrent users supported ✓

---

## Success Criteria

**Deployment considered successful if**:
- ✅ Health checks passing (48 hours)
- ✅ Error rate < 1%
- ✅ Permission API p95 < 100ms
- ✅ No critical bugs reported
- ✅ User feedback positive (>80%)
- ✅ Tier upgrade conversion baseline or higher
- ✅ No security incidents

**Sign-off**: Product Manager + Tech Lead approval after 1 week stable

---

## Resources

- Docker Docs: https://docs.docker.com/
- Kubernetes Docs: https://kubernetes.io/docs/
- Prometheus: https://prometheus.io/docs/
- Grafana: https://grafana.com/docs/
- k6 Load Testing: https://k6.io/docs/
