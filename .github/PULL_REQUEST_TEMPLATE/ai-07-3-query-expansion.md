# AI-07.3: LLM-Based Query Expansion for Improved Retrieval

## Summary

Implements LLM-based query expansion with multi-query retrieval and Reciprocal Rank Fusion (RRF) to improve Recall by +8-12%. Includes Redis caching to minimize costs.

**Technical Design**: [docs/technic/ai-07-rag-optimization-phase1.md](../docs/technic/ai-07-rag-optimization-phase1.md#4-optimization-3-llm-based-query-expansion)

## Related Issue

Closes #470

## Type of Change

- [x] New feature (non-breaking change which adds functionality)
- [x] Performance improvement
- [x] Configuration/infrastructure change

## Changes Made

### Core Implementation

- [ ] Created `QueryExpansionService.cs` with LLM integration:
  - [ ] `ExpandQueryAsync(query)` - Generate 2-3 query variations
  - [ ] `GetCachedExpansion(query)` - Redis cache lookup
  - [ ] `GenerateExpansionWithLlm(query)` - LLM call to Claude Haiku
  - [ ] `CacheExpansion(query, variations)` - Store in Redis (TTL: 1 hour)
- [ ] Implemented LLM expansion prompt and response parser
- [ ] Implemented cost tracking (tokens used, monthly budget enforcement)
- [ ] Modified `RagService.SearchAsync()` for multi-query retrieval:
  - [ ] Loop through query variations
  - [ ] Search Qdrant for each variation (top-20 per query)
  - [ ] Merge results with Reciprocal Rank Fusion (RRF)
  - [ ] Deduplicate chunks by ID
  - [ ] Return top-10 fused results
- [ ] Implemented RRF algorithm (`MergeSearchResults()` method)
- [ ] Added DI registration in `Program.cs`

### Configuration

- [ ] Added `QueryExpansion` section to `appsettings.json`:
  - [ ] `Enabled`: true/false toggle
  - [ ] `Model`: "anthropic/claude-3-haiku" (cheap, fast)
  - [ ] `MaxVariations`: 3
  - [ ] `MinQueryLength`: 5 (don't expand very short queries)
  - [ ] `FusionMethod`: "RRF" (Reciprocal Rank Fusion)
  - [ ] `RrfConstant`: 60 (standard k parameter)
  - [ ] `TopKPerQuery`: 20, `FinalTopK`: 10
  - [ ] `Caching.Enabled`: true, `TtlSeconds`: 3600
  - [ ] `CostControl.MonthlyBudgetUsd`: 5.0, `AlertThresholdUsd`: 4.0

### Monitoring

- [ ] Added custom metrics to `MeepleAiMetrics.cs`:
  - [ ] `meepleai.query_expansion.total` - Total expansions attempted
  - [ ] `meepleai.query_expansion.cache_hits` - Redis cache hits
  - [ ] `meepleai.query_expansion.llm_calls` - LLM calls (for cost tracking)
  - [ ] `meepleai.query_expansion.tokens` - Tokens used per expansion
  - [ ] `meepleai.query_expansion.cost_usd` - Cost per expansion
- [ ] Added Grafana dashboard panel for query expansion metrics
- [ ] Set up alerts:
  - [ ] Monthly cost exceeds $4 → Email ops team
  - [ ] Cache hit rate drops below 40% → Investigate cache eviction

## Testing

### Test Coverage

- [x] Unit tests added/updated (`QueryExpansionServiceTests.cs`)
  - [ ] Cache hit/miss scenarios
  - [ ] LLM response parsing (valid, malformed, empty)
  - [ ] Cost calculation accuracy (tokens → USD)
  - [ ] Configuration validation (enabled/disabled, max variations)
  - [ ] Budget enforcement (disable expansion if monthly cost exceeds limit)
- [x] Integration tests added/updated (`QueryExpansionIntegrationTests.cs`)
  - [ ] Real LLM calls with Claude Haiku
  - [ ] Redis cache read/write/invalidation
  - [ ] TTL expiration behavior
  - [ ] Concurrent cache access (thread safety)
- [x] RRF tests added/updated (`RagServiceTests.cs`)
  - [ ] Score fusion correctness (manual calculation vs. implementation)
  - [ ] Deduplication (same chunk from multiple query variations)
  - [ ] Ranking stability (deterministic results)
- [x] All tests passing locally
- [x] Test names follow BDD convention

### RAG Evaluation

- [ ] Baseline evaluation (expansion disabled)
- [ ] Post-implementation evaluation (expansion enabled)
- [ ] Metrics comparison:
  - [ ] Recall@10 improvement: ____% (target: +8-12%)
  - [ ] Precision@5 change: ____% (expect neutral or slight improvement)
  - [ ] Latency p95 change: ____ms (expect +200-300ms for multi-query)

**Evaluation Results**:
```
<!-- Paste RAG evaluation results (before/after expansion) -->
Baseline (No Expansion):
  Recall@10: 0.XX, Latency p95: XXXXms

With Expansion:
  Recall@10: 0.XX, Latency p95: XXXXms
```

### Cost Estimation Tests

- [ ] Mock 1000 test queries, measure total tokens
- [ ] Validate cost projection formula (tokens → USD)
- [ ] Test monthly budget enforcement (fail-safe: disable expansion if >$5)

**Cost Test Results**:
```
<!-- Paste cost estimation test results -->
1000 queries simulated:
  - Cache hit rate: XX%
  - LLM calls: XXX
  - Total tokens: XXXK
  - Estimated cost: $X.XX
  - Projected monthly cost (at 10K queries/month): $X.XX
```

### Manual Testing

- [ ] Test query expansion with real queries:
  - [ ] "castling" → ["castling", "king-rook special move", "castle move in chess"]
  - [ ] "check" → ["check", "putting opponent's king in check", "attacking the king"]
  - [ ] "pawn promotion" → ["pawn promotion", "promoting pawn to another piece", "pawn reaching opposite end"]
- [ ] Verify Redis caching (second identical query = cache hit)
- [ ] Test expansion disabled via config (graceful degradation)
- [ ] Test budget enforcement (manual override of monthly limit)

**Sample Expansions**:
```
<!-- Paste sample query expansions from LLM -->
Original: "castling"
Variations:
  1. castling
  2. king-rook special move
  3. castle move in chess

Original: "en passant"
Variations:
  1. en passant
  2. special pawn capture move
  3. pawn capturing in passing
```

## Acceptance Criteria (from #470)

- [ ] `QueryExpansionService` created with LLM integration
- [ ] Config section `QueryExpansion` with enable/disable toggle
- [ ] `RagService.SearchAsync()` expanded to handle multi-query retrieval
- [ ] RRF score fusion implemented and tested
- [ ] Redis caching for expansions (TTL 1 hour)
- [ ] Unit tests mock LLM responses for expansion
- [ ] Integration tests with real Qdrant show +8-12% Recall improvement
- [ ] Cost monitoring: log LLM tokens used for expansion
- [ ] Feature flag: can disable expansion via config without code changes

## Checklist

- [ ] Code follows project style guidelines
- [ ] Self-review of code completed
- [ ] Comments added for RRF algorithm and cost calculation
- [ ] Documentation updated (technical design doc already exists)
- [ ] No new warnings introduced
- [ ] Tests added/updated and passing
- [ ] Test names follow BDD-style naming convention
- [ ] Changes are backwards compatible (expansion can be disabled)
- [ ] No secrets or API keys committed (uses existing OpenRouter key)

## Configuration Example

**appsettings.json** (excerpt):
```json
{
  "QueryExpansion": {
    "Enabled": true,
    "Model": "anthropic/claude-3-haiku",
    "MaxVariations": 3,
    "MinQueryLength": 5,
    "MaxQueryLength": 100,
    "FusionMethod": "RRF",
    "RrfConstant": 60,
    "TopKPerQuery": 20,
    "FinalTopK": 10,
    "Caching": {
      "Enabled": true,
      "TtlSeconds": 3600,
      "MaxCacheSize": 10000,
      "KeyPrefix": "qe"
    },
    "CostControl": {
      "MaxTokensPerExpansion": 150,
      "MonthlyBudgetUsd": 5.0,
      "AlertThresholdUsd": 4.0
    }
  }
}
```

## Performance Impact

- [ ] Latency impact measured:
  - Single-query (no expansion): ____ ms
  - Multi-query (3 variations): ____ ms (expect +200-300ms)
- [ ] Memory usage: Minimal increase for caching expansions
- [ ] Cache hit rate: ____% (target: >60%)

**Performance Benchmark**:
```
<!-- Paste performance benchmark results -->
```

## Cost Analysis

**Model Pricing** (Claude Haiku via OpenRouter):
- Input: $0.25 per 1M tokens
- Output: $1.25 per 1M tokens

**Per-Query Cost**:
- Prompt: ~50 tokens (expansion instructions + query)
- Response: ~100 tokens (3 variations)
- Total: 150 tokens/query
- Cost: $0.000025 per query (2.5¢ per 1000 queries)

**Monthly Cost Projections**:
| Queries/Month | Cache Hit Rate | LLM Calls | Cost |
|---------------|----------------|-----------|------|
| 1,000 | 50% | 500 | $0.01 |
| 5,000 | 60% | 2,000 | $0.05 |
| 10,000 | 60% | 4,000 | $0.10 |
| 50,000 | 70% | 15,000 | $0.38 |

**Conservative Estimate**: $2-3/month for 10,000-20,000 queries/month with 60% cache hit rate.

**Actual Cost** (post-deployment):
```
<!-- Update with actual cost data after 1 week in production -->
Week 1: X,XXX queries, XX% cache hit rate, $X.XX spent
```

## Monitoring Dashboard

**Grafana Panel** (Query Expansion Metrics):
- Query expansion rate (enabled vs. disabled queries)
- Cache hit rate (target: >60%, current: ___%)
- LLM call rate and token usage
- Monthly cost projection (current: $___/month)
- Expansion latency (p50: ___ms, p95: ___ms, target: <300ms)

**Prometheus Queries**:
```promql
# Cache hit rate
rate(meepleai_query_expansion_cache_hits_total[5m]) /
rate(meepleai_query_expansion_total[5m])

# Monthly cost projection
sum(increase(meepleai_query_expansion_cost_usd[30d]))

# Expansion latency p95
histogram_quantile(0.95, rate(meepleai_query_expansion_duration_seconds_bucket[5m]))
```

## Rollback Plan

If metrics regress or costs exceed budget:
1. **Immediate** (< 1 min):
   - Set `QueryExpansion.Enabled` to `false` in `appsettings.json`
   - Restart API pods (zero-downtime rolling restart)
2. **Short-term** (< 5 min):
   - Revert PR via Git revert
   - Redeploy previous version
3. **Post-mortem**:
   - Analyze which query types caused latency/cost issues
   - Optimize expansion prompt or reduce `MaxVariations`
   - Re-test before next deployment attempt

## Additional Notes

<!-- Any additional context or implementation decisions -->

**Design Decisions**:
- Chose Claude Haiku over GPT-4o-mini for better quality/cost ratio
- RRF over CombSUM for simplicity and robustness (no parameter tuning)
- Redis TTL=1h balances cache efficiency and freshness

**Known Limitations**:
- Very short queries (<5 chars) skip expansion (e.g., single-word queries)
- LLM may occasionally generate invalid expansions (parser handles gracefully)
- Cache invalidation on config changes requires manual Redis flush

**Future Optimizations** (Phase 2+):
- Adaptive TTL based on query frequency (hot queries cached 24h)
- Query clustering for similar questions (reduce unique expansions)
- Hybrid expansion (LLM + WordNet synonyms for fallback)

**Next Steps** (after merge):
- Monitor cache hit rate and costs for 2 weeks
- A/B test with 25% rollout before full deployment
- Combine with AI-07.1 + AI-07.2 for full Phase 1 evaluation

**Cost Impact**: $2-3/month (LLM API calls, mitigated by Redis caching)

---

## Grafana Alert Example

**Alert**: Query Expansion Monthly Cost Exceeds $4

```yaml
alert: QueryExpansionCostHigh
expr: sum(increase(meepleai_query_expansion_cost_usd[30d])) > 4.0
for: 5m
labels:
  severity: warning
  component: query-expansion
annotations:
  summary: "Query expansion monthly cost is ${{ $value | printf \"%.2f\" }}"
  description: "Approaching monthly budget limit of $5. Review cache hit rate and query volume."
```

**Action**: Email ops team, consider reducing `MaxVariations` or increasing cache TTL.
