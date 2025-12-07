# Grafana LLM Cost Monitoring Dashboard

**Feature**: Issue #1725 - LLM Advanced Observability
**Dashboard**: `llm-cost-monitoring.json`
**Status**: ✅ Production Ready
**Version**: 1.0
**Created**: 2025-12-07

---

## Overview

Real-time LLM cost monitoring dashboard with 4 panels for comprehensive observability:
1. **LLM Cost by Model** - Timeseries cost tracking
2. **Token Usage Rate** - Prompt vs completion analysis
3. **Model Performance** - Cost per query efficiency
4. **Budget Status** - Monthly spend vs limit

**Auto-Import**: Provisioned via Docker Compose Grafana configuration

---

## Dashboard Panels

### Panel 1: LLM Cost by Model (Real-Time)

**Type**: Time Series
**Metric**: `meepleai_llm_cost_usd`
**Aggregations**: Hourly, Daily, Monthly

**Queries**:
```promql
# Hourly cost by model
sum(increase(meepleai_llm_cost_usd[1h])) by (model_id)

# Daily cost by model
sum(increase(meepleai_llm_cost_usd[1d])) by (model_id)

# Monthly cost by model
sum(increase(meepleai_llm_cost_usd[30d])) by (model_id)
```

**Use Case**: Identify which models drive highest costs

**Alert Threshold**: Yellow >$10/hour, Red >$50/hour

---

### Panel 2: Token Usage Rate (Prompt vs Completion)

**Type**: Time Series
**Metric**: `gen_ai.client.token.usage`
**Breakdown**: Input (prompt) vs Output (completion)

**Queries**:
```promql
# Prompt tokens/sec by model
sum(rate(gen_ai_client_token_usage{gen_ai_token_type="input"}[5m])) by (gen_ai_request_model)

# Completion tokens/sec by model
sum(rate(gen_ai_client_token_usage{gen_ai_token_type="output"}[5m])) by (gen_ai_request_model)
```

**Use Case**:
- Monitor token consumption patterns
- Identify models with high completion/prompt ratios
- Detect anomalous token usage

**Insights**:
- High completion/prompt ratio → Verbose models (optimization opportunity)
- Sudden spikes → Potential runaway generation

---

### Panel 3: Model Performance (Cost per Query)

**Type**: Gauge
**Metric**: Cost efficiency (cost/query ratio)

**Query**:
```promql
sum(rate(meepleai_llm_cost_usd[5m])) / sum(rate(meepleai_agent_invocations_total[5m]))
```

**Thresholds**:
- **Green**: <$0.01 per query
- **Yellow**: $0.01-$0.05 per query
- **Red**: >$0.05 per query

**Use Case**:
- Monitor average cost per user interaction
- Identify cost inefficiencies
- Compare model cost/quality trade-offs

**Target**: <$0.02 per query (Phase 4 - 10K MAU)

---

### Panel 4: Budget Status (Monthly)

**Type**: Gauge
**Metric**: Monthly spend vs $1000 limit

**Queries**:
```promql
# Monthly spend (accumulated)
sum(increase(meepleai_llm_cost_usd[30d]))

# Remaining budget
1000 - sum(increase(meepleai_llm_cost_usd[30d]))
```

**Thresholds**:
- **Green**: <$800 (80%)
- **Yellow**: $800-$950 (80-95%)
- **Red**: >$950 (95%)

**Use Case**:
- Real-time budget tracking
- Visual confirmation of budget alerts
- Month-to-date spend monitoring

**Auto-Refresh**: 30 seconds

---

## Access & Navigation

### URL
```
http://localhost:3001/d/llm-cost-monitoring/llm-cost-monitoring
```

**From Grafana Home**:
1. Click "Dashboards" (sidebar)
2. Search "LLM Cost"
3. Select "LLM Cost Monitoring"

### Filters & Variables
**Current**: No dashboard variables (global view)

**Future Enhancements**:
- Model filter (dropdown: all models)
- Provider filter (OpenRouter vs Ollama)
- User segment filter (free, pro, enterprise, admin)
- Time range presets (6h, 24h, 7d, 30d)

---

## Advanced Prometheus Queries

### Cost Analysis

**Cost by Provider**:
```promql
sum(meepleai_llm_cost_usd) by (provider)
```

**Cost by User Segment** (Issue #1725):
```promql
sum(meepleai_llm_cost_usd) by (user_segment)
```

**Hourly Cost Trend** (last 24 hours):
```promql
sum(increase(meepleai_llm_cost_usd[1h]))
```

### Token Analysis

**Token Efficiency** (completion/prompt ratio):
```promql
sum(rate(gen_ai_client_token_usage{gen_ai_token_type="output"}[5m]))
/
sum(rate(gen_ai_client_token_usage{gen_ai_token_type="input"}[5m]))
```

**Top Token Consumers**:
```promql
topk(5, sum(rate(gen_ai_client_token_usage[5m])) by (gen_ai_request_model))
```

### Performance vs Cost

**Latency vs Cost Scatter**:
```promql
# P95 latency
histogram_quantile(0.95, gen_ai_client_operation_duration_bucket)

# vs Cost per query
sum(rate(meepleai_llm_cost_usd[5m])) / sum(rate(meepleai_agent_invocations_total[5m]))
```

---

## Alerts Configuration (Grafana)

### Recommended Alerts

**1. Daily Budget Approaching**:
```yaml
Condition: sum(increase(meepleai_llm_cost_usd[1d])) > 40
Severity: Warning
Notification: Slack #cost-alerts
```

**2. Hourly Cost Spike**:
```yaml
Condition: sum(increase(meepleai_llm_cost_usd[1h])) > 10
Severity: Warning
Notification: Slack #engineering
```

**3. Token Usage Anomaly**:
```yaml
Condition: sum(rate(gen_ai_client_token_usage[5m])) > 1000
Severity: Info
Notification: Slack #observability
```

---

## Dashboard Maintenance

### Auto-Provisioning

**File**: `infra/dashboards/llm-cost-monitoring.json`

**Docker Compose**:
```yaml
grafana:
  volumes:
    - ./dashboards:/etc/grafana/provisioning/dashboards
```

**Provisioning**: Automatic on Grafana startup

### Manual Import

1. Login to Grafana (http://localhost:3001)
2. Click "+" → "Import dashboard"
3. Upload `infra/dashboards/llm-cost-monitoring.json`
4. Select Prometheus datasource
5. Click "Import"

### Updates

**Process**:
1. Edit `infra/dashboards/llm-cost-monitoring.json`
2. Restart Grafana: `docker compose restart grafana`
3. Verify changes at dashboard URL

**Versioning**: Dashboard JSON includes `"version": 1` field (increment on major changes)

---

## Troubleshooting

### Dashboard Not Visible

**Check**:
```bash
docker logs meepleai-grafana 2>&1 | grep "llm-cost-monitoring"
```

**Fix**: Verify file exists and provisioning directory mounted

### No Data in Panels

**Check Prometheus**:
```bash
curl http://localhost:9090/api/v1/query?query=meepleai_llm_cost_usd | jq '.data.result | length'
# Expected: >0
```

**Check Time Range**: Ensure dashboard time range includes data (default: last 6 hours)

**Check Datasource**: Verify Prometheus datasource configured (`uid: "prometheus"`)

### Incorrect Costs

**Validate Metrics**:
```bash
# Check raw Prometheus data
curl http://localhost:9090/api/v1/query?query=meepleai_llm_cost_usd | jq '.data.result[] | {model: .metric.model_id, value: .value[1]}'
```

**Compare with Database**:
```sql
SELECT SUM(total_cost_usd) FROM llm_cost_logs WHERE request_date = CURRENT_DATE;
```

---

## Future Enhancements

### Phase 5 Additions
- [ ] Per-user cost breakdown (user_id_hash labels)
- [ ] Model recommendation based on cost/quality trade-offs
- [ ] Forecasting panel (ML-based spend prediction)
- [ ] Cache effectiveness correlation
- [ ] Cost optimization suggestions

---

## Related Documentation

- [Budget Alert Runbook](../runbooks/llm-budget-alerts.md)
- [LLM Cost Tracking API](../../03-api/llm-cost-tracking-api.md)
- [Logging & Observability Guide](../../02-development/logging-and-observability-guide.md)
- [Issue #1725: Advanced Observability](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1725)

---

**Version**: 1.0
**Last Updated**: 2025-12-07
**Owner**: Platform Team
