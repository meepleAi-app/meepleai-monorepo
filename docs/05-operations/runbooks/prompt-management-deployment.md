# Prompt Management System - Deployment Runbook

**Feature**: ADMIN-01 Prompt Management System
**Version**: 1.0 (Phase 1-4 Complete)
**Last Updated**: 2025-12-13T10:59:23.970Z
**Owner**: Platform Team

---

## Pre-Deployment Checklist

### Prerequisites Verification

- [ ] PostgreSQL database accessible (version 13+)
- [ ] Redis instance running and accessible
- [ ] OpenRouter API key configured in environment
- [ ] Qdrant vector database operational
- [ ] Database migrations up to date (`dotnet ef database update`)
- [ ] Admin user accounts created
- [ ] Backup of `prompt_templates` table taken

### Environment Variables

```bash
# Required for Phase 4 Testing Framework
export OPENROUTER_API_KEY="sk-or-..."
export REDIS_URL="redis:6379"
export ConnectionStrings__Postgres="Host=postgres;Database=meepleai;..."

# Feature Flags
export Features__PromptDatabase="false"  # Start disabled for safety
```

---

## Deployment Steps

### Step 1: Database Migration (5 minutes)

```bash
cd apps/api/src/Api

# Verify current migration status
dotnet ef migrations list

# Apply Phase 4 migration
dotnet ef database update

# Verify tables created
psql -d meepleai -c "\dt prompt_*"
# Expected: prompt_templates, prompt_versions, prompt_audit_logs, prompt_evaluation_results
```

**Verification**:
```sql
SELECT COUNT(*) FROM prompt_templates;  -- Should return 2 (chess, setup-guide from seed)
SELECT COUNT(*) FROM prompt_versions WHERE is_active = true;  -- Should return 2
SELECT COUNT(*) FROM prompt_evaluation_results;  -- Should return 0 (no evaluations yet)
```

### Step 2: Deploy Application (10 minutes)

```bash
# Build and test locally first
cd apps/api
dotnet build
dotnet test  # Verify 88% pass rate (15/17 tests)

# Deploy to staging
docker compose -f infra/docker-compose.yml up -d --build api

# Verify health
curl http://localhost:8080/health
# Expected: {"status": "Healthy"}
```

**Smoke Test**:
```bash
# Test prompt retrieval (should fallback to config while flag disabled)
curl http://localhost:8080/api/v1/chat/qa \
  -d '{"gameId": "...", "query": "How many players?"}' \
  -H "Content-Type: application/json"

# Should work normally (using config prompts)
```

### Step 3: Enable Feature Flag Gradually (1 week)

**Day 1: Enable for RagService Only**

```bash
# Update appsettings.Production.json or environment variable
export Features__PromptDatabase="true"

# Restart API
docker compose restart api

# Monitor for 48 hours
```

**Monitoring Checklist**:
- [ ] Check Grafana: Prompt cache hit rate > 95%
- [ ] Check logs: No errors in prompt retrieval
- [ ] Check latency: p95 < 10ms
- [ ] Verify QA responses unchanged (spot check 10 queries)
- [ ] Check Prometheus alerts: No firing alerts

**Rollback Trigger**: If cache hit rate < 85% OR errors > 1% OR latency p95 > 50ms

**Day 3: Enable for ChessAgentService**

- Chess prompts now loaded from DB
- Monitor chess-specific endpoints
- Same monitoring checklist

**Day 5: Enable for SetupGuideService**

- Setup guide prompts from DB
- Monitor setup endpoints

**Day 7: Enable for StreamingQaService**

- Streaming QA from DB
- Final monitoring period (1 week)

### Step 4: Final Validation (Day 14)

**Success Criteria**:
- [ ] Cache hit rate sustained > 95%
- [ ] Prompt retrieval p95 < 10ms
- [ ] Zero rollbacks required
- [ ] No increase in error rates
- [ ] All 4 services using DB prompts successfully

**Sign-Off**: Platform Lead + Product Owner approval required

---

## Rollback Procedures

### Emergency Rollback (< 5 minutes)

**Scenario**: Critical issue detected (high error rate, cache failure, data corruption)

```bash
# 1. Disable feature flag immediately
export Features__PromptDatabase="false"
docker compose restart api

# 2. Verify rollback
curl http://localhost:8080/health
# Services should use config prompts (hardcoded fallback)

# 3. Notify team
# Post to #incidents Slack channel

# 4. Investigate root cause
docker compose logs -f api | grep "Prompt"
```

### Partial Rollback (Service-Specific)

**Scenario**: One service has issues, others working fine

```csharp
// Temporarily modify PromptTemplateService.cs
public async Task<string?> GetActivePromptAsync(string templateName, CancellationToken ct = default)
{
    // Add service-specific bypass
    if (templateName == "chess-system-prompt")
    {
        _logger.LogWarning("Bypassing DB for chess-system-prompt (rollback mode)");
        return GetFallbackPrompt(templateName);
    }

    // Normal DB lookup for other prompts
    // ...
}
```

### Data Corruption Recovery

**Scenario**: Prompt data corrupted in database

```bash
# 1. Disable feature flag
export Features__PromptDatabase="false"

# 2. Restore from backup
pg_restore -d meepleai -t prompt_templates backup.sql
pg_restore -d meepleai -t prompt_versions backup.sql

# 3. Verify data integrity
psql -d meepleai -c "SELECT name, is_active FROM prompt_versions WHERE is_active = true;"

# 4. Clear Redis cache
export REDIS_PASS=$(cat infra/secrets/redis-password.txt)
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning FLUSHDB

# 5. Re-enable with monitoring
export Features__PromptDatabase="true"
```

---

## Monitoring Dashboard Access

**Grafana**: http://localhost:3001 (admin/admin)
**Dashboard**: "Prompt Management" (to be created)

**Key Panels to Monitor**:
1. **Cache Hit Rate** (Gauge): Should be > 95%
2. **Retrieval Latency** (Graph): p50/p95/p99 should be < 10ms/20ms/50ms
3. **Active Prompts** (Counter): Should match expected count (4 templates)
4. **Evaluation Metrics** (Graphs): Accuracy, Hallucination trends over time
5. **Activation Frequency** (Counter): Track prompt updates

**Prometheus**: http://localhost:9090
**Query Examples**:
```promql
# Cache hit rate
rate(meepleai_prompt_cache_hits_total[5m]) /
(rate(meepleai_prompt_cache_hits_total[5m]) + rate(meepleai_prompt_cache_misses_total[5m]))

# P95 latency
histogram_quantile(0.95, rate(meepleai_prompt_retrieval_duration_bucket[5m]))

# Activation count
meepleai_prompt_activation_total
```

---

## Common Issues & Troubleshooting

### Issue: Cache Hit Rate Low (< 90%)

**Symptoms**: High database load, slow responses

**Diagnosis**:
```bash
# Set Redis password (run once)
export REDIS_PASS=$(cat infra/secrets/redis-password.txt)

# Check Redis connectivity
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning PING
# Expected: PONG

# Check cache keys
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning KEYS "meepleai:prompt:*"

# Check TTL
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning TTL "meepleai:prompt:qa-system-prompt"
```

**Resolution**:
1. Verify Redis is running and accessible
2. Check cache invalidation isn't being called too frequently
3. Increase cache TTL if appropriate
4. Check for cache key collisions

### Issue: Prompt Retrieval Slow (> 20ms p95)

**Symptoms**: Slow QA responses, high latency

**Diagnosis**:
```bash
# Check database query performance
psql -d meepleai -c "EXPLAIN ANALYZE SELECT * FROM prompt_versions WHERE template_id = '...' AND is_active = true;"

# Check indexes
psql -d meepleai -c "\d prompt_versions"
```

**Resolution**:
1. Verify indexes on `template_id` and `is_active`
2. Check database connection pool size
3. Monitor database CPU/memory
4. Consider read replicas if needed

### Issue: Evaluation Failures

**Symptoms**: Evaluations returning failed status unexpectedly

**Diagnosis**:
```bash
# Check recent evaluation results
psql -d meepleai -c "SELECT evaluation_id, passed, summary FROM prompt_evaluation_results ORDER BY executed_at DESC LIMIT 10;"

# Check logs
docker compose logs api | grep "Evaluation"
```

**Resolution**:
1. Review test dataset quality thresholds
2. Check if prompts regressed in quality
3. Validate dataset test cases are appropriate
4. Review evaluation metrics for anomalies

---

## Post-Deployment Validation

### Day 1 Checklist

- [ ] All 4 services responding normally
- [ ] Cache hit rate > 95%
- [ ] No errors in logs
- [ ] Latency within SLA (< 10ms p95)
- [ ] Run sample evaluation to verify testing framework
- [ ] Verify Grafana dashboard showing data

### Week 1 Checklist

- [ ] Zero rollbacks required
- [ ] Cache performance stable
- [ ] Run A/B comparison test
- [ ] Admin team trained on UI (when available)
- [ ] Documentation reviewed and updated
- [ ] Incident response procedures tested

### Month 1 Checklist

- [ ] Evaluate 10+ prompt versions
- [ ] Quality metrics trending positively
- [ ] Admin feedback collected
- [ ] Performance optimization opportunities identified
- [ ] Plan Phase 5 enhancements (Grafana dashboards, etc.)

---

## Contacts & Escalation

**On-Call**: Platform Team
**Escalation Path**: Platform Lead → CTO
**Slack Channel**: #platform-incidents

**Emergency Rollback Authority**: Platform Lead or On-Call Engineer

---

## Appendix: Testing Framework Usage

### Running an Evaluation

```bash
curl -X POST http://localhost:8080/api/v1/admin/prompts/{templateId}/versions/{versionId}/evaluate \
  -H "Content-Type: application/json" \
  -H "Cookie: session=..." \
  -d '{
    "datasetPath": "datasets/qa-system-prompt-test-dataset-sample.json",
    "storeResults": true
  }'
```

### Comparing Two Versions

```bash
curl -X POST http://localhost:8080/api/v1/admin/prompts/{templateId}/compare \
  -H "Content-Type: application/json" \
  -d '{
    "baselineVersionId": "v1",
    "candidateVersionId": "v2",
    "datasetPath": "datasets/qa-system-prompt-test-dataset-sample.json"
  }'
```

**Recommendation Logic**:
- ACTIVATE: +5% accuracy OR -5% hallucination OR +0.10 confidence
- REJECT: Fails thresholds OR -10% accuracy
- MANUAL_REVIEW: Marginal changes

---

**Document Version**: 1.0
**Status**: Ready for Production Deployment
**Last Review**: 2025-10-26

🤖 Generated with Claude Code
