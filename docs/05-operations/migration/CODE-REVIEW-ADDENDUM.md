# HyperDX Implementation Plan - Code Review Addendum

**Status**: Critical Sections to Add
**Date**: 2025-11-21

These sections must be added to `hyperdx-implementation-plan.md` to complete all P0 and P2 fixes.

---

## P0-3: CORS Configuration (Add to Phase 2, Step 2.1)

Add this section **after** the OpenTelemetry configuration in `Program.cs`:

```markdown
#### Step 2.2: Configure CORS for Trace Propagation

**Problem**: Frontend cannot read trace headers without CORS exposure.

**File**: `apps/api/src/Api/Program.cs`

Add CORS configuration **before** the middleware pipeline:

```csharp
// Configure CORS to expose trace headers
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins("http://localhost:3000", "https://meepleai.dev")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("X-Trace-Id", "X-Span-Id");  // CRITICAL for correlation
    });
});

var app = builder.Build();

// Apply CORS middleware
app.UseCors();

// Add trace context to responses
app.Use(async (context, next) =>
{
    var activity = Activity.Current;
    if (activity != null)
    {
        context.Response.Headers["X-Trace-Id"] = activity.TraceId.ToString();
        context.Response.Headers["X-Span-Id"] = activity.SpanId.ToString();
    }
    await next();
});

app.Run();
```

**Verification**:
```bash
# Test CORS headers are exposed
curl -v http://localhost:5080/api/v1/games \
  -H "Origin: http://localhost:3000" | grep "Access-Control-Expose-Headers"

# Should output:
# Access-Control-Expose-Headers: X-Trace-Id,X-Span-Id
```
```

---

## P0-4: SSR-Safe HyperDX Initialization (Replace existing layout.tsx section)

**Replace** the existing `Step 3.3: Initialize in Root Layout` with:

```markdown
#### Step 3.3: Update Root Layout (SSR-Safe)

**Problem**: Using `'use client'` in root layout breaks Server-Side Rendering.

**Solution**: Use separate HyperDXProvider component.

**File**: `apps/web/src/app/layout.tsx` (UPDATE EXISTING)

```tsx
import { HyperDXProvider } from '@/components/HyperDXProvider';
import { AppProviders } from './providers';

// Keep layout as Server Component for optimal SSR performance
export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="it" suppressHydrationWarning>
      <body>
        {/* HyperDX wraps everything to capture all user interactions */}
        <HyperDXProvider>
          <AppProviders>
            {children}
          </AppProviders>
        </HyperDXProvider>
      </body>
    </html>
  );
}
```

**File**: `apps/web/src/components/HyperDXProvider.tsx` (ALREADY CREATED)

See file at: `apps/web/src/components/HyperDXProvider.tsx`

**Verification**:
```bash
# Check that pages still render server-side
pnpm build
# Should succeed without warnings about layout being client component

# Check HyperDX initializes on client
pnpm dev
# Open http://localhost:3000 → Check browser console → Should see HyperDX initialization logs
```
```

---

## P1-SEC3: Backend Sensitive Data Scrubbing (Add to Phase 2)

Add this as **Step 2.3** (new section):

```markdown
#### Step 2.3: Add Sensitive Data Processor

**Security Requirement**: Scrub passwords, tokens, and secrets from traces.

**File**: `apps/api/src/Api/Infrastructure/Telemetry/SensitiveDataProcessor.cs` (ALREADY CREATED)

See file at: `apps/api/src/Api/Infrastructure/Telemetry/SensitiveDataProcessor.cs`

**Integration** (Update `Program.cs`):

```csharp
using Api.Infrastructure.Telemetry;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()

            // ADD: Sensitive data scrubbing processor
            .AddProcessor(new SensitiveDataProcessor())

            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://meepleai-hyperdx:14317");
                options.Protocol = OtlpExportProtocol.Grpc;
            });
    });
```

**Verification**:
```bash
# Test that passwords are redacted
curl -X POST http://localhost:5080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"secret123"}'

# Check HyperDX trace → Should show password as "[REDACTED]"
```
```

---

## P2: Migration Verification Checklist (Add to Phase 5)

Add this as **new section** after testing:

```markdown
### Phase 5: Verification & Validation

#### Comprehensive Verification Checklist

Use this checklist to ensure complete and correct implementation.

**Infrastructure Verification** (Week 1 Day 1):
- [ ] HyperDX container running: `docker ps | grep hyperdx`
- [ ] HyperDX UI accessible: http://localhost:8180
- [ ] OTLP gRPC port open: `nc -zv localhost 14317`
- [ ] OTLP HTTP port open: `nc -zv localhost 14318`
- [ ] ClickHouse port NOT exposed: `nc -zv localhost 8123` (should fail)
- [ ] Docker network exists: `docker network ls | grep meepleai`
- [ ] Health check passing: `docker inspect meepleai-hyperdx | grep -A5 Health`

**Backend Integration Verification** (Week 1 Day 3):
- [ ] OpenTelemetry packages installed: `dotnet list package | grep OpenTelemetry`
- [ ] No build errors: `dotnet build`
- [ ] Backend logs appearing in HyperDX within 10s
- [ ] Backend traces appearing with service name "meepleai-api"
- [ ] Trace IDs match between logs and traces
- [ ] Sensitive data redacted: Check trace with password field shows "[REDACTED]"
- [ ] No OTel exporter errors in API logs: `docker logs meepleai-api 2>&1 | grep -i error | grep -i otel`
- [ ] CORS headers exposed: `curl -I http://localhost:5080/api/v1/games -H "Origin: http://localhost:3000" | grep Expose`

**Old Services Removal Verification** (Week 1 Day 4):
- [ ] Seq container stopped: `docker ps | grep seq` (should be empty)
- [ ] Jaeger container stopped: `docker ps | grep jaeger` (should be empty)
- [ ] No references to :8081 in code: `grep -r "8081" apps/`
- [ ] No references to :16686 in code: `grep -r "16686" apps/`
- [ ] Seq sink removed from LoggingConfiguration.cs
- [ ] Jaeger exporter removed from Program.cs

**Frontend Integration Verification** (Week 2 Day 2):
- [ ] HyperDX browser SDK installed: `pnpm list | grep hyperdx`
- [ ] HyperDXProvider component exists: `apps/web/src/components/HyperDXProvider.tsx`
- [ ] Root layout remains Server Component (no 'use client' directive)
- [ ] Session replay recording: Open app → Check HyperDX UI → Sessions tab
- [ ] User identification working: Login → Check session shows user ID
- [ ] Frontend → Backend correlation: Click action → Trace shows both frontend + backend spans
- [ ] Sensitive input masking: Check session replay → Password fields masked

**Alert Configuration Verification** (Week 2 Day 3):
- [ ] HyperDX alerts created (at least 3: High Error Rate, Slow Response, Frontend Error Spike)
- [ ] Test alert triggers: Generate errors → Email/Slack notification received
- [ ] Alert latency < 2 minutes
- [ ] Infrastructure alerts still in Alertmanager (DatabaseDown, RedisDown, etc.)

**Performance & Load Testing** (Week 2 Day 4):
- [ ] Load test completes: `k6 run tests/k6/hyperdx-ingestion-test.js`
- [ ] No data loss during load: All logs/traces ingested
- [ ] Query performance: Log search < 1s for 10K logs
- [ ] Trace query performance: < 500ms for 1K traces
- [ ] HyperDX resource usage: < 4GB RAM, < 2 CPU cores
- [ ] Storage usage: < 50GB after 1 week of data

**Documentation Verification** (Week 2 Day 5):
- [ ] CLAUDE.md updated: HyperDX mentioned as primary observability tool
- [ ] README.md updated: Links point to http://localhost:8180
- [ ] Runbooks updated: No references to Seq/Jaeger
- [ ] ADR-015 created: `docs/01-architecture/adr/adr-015-hyperdx-observability.md`
- [ ] Environment variables documented: `.env.example` has `HYPERDX_API_KEY`

**Security Hardening** (Week 2 Final):
- [ ] Production API key configured (not "demo"): Check `docker-compose.yml`
- [ ] ClickHouse port not exposed publicly
- [ ] Sensitive data scrubbing verified in production traces
- [ ] HTTPS enabled for HyperDX UI in production (if applicable)
- [ ] Access controls configured (if using HyperDX Cloud)

**Go-Live Readiness**:
- [ ] All above checkboxes marked
- [ ] Team trained on HyperDX UI
- [ ] Runbook for HyperDX issues created
- [ ] Rollback plan tested in staging
- [ ] Monitoring alerts validated
- [ ] Final sign-off from DevOps + Security teams
```

---

## P2: Enhanced Rollback Plan (Replace existing rollback section)

**Replace** the existing "Rollback Plan" section with:

```markdown
## Enhanced Rollback Plan

### Pre-Rollback (Preserve Data)

**Before** rolling back, preserve critical data from HyperDX:

```bash
#!/bin/bash
# File: tools/hyperdx-rollback-backup.sh

set -e

echo "=== HyperDX Rollback Backup ==="
BACKUP_DIR="./hyperdx-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 1. Export last 24 hours of logs
echo "Exporting logs..."
docker exec meepleai-hyperdx clickhouse-client --query "
  SELECT * FROM logs
  WHERE timestamp > now() - INTERVAL 1 DAY
  FORMAT JSONEachRow
" > "$BACKUP_DIR/logs.jsonl"

# 2. Export last 24 hours of traces
echo "Exporting traces..."
docker exec meepleai-hyperdx clickhouse-client --query "
  SELECT * FROM traces
  WHERE timestamp > now() - INTERVAL 1 DAY
  FORMAT JSONEachRow
" > "$BACKUP_DIR/traces.jsonl"

# 3. Export alert configurations
echo "Exporting alerts..."
curl -s http://localhost:8180/api/v1/alerts > "$BACKUP_DIR/alerts.json"

# 4. Document custom dashboards (manual screenshot or export if available)
echo "✅ Backup complete: $BACKUP_DIR"
echo "⚠️  Manually export any custom dashboards from HyperDX UI"
```

### Rollback Execution

**Step 1**: Stop HyperDX
```bash
cd infra
docker compose -f docker-compose.hyperdx.yml down
```

**Step 2**: Restore Seq + Jaeger in `docker-compose.yml`
```bash
# Uncomment Seq and Jaeger services in docker-compose.yml
git checkout main -- infra/docker-compose.yml

# Start restored services
docker compose up -d meepleai-seq meepleai-jaeger
```

**Step 3**: Revert API Configuration
```bash
cd apps/api/src/Api

# Revert OpenTelemetry exporter changes
git checkout main -- Program.cs
git checkout main -- Infrastructure/Logging/LoggingConfiguration.cs

# Rebuild and restart
dotnet build
docker compose restart meepleai-api
```

**Step 4**: Revert Frontend Configuration
```bash
cd apps/web

# Remove HyperDX package
pnpm remove @hyperdx/browser

# Revert layout changes
git checkout main -- src/app/layout.tsx

# Remove HyperDX provider
rm -f src/components/HyperDXProvider.tsx
rm -f src/lib/hyperdx.ts

# Rebuild
pnpm build
docker compose restart meepleai-web
```

### Post-Rollback Verification

**Critical Checks** (must pass before declaring rollback successful):

```bash
#!/bin/bash
# File: tools/verify-rollback.sh

set -e
echo "=== Verifying Rollback ==="

# 1. Seq accessible
echo -n "Seq UI accessible..."
curl -f -s http://localhost:8081 > /dev/null && echo "✅" || echo "❌"

# 2. Jaeger accessible
echo -n "Jaeger UI accessible..."
curl -f -s http://localhost:16686 > /dev/null && echo "✅" || echo "❌"

# 3. API health check
echo -n "API health check..."
curl -f -s http://localhost:5080/health > /dev/null && echo "✅" || echo "❌"

# 4. Generate test log
echo -n "Logs ingesting to Seq..."
curl -X POST -s http://localhost:5080/api/v1/test-error > /dev/null
sleep 5
# Check Seq for new log (manual verification required)
echo "⚠️  Manually check http://localhost:8081 for test log"

# 5. Generate test trace
echo -n "Traces ingesting to Jaeger..."
curl -s http://localhost:5080/api/v1/games > /dev/null
sleep 5
# Check Jaeger for new trace (manual verification required)
echo "⚠️  Manually check http://localhost:16686 for test trace"

echo "=== Rollback Verification Complete ==="
```

**Manual Verification Steps**:
1. Open Seq (http://localhost:8081) → Verify logs from last 5 minutes appear
2. Open Jaeger (http://localhost:16686) → Search for service "meepleai-api" → Verify traces appear
3. Test API endpoints manually → Verify functionality unchanged
4. Check Prometheus metrics still accessible (http://localhost:9090)
5. Check Grafana dashboards still working (http://localhost:3001)

### Rollback Timeline

| Phase | Duration | Cumulative |
|-------|----------|------------|
| Pre-Rollback Backup | 10 min | 10 min |
| Stop HyperDX | 2 min | 12 min |
| Restore Services | 5 min | 17 min |
| Revert API Config | 5 min | 22 min |
| Revert Frontend Config | 3 min | 25 min |
| Post-Rollback Verification | 10 min | **35 min** |

**Total Estimated Time**: **30-35 minutes** (including verification)

**Impact**: Brief telemetry gap (5-10 minutes) during service restart.

### Rollback Decision Criteria

**Trigger Rollback if**:
- ❌ HyperDX fails to start after 3 attempts
- ❌ >10% logs/traces not ingested (data loss)
- ❌ Query performance >5s P95 (unusable)
- ❌ HyperDX resource usage >8GB RAM (memory leak)
- ❌ Security breach detected (unauthorized access)
- ❌ Critical bug prevents production debugging

**Do NOT Rollback if**:
- ✅ Minor UI issues (can be fixed without rollback)
- ✅ Alert configuration needs tuning
- ✅ Team needs more training (provide training instead)
- ✅ Session replay not working (non-critical feature)

### Post-Rollback Actions

1. **Root Cause Analysis**: Document why rollback was necessary
2. **Fix Planning**: Create plan to address issues before retry
3. **Stakeholder Communication**: Notify team of rollback and next steps
4. **Data Preservation**: Keep HyperDX backup for 30 days
5. **Retry Timeline**: Schedule second attempt after fixes (minimum 1 week)
```

---

## Usage Instructions

1. **Apply sed fixes**: Already done (ports, endpoints, network, security)
2. **Add P0-3 section**: Insert "Step 2.2: Configure CORS" after existing Step 2.1
3. **Replace layout section**: Replace Step 3.3 with SSR-safe version
4. **Add P1-SEC3 section**: Insert "Step 2.3: Add Sensitive Data Processor" after Step 2.2
5. **Add verification checklist**: Insert as new Phase 5 after testing
6. **Replace rollback section**: Replace existing rollback with enhanced version

---

**Files Created**:
- `apps/web/src/components/HyperDXProvider.tsx` ✅
- `apps/api/src/Api/Infrastructure/Telemetry/SensitiveDataProcessor.cs` ✅
- `docs/05-operations/migration/REVIEW-FIXES-SUMMARY.md` ✅
- `docs/05-operations/migration/CODE-REVIEW-ADDENDUM.md` ✅ (this file)

**Files Modified**:
- `docs/05-operations/migration/hyperdx-implementation-plan.md` ⚠️ (partial - sed fixes applied)

**Next Steps**:
- Manually integrate addendum sections into main plan
- Test all code snippets
- Final review and commit
