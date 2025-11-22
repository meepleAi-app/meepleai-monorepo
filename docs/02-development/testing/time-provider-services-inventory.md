# Time Provider Migration: Services Inventory

**Generated**: 2025-10-30
**Purpose**: Complete inventory of services requiring `TimeProvider` injection
**Scope**: All services with `DateTime.UtcNow`, `Task.Delay`, or timing dependencies

---

## Summary Statistics

- **Total Services to Refactor**: 24 production services
- **Total Test Files with Task.Delay**: 14 test files
- **Total Task.Delay Occurrences in Tests**: 51 instances
- **Total DateTime.UtcNow Occurrences in Services**: 150+ instances
- **Estimated Refactoring Time**: 40-60 hours
- **Expected CI Performance Gain**: ~97% reduction in test execution time

---

## Priority Classification

### 🔴 High Priority: Background Services with Task.Delay

These services are critical because they actively use `Task.Delay` for scheduling, causing real delays in tests.

#### 1. SessionAutoRevocationService.cs
**Path**: `apps/api/src/Api/Services/SessionAutoRevocationService.cs`
**Issues**:
- Line 53: `await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);` - Initial startup delay
- Line 73: `await Task.Delay(delay, stoppingToken);` - Hourly interval delay

**Impact**: Tests currently wait real time for session revocation logic.
**Test File**: `SessionAutoRevocationServiceTests.cs` (9 Task.Delay occurrences)
**Estimated Effort**: 4 hours (service + tests)

---

#### 2. CacheWarmingService.cs
**Path**: `apps/api/src/Api/Services/CacheWarmingService.cs`
**Issues**:
- Line 66-67: `await Task.Delay(TimeSpan.FromMinutes(_config.WarmingStartupDelayMinutes), stoppingToken);` - Startup delay
- Line 82-83: `await Task.Delay(TimeSpan.FromHours(_config.WarmingIntervalHours), stoppingToken);` - Warming interval
- Line 95: `await Task.Delay(TimeSpan.FromHours(_config.WarmingIntervalHours), stoppingToken);` - Error retry
- Line 100: `await Task.Delay(TimeSpan.FromHours(_config.WarmingIntervalHours), stoppingToken);` - HTTP error retry

**Impact**: Tests wait multiple seconds for cache warming cycles.
**Test File**: `CacheWarmingServiceTests.cs` (5 Task.Delay occurrences)
**Estimated Effort**: 5 hours (service + tests)

---

#### 3. QualityReportService.cs
**Path**: `apps/api/src/Api/Services/QualityReportService.cs`
**Issues**:
- Line 83: `await Task.Delay(_initialDelay, stoppingToken);` - Initial delay before first report
- Line 137: `var endDate = DateTime.UtcNow;` - Date range calculation for reports
- Line 138: `var startDate = endDate.AddDays(-_reportWindowDays);` - Window calculation

**Impact**: Tests wait for initial delay + report intervals.
**Test File**: `QualityReportServiceTests.cs` (5 Task.Delay occurrences)
**Estimated Effort**: 4 hours (service + tests)

---

### 🟡 Medium Priority: Services with DateTime.UtcNow

These services use `DateTime.UtcNow` for timestamps but don't have Task.Delay. Refactoring improves determinism.

#### 4. AdminStatsService.cs
**Path**: `apps/api/src/Api/Services/AdminStatsService.cs`
**Issues**:
- Line 49: `var now = DateTime.UtcNow;` - Dashboard stats date range
- Line 71: `GeneratedAt: DateTime.UtcNow` - Stats timestamp

**Estimated Effort**: 2 hours

---

#### 5. AlertingService.cs
**Path**: `apps/api/src/Api/Services/AlertingService.cs`
**Issues**:
- Line 82: `TriggeredAt = DateTime.UtcNow` - Alert creation timestamp
- Line 164: `alert.ResolvedAt = DateTime.UtcNow;` - Alert resolution timestamp
- Line 207: `var throttleWindow = DateTime.UtcNow.AddMinutes(-_config.ThrottleMinutes);` - Throttle window calculation

**Estimated Effort**: 2 hours

---

#### 6. RagEvaluationService.cs
**Path**: `apps/api/src/Api/Services/PromptEvaluationService.cs`
**Issues**:
- Line 212: `EvaluatedAt = DateTime.UtcNow` - Evaluation timestamp
- Line 379 (test): `await Task.Delay(latency, ct);` - Simulated latency in tests

**Test File**: `RagEvaluationServiceTests.cs` (1 Task.Delay occurrence)
**Estimated Effort**: 3 hours (service + tests)

---

#### 7. StreamingQaService.cs
**Path**: `apps/api/src/Api/Services/StreamingQaService.cs`
**Issues**:
- Line 85: `await Task.Delay(10, cancellationToken);` - Word streaming delay
- Line 116: `var startTime = DateTime.UtcNow;` - Performance measurement start
- Line 223: `var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;` - Elapsed time calculation
- Line 234: `return new RagStreamingEvent(type, data, DateTime.UtcNow);` - Event timestamp

**Estimated Effort**: 3 hours

---

#### 8. StreamingRagService.cs
**Path**: `apps/api/src/Api/Services/StreamingRagService.cs`
**Issues**:
- Line 131: `await Task.Delay(50, cancellationToken);` - Script chunk streaming delay
- Line 157: `return new RagStreamingEvent(type, data, DateTime.UtcNow);` - Event timestamp

**Estimated Effort**: 2 hours

---

#### 9. TempSessionService.cs
**Path**: `apps/api/src/Api/Services/TempSessionService.cs`
**Issues**:
- Line 41: `CreatedAt = DateTime.UtcNow` - Session creation
- Line 42: `ExpiresAt = DateTime.UtcNow.AddMinutes(TempSessionLifetimeMinutes)` - Session expiry
- Line 61: `var now = DateTime.UtcNow;` - Validation timestamp
- Line 78: `tempSession.UsedAt = DateTime.UtcNow;` - Usage timestamp
- Line 104: `var now = DateTime.UtcNow;` - Cleanup timestamp

**Estimated Effort**: 3 hours

---

#### 10. TotpService.cs
**Path**: `apps/api/src/Api/Services/TotpService.cs`
**Issues**:
- Line 89: `CreatedAt = DateTime.UtcNow` - Backup code creation
- Line 138: `user.TwoFactorEnabledAt = DateTime.UtcNow;` - 2FA enable timestamp
- Line 216: `storedCode.UsedAt = DateTime.UtcNow;` - Backup code usage

**Estimated Effort**: 2 hours

---

#### 11. PdfStorageService.cs
**Path**: `apps/api/src/Api/Services/PdfStorageService.cs`
**Issues**:
- Line 126: `UploadedAt = DateTime.UtcNow` - PDF upload timestamp
- Line 144: `StartedAt = DateTime.UtcNow` - Processing start
- Line 316: `var startTime = DateTime.UtcNow;` - Performance measurement
- Line 336: `pdfDoc.ProcessedAt = DateTime.UtcNow;` - Processing completion (failure)
- Line 433, 449, 472, 502, 534, 551, 565, 579, 593: Multiple `ProcessedAt = DateTime.UtcNow` (various error paths)
- Line 614: `var elapsed = DateTime.UtcNow - startTime;` - Elapsed time
- Line 627: `CompletedAt = step == ProcessingStep.Completed || step == ProcessingStep.Failed ? DateTime.UtcNow : null` - Completion timestamp

**Estimated Effort**: 4 hours

---

#### 12. SessionCacheService.cs
**Path**: `apps/api/src/Api/Services/SessionCacheService.cs`
**Issues**:
- Line 78: `var ttl = expiresAt - DateTime.UtcNow;` - TTL calculation

**Estimated Effort**: 1 hour

---

#### 13. N8nConfigService.cs
**Path**: `apps/api/src/Api/Services/N8nConfigService.cs`
**Issues**:
- Line 92-93: `CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow` - Config creation
- Line 158: `config.UpdatedAt = DateTime.UtcNow;` - Config update
- Line 211: `var startTime = DateTime.UtcNow;` - Connection test start
- Line 213: `var latency = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;` - Latency calculation
- Line 220: `config.LastTestedAt = DateTime.UtcNow;` - Test timestamp
- Line 233: `config.LastTestedAt = DateTime.UtcNow;` - Test failure timestamp

**Estimated Effort**: 2 hours

---

### 🟢 Low Priority: Entity Timestamp Services

These services primarily set entity timestamps. Lower impact on test determinism but improves consistency.

#### 14-24. Entity Creation Services

| Service | Path | DateTime.UtcNow Count | Primary Use |
|---------|------|----------------------|-------------|
| AgentFeedbackService.cs | `Services/AgentFeedbackService.cs` | 3 | Line 66-67, 77 (CreatedAt, UpdatedAt) |
| AiRequestLogService.cs | `Services/AiRequestLogService.cs` | 1 | Line 59 (CreatedAt) |
| ApiKeyManagementService.cs | `Services/ApiKeyManagementService.cs` | 2 | Line 330, 359 (ResetsAt) |
| AuditService.cs | `Services/AuditService.cs` | 1 | Line 40 (CreatedAt) |
| ChatService.cs | `Services/ChatService.cs` | 6 | Lines 93, 134, 140, 220, 270, 341 (timestamps) |
| ConfigurationService.cs | `Services/ConfigurationService.cs` | 14 | Lines 174-175, 246, 261, etc. (version control) |
| EmailAlertChannel.cs | `Services/EmailAlertChannel.cs` | 1 | Line 144 (email timestamp) |
| MdExportFormatter.cs | `Services/MdExportFormatter.cs` | 1 | Line 35 (export timestamp) |
| PdfExportFormatter.cs | `Services/PdfExportFormatter.cs` | 1 | Line 106 (export timestamp) |
| TxtExportFormatter.cs | `Services/TxtExportFormatter.cs` | 1 | Line 33 (export timestamp) |
| RuleCommentService.cs | `Services/RuleCommentService.cs` | 8 | Lines 55, 104, 183-184, etc. (comment lifecycle) |

**Total Estimated Effort**: 11 hours (1 hour each)

---

## Test Files Requiring Task.Delay Replacement

### High Impact Test Files

#### 1. SessionAutoRevocationServiceTests.cs
**Path**: `apps/api/tests/Api.Tests/SessionAutoRevocationServiceTests.cs`
**Task.Delay Occurrences**: 9 instances
- Line 64: `await Task.Delay(100);` - Wait for service start
- Line 104: `await Task.Delay(100);` - Wait for execution
- Line 134: `await Task.Delay(100);` - Wait for processing
- Line 164: `await Task.Delay(100);` - Wait for completion
- Line 194: `await Task.Delay(100);` - Wait for revocation
- Line 229: `await Task.Delay(TimeSpan.FromSeconds(2));` - Wait for interval
- Line 319: `await Task.Delay(100);` - Simulate wait time
- Line 376: `await Task.Delay(100);` - Cancellation test
- Line 409: `await Task.WhenAny(stopTask, Task.Delay(TimeSpan.FromSeconds(5)));` - Timeout check

**Estimated Effort**: 4 hours

---

#### 2. CacheWarmingServiceTests.cs
**Path**: `apps/api/tests/Api.Tests/Services/CacheWarmingServiceTests.cs`
**Task.Delay Occurrences**: 5 instances
- Line 130: `await Task.Delay(TimeSpan.FromSeconds(3));` - Wait for warming
- Line 175: `await Task.Delay(TimeSpan.FromSeconds(1));` - Let it start
- Line 238: `await Task.Delay(TimeSpan.FromSeconds(3));` - Wait for cycle
- Line 307: `await Task.Delay(TimeSpan.FromSeconds(3));` - Error handling test
- Line 386: `await Task.Delay(TimeSpan.FromSeconds(3));` - Configuration test

**Estimated Effort**: 3 hours

---

#### 3. QualityReportServiceTests.cs
**Path**: `apps/api/tests/Api.Tests/Services/QualityReportServiceTests.cs`
**Task.Delay Occurrences**: 5 instances
- Line 64: `await Task.Delay(400);` - Wait for intervals
- Line 110: `await Task.Delay(200);` - Partial wait
- Line 298: `await Task.Delay(400);` - Multiple intervals
- Line 345: `await Task.Delay(100);` - Service start
- Line 389: `await Task.Delay(250);` - Report generation

**Estimated Effort**: 3 hours

---

#### 4. CacheMetricsRecorderTests.cs
**Path**: `apps/api/tests/Api.Tests/Services/CacheMetricsRecorderTests.cs`
**Task.Delay Occurrences**: 10 instances
- Line 80: `await Task.Delay(50);` - Fire-and-forget completion
- Line 103: `await Task.Delay(50);` - Task completion
- Line 139: `await Task.Delay(50);` - Metrics recording
- Line 161: `await Task.Delay(50);` - Background task
- Line 180: `await Task.Delay(50);` - Async operation
- Line 202: `await Task.Delay(50);` - Multiple metrics
- Line 224: `await Task.Delay(100);` - Longer wait
- Line 248: `await Task.Delay(100);` - Batch operations
- Line 267: `await Task.Delay(50);` - Error handling
- Line 289: `await Task.Delay(50);` - Final verification

**Estimated Effort**: 2 hours

---

### Medium Impact Test Files

#### 5. BackgroundTaskServiceTests.cs
**Path**: `apps/api/tests/Api.Tests/BackgroundTaskServiceTests.cs`
**Task.Delay Occurrences**: 3 instances
- Line 80: `await Task.Delay(100);` - Simulate long-running task
- Line 143: `await Task.Delay(10);` - Task completion check
- Line 168: `await Task.Delay(10);` - Exception handling

**Estimated Effort**: 2 hours

---

#### 6. ChatContextSwitchingIntegrationTests.cs
**Path**: `apps/api/tests/Api.Tests/Integration/ChatContextSwitchingIntegrationTests.cs`
**Task.Delay Occurrences**: 6 instances
- Line 306: `await Task.Delay(10);` - Timestamp difference
- Line 333: `await Task.Delay(10);` - Between chat creation
- Line 336: `await Task.Delay(10);` - Between chats
- Line 339: `await Task.Delay(10);` - Chat ordering
- Line 343: `await Task.Delay(10);` - Message ordering
- Line 345: `await Task.Delay(10);` - Final message

**Estimated Effort**: 2 hours

---

#### 7-12. Lower Impact Test Files

| Test File | Task.Delay Count | Lines | Estimated Effort |
|-----------|------------------|-------|------------------|
| RuleCommentServiceTests.cs | 2 | 475, 477 | 1 hour |
| PromptManagementServiceTests.cs | 2 | 162, 347 | 1 hour |
| ChatEndpointsTests.cs | 1 | 110 | 0.5 hours |
| FollowUpQuestionServiceTests.cs | 1 | 135 | 0.5 hours |
| LlmServiceTests.cs | 1 | 372 | 0.5 hours |
| RagEvaluationServiceTests.cs | 1 | 379 | 0.5 hours |

**Total Estimated Effort**: 4 hours

---

## Migration Phases

### Phase 1: Infrastructure ✅ Complete
- [x] Create `TestTimeProvider.cs` with thread-safe time control
- [x] Create `TimeTestHelpers.cs` with factory methods and extensions
- [x] Document migration guide with patterns and examples
- [x] Generate complete services inventory

### Phase 2: High-Priority Background Services (13 hours)
1. [ ] Refactor `SessionAutoRevocationService.cs` + tests (4 hours)
2. [ ] Refactor `CacheWarmingService.cs` + tests (5 hours)
3. [ ] Refactor `QualityReportService.cs` + tests (4 hours)

### Phase 3: Medium-Priority Services (28 hours)
4. [ ] Refactor `AdminStatsService.cs` (2 hours)
5. [ ] Refactor `AlertingService.cs` (2 hours)
6. [ ] Refactor `RagEvaluationService.cs` + tests (3 hours)
7. [ ] Refactor `StreamingQaService.cs` (3 hours)
8. [ ] Refactor `StreamingRagService.cs` (2 hours)
9. [ ] Refactor `TempSessionService.cs` (3 hours)
10. [ ] Refactor `TotpService.cs` (2 hours)
11. [ ] Refactor `PdfStorageService.cs` (4 hours)
12. [ ] Refactor `SessionCacheService.cs` (1 hour)
13. [ ] Refactor `N8nConfigService.cs` (2 hours)
14. [ ] Refactor remaining test files (4 hours)

### Phase 4: Low-Priority Entity Services (11 hours)
15. [ ] Refactor 11 entity timestamp services (1 hour each)

### Phase 5: Validation & Performance Testing (3 hours)
16. [ ] Run full test suite with `TestTimeProvider`
17. [ ] Measure CI performance improvements
18. [ ] Document actual time savings

---

## Expected Outcomes

### Performance Improvements
- **Test Execution Time**: ~97% reduction for timing-dependent tests
- **CI Pipeline**: ~30-60 seconds saved per run
- **Developer Experience**: Instant test feedback (no waiting)

### Quality Improvements
- **Determinism**: 100% reproducible test results
- **Flakiness**: Elimination of timing-related flakes
- **Debuggability**: Easier to reason about test failures

### Maintenance Benefits
- **Test Clarity**: Explicit time control makes intent obvious
- **Refactoring Safety**: Time-dependent logic isolated and testable
- **Future-Proofing**: Foundation for advanced scenarios (time zones, daylight saving)

---

## Risk Mitigation

### Potential Issues
1. **Breaking Changes**: Some services may have complex timing logic
2. **Test Coverage Gaps**: Edge cases may emerge during refactoring
3. **Integration Tests**: May require additional Testcontainers setup

### Mitigation Strategies
1. **Incremental Rollout**: Refactor one service at a time, verify tests pass
2. **Parallel Testing**: Keep original tests temporarily for comparison
3. **Code Review**: Have Performance Engineer + Quality Engineer review changes
4. **Rollback Plan**: Git branches for easy revert if issues arise

---

## Resources

- **Migration Guide**: `docs/testing/time-provider-migration-guide.md`
- **Infrastructure Code**: `apps/api/tests/Api.Tests/Infrastructure/TestTimeProvider.cs`
- **Helper Methods**: `apps/api/tests/Api.Tests/Helpers/TimeTestHelpers.cs`
- **Example Refactoring**: See migration guide for complete before/after examples

---

**Next Action**: Begin Phase 2 with `SessionAutoRevocationService.cs` refactoring.

**Champion**: Performance Engineer Agent
**Reviewers**: Quality Engineer Agent
**Status**: Ready for Implementation
