# LLM Budget Alerts Runbook

**Feature**: Issue #1725 - LLM Budget Monitoring & Alerting
**Service**: `LlmBudgetMonitoringService`
**Status**: ✅ Production Ready
**Version**: 1.0
**Created**: 2025-12-07

---

## Overview

Automated background service monitoring LLM spend against configured budgets with multi-tier alerting for cost control.

**Key Features**:
- Daily ($50) + Monthly ($1000) budget tracking
- Warning (80%) + Critical (95%) thresholds
- Hourly checks with IAlertingService integration
- Email/Slack/PagerDuty notifications

---

## Architecture

```
LlmBudgetMonitoringService (Hosted Service)
    ├─ Every 60 minutes (configurable)
    ├─ CheckBudgetThresholdsAsync()
    │   ├─ Check Daily: GetTotalCostAsync(today)
    │   └─ Check Monthly: GetTotalCostAsync(month start → now)
    └─ ProcessThresholdAsync()
        ├─ If ≥ 95%: SendAlertAsync(severity="Error")
        └─ If ≥ 80%: SendAlertAsync(severity="Warning")
```

---

## Configuration

### appsettings.json
```json
"LlmBudgetAlerts": {
  "DailyBudgetUsd": 50.00,
  "MonthlyBudgetUsd": 1000.00,
  "Thresholds": {
    "Warning": 0.80,
    "Critical": 0.95
  },
  "CheckIntervalMinutes": 60
}
```

### Environment Overrides
```bash
# .env or Docker environment
LLMBUDGETALERTS__DAILYBUDGETUSD=100.00
LLMBUDGETALERTS__MONTHLYBUDGETUSD=2000.00
LLMBUDGETALERTS__THRESHOLDS__WARNING=0.75
LLMBUDGETALERTS__THRESHOLDS__CRITICAL=0.90
LLMBUDGETALERTS__CHECKINTERVALMINUTES=30
```

---

## Alert Scenarios

### Scenario 1: Daily Budget Warning (80%)
**Trigger**: Daily spend ≥ $40.00 (80% of $50)

**Alert**:
```
🚨 LLM Budget Alert - Warning

Period: Daily
Actual Spend: $42.50
Budget Limit: $50.00
Usage: 85%

Threshold: 80%
```

**Action**: Review daily spend breakdown, investigate anomalies

### Scenario 2: Daily Budget Critical (95%)
**Trigger**: Daily spend ≥ $47.50 (95% of $50)

**Alert**:
```
🚨 LLM Budget Alert - Critical

Period: Daily
Actual Spend: $48.75
Budget Limit: $50.00
Usage: 98%

Threshold: 95%
```

**Action**:
1. Immediate review of high-cost queries
2. Consider temporary model downgrade
3. Check for runaway processes

### Scenario 3: Monthly Budget Critical (95%)
**Trigger**: Monthly spend ≥ $950 (95% of $1000)

**Alert**:
```
🚨 LLM Budget Alert - Critical

Period: Monthly
Actual Spend: $987.50
Budget Limit: $1000.00
Usage: 99%

Threshold: 95%
```

**Action**:
1. Freeze non-essential LLM operations
2. Review cost optimization opportunities
3. Plan budget increase for next month

---

## Troubleshooting

### Alert Not Triggering

**Symptom**: Spend exceeds threshold but no alert received

**Checks**:
1. Verify service is running:
```bash
docker logs meepleai-api 2>&1 | grep "LlmBudgetMonitoringService"
# Expected: "LLM Budget Monitoring Service starting..."
```

2. Check configuration:
```bash
curl http://localhost:8080/admin/config | jq '.LlmBudgetAlerts'
```

3. Verify IAlertingService enabled:
```bash
curl http://localhost:8080/admin/config | jq '.Alerting.Enabled'
```

4. Check alert throttling:
```sql
-- Query: Last budget alerts sent
SELECT alert_type, severity, created_at
FROM alerts
WHERE alert_type LIKE 'LlmBudget%'
ORDER BY created_at DESC
LIMIT 5;
```

**Fix**: Ensure `Alerting.Enabled = true` and at least one channel configured (Email/Slack/PagerDuty)

---

### Service Not Starting

**Symptom**: No budget monitoring logs in application output

**Checks**:
1. Verify DI registration:
```bash
grep -r "AddHostedService<LlmBudgetMonitoringService>" apps/api/src/Api/
# Expected: KnowledgeBaseServiceExtensions.cs:149
```

2. Check for startup errors:
```bash
docker logs meepleai-api 2>&1 | grep -i "error.*budget"
```

**Fix**: Ensure service registered in `KnowledgeBaseServiceExtensions.AddKnowledgeBaseServices()`

---

### Incorrect Cost Calculations

**Symptom**: Budget alerts triggered prematurely or too late

**Checks**:
1. Verify GetTotalCostAsync query:
```sql
-- Manual cost check (today)
SELECT SUM(total_cost_usd)
FROM llm_cost_logs
WHERE request_date = CURRENT_DATE;
```

2. Compare with Prometheus:
```promql
sum(increase(meepleai_llm_cost_usd[1d]))
```

3. Check date range logic:
```bash
docker logs meepleai-api 2>&1 | grep "Budget status"
# Expected: "Daily budget status: $X.XX / $50.00 (XX%)"
```

**Fix**: If mismatch, verify ILlmCostLogRepository.GetTotalCostAsync implementation

---

### Alert Spam / Throttling Issues

**Symptom**: Receiving alerts every hour for same threshold

**Expected Behavior**: IAlertingService throttles to 1 alert/hour per alert type

**Checks**:
1. Verify throttle configuration:
```bash
curl http://localhost:8080/admin/config | jq '.Alerting.ThrottleMinutes'
# Expected: 60
```

2. Check alert history:
```sql
SELECT alert_type, COUNT(*), MIN(created_at), MAX(created_at)
FROM alerts
WHERE alert_type LIKE 'LlmBudget%'
  AND created_at > NOW() - INTERVAL '24 hours'
GROUP BY alert_type;
```

**Fix**: Adjust `Alerting.ThrottleMinutes` or implement cost-based de-duplication

---

## Monitoring Queries

### Prometheus

**Current Budget Status**:
```promql
# Monthly spend vs $1000 limit
sum(increase(meepleai_llm_cost_usd[30d]))

# Daily spend vs $50 limit
sum(increase(meepleai_llm_cost_usd[1d]))

# Budget remaining (monthly)
1000 - sum(increase(meepleai_llm_cost_usd[30d]))
```

**Burn Rate**:
```promql
# Daily average spend (last 7 days)
sum(increase(meepleai_llm_cost_usd[7d])) / 7

# Projected monthly spend (based on current rate)
(sum(increase(meepleai_llm_cost_usd[7d])) / 7) * 30
```

### Database

**Daily Cost Summary**:
```sql
SELECT
    request_date,
    SUM(total_cost_usd) AS daily_cost,
    COUNT(*) AS request_count,
    SUM(total_tokens) AS total_tokens
FROM llm_cost_logs
WHERE request_date >= CURRENT_DATE - INTERVAL '7 days'
GROUP BY request_date
ORDER BY request_date DESC;
```

**Top Cost Drivers**:
```sql
SELECT
    model_id,
    provider,
    COUNT(*) AS requests,
    SUM(total_cost_usd) AS total_cost,
    AVG(total_cost_usd) AS avg_cost_per_request
FROM llm_cost_logs
WHERE request_date = CURRENT_DATE
GROUP BY model_id, provider
ORDER BY total_cost DESC
LIMIT 10;
```

---

## Response Procedures

### ⚠️ Warning Alert (80% threshold)

**Priority**: P2 (Review within 4 hours)

**Actions**:
1. **Review Grafana Dashboard**: Check cost trends and anomalies
2. **Identify High-Cost Queries**: Use Prometheus/Database queries
3. **Assess Impact**: Determine if legitimate usage spike or wasteful
4. **Plan Optimization**: Cache improvements, model downgrade, query reduction
5. **Monitor**: Watch for critical threshold approach

**Escalation**: If approaching critical threshold, escalate to P1

### 🚨 Critical Alert (95% threshold)

**Priority**: P1 (Immediate action required)

**Actions**:
1. **Immediate Review**: Stop non-essential LLM operations
2. **Cost Analysis**: Identify top 10 cost drivers (last hour)
3. **Mitigation Options**:
   - **Option A**: Temporary model downgrade (GPT-4o-mini → Llama 3.3 free)
   - **Option B**: Rate limiting (reduce concurrent LLM calls)
   - **Option C**: Cache aggressive (increase TTL, expand coverage)
4. **Communication**: Notify team of temporary restrictions
5. **Budget Review**: Evaluate if threshold adjustment needed

**Escalation**: If budget exceeded, escalate to engineering lead + finance

---

## Configuration Tuning

### Adjusting Budgets

**Scenario**: Current $50 daily / $1000 monthly too restrictive

**Solution**: Update appsettings.json:
```json
"LlmBudgetAlerts": {
  "DailyBudgetUsd": 100.00,      // Doubled
  "MonthlyBudgetUsd": 2000.00    // Doubled
}
```

**Deployment**: Configuration reload (no restart required if using dynamic config)

### Adjusting Thresholds

**Scenario**: Want earlier warnings (70%) + later critical (98%)

**Solution**:
```json
"Thresholds": {
  "Warning": 0.70,   // 70% threshold
  "Critical": 0.98   // 98% threshold
}
```

### Adjusting Check Frequency

**Scenario**: Need more frequent checks during high-traffic periods

**Solution**:
```json
"CheckIntervalMinutes": 30  // Check every 30 minutes
```

**Note**: More frequent checks → more alert potential (consider throttle settings)

---

## Integration with Grafana

**Dashboard**: `llm-cost-monitoring` (UID: `llm-cost-monitoring`)

**Budget Status Panel**: Shows real-time budget consumption with color-coded thresholds
- **Green**: <80%
- **Yellow**: 80-95%
- **Red**: >95%

**Alert Correlation**: Grafana alerts can be configured to match LlmBudgetMonitoringService thresholds for visual confirmation

---

## Cost Optimization Strategies

### When Approaching Thresholds

1. **Model Downgrade**: Switch expensive models to cheaper alternatives
   - Claude Haiku → GPT-4o-mini (5-10x cheaper)
   - GPT-4o-mini → Llama 3.3 70B free (100x cheaper)

2. **Cache Optimization**: Increase cache hit rate
   - Extend TTL from 86400s (24h) to 172800s (48h)
   - Implement semantic cache for similar queries

3. **Query Reduction**: Batch operations, reduce redundant calls
   - Combine multiple RAG queries into single request
   - Pre-compute common answers

4. **Provider Optimization**: Use free tier aggressively
   - Increase Ollama routing from 80% → 95%
   - Use OpenRouter free models (Llama 3.3 70B)

---

## Testing

### Manual Alert Trigger (Staging)

```bash
# 1. Temporarily lower daily budget to $1
curl -X PUT http://localhost:8080/admin/config \
  -H "Cookie: session_id=admin" \
  -H "Content-Type: application/json" \
  -d '{"key":"LlmBudgetAlerts:DailyBudgetUsd","value":1.0}'

# 2. Generate LLM requests to exceed $0.80 (80% of $1)
for i in {1..10}; do
  curl -X POST http://localhost:8080/api/v1/chat \
    -H "Cookie: session_id=..." \
    -H "Content-Type: application/json" \
    -d '{"gameId":"catan","query":"How to win?"}' &
done

# 3. Wait for hourly check or manually trigger (if endpoint exists)
# Expected: Warning alert within 60 minutes

# 4. Restore original budget
curl -X PUT http://localhost:8080/admin/config \
  -H "Cookie: session_id=admin" \
  -H "Content-Type: application/json" \
  -d '{"key":"LlmBudgetAlerts:DailyBudgetUsd","value":50.0}'
```

---

## Metrics Reference

### OpenTelemetry Metrics (Issue #1694 + #1725)

**Token Usage**:
- `gen_ai.client.token.usage{gen_ai.token.type="input"}` - Prompt tokens
- `gen_ai.client.token.usage{gen_ai.token.type="output"}` - Completion tokens

**Cost Tracking**:
- `meepleai.llm.cost.usd{model_id, provider}` - Per-request cost
- `meepleai.llm.cost.usd{model_id, provider, user_segment}` - With user attribution

**Operations**:
- `gen_ai.client.operation.duration{gen_ai.request.model}` - LLM latency
- `meepleai.agent.invocations.total` - Total agent calls

---

## Related Documentation

- [LLM Cost Tracking API](../../03-api/llm-cost-tracking-api.md)
- [Grafana LLM Cost Dashboard](../monitoring/grafana-llm-cost-dashboard.md)
- [ADR-015: HyperDX Observability](../../01-architecture/adr/adr-015-hyperdx-observability.md)
- [Issue #1725](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1725)
- [Issue #1694](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1694)

---

**Version**: 1.0
**Last Updated**: 2025-12-07
**Owner**: Platform Team
