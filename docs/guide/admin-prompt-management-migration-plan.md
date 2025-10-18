# Prompt Management System - Zero-Downtime Migration Plan

**Document Version**: 1.0
**Created**: 2025-10-18
**Target Completion**: 6 weeks from start
**Risk Level**: Medium

---

## Table of Contents

1. [Overview](#overview)
2. [Pre-Migration Checklist](#pre-migration-checklist)
3. [Migration Strategy](#migration-strategy)
4. [Phase-by-Phase Rollout](#phase-by-phase-rollout)
5. [Rollback Procedures](#rollback-procedures)
6. [Monitoring & Validation](#monitoring--validation)
7. [Post-Migration Tasks](#post-migration-tasks)

---

## Overview

### Migration Goals

1. **Zero Downtime**: No service disruptions during migration
2. **Backward Compatibility**: Feature flags allow instant rollback
3. **Gradual Rollout**: Migrate one service at a time, monitor before next
4. **Data Integrity**: Preserve exact prompt content during migration
5. **Auditability**: Track all migration steps in audit logs

### Migration Scope

**What's Changing**:
- Prompt storage: Code → Database
- Prompt retrieval: Hardcoded → Redis-cached DB query
- Prompt updates: Code deployment → Admin UI

**What's NOT Changing**:
- Prompt content (exact same text)
- LLM API integration
- RAG search logic
- User-facing behavior

### Key Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Cache consistency issues | Medium | Low | Transaction-based invalidation + 1h TTL |
| Redis outage | Low | High | Fallback to DB query + circuit breaker |
| Prompt quality regression | High | Critical | Comprehensive test framework + rollback |
| Performance degradation | Low | Medium | Load testing + monitoring |
| Configuration errors | Medium | High | Configuration validation on startup |

---

## Pre-Migration Checklist

### Infrastructure Validation

- [ ] **Redis Availability**
  - Verify Redis connection: `redis-cli -h redis -p 6379 PING` → `PONG`
  - Check memory: `redis-cli INFO memory` → > 100MB available
  - Test set/get: `redis-cli SET test "value" EX 60` → `OK`

- [ ] **Database Schema**
  - Verify tables exist:
    ```sql
    SELECT COUNT(*) FROM information_schema.tables
    WHERE table_name IN ('prompt_templates', 'prompt_versions', 'prompt_audit_logs');
    -- Should return 3
    ```
  - Verify indexes:
    ```sql
    SELECT tablename, indexname FROM pg_indexes
    WHERE tablename LIKE 'prompt_%';
    -- Should show 10+ indexes
    ```

- [ ] **Configuration**
  - `appsettings.json` has `Features:PromptDatabase: false`
  - `appsettings.json` has `PromptManagement` section
  - Environment variables set in Docker Compose (`infra/env/api.env.dev`)

### Data Preparation

- [ ] **Extract Current Prompts**
  - Location: `apps/api/src/Api/Services/*Service.cs` (lines with hardcoded prompts)
  - Extract 4 prompts:
    1. Q&A System Prompt (RagService.cs, line 111)
    2. Streaming Q&A Prompt (StreamingQaService.cs, line 162)
    3. Chess System Prompt (ChessAgentService.cs, line 137)
    4. Setup Guide Prompt (SetupGuideService.cs, line 108)

- [ ] **Create Seed Script**
  - File: `apps/api/src/Api/Migrations/Seeds/SeedPromptTemplates.sql`
  - Insert 4 templates with v1 (active)
  - Use admin user as `created_by_user_id`

- [ ] **Validate Seed Data**
  - Run seed script in test environment
  - Query: `SELECT name, version_number, is_active FROM prompt_templates pt JOIN prompt_versions pv ON pt.id = pv.template_id;`
  - Verify 4 rows with `is_active = true`

### Testing Environment

- [ ] **Set Up Test Environment**
  - Clone production database schema to test DB
  - Run seed script
  - Deploy API with `Features:PromptDatabase: true` (test only)
  - Deploy admin UI

- [ ] **Smoke Tests**
  - Test prompt retrieval via API: `GET /api/v1/admin/prompts`
  - Test prompt activation: `POST /api/v1/admin/prompts/{id}/versions/{versionId}/activate`
  - Test cache hit: Query prompt twice, second should be < 5ms
  - Test cache invalidation: Activate version, query again, should see new content

### Team Readiness

- [ ] **Training Completed**
  - Admin users trained on UI
  - Runbook reviewed by on-call team
  - Rollback procedure tested in staging

- [ ] **Communication Plan**
  - Notify stakeholders of migration timeline
  - Schedule deployment windows (off-peak hours)
  - Set up incident response Slack channel

---

## Migration Strategy

### Feature Flag-Based Rollout

**Pattern**: Blue-Green Deployment with Feature Flags

```
Deployment 1 (Week 3):
- Code: Refactored services with feature flag
- Feature Flag: OFF (uses hardcoded prompts)
- Risk: Low (no behavior change)

Deployment 2 (Week 4):
- Code: Same as Deployment 1
- Feature Flag: ON for RagService only
- Risk: Medium (single service change)
- Monitor: 48 hours before next rollout

Deployment 3 (Week 5):
- Feature Flag: ON for ChessAgentService
- Monitor: 48 hours

Deployment 4 (Week 6):
- Feature Flag: ON for all services
- Monitor: 1 week
```

### Configuration Management

**appsettings.Production.json**:
```json
{
  "Features": {
    "PromptDatabase": false  // Toggle per service during rollout
  },
  "PromptManagement": {
    "CacheTtlSeconds": 3600,
    "MaxPromptSizeBytes": 16384,
    "EnableAutomaticCacheWarming": true,
    "CriticalPrompts": [
      "qa-system-prompt",
      "streaming-qa-system-prompt",
      "chess-system-prompt",
      "setup-guide-system-prompt"
    ]
  }
}
```

**Environment Variable Override** (for faster rollback):
```bash
# In infra/env/api.env.prod
Features__PromptDatabase=false
```

### Service-by-Service Rollout Order

1. **RagService** (Week 4, Day 1)
   - Reason: Most critical, highest traffic
   - Template: `qa-system-prompt`
   - Monitor: Error rate, latency, cache hit rate

2. **ChessAgentService** (Week 4, Day 3)
   - Reason: Lower traffic, simpler prompt
   - Template: `chess-system-prompt`
   - Monitor: Same as above

3. **SetupGuideService** (Week 5, Day 1)
   - Reason: Medium traffic, RAG-based
   - Template: `setup-guide-system-prompt`
   - Monitor: Same as above

4. **StreamingQaService** (Week 5, Day 3)
   - Reason: Same prompt as RagService, SSE complexity
   - Template: `streaming-qa-system-prompt`
   - Monitor: Same + SSE connection stability

---

## Phase-by-Phase Rollout

### Week 3: Code Deployment (Feature Flag OFF)

**Goal**: Deploy refactored code without changing behavior

**Steps**:

1. **Deploy API** (Wednesday 2 AM UTC - off-peak)
   ```bash
   cd infra
   docker compose pull api
   docker compose up -d api
   docker compose logs -f api | grep "Application started"
   ```

2. **Verify Deployment**
   ```bash
   # Health check
   curl http://localhost:8080/health
   # Should return 200 OK

   # Verify feature flag is OFF
   curl http://localhost:8080/health | jq '.features.promptDatabase'
   # Should return false
   ```

3. **Smoke Test**
   ```bash
   # Q&A request should still work (using hardcoded prompt)
   curl -X POST http://localhost:8080/api/v1/agents/qa \
     -H "Content-Type: application/json" \
     -d '{"gameId": "tic-tac-toe-id", "query": "How many players?"}'
   # Should return normal response
   ```

4. **Monitor for 24 hours**
   - Check Seq logs: No new errors
   - Check Jaeger traces: No latency increase
   - Check Prometheus: No anomalies

**Rollback Plan**:
- If any issues: `docker compose restart api` (restart with previous image)

---

### Week 4, Day 1: RagService Migration

**Goal**: Enable prompt database for Q&A agent only

**Steps**:

1. **Verify Seed Data in Production**
   ```sql
   SELECT pt.name, pv.version_number, pv.is_active, LENGTH(pv.content) as content_length
   FROM prompt_templates pt
   JOIN prompt_versions pv ON pt.id = pv.template_id
   WHERE pt.name = 'qa-system-prompt';
   -- Verify 1 row, is_active = true, content_length ~= 400
   ```

2. **Update Configuration** (2 AM UTC)
   ```bash
   # Method 1: Update appsettings.json and redeploy
   # Method 2: Set environment variable (faster)
   echo "Features__PromptDatabase=true" >> infra/env/api.env.prod

   # Restart API
   docker compose up -d api

   # Verify configuration
   curl http://localhost:8080/health | jq '.features.promptDatabase'
   # Should return true
   ```

3. **Warm Cache**
   ```bash
   # Trigger cache warming (automatic on startup)
   # Verify in logs
   docker compose logs api | grep "Cache warmed for prompt"
   # Should show: "Cache warmed for prompt: qa-system-prompt"
   ```

4. **Test Q&A Request**
   ```bash
   # First request (cache miss)
   time curl -X POST http://localhost:8080/api/v1/agents/qa \
     -H "Content-Type: application/json" \
     -d '{"gameId": "tic-tac-toe-id", "query": "How many players?"}' \
     | jq '.latencyMs'
   # Note latency

   # Second request (cache hit)
   time curl -X POST http://localhost:8080/api/v1/agents/qa \
     -H "Content-Type: application/json" \
     -d '{"gameId": "tic-tac-toe-id", "query": "How do you win?"}' \
     | jq '.latencyMs'
   # Should be same latency (cache adds < 10ms)
   ```

5. **Monitor Metrics** (first 1 hour)
   - Open Grafana: `http://localhost:3001/d/prompt-management`
   - Check panels:
     - Cache hit rate: Should start at 100% (warmed), then stabilize at 95%+
     - Prompt retrieval latency: Should be < 10ms (p95)
     - Q&A endpoint latency: No change from baseline
     - Error rate: No increase

6. **Check Redis Cache**
   ```bash
   # Verify cache key exists
   redis-cli GET "prompt:qa-system-prompt:active"
   # Should return JSON with prompt content

   # Check TTL
   redis-cli TTL "prompt:qa-system-prompt:active"
   # Should return ~3600 (1 hour)
   ```

7. **Monitor for 48 hours**
   - Daily checks:
     - Cache hit rate trend (graph should be stable at 95%+)
     - No errors in Seq logs related to prompts
     - No user reports of incorrect answers
   - Alert thresholds:
     - If cache hit rate < 90% → investigate Redis
     - If latency p95 > 20ms → investigate DB query
     - If error rate increases → rollback immediately

**Success Criteria**:
- ✅ Cache hit rate > 95%
- ✅ Latency < 10ms (p95)
- ✅ No errors in 48 hours
- ✅ User-facing behavior unchanged

**Rollback Plan**:
```bash
# Set feature flag to OFF
echo "Features__PromptDatabase=false" > infra/env/api.env.prod
docker compose up -d api

# Verify rollback
curl http://localhost:8080/health | jq '.features.promptDatabase'
# Should return false

# Monitor for 30 minutes to confirm stability
```

---

### Week 4, Day 3: ChessAgentService Migration

**Goal**: Enable prompt database for Chess agent

**Steps**: Same as Week 4 Day 1, but:
- Template name: `chess-system-prompt`
- Test endpoint: `POST /api/v1/agents/chess/query`
- Test query: `{"gameId": "chess-id", "query": "How does a knight move?", "fenPosition": null}`

**Monitor for 48 hours** before next rollout.

---

### Week 5, Day 1: SetupGuideService Migration

**Goal**: Enable prompt database for Setup Guide agent

**Steps**: Same pattern as above
- Template name: `setup-guide-system-prompt`
- Test endpoint: `POST /api/v1/setup/generate`
- Test query: `{"gameId": "tic-tac-toe-id"}`

**Monitor for 48 hours** before final rollout.

---

### Week 5, Day 3: StreamingQaService Migration

**Goal**: Enable prompt database for Streaming Q&A agent (final service)

**Steps**: Same pattern as above
- Template name: `streaming-qa-system-prompt`
- Test endpoint: `POST /api/v1/agents/qa/stream` (SSE)
- Test query: `{"gameId": "tic-tac-toe-id", "query": "How do you win?"}`

**Special Monitoring** (SSE-specific):
- Verify streaming still works (check browser network tab)
- Check for connection drops
- Verify token-by-token streaming latency

**Monitor for 1 week** before declaring migration complete.

---

### Week 6: Final Validation & Cleanup

**Goal**: Validate full system, remove feature flags

**Steps**:

1. **Full System Test**
   - Test all 4 agents (Q&A, Chess, Setup, Streaming Q&A)
   - Verify cache hit rates for all templates
   - Review 1 week of logs (no errors)

2. **Remove Feature Flags** (Code Cleanup)
   ```csharp
   // Before (Week 1-5)
   if (UsePromptDatabase)
       systemPrompt = await _promptService.GetActivePromptAsync("qa-system-prompt");
   else
       systemPrompt = GetFallbackPrompt();

   // After (Week 6+)
   systemPrompt = await _promptService.GetActivePromptAsync("qa-system-prompt")
       ?? throw new InvalidOperationException("No active prompt found");
   // Remove GetFallbackPrompt() method
   ```

3. **Remove Configuration**
   - Delete `Features:PromptDatabase` from appsettings.json
   - Remove fallback prompt methods from services

4. **Deploy Cleanup**
   ```bash
   cd infra
   docker compose pull api
   docker compose up -d api
   ```

5. **Archive Hardcoded Prompts**
   - Create `docs/archive/prompts-v1-hardcoded.md`
   - Copy original prompts for historical reference
   - Update CLAUDE.md to remove hardcoded prompt references

---

## Rollback Procedures

### Scenario 1: Cache Issues (High Miss Rate)

**Symptoms**:
- Cache hit rate < 80%
- Database query spike
- Latency increase

**Diagnosis**:
```bash
# Check Redis health
redis-cli PING
# Check Redis memory
redis-cli INFO memory | grep used_memory_human
# Check cache keys
redis-cli KEYS "prompt:*"
```

**Resolution**:
1. If Redis down → Fallback to DB (automatic via code)
2. If Redis memory full → Increase Redis memory limit
3. If cache not warming → Check startup logs

**Rollback Not Required** (fallback handles it)

---

### Scenario 2: Prompt Quality Regression

**Symptoms**:
- User reports incorrect answers
- AI request log shows confidence drop
- Hallucination rate increase

**Diagnosis**:
```sql
-- Check active prompt content
SELECT pv.content
FROM prompt_templates pt
JOIN prompt_versions pv ON pt.id = pv.template_id
WHERE pt.name = 'qa-system-prompt' AND pv.is_active = true;

-- Check recent prompt changes
SELECT pa.action, pa.changed_at, pa.details, u.email
FROM prompt_audit_logs pa
JOIN users u ON pa.changed_by_user_id = u.id
WHERE pa.template_id = (SELECT id FROM prompt_templates WHERE name = 'qa-system-prompt')
ORDER BY pa.changed_at DESC
LIMIT 5;
```

**Resolution**:
1. **Option 1: Rollback via Admin UI** (fastest)
   - Open `/admin/prompts/{templateId}`
   - Click version history
   - Click previous version → "Activate"
   - Test immediately

2. **Option 2: Rollback via SQL** (if UI unavailable)
   ```sql
   BEGIN;
   -- Deactivate current version
   UPDATE prompt_versions
   SET is_active = false
   WHERE template_id = (SELECT id FROM prompt_templates WHERE name = 'qa-system-prompt')
     AND is_active = true;

   -- Activate previous version
   UPDATE prompt_versions
   SET is_active = true
   WHERE template_id = (SELECT id FROM prompt_templates WHERE name = 'qa-system-prompt')
     AND version_number = (SELECT MAX(version_number) - 1 FROM prompt_versions WHERE template_id = (SELECT id FROM prompt_templates WHERE name = 'qa-system-prompt'));

   -- Audit log
   INSERT INTO prompt_audit_logs (id, template_id, version_id, action, changed_by_user_id, changed_at, details)
   VALUES (gen_random_uuid(), (SELECT id FROM prompt_templates WHERE name = 'qa-system-prompt'), (SELECT id FROM prompt_versions WHERE template_id = (SELECT id FROM prompt_templates WHERE name = 'qa-system-prompt') AND is_active = true), 'rollback', 'admin-user-id', NOW(), 'Emergency rollback due to quality regression');

   COMMIT;

   -- Invalidate cache
   redis-cli DEL "prompt:qa-system-prompt:active"
   ```

3. **Test Rollback**
   ```bash
   # Query should use previous prompt
   curl -X POST http://localhost:8080/api/v1/agents/qa \
     -d '{"gameId": "tic-tac-toe-id", "query": "How many players?"}'
   ```

**Recovery Time**: < 5 minutes

---

### Scenario 3: Complete System Failure

**Symptoms**:
- All Q&A requests failing
- 500 errors in logs
- Database connection issues

**Emergency Rollback** (disable feature flag):
```bash
# Set feature flag to OFF immediately
echo "Features__PromptDatabase=false" > infra/env/api.env.prod
docker compose restart api

# Verify rollback
curl http://localhost:8080/health | jq '.features.promptDatabase'
# Should return false

# Test Q&A request (should work with hardcoded prompt)
curl -X POST http://localhost:8080/api/v1/agents/qa \
  -d '{"gameId": "tic-tac-toe-id", "query": "How many players?"}'
```

**Recovery Time**: < 2 minutes

---

## Monitoring & Validation

### Key Metrics to Monitor

**Prometheus Queries**:

```promql
# Cache Hit Rate (should be > 95%)
sum(rate(meepleai_prompt_cache_hits_total[5m]))
/ (sum(rate(meepleai_prompt_cache_hits_total[5m])) + sum(rate(meepleai_prompt_cache_misses_total[5m])))

# Prompt Retrieval Latency p95 (should be < 10ms)
histogram_quantile(0.95, rate(meepleai_prompt_retrieval_duration_bucket[5m]))

# Error Rate (should be 0)
sum(rate(meepleai_prompt_errors_total[5m]))

# Activation Frequency (track prompt changes)
sum(increase(meepleai_prompt_activation_total[1d]))
```

**Seq Queries**:

```
# Prompt retrieval errors
Level = "Error" AND RequestPath LIKE "/api/v1/admin/prompts%"

# Cache misses (should be rare)
Message CONTAINS "Cache miss for prompt"

# Activation events
Message CONTAINS "Activated version" AND Level = "Information"
```

### Daily Monitoring Checklist (Week 4-6)

- [ ] Check Grafana dashboard: `http://localhost:3001/d/prompt-management`
  - Cache hit rate trend (should be flat at 95%+)
  - Latency trend (should be flat at < 10ms)
  - Error count (should be 0)

- [ ] Check Seq logs: `http://localhost:8081`
  - Filter: `Level = "Error"` AND last 24 hours
  - Verify: 0 errors related to prompts

- [ ] Check Redis health
  ```bash
  redis-cli INFO stats | grep keyspace
  # Verify: prompt:* keys exist
  ```

- [ ] Check database
  ```sql
  SELECT COUNT(*) FROM prompt_versions WHERE is_active = true;
  -- Should be 4 (one per template)
  ```

- [ ] Review AI request logs
  ```sql
  SELECT AVG(confidence) as avg_confidence,
         AVG(latency_ms) as avg_latency,
         COUNT(*) as total_requests
  FROM ai_request_logs
  WHERE endpoint = 'qa'
    AND created_at > NOW() - INTERVAL '24 hours';
  -- Verify: avg_confidence ~= baseline, avg_latency ~= baseline
  ```

### Weekly Review (Week 4-6)

- [ ] Review activation audit logs (identify frequent changes)
- [ ] Review test results (if evaluation framework operational)
- [ ] Check for user feedback (support tickets, bug reports)
- [ ] Verify backup of prompt_templates table completed
- [ ] Update migration status in project tracker

---

## Post-Migration Tasks

### Week 7: Optimization & Cleanup

- [ ] **Remove Feature Flags** (code cleanup)
  - Remove `Features:PromptDatabase` configuration
  - Remove fallback prompt methods
  - Remove conditional logic

- [ ] **Remove Hardcoded Prompts** (code cleanup)
  - Delete hardcoded prompt strings from services
  - Archive original prompts in documentation

- [ ] **Update Documentation**
  - Update CLAUDE.md (remove hardcoded prompt references)
  - Update `docs/database-schema.md` (highlight prompt tables)
  - Create admin guide: `docs/guide/admin-prompt-management-user-guide.md`

- [ ] **Enable Prompt Testing in CI**
  - Activate `.github/workflows/prompt-evaluation.yml`
  - Run first evaluation on all 4 prompts
  - Verify reports generated

- [ ] **Conduct Retrospective**
  - What went well?
  - What went wrong?
  - Lessons learned
  - Update this migration plan for future use

### Week 8: Training & Handoff

- [ ] **Admin User Training**
  - Schedule 1-hour session
  - Cover: creating templates, activating versions, running tests
  - Record session for future reference

- [ ] **On-Call Team Briefing**
  - Review runbook: `docs/runbook/prompt-management.md`
  - Practice rollback procedure
  - Review monitoring dashboards

- [ ] **Stakeholder Communication**
  - Announce successful migration
  - Share metrics (cache hit rate, latency, uptime)
  - Highlight benefits (faster updates, version history)

---

## Success Criteria (Final Validation)

After 1 month in production:

- ✅ **Reliability**
  - 99.9%+ uptime for prompt retrieval
  - 0 incidents requiring rollback
  - Cache hit rate consistently > 95%

- ✅ **Performance**
  - Prompt retrieval latency < 10ms (p95)
  - No increase in end-to-end Q&A latency
  - Database load unchanged (prompts cached)

- ✅ **Functionality**
  - 10+ prompt updates performed via admin UI
  - All prompts have passing test results
  - Audit log complete for all changes

- ✅ **User Satisfaction**
  - Admin users rate UI 8+/10
  - No user-reported issues with answer quality
  - Faster iteration on prompt improvements

---

## Contact & Escalation

**Migration Lead**: [Name]
**Email**: [email]
**Slack**: [channel]

**Escalation Path**:
1. Migration Lead (response: < 30 min)
2. Tech Lead (response: < 1 hour)
3. CTO (response: < 2 hours)

**Emergency Contact** (after hours): [Phone]

---

## Document History

- v1.0 (2025-10-18): Initial migration plan

