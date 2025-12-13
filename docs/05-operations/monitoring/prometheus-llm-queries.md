# Prometheus LLM Queries Reference

**Feature**: Issue #1725 - LLM Advanced Observability
**Metrics**: OpenTelemetry GenAI Semantic Conventions
**Version**: 1.0
**Created**: 2025-12-07

---

## Quick Reference

| Metric | Description | Labels |
|--------|-------------|--------|
| `gen_ai.client.token.usage` | Token usage (OTel standard) | gen_ai.token.type, gen_ai.request.model, gen_ai.system |
| `gen_ai.client.operation.duration` | LLM operation latency | gen_ai.request.model, gen_ai.operation.name, gen_ai.system |
| `meepleai.llm.cost.usd` | LLM cost per request | model_id, provider, user_segment, user_id_hash |
| `meepleai.agent.tokens.total` | Total tokens per agent | agent_type, model |
| `meepleai.agent.cost.usd` | Cost per agent invocation | agent_type, model |

---

## Cost Analysis Queries

### Daily/Monthly Cost Tracking

**Daily cost by model**:
```promql
sum(increase(meepleai_llm_cost_usd[1d])) by (model_id)
```

**Monthly cost by model**:
```promql
sum(increase(meepleai_llm_cost_usd[30d])) by (model_id)
```

**Hourly cost trend** (last 24 hours):
```promql
sum(increase(meepleai_llm_cost_usd[1h]))
```

**Cost by provider**:
```promql
sum(meepleai_llm_cost_usd) by (provider)
```

### Budget Monitoring

**Monthly spend vs $1000 limit**:
```promql
sum(increase(meepleai_llm_cost_usd[30d]))
```

**Remaining budget**:
```promql
1000 - sum(increase(meepleai_llm_cost_usd[30d]))
```

**Budget usage percentage**:
```promql
(sum(increase(meepleai_llm_cost_usd[30d])) / 1000) * 100
```

**Burn rate** (daily average, last 7 days):
```promql
sum(increase(meepleai_llm_cost_usd[7d])) / 7
```

**Projected monthly spend** (based on 7-day average):
```promql
(sum(increase(meepleai_llm_cost_usd[7d])) / 7) * 30
```

### Cost Efficiency

**Cost per query** (average):
```promql
sum(rate(meepleai_llm_cost_usd[5m])) / sum(rate(meepleai_agent_invocations_total[5m]))
```

**Cost per model** (average per request):
```promql
sum(rate(meepleai_llm_cost_usd[5m])) by (model_id)
/
sum(rate(meepleai_agent_invocations_total[5m])) by (model)
```

**Top 5 most expensive models** (by total cost):
```promql
topk(5, sum(meepleai_llm_cost_usd) by (model_id))
```

---

## Token Usage Queries (OpenTelemetry GenAI)

### Token Rates

**Prompt tokens/sec by model**:
```promql
sum(rate(gen_ai_client_token_usage{gen_ai_token_type="input"}[5m])) by (gen_ai_request_model)
```

**Completion tokens/sec by model**:
```promql
sum(rate(gen_ai_client_token_usage{gen_ai_token_type="output"}[5m])) by (gen_ai_request_model)
```

**Total token rate**:
```promql
sum(rate(gen_ai_client_token_usage[5m]))
```

### Token Analysis

**Completion/Prompt ratio** (verbosity indicator):
```promql
sum(rate(gen_ai_client_token_usage{gen_ai_token_type="output"}[5m]))
/
sum(rate(gen_ai_client_token_usage{gen_ai_token_type="input"}[5m]))
```

**Top token consumers** (by model):
```promql
topk(5, sum(rate(gen_ai_client_token_usage[5m])) by (gen_ai_request_model))
```

**Daily token usage** (total):
```promql
sum(increase(gen_ai_client_token_usage[1d]))
```

---

## User Attribution Queries (Issue #1725)

### Cost by User Segment

**Total cost per segment** (free, pro, enterprise, admin):
```promql
sum(meepleai_llm_cost_usd) by (user_segment)
```

**Monthly cost per segment**:
```promql
sum(increase(meepleai_llm_cost_usd[30d])) by (user_segment)
```

**Cost per segment by model**:
```promql
sum(meepleai_llm_cost_usd) by (user_segment, model_id)
```

### Individual User Tracking

**Cost by user ID hash** (top 10 users):
```promql
topk(10, sum(meepleai_llm_cost_usd) by (user_id_hash))
```

**User segment distribution**:
```promql
count(meepleai_llm_cost_usd) by (user_segment)
```

---

## Performance Analysis

### Latency vs Cost

**P50 latency by model**:
```promql
histogram_quantile(0.50, gen_ai_client_operation_duration_bucket) by (gen_ai_request_model)
```

**P95 latency by model**:
```promql
histogram_quantile(0.95, gen_ai_client_operation_duration_bucket) by (gen_ai_request_model)
```

**P99 latency by model**:
```promql
histogram_quantile(0.99, gen_ai_client_operation_duration_bucket) by (gen_ai_request_model)
```

**Cost vs latency correlation** (requires join in Grafana):
- X-axis: `histogram_quantile(0.95, gen_ai_client_operation_duration_bucket)`
- Y-axis: `sum(rate(meepleai_llm_cost_usd[5m])) by (model_id)`

### Request Volume

**LLM requests per second**:
```promql
sum(rate(meepleai_agent_invocations_total[5m]))
```

**Requests by model**:
```promql
sum(rate(meepleai_agent_invocations_total[5m])) by (model)
```

---

## Agent-Specific Queries

### Agent Metrics (Issue #1694)

**Total tokens per agent type**:
```promql
sum(meepleai_agent_tokens_total) by (agent_type)
```

**Cost per agent type**:
```promql
sum(meepleai_agent_cost_usd) by (agent_type)
```

**Average cost per agent invocation**:
```promql
sum(rate(meepleai_agent_cost_usd[5m])) by (agent_type)
/
sum(rate(meepleai_agent_invocations_total[5m])) by (agent_type)
```

---

## Alerting Rules

### Recommended Alerts

**1. Daily Budget Warning (80%)**:
```yaml
alert: LlmDailyBudgetWarning
expr: sum(increase(meepleai_llm_cost_usd[1d])) > 40
for: 5m
labels:
  severity: warning
  component: llm
annotations:
  summary: "LLM daily budget at {{ $value | printf \"%.2f\" }}% (threshold: $40)"
```

**2. Hourly Cost Spike**:
```yaml
alert: LlmHourlyCostSpike
expr: sum(increase(meepleai_llm_cost_usd[1h])) > 10
for: 10m
labels:
  severity: warning
  component: llm
annotations:
  summary: "Unusual LLM cost spike: ${{ $value | printf \"%.2f\" }}/hour"
```

**3. Token Usage Anomaly**:
```yaml
alert: LlmTokenUsageAnomaly
expr: sum(rate(gen_ai_client_token_usage[5m])) > 1000
for: 15m
labels:
  severity: info
  component: llm
annotations:
  summary: "High token usage: {{ $value | printf \"%.0f\" }} tokens/sec"
```

---

## Dashboard Integration

### Grafana Dashboards

**LLM Cost Monitoring**: `/d/llm-cost-monitoring`
- Cost by Model (timeseries)
- Token Usage Rate (prompt vs completion)
- Cost per Query (gauge)
- Budget Status (gauge)

**AI/RAG Operations**: `/d/ai-rag-operations`
- RAG query latency
- Retrieval confidence
- Agent invocations

**AI Quality Monitoring**: `/d/ai-quality-monitoring`
- Confidence score trends
- Quality tier distribution

### Panel Variables

**Model Filter** (future enhancement):
```promql
label_values(meepleai_llm_cost_usd, model_id)
```

**Provider Filter**:
```promql
label_values(meepleai_llm_cost_usd, provider)
```

**User Segment Filter**:
```promql
label_values(meepleai_llm_cost_usd, user_segment)
```

---

## Advanced Analytics

### Cost Optimization Opportunities

**Cache hit rate impact on cost**:
```promql
# Cost saved by caching (approximation)
sum(meepleai_cache_hits_total) * avg(meepleai_llm_cost_usd)
```

**Provider cost comparison**:
```promql
# OpenRouter vs Ollama cost breakdown
sum(meepleai_llm_cost_usd{provider="openrouter"})
vs
sum(meepleai_llm_cost_usd{provider="ollama"})
```

**Model efficiency ranking** (cost per token):
```promql
sum(rate(meepleai_llm_cost_usd[5m])) by (model_id)
/
sum(rate(gen_ai_client_token_usage[5m])) by (gen_ai_request_model)
```

### User Behavior Analysis

**Average spend per user segment**:
```promql
avg(meepleai_llm_cost_usd) by (user_segment)
```

**Request distribution by segment**:
```promql
count(meepleai_llm_cost_usd) by (user_segment)
```

---

## Troubleshooting

### Metrics Not Appearing

**Check Prometheus targets**:
```bash
curl http://localhost:9090/api/v1/targets | jq '.data.activeTargets[] | select(.labels.job=="meepleai-api")'
```

**Check metrics endpoint**:
```bash
curl http://localhost:8080/metrics | grep "meepleai_llm_cost_usd"
# Expected: meepleai_llm_cost_usd{model_id="...",provider="..."} X.XXXXXX
```

### Incorrect Cost Values

**Compare Prometheus vs Database**:
```sql
-- Database total (today)
SELECT SUM(total_cost_usd) FROM llm_cost_logs WHERE request_date = CURRENT_DATE;
```

```promql
# Prometheus total (today)
sum(increase(meepleai_llm_cost_usd[1d]))
```

**Difference**: Should match within 1% (scrape interval variance)

---

## Related Documentation

- [LLM Cost Tracking API](../../03-api/llm-cost-tracking-api.md)
- [Budget Alert Runbook](../runbooks/llm-budget-alerts.md)
- [Grafana Dashboard Guide](./grafana-llm-cost-dashboard.md)
- [Issue #1725](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1725)

---

**Version**: 1.0
**Last Updated**: 2025-12-13T10:59:23.970Z
**Owner**: Platform Team

