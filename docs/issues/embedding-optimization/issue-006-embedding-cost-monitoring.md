# Issue #6: Embedding Cost Monitoring Dashboard

**Priority**: 🟢 Medium
**Category**: Observability
**Effort**: 6-8 hours
**Impact**: Medium - Cost visibility & optimization insights

---

## 📋 Problem

No visibility into embedding costs:
- How much are we spending on embeddings?
- Which provider is being used?
- What's the cache hit rate?
- Are costs trending up or down?

**Current State**: Blind to embedding costs ❌

---

## 🎯 Solution

**Grafana dashboard** with real-time embedding cost tracking:

1. **Prometheus metrics** for embedding operations
2. **Cost calculation** per provider/model
3. **Grafana dashboard** with 5 panels

---

## 🛠️ Implementation

### 1. Prometheus Metrics
```csharp
// File: Observability/EmbeddingMetrics.cs

public static class EmbeddingMetrics
{
    private static readonly Counter EmbeddingsGenerated = Metrics.CreateCounter(
        "embeddings_generated_total",
        "Total embeddings generated",
        new CounterConfiguration
        {
            LabelNames = new[] { "provider", "model", "operation" }
        });

    private static readonly Counter EmbeddingCost = Metrics.CreateCounter(
        "embedding_cost_usd_total",
        "Total embedding cost in USD",
        new CounterConfiguration
        {
            LabelNames = new[] { "provider", "model" }
        });

    private static readonly Histogram EmbeddingLatency = Metrics.CreateHistogram(
        "embedding_latency_seconds",
        "Embedding generation latency",
        new HistogramConfiguration
        {
            LabelNames = new[] { "provider", "cached" },
            Buckets = Histogram.ExponentialBuckets(0.01, 2, 10) // 10ms to 10s
        });

    private static readonly Counter CacheHits = Metrics.CreateCounter(
        "embedding_cache_hits_total",
        "Total embedding cache hits");

    private static readonly Counter CacheMisses = Metrics.CreateCounter(
        "embedding_cache_misses_total",
        "Total embedding cache misses");

    public static void RecordEmbedding(string provider, string model, string operation, decimal costUsd)
    {
        EmbeddingsGenerated.WithLabels(provider, model, operation).Inc();
        EmbeddingCost.WithLabels(provider, model).Inc(Convert.ToDouble(costUsd));
    }

    public static void RecordLatency(string provider, bool cached, double seconds)
    {
        EmbeddingLatency.WithLabels(provider, cached ? "true" : "false").Observe(seconds);
    }

    public static void RecordCacheHit() => CacheHits.Inc();
    public static void RecordCacheMiss() => CacheMisses.Inc();
}
```

### 2. Instrument EmbeddingService
```csharp
// File: Services/EmbeddingService.cs

public async Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
{
    var stopwatch = Stopwatch.StartNew();

    var result = await _inner.GenerateEmbeddingAsync(text, ct);

    stopwatch.Stop();

    if (result.Success)
    {
        // Calculate cost
        var tokenCount = EstimateTokenCount(text);
        var costPer1M = GetCostPer1MTokens(_provider, _embeddingModel);
        var cost = (tokenCount / 1_000_000.0m) * costPer1M;

        // Record metrics
        EmbeddingMetrics.RecordEmbedding(_provider, _embeddingModel, "query", cost);
        EmbeddingMetrics.RecordLatency(_provider, false, stopwatch.Elapsed.TotalSeconds);
    }

    return result;
}

private int EstimateTokenCount(string text)
{
    // Rough estimate: 1 token ≈ 4 characters
    return (int)(text.Length / 4.0);
}
```

### 3. Grafana Dashboard JSON
```json
// File: infra/grafana/dashboards/embedding-costs.json
{
  "dashboard": {
    "title": "Embedding Cost Monitoring",
    "panels": [
      {
        "id": 1,
        "title": "Total Cost (Last 30 Days)",
        "type": "stat",
        "targets": [
          {
            "expr": "sum(increase(embedding_cost_usd_total[30d]))",
            "legendFormat": "Total Cost"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "currencyUSD"
          }
        }
      },
      {
        "id": 2,
        "title": "Cost by Provider",
        "type": "timeseries",
        "targets": [
          {
            "expr": "sum by (provider) (rate(embedding_cost_usd_total[5m]))",
            "legendFormat": "{{provider}}"
          }
        ]
      },
      {
        "id": 3,
        "title": "Cache Hit Rate",
        "type": "gauge",
        "targets": [
          {
            "expr": "rate(embedding_cache_hits_total[5m]) / (rate(embedding_cache_hits_total[5m]) + rate(embedding_cache_misses_total[5m]))",
            "legendFormat": "Hit Rate"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "percentunit",
            "thresholds": {
              "steps": [
                { "value": 0, "color": "red" },
                { "value": 0.6, "color": "yellow" },
                { "value": 0.75, "color": "green" }
              ]
            }
          }
        }
      },
      {
        "id": 4,
        "title": "Embeddings Generated",
        "type": "timeseries",
        "targets": [
          {
            "expr": "sum by (operation) (rate(embeddings_generated_total[5m]))",
            "legendFormat": "{{operation}}"
          }
        ]
      },
      {
        "id": 5,
        "title": "Embedding Latency (P95)",
        "type": "timeseries",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, sum by (provider, cached, le) (rate(embedding_latency_seconds_bucket[5m])))",
            "legendFormat": "{{provider}} (cached={{cached}})"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "s"
          }
        }
      }
    ]
  }
}
```

### 4. Cost Estimation API
```csharp
// File: Routing/AdminEndpoints.cs

app.MapGet("/api/v1/admin/embedding-costs",
    [Authorize(Roles = "Admin")]
    async (
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        IMediator mediator) =>
    {
        var query = new GetEmbeddingCostsQuery
        {
            StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
            EndDate = endDate ?? DateTime.UtcNow
        };

        var result = await mediator.Send(query);
        return Results.Ok(result);
    })
    .WithName("GetEmbeddingCosts")
    .WithTags("Admin", "Embeddings");

// Response model
public record EmbeddingCostsResponse
{
    public decimal TotalCostUsd { get; init; }
    public Dictionary<string, decimal> CostByProvider { get; init; } = new();
    public Dictionary<string, decimal> CostByModel { get; init; } = new();
    public long TotalEmbeddings { get; init; }
    public decimal CacheHitRate { get; init; }
    public decimal ProjectedMonthlyCost { get; init; }
}
```

---

## ✅ Acceptance Criteria

1. **Grafana Dashboard**
   - [ ] 5 panels visible (cost, provider, cache, embeddings, latency)
   - [ ] Real-time updates (30s refresh)
   - [ ] Time range selector (1h, 24h, 7d, 30d)

2. **Cost Tracking**
   - [ ] Accurate cost per provider/model
   - [ ] Monthly projection calculated
   - [ ] Alert when monthly cost >$50

3. **Cache Monitoring**
   - [ ] Cache hit rate displayed
   - [ ] Target: >60% hit rate (yellow alert <60%, green >75%)

4. **API Endpoint**
   - [ ] `/api/v1/admin/embedding-costs` returns accurate data
   - [ ] Cost breakdown by provider and model

---

## 📈 Dashboard Panels

1. **Total Cost (Last 30 Days)** - Big number, USD
2. **Cost Trend** - Line chart, by provider
3. **Cache Hit Rate** - Gauge, target >60%
4. **Embeddings Generated** - Line chart, by operation (query/pdf)
5. **Latency P95** - Line chart, cached vs uncached

---

**Created**: 2025-11-22
**Estimated Effort**: 6-8 hours
