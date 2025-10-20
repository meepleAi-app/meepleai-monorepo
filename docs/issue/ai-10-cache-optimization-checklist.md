# AI-10 Cache Optimization - Implementation Checklist

**Epic**: AI-10 Cache Optimization
**Architecture Doc**: `docs/technic/ai-10-cache-optimization-architecture.md`
**Dashboard**: `infra/dashboards/cache-optimization.json`

## Phase 1: Foundation (Week 1)

### 1.1 Interface Definitions
- [ ] Create `Services/ICacheMetricsRecorder.cs`
  - Methods: `RecordCacheAccess()`, `RecordInvalidation()`, `RecordMetricsError()`
- [ ] Create `Services/ITtlCalculationStrategy.cs`
  - Methods: `CalculateTtlAsync()`, `GetDefaultTtl()`
- [ ] Create `Services/IFrequencyTracker.cs`
  - Methods: `IncrementAsync()`, `GetTopKeysAsync()`, `GetFrequencyAsync()`, `DeleteFrequencyDataAsync()`, `TrimFrequencyDataAsync()`

### 1.2 Configuration
- [ ] Create `Configuration/CacheOptimizationConfiguration.cs`
  - Classes: `CacheOptimizationConfiguration`, `TtlStrategyConfiguration`, `FrequencyTrackingConfiguration`, `CacheWarmingConfiguration`
- [ ] Add configuration section to `appsettings.json`
  - Section: `CacheOptimization` with all sub-sections
- [ ] Add configuration section to `appsettings.Development.json`
  - Override dev-specific settings (e.g., enable all features for testing)
- [ ] Add configuration section to `appsettings.Production.json.example`
  - Document recommended production settings

### 1.3 Frequency Tracking Implementation
- [ ] Create `Services/RedisFrequencyTracker.cs`
  - Implement `IFrequencyTracker` interface
  - Use Redis ZSET (`cache:freq:{gameId}`)
  - Methods: `IncrementAsync()` (ZINCRBY), `GetTopKeysAsync()` (ZREVRANGE), `GetFrequencyAsync()` (ZSCORE)
- [ ] Add unit tests: `tests/Api.Tests/Services/RedisFrequencyTrackerTests.cs`
  - Test increment atomicity
  - Test top keys retrieval
  - Test frequency query
  - Test delete/trim operations
  - Use Testcontainers for real Redis

### 1.4 Dependency Injection
- [ ] Update `Program.cs` DI registration
  - Register `IFrequencyTracker` → `RedisFrequencyTracker` (singleton)
  - Configure `CacheOptimizationConfiguration` from appsettings
  - Feature flag: `EnableFrequencyTracking: true`, others `false`

### 1.5 Integration Testing
- [ ] Update `AiResponseCacheService` (minimal changes)
  - Inject `IFrequencyTracker` via constructor
  - Call `IncrementAsync()` in `GetAsync()` (fire-and-forget)
  - Call `IncrementAsync()` in `SetAsync()` (fire-and-forget)
- [ ] Add integration tests: `tests/Api.Tests/Integration/FrequencyTrackingIntegrationTests.cs`
  - Test frequency incremented after cache hit
  - Test frequency incremented after cache set
  - Verify ZSET populated in Redis

### Phase 1 Validation
- [ ] All unit tests pass (20+ tests)
- [ ] Redis ZSET `cache:freq:{gameId}` populated after cache operations
- [ ] No performance regression (<5ms cache operations)
- [ ] Logs show frequency tracking activity (debug level)

---

## Phase 2: Metrics & Dynamic TTL (Week 2)

### 2.1 Metrics Recorder Implementation
- [ ] Create `Services/CacheMetricsRecorder.cs`
  - Implement `ICacheMetricsRecorder` interface
  - Async fire-and-forget pattern with try-catch
  - Delegate to `MeepleAiMetrics` static methods
- [ ] Add unit tests: `tests/Api.Tests/Services/CacheMetricsRecorderTests.cs`
  - Test metrics recording with correct dimensions
  - Test async non-blocking behavior
  - Test error handling (fallback counter)

### 2.2 OpenTelemetry Metrics
- [ ] Update `Api/Observability/MeepleAiMetrics.cs`
  - Add `CacheOperationDuration` histogram
  - Add `CacheInvalidationsCounter` counter
  - Add `MetricsErrorsCounter` counter
  - Add public methods: `RecordCacheAccess()`, `RecordCacheInvalidation()`, `RecordMetricsError()`
- [ ] Update tests: `tests/Api.Tests/Observability/OpenTelemetryIntegrationTests.cs`
  - Verify new metrics exported to `/metrics` endpoint
  - Verify metrics dimensions (operation, outcome, key_type, cache_type, reason)

### 2.3 Dynamic TTL Strategy Implementation
- [ ] Create `Services/DynamicTtlStrategy.cs`
  - Implement `ITtlCalculationStrategy` interface
  - Support strategies: constant, linear, exponential
  - Use `IFrequencyTracker.GetFrequencyAsync()` for data
  - Apply configuration: base, max, multiplier
- [ ] Add unit tests: `tests/Api.Tests/Services/DynamicTtlStrategyTests.cs`
  - Test constant strategy (always base TTL)
  - Test linear strategy (base + frequency × multiplier)
  - Test exponential strategy (base × (1 + log(frequency + 1)))
  - Test edge cases: zero frequency, high frequency, max cap

### 2.4 Enhanced Cache Service
- [ ] Update `Services/AiResponseCacheService.cs`
  - Inject `ICacheMetricsRecorder`, `ITtlCalculationStrategy`, `IFrequencyTracker`
  - Update `GetAsync()`: Add metrics recording (fire-and-forget), Activity span, tags
  - Update `SetAsync()`: Add dynamic TTL calculation, metrics recording, Activity span
  - Update `InvalidateByGameIdAsync()`: Add metrics recording, delete frequency data, Activity span
- [ ] Update tests: `tests/Api.Tests/Services/AiResponseCacheServiceTests.cs`
  - Mock `ICacheMetricsRecorder`, `ITtlCalculationStrategy`, `IFrequencyTracker`
  - Verify metrics recorder called on hit/miss
  - Verify TTL strategy called on set
  - Verify frequency tracker called on get/set
  - Verify invalidation deletes frequency data

### 2.5 OpenTelemetry Tracing
- [ ] Update `Api/Observability/ActivitySourceConfiguration.cs` (if needed)
  - Ensure `cache.*` spans included in tracing
- [ ] Add Activity span creation in `AiResponseCacheService`
  - Span names: `cache.get`, `cache.set`, `cache.invalidate`
  - Span tags: `cache.operation`, `cache.outcome`, `cache.game_id`, `cache.key_type`, `cache.ttl_seconds`, `cache.keys_invalidated`, `cache.invalidation_reason`
- [ ] Test in Jaeger UI: Verify spans visible at `http://localhost:16686`

### 2.6 Dependency Injection
- [ ] Update `Program.cs` DI registration
  - Register `ICacheMetricsRecorder` → `CacheMetricsRecorder` (singleton)
  - Register `ITtlCalculationStrategy` → `DynamicTtlStrategy` (singleton)
  - Feature flag: `EnableMetrics: true`, `EnableDynamicTtl: false` (manual testing first)

### Phase 2 Validation
- [ ] All unit tests pass (40+ tests)
- [ ] Integration tests pass (15+ tests)
- [ ] Metrics visible in Prometheus `/metrics` endpoint
- [ ] Grafana dashboard shows cache hit rate, latency, invalidations
- [ ] Dynamic TTL calculated correctly (verify in logs)
- [ ] Activity spans visible in Jaeger UI
- [ ] No performance regression (<5ms cache operations)

---

## Phase 3: Cache Warming (Week 3)

### 3.1 Cache Warming Service Implementation
- [ ] Create `Services/CacheWarmingService.cs`
  - Extend `BackgroundService`
  - Implement `ExecuteAsync()` with `PeriodicTimer` (parse cron schedule)
  - Implement `WarmTopQueriesAsync()`: Call `IFrequencyTracker.GetTopKeysAsync()`, execute via `IRagService`
  - Implement circuit breaker: Check CPU/queue depth, skip if threshold exceeded
  - Parallel processing with `Parallel.ForEachAsync(maxDegreeOfParallelism: config.MaxConcurrency)`
  - Comprehensive logging with correlation IDs

### 3.2 Unit Tests
- [ ] Create `tests/Api.Tests/Services/CacheWarmingServiceTests.cs`
  - Test periodic timer triggers warming
  - Test circuit breaker skips warming when CPU high
  - Test error handling (query failure doesn't stop batch)
  - Test concurrency limit (max 5 parallel queries)
  - Test cancellation token (graceful shutdown)
  - Mock `IFrequencyTracker`, `IRagService`, `IAiResponseCacheService`

### 3.3 Integration Tests
- [ ] Create `tests/Api.Tests/Integration/CacheWarmingIntegrationTests.cs`
  - Test manual trigger endpoint (if added)
  - Test warming populates cache (verify Redis keys)
  - Test warming logs success/error counts
  - Use Testcontainers (Redis, Postgres)

### 3.4 Admin Endpoint (Optional)
- [ ] Add manual cache warming endpoint to `Program.cs`
  - `POST /admin/cache/warm` (admin role required)
  - Request: `{ "gameId": "uuid?", "topN": 10 }` (optional gameId, default: all games)
  - Response: `{ "gamesWarmed": 2, "queriesWarmed": 95, "errors": 5, "durationSeconds": 42 }`
- [ ] Add tests: `tests/Api.Tests/Endpoints/AdminCacheEndpointsTests.cs`

### 3.5 Dependency Injection
- [ ] Update `Program.cs` DI registration
  - Conditionally register `CacheWarmingService` as `IHostedService` (based on `CacheWarming:Enabled` config)
  - Feature flag: `EnableCacheWarming: false` (manual testing only)

### Phase 3 Validation
- [ ] All unit tests pass (50+ tests)
- [ ] Background service starts/stops correctly
- [ ] Manual trigger warms top 10 queries successfully (if endpoint added)
- [ ] Circuit breaker skips run when CPU high (simulate with load test)
- [ ] Errors logged but don't crash service
- [ ] Warming duration logged in Seq with correlation ID

---

## Phase 4: Production Rollout (Week 4)

### 4.1 Grafana Dashboard
- [ ] Import dashboard JSON to Grafana: `infra/dashboards/cache-optimization.json`
  - Manually import via Grafana UI at `http://localhost:3001`
  - Set Prometheus datasource
  - Verify all panels render correctly
- [ ] Add dashboard link to `docs/observability.md`

### 4.2 Configuration Tuning
- [ ] Update `infra/env/api.env.dev` (development)
  - Enable all features: `EnableMetrics: true`, `EnableDynamicTtl: true`, `EnableCacheWarming: false` (manual only)
- [ ] Create `infra/env/api.env.prod.example` (production)
  - Conservative settings: `EnableDynamicTtl: false` (start disabled), `EnableCacheWarming: false`
  - Document recommended production values

### 4.3 Feature Flag Rollout
- [ ] **Day 1-2**: Enable `EnableMetrics: true` in production
  - Monitor Grafana dashboard for 48 hours
  - Verify metrics recording error rate <1%
  - Verify no performance regression
- [ ] **Day 3-5**: Enable `EnableDynamicTtl: true` in production
  - Monitor cache hit rate change (target: +10%)
  - Monitor TTL distribution (verify values reasonable)
  - Tune TTL parameters if needed (adjust `LinearMultiplierMinutes`, `MaxMinutes`)
- [ ] **Day 6-7**: Enable `EnableCacheWarming: true` in production
  - Set schedule: 2am UTC daily (low traffic period)
  - Monitor warming logs in Seq (verify success rate >95%)
  - Monitor warming duration (target: <5 minutes)
  - Adjust `MaxConcurrency` if needed

### 4.4 Success Metrics Measurement
- [ ] Measure baseline metrics (before AI-10)
  - Cache hit rate: ___% (document)
  - Average cache operation latency: ___ms (document)
  - Redis memory usage: ___MB (document)
- [ ] Measure post-rollout metrics (7 days after full enable)
  - Cache hit rate: ___% (target: +10% improvement)
  - Average cache operation latency: ___ms (target: <5ms)
  - Redis memory usage: ___MB (target: <10MB increase)
  - Cache warming success rate: ___% (target: >95%)
- [ ] Document results in `docs/issue/ai-10-cache-optimization-results.md`

### 4.5 Documentation Updates
- [ ] Update `CLAUDE.md`
  - Add cache optimization features to architecture section
  - Document configuration options
  - Add troubleshooting tips
- [ ] Update `docs/observability.md`
  - Add cache optimization section
  - Link to Grafana dashboard
  - Document key metrics and PromQL queries
- [ ] Create `docs/guide/cache-optimization-guide.md`
  - User guide: How to tune TTL parameters
  - How to interpret Grafana dashboard
  - How to manually trigger cache warming (if endpoint exists)
  - Troubleshooting common issues

### Phase 4 Validation
- [ ] Cache hit rate increased by ≥10% over baseline (7-day average)
- [ ] Average cache operation latency <5ms (p95 <10ms)
- [ ] Redis memory usage stable (<10MB increase)
- [ ] Cache warming success rate >95% (7-day average)
- [ ] Zero cache-related incidents in first 30 days
- [ ] Metrics recording error rate <1%
- [ ] Team trained on new features (demo session conducted)

---

## Additional Tasks

### Testing
- [ ] Add benchmark tests: `tests/Api.Benchmarks/CacheBenchmarkTests.cs`
  - Benchmark `GetAsync()` with/without metrics
  - Benchmark `SetAsync()` with dynamic TTL
  - Benchmark frequency tracking overhead
  - Use `BenchmarkDotNet`, target: <5ms for GetAsync

### CI/CD
- [ ] Update `.github/workflows/ci.yml`
  - Ensure new tests run in CI
  - Add cache optimization configuration for CI environment
- [ ] Verify security scanning
  - Run `dotnet list package --vulnerable` in `apps/api`
  - No new vulnerabilities introduced

### Monitoring & Alerts
- [ ] Add Grafana alerts (optional, future)
  - Alert: Cache hit rate drops below 50%
  - Alert: Cache operation latency p95 >10ms
  - Alert: Redis memory usage >80%
  - Alert: Metrics recording error rate >1%
  - Alert: Cache warming failure rate >5%

---

## Rollback Plan

If issues arise, follow this rollback sequence:

1. **Disable cache warming**: Set `EnableCacheWarming: false` in config (no deployment needed)
2. **Disable dynamic TTL**: Set `EnableDynamicTtl: false` in config (no deployment needed)
3. **Disable metrics**: Set `EnableMetrics: false` in config (no deployment needed)
4. **Disable frequency tracking**: Set `EnableFrequencyTracking: false` in config (no deployment needed)
5. **Full rollback**: Revert to commit before AI-10 implementation (if all else fails)

**Note**: All features are backward compatible. Disabling via config is instant (no downtime).

---

## Definition of Done

- [ ] All unit tests pass (50+ tests)
- [ ] All integration tests pass (20+ tests)
- [ ] Code coverage >80% for new code
- [ ] Benchmark tests validate <5ms latency target
- [ ] Grafana dashboard imported and functional
- [ ] OpenTelemetry spans visible in Jaeger
- [ ] Prometheus metrics exported correctly
- [ ] Documentation updated (CLAUDE.md, observability.md, new guides)
- [ ] Production rollout completed (4 phases)
- [ ] Success metrics achieved (hit rate +10%, latency <5ms)
- [ ] Team demo conducted (show dashboard, explain features)
- [ ] Rollback plan validated (tested feature flag disable)

---

## Notes

- **Context Remaining**: Remember to notify user when context <5% remaining (per CLAUDE.md)
- **Testing Priority**: Focus on integration tests with Testcontainers (most valuable for cache optimization)
- **Performance**: Continuously monitor latency during development (fail fast if >5ms)
- **Feature Flags**: Keep features disabled by default until fully validated
- **Incremental Rollout**: Never enable multiple features simultaneously in production
