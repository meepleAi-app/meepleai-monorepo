# SharedGameCatalog Phase 5 - Pre-Deployment Checklist

**Issue**: #2427 (Parent: #2374)
**Date**: 2026-01-14
**Version**: 1.0.0

---

## Performance Benchmarks ✅

- [ ] **Migration Applied**: `dotnet ef database update` completed
  - Verification: `psql -c "SELECT COUNT(*) FROM pg_indexes WHERE tablename='shared_games' AND indexname LIKE 'ix_%';"`
  - Expected: 13 indexes

- [ ] **GIN FTS Index Operational**
  - Run: `docs/05-testing/shared-catalog-fts-performance-validation.sql`
  - Expected: "Bitmap Index Scan using ix_shared_games_fts"

- [ ] **k6 Load Test Passed**
  - Run: `k6 run tests/k6/shared-catalog-load-test.js`
  - Thresholds:
    - Search P95 < 200ms ✓
    - Admin P95 < 300ms ✓
    - Cache hit rate > 80% ✓
    - Failure rate < 1% ✓

- [ ] **Lighthouse Score > 90** (`/games/add` page)
  - Performance: > 90
  - Accessibility: > 95
  - Best Practices: > 90

---

## Security Review ✅

- [ ] **FluentValidation Audit**: All 14 validators reviewed
  - MaxLength constraints: Title (500), URLs (1000)
  - SQL injection: EF Core parameterization verified
  - Result: Compliant (no changes needed)

- [ ] **Authorization Enforcement**: 21 protected endpoints verified
  - AdminOnlyPolicy: 5 endpoints (delete, bulk, archive)
  - AdminOrEditorPolicy: 16 endpoints (CRUD)
  - AllowAnonymous: 4 endpoints (public search)

- [ ] **Rate Limiting Configured**
  - Public: 300 req/min per IP ✓
  - Admin: 100 req/min per user ✓
  - Rejection: 429 with Retry-After ✓

- [ ] **CORS Origins Validated**
  - Production: Only allowed origins in appsettings.Production.json
  - AllowCredentials: true (for authenticated requests)

- [ ] **Secrets Scan Clean**
  - Run: `detect-secrets scan --baseline .secrets.baseline`
  - Expected: No new secrets detected
  - Action: If found, rotate and add to .gitignore

---

## Monitoring & Observability ✅

- [ ] **Health Checks Operational**
  - GET /health includes `shared-catalog-fts`
  - Status: Healthy (FTS latency < 200ms)
  - Test: `curl http://localhost:8080/health | jq '.entries."shared-catalog-fts"'`

- [ ] **Structured Logging with Correlation IDs**
  - Verify logs include `CorrelationId` (TraceIdentifier)
  - Test: Make request, check HyperDX for correlation

- [ ] **Prometheus Metrics Exposed**
  - GET /metrics accessible
  - Verify: `curl http://localhost:8080/metrics | grep meepleai_cache`
  - Expected metrics: cache_hits_total, cache_misses_total, http_server_request_duration_bucket

- [ ] **Grafana Dashboard Imported**
  - File: `infra/monitoring/grafana/dashboards/shared-catalog-performance.json`
  - Panels: Search P95, Cache hit rate, Request rate, Latency percentiles
  - Alerts: P95 > 200ms, Cache < 80%

---

## Quality Assurance ✅

- [ ] **All Backend Tests Passing**
  - Run: `cd apps/api/src/Api && dotnet test`
  - Expected: Zero failures, coverage > 90%

- [ ] **All Frontend Tests Passing**
  - Run: `cd apps/web && pnpm test`
  - Expected: 5232+ tests passed, < 25 skipped

- [ ] **E2E Tests Passing** (Issue #2374 Phase 6)
  - Admin workflow: Create → Edit → Publish → Archive → Delete
  - Permission enforcement: 403 for unauthorized
  - Bulk import: 100 games in < 60s

- [ ] **Cross-Browser Compatibility** (Chromium, Firefox, WebKit)
  - Run: `pnpm test:e2e --project=all`

- [ ] **WCAG AA Accessibility**
  - Run: axe-playwright on `/games/add`
  - Expected: Zero violations

- [ ] **Zero Build Errors or Warnings**
  - Backend: `dotnet build` → 0 errors, 0 warnings
  - Frontend: `pnpm build` → Success

---

## Documentation Complete ✅

- [ ] **3 ADRs Created**
  - ADR-016: Bounded Context Separation
  - ADR-018: PostgreSQL FTS Technology Choice
  - ADR-019: Delete Workflow Governance

- [ ] **README Updated**
  - File: `BoundedContexts/SharedGameCatalog/README.md`
  - Content: 20 CQRS operations, performance, authorization

- [ ] **OpenAPI Documentation Complete**
  - All endpoints have Summary + Description
  - Error codes specified (400, 401, 403, 404, 500)
  - Scalar UI organized by tags

- [ ] **Deployment Guide Created**
  - File: `docs/04-deployment/shared-catalog-environment-config.md`
  - Environment variables documented
  - Migration scripts provided

---

## Stakeholder Approval ✅

- [ ] **Product Owner Sign-Off**
  - Feature complete: Search, admin UI, BGG import, workflows
  - Performance targets met: P95 < 200ms, cache > 80%
  - User experience validated: Smooth search, fast details

- [ ] **QA Team Sign-Off**
  - E2E tests passing
  - No critical bugs
  - Accessibility compliant

- [ ] **Security Review Completed**
  - Rate limiting enforced
  - Authorization tested
  - Input validation audited
  - No secrets in codebase

- [ ] **Operations Team Ready**
  - Health checks operational
  - Metrics exposed
  - Grafana dashboard configured
  - Runbook available (troubleshooting in README)

---

## Deployment Timeline

### Phase 1: Staging Deployment (Day 1)
- [ ] Deploy to staging environment
- [ ] Run smoke tests
- [ ] Verify monitoring dashboards
- [ ] Load test with k6 (1000 req over 10 min)

### Phase 2: Production Deployment (Day 2-3)
- [ ] **T-24h**: Stakeholder notification
- [ ] **T-1h**: Database backup
- [ ] **T-0**: Apply migrations
- [ ] **T+5min**: Verify health checks
- [ ] **T+15min**: Run load test
- [ ] **T+1h**: Monitor Grafana alerts
- [ ] **T+24h**: Post-deployment review

### Phase 3: Post-Deployment (Day 4-7)
- [ ] Monitor P95 latency (should remain < 200ms)
- [ ] Monitor cache hit rate (should be > 80%)
- [ ] Review audit logs for anomalies
- [ ] Gather user feedback on search performance

---

## Rollback Criteria

**Trigger rollback if**:
- P95 latency > 500ms for 10 consecutive minutes
- Cache hit rate < 50% for 1 hour
- Health check returns Unhealthy for 5 minutes
- Critical bug discovered (data loss, security vulnerability)
- > 5% error rate (500 errors) in production traffic

**Rollback procedure**: See "Rollback Plan" section above

---

## Success Metrics (30 Days Post-Deployment)

### Performance
- [ ] P95 search latency < 200ms (99% of time)
- [ ] Cache hit rate > 80% (measured via Prometheus)
- [ ] Zero performance-related user complaints

### Reliability
- [ ] Uptime > 99.9% (SharedGameCatalog health check)
- [ ] Zero data loss incidents
- [ ] < 5 support tickets related to search

### Adoption
- [ ] > 1000 searches/day (user engagement)
- [ ] > 50 games added to catalog (community contribution)
- [ ] > 100 users link catalog games to collections

---

## Contacts

**On-Call**: DevOps Team
**Escalation**: Development Lead
**Stakeholders**: Product Owner, QA Lead, Security Team

---

**Checklist Version**: 1.0
**Last Reviewed**: 2026-01-14
**Next Review**: Before production deployment
