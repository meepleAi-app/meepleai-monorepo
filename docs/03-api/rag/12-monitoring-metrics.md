# Monitoring & Metrics

---

## Prometheus Metrics

### Token Tracking
```python
rag_tokens_total = Counter('rag_tokens_total', '', ['strategy', 'template', 'user_tier'])
rag_tokens_input = Histogram('rag_tokens_input', '', ['strategy'])
rag_tokens_output = Histogram('rag_tokens_output', '', ['strategy'])
rag_tokens_saved_cache = Counter('rag_tokens_saved_cache', '')
```

### Performance
```python
rag_latency_ms = Histogram('rag_latency_ms', '', ['strategy'])
rag_cache_hit_rate = Gauge('rag_cache_hit_rate', '', ['user_tier'])
rag_strategy_distribution = Counter('rag_strategy_distribution', '', ['strategy', 'user_tier'])
rag_escalations = Counter('rag_escalations', '', ['from_strategy', 'to_strategy'])
```

### Cost
```python
rag_cost_usd = Counter('rag_cost_usd', '', ['strategy', 'model', 'user_tier'])
rag_daily_budget_utilization = Gauge('rag_daily_budget_pct', '')
```

### Quality
```python
rag_confidence_score = Histogram('rag_confidence', '', ['strategy', 'template'])
rag_citation_recall = Histogram('rag_citation_recall', '', ['strategy'])
rag_crag_evaluation = Counter('rag_crag_eval_category', '', ['category'])  # correct/ambiguous/incorrect
```

---

## Grafana Dashboard

**Panels**:
1. Token consumption by strategy (time series)
2. Cache hit rate (gauge, target 80%)
3. Cost per query (bar chart by strategy)
4. Strategy distribution (pie chart)
5. Escalation rate (line graph)
6. P95 latency by strategy (heatmap)

---

## Alerts

```yaml
- alert: HighTokenConsumption
  expr: avg(rag_tokens_total) > 2500
  for: 5m
  annotations:
    summary: "Avg tokens > 2,500 (target: 1,310)"

- alert: LowCacheHitRate
  expr: rag_cache_hit_rate < 0.6
  for: 10m
  annotations:
    summary: "Cache hit < 60% (target: 80%)"

- alert: DailyBudgetExceeded
  expr: rag_daily_budget_utilization > 0.9
  annotations:
    summary: "Daily budget at 90%"
```

---

**Back**: [Overview](00-overview.md)
