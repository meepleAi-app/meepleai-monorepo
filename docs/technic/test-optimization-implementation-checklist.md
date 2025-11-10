# Test Optimization Implementation Checklist

**Document**: Implementation tracking for test-optimization-strategy.md
**Target**: 60-70% execution time reduction (15min → 5-7min)
**Status**: 📋 Planning Phase

---

## Phase 1: Parallelization (Week 1) - Target: 40% Reduction

### Day 1-2: Collection Splitting ⏳

- [ ] **Create New Collection Definitions**
  - [ ] `AdminUserMgmtCollection` (User management tests)
  - [ ] `AdminSessionMgmtCollection` (Session management tests)
  - [ ] `AdminApiKeyMgmtCollection` (API key tests)
  - [ ] `AdminCacheMgmtCollection` (Cache management tests)
  - [ ] `AdminPromptMgmtCollection` (Prompt template tests)

- [ ] **Migrate Test Files**
  - [ ] UserManagementEndpointsTests.cs → AdminUserMgmtCollection
  - [ ] SessionManagementEndpointsTests.cs → AdminSessionMgmtCollection
  - [ ] SessionStatusEndpointsTests.cs → AdminSessionMgmtCollection
  - [ ] ApiKeyManagementEndpointsTests.cs → AdminApiKeyMgmtCollection
  - [ ] ApiKeyAuthenticationIntegrationTests.cs → AdminApiKeyMgmtCollection
  - [ ] CacheAdminEndpointsTests.cs → AdminCacheMgmtCollection
  - [ ] CacheInvalidationIntegrationTests.cs → AdminCacheMgmtCollection
  - [ ] PromptManagementEndpointsTests.cs → AdminPromptMgmtCollection
  - [ ] (Add ConfigurationManagementEndpointsTests.cs if exists)

- [ ] **Verification**
  - [ ] Run tests 10 consecutive times: `for i in {1..10}; do dotnet test || echo "FAIL $i"; done`
  - [ ] Check for intermittent failures
  - [ ] Verify no deadlocks (timeout < 5 min per run)
  - [ ] Document any issues found

**Success Criteria**: All tests pass 10/10 times, total time < 9 minutes

---

### Day 3-4: Cache Isolation ⏳

- [ ] **Implement Cache Isolation Helpers**
  ```csharp
  // IntegrationTestBase.cs
  protected string GetTestCacheKey(string key) => $"test:{TestRunId}:{key}";
  ```

- [ ] **Update Cache Tests**
  - [ ] CacheAdminEndpointsTests.cs - use GetTestCacheKey()
  - [ ] CacheInvalidationIntegrationTests.cs - use GetTestCacheKey()
  - [ ] AiResponseCacheEndToEndTests.cs - use GetTestCacheKey()

- [ ] **Add Cache Isolation Test**
  ```csharp
  [Fact]
  public async Task CacheIsolation_VerifyNoLeakage()
  {
      // Test that parallel tests don't share cache
  }
  ```

- [ ] **Verification**
  - [ ] Run cache tests in parallel: `dotnet test --filter "Cache" --parallel`
  - [ ] Run 20 times to detect flakiness
  - [ ] Check Redis logs for conflicts

**Success Criteria**: Cache tests pass 20/20 times in parallel, no cross-test contamination

---

### Day 5: Configuration Isolation ⏳

- [ ] **Implement Config Isolation Helpers**
  ```csharp
  // IntegrationTestBase.cs
  protected string GetTestConfigKey(string key) => $"Test:{TestRunId}:{key}";

  protected async Task<ConfigurationEntity> CreateTestConfigAsync(...)
  {
      var testKey = GetTestConfigKey(key);
      // ...
  }
  ```

- [ ] **Update Config Tests** (if they exist)
  - [ ] ConfigurationManagementEndpointsTests.cs - use GetTestConfigKey()
  - [ ] LlmServiceConfigurationIntegrationTests.cs - use GetTestConfigKey()

- [ ] **Documentation**
  - [ ] Document cache isolation pattern in test-writing-guide.md
  - [ ] Add examples of when to use GetTestCacheKey()

**Success Criteria**: Config tests pass in parallel, isolation patterns documented

---

### End of Week 1 Checkpoint 🎯

- [ ] **Performance Verification**
  - [ ] Measure total test time (target: <9 minutes)
  - [ ] Calculate improvement percentage
  - [ ] Document actual vs. target

- [ ] **Stability Verification**
  - [ ] Run full test suite 20 times (CI + local)
  - [ ] Calculate pass rate (target: 100%)
  - [ ] Identify any flaky tests

- [ ] **Team Review**
  - [ ] Code review by 2 team members
  - [ ] Demo parallelization improvements
  - [ ] Get approval for Phase 2

**Rollback Trigger**: If pass rate <95%, rollback parallelization and investigate

---

## Phase 2: Database Optimization (Week 2) - Target: +20% Reduction

### Day 1-2: Batch Cleanup ⏳

- [ ] **Implement Batch Cleanup SQL**
  ```csharp
  // IntegrationTestBase.DisposeAsync()
  var sql = @"
      WITH deleted_users AS (
          DELETE FROM users WHERE id = ANY(@userIds) RETURNING id
      ),
      deleted_sessions AS (
          DELETE FROM user_sessions WHERE user_id = ANY(@userIds)
      ),
      -- ...other tables...
  ";
  await db.Database.ExecuteSqlRawAsync(sql, parameters);
  ```

- [ ] **Measure Cleanup Performance**
  - [ ] Before: Average cleanup time per test
  - [ ] After: Average cleanup time per test
  - [ ] Document improvement (target: 85ms → 15ms)

- [ ] **Test Under Load**
  - [ ] Run 20 tests concurrently
  - [ ] Check for deadlocks
  - [ ] Verify all cleanup happens

**Success Criteria**: Cleanup time reduced by 70ms per test, no deadlocks

---

### Day 3-4: Shared Test Users ⏳

- [ ] **Add Shared Users to Fixture**
  ```csharp
  // PostgresCollectionFixture.cs
  public UserEntity AdminUser { get; private set; } = null!;
  public UserEntity EditorUser { get; private set; } = null!;
  public UserEntity RegularUser { get; private set; } = null!;

  // InitializeAsync()
  // Pre-create shared users (one-time PBKDF2 cost)
  ```

- [ ] **Add Helper Method**
  ```csharp
  // IntegrationTestBase.cs
  protected UserEntity GetSharedUser(UserRole role)
  {
      return role switch
      {
          UserRole.Admin => _postgresFixture.AdminUser,
          UserRole.Editor => _postgresFixture.EditorUser,
          UserRole.User => _postgresFixture.RegularUser,
          _ => throw new ArgumentException($"No shared user for role {role}")
      };
  }
  ```

- [ ] **Migrate Read-Only Tests**
  - [ ] Identify tests that don't modify user state (~100 tests)
  - [ ] Replace CreateTestUserAsync with GetSharedUser
  - [ ] Document when to use shared vs unique users

- [ ] **Add Safety Check**
  ```csharp
  // Warn if tests try to modify shared users
  protected void EnsureNotSharedUser(UserEntity user)
  {
      if (user.Email.EndsWith("@test.fixture"))
      {
          throw new InvalidOperationException(
              "Cannot modify shared fixture user. Create unique user for state modification tests.");
      }
  }
  ```

**Success Criteria**: 100 tests using shared users, 15 seconds saved

---

### Day 5: Database Seeding ⏳

- [ ] **Add Shared Games to Fixture**
  ```csharp
  // PostgresCollectionFixture.cs
  public GameEntity ChessGame { get; private set; } = null!;
  public GameEntity TicTacToeGame { get; private set; } = null!;

  // Pre-seed in InitializeAsync()
  ```

- [ ] **Migrate Tests to Use Fixture Games**
  - [ ] Identify tests using "chess" or "tic-tac-toe"
  - [ ] Replace CreateTestGameAsync with fixture properties
  - [ ] Document when to use shared vs unique games

- [ ] **Documentation Update**
  - [ ] Update test-writing-guide.md with shared resource patterns
  - [ ] Add examples of read-only vs state-modifying tests

**Success Criteria**: Common games seeded once, tests using shared resources

---

### End of Week 2 Checkpoint 🎯

- [ ] **Performance Verification**
  - [ ] Measure total test time (target: <7 minutes)
  - [ ] Calculate cumulative improvement (target: 60%)
  - [ ] Breakdown: parallelization + database optimization

- [ ] **Stability Verification**
  - [ ] Run full suite 20 times
  - [ ] Verify no new flaky tests
  - [ ] Check memory usage (no >10% increase)

- [ ] **Team Review**
  - [ ] Demo database optimizations
  - [ ] Review shared resource safety patterns
  - [ ] Get approval for Phase 3

---

## Phase 3: Fixture & CI Optimization (Week 3) - Target: +10% Reduction

### Day 1-2: DbContext Pooling ⏳

- [ ] **Implement DbContext Pool**
  ```csharp
  // DbContextPooledObjectPolicy.cs
  public class DbContextPooledObjectPolicy : IPooledObjectPolicy<MeepleAiDbContext>
  {
      public MeepleAiDbContext Create() { ... }
      public bool Return(MeepleAiDbContext obj)
      {
          obj.ChangeTracker.Clear();
          obj.Database.CloseConnection();
          return true;
      }
  }
  ```

- [ ] **Add Pool to Fixture**
  ```csharp
  // PostgresCollectionFixture.cs
  private readonly ObjectPool<MeepleAiDbContext> _dbContextPool;

  public MeepleAiDbContext GetPooledDbContext() { ... }
  public void ReturnDbContext(MeepleAiDbContext context) { ... }
  ```

- [ ] **Add Helper Method**
  ```csharp
  // IntegrationTestBase.cs
  protected async Task<T> WithDbContextAsync<T>(Func<MeepleAiDbContext, Task<T>> action)
  {
      var context = _postgresFixture.GetPooledDbContext();
      try { return await action(context); }
      finally { _postgresFixture.ReturnDbContext(context); }
  }
  ```

- [ ] **Test Pool Behavior**
  - [ ] Verify ChangeTracker is cleared
  - [ ] Test under 50 concurrent requests
  - [ ] Check for state leakage between tests

**Success Criteria**: DbContext creation overhead reduced 5-10ms per test

---

### Day 3-4: Container Optimization ⏳

- [ ] **Implement Container Reuse (Local Dev)**
  ```csharp
  // PostgresCollectionFixture.cs
  private async Task StartPostgresAsync()
  {
      if (!IsCI())
      {
          var existing = await TryGetExistingContainerAsync(ContainerLabel);
          if (existing != null) { /* reuse */ }
      }

      PostgresContainer = new PostgreSqlBuilder()
          .WithReuse(!IsCI())  // Only reuse in local dev
          .Build();
  }
  ```

- [ ] **Add CI Image Pre-Pull**
  ```yaml
  # .github/workflows/ci.yml
  - name: Pre-pull test container images
    run: |
      docker pull postgres:15-alpine
      docker pull redis:7-alpine || true
      docker pull qdrant/qdrant:latest || true
  ```

- [ ] **Test Parallel Container Start**
  ```csharp
  // If multiple fixtures exist
  public async ValueTask InitializeAsync()
  {
      await Task.WhenAll(
          StartPostgresAsync(),
          StartRedisAsync(),
          StartQdrantAsync()
      );
  }
  ```

**Success Criteria**: Local dev 2.5-4.5s savings, CI 10-20s image pull savings

---

### Day 5: CI Configuration ⏳

- [ ] **Update GitHub Actions Workflow**
  ```yaml
  # .github/workflows/ci.yml
  - name: Run API tests
    timeout-minutes: 10  # Reduced from 20
    run: |
      dotnet test \
        --blame-hang-timeout 5m \
        --logger "trx;LogFileName=test-results.trx"
  ```

- [ ] **Add Test Sharding (Optional)**
  ```yaml
  strategy:
    matrix:
      shard: [1, 2, 3, 4]
  steps:
    - name: Run tests (shard ${{ matrix.shard }}/4)
      run: dotnet test --shard ${{ matrix.shard }}
  ```

- [ ] **Configure Docker Buildx Cache**
  ```yaml
  - name: Set up Docker Buildx
    uses: docker/setup-buildx-action@v2

  - name: Cache Docker layers
    uses: actions/cache@v3
    with:
      path: /tmp/.buildx-cache
      key: ${{ runner.os }}-buildx-${{ hashFiles('**/Dockerfile') }}
  ```

**Success Criteria**: CI runs complete in <10 minutes, timeouts reduced

---

### End of Week 3 Checkpoint 🎯

- [ ] **Performance Verification**
  - [ ] Measure total test time (target: 5-7 minutes)
  - [ ] Calculate cumulative improvement (target: 70%)
  - [ ] Measure CI vs. local dev improvements

- [ ] **Stability Verification**
  - [ ] Run CI 20 times (check Actions history)
  - [ ] Verify container reuse works locally
  - [ ] Check for CI timeout failures

---

## Phase 4: Monitoring & Validation (Week 4)

### Day 1-2: Performance Instrumentation ⏳

- [ ] **Add Test Timing Scope**
  ```csharp
  // IntegrationTestBase.cs
  protected class TestTimingScope : IAsyncDisposable
  {
      // ...implementation...
  }
  ```

- [ ] **Add Performance Metrics**
  ```csharp
  // TestPerformanceMetrics.cs
  public static class TestPerformanceMetrics
  {
      public static void RecordTiming(string testName, long ms) { ... }
      public static void PrintSummary() { ... }
  }
  ```

- [ ] **Update Tests**
  - [ ] Add TestTimingScope to 10 slowest tests
  - [ ] Hook into fixture disposal for summary

**Success Criteria**: Timing instrumentation in place, slowest tests identified

---

### Day 3-4: Regression Prevention ⏳

- [ ] **Create Baseline Timings**
  ```bash
  # Run tests and capture timings
  dotnet test --logger "trx" > baseline-timings.trx
  python scripts/extract-timings.py baseline-timings.trx > baseline-timings.json
  ```

- [ ] **Add Performance Regression Check**
  ```yaml
  # .github/workflows/ci.yml
  - name: Check for performance regression
    run: |
      python scripts/check-performance-regression.py \
        --baseline baseline-timings.json \
        --current test-results.xml \
        --threshold 1.2
  ```

- [ ] **Create Scripts**
  - [ ] `scripts/extract-timings.py` - Parse TRX to JSON
  - [ ] `scripts/check-performance-regression.py` - Compare timings
  - [ ] `scripts/analyze-test-performance.py` - Generate HTML report

**Success Criteria**: Performance regression check runs in CI, fails on >20% slowdown

---

### Day 5: Final Validation & Documentation ⏳

- [ ] **Run Full Test Suite 20 Times**
  - [ ] 10 times in CI (GitHub Actions)
  - [ ] 10 times locally (different machines if possible)

- [ ] **Document All Optimizations**
  - [ ] Update docs/testing/test-writing-guide.md
  - [ ] Update docs/testing/test-patterns.md
  - [ ] Create docs/testing/test-optimization-results.md

- [ ] **Create Rollback Procedures**
  - [ ] Document emergency rollback (feature flag)
  - [ ] Document partial rollback (per-collection)
  - [ ] Document phased rollback (week-by-week)

- [ ] **Team Presentation**
  - [ ] Prepare slides (before/after metrics)
  - [ ] Demo local dev workflow
  - [ ] Demo CI improvements
  - [ ] Knowledge transfer session (30 min)

**Success Criteria**: 20/20 test runs pass, documentation complete, team trained

---

### End of Week 4 Checkpoint 🎯

- [ ] **Final Performance Verification**
  - [ ] Total test time: ______ minutes (target: 5-7 minutes)
  - [ ] Improvement: ______ % (target: 60-70%)
  - [ ] Breakdown:
    - [ ] Parallelization: ______ %
    - [ ] Database optimization: ______ %
    - [ ] Fixture optimization: ______ %

- [ ] **Final Stability Verification**
  - [ ] CI pass rate: ______ % (target: 100%)
  - [ ] Flaky tests: ______ (target: 0)
  - [ ] Memory increase: ______ % (target: <10%)

- [ ] **Deliverables Complete**
  - [ ] All code merged to main
  - [ ] All documentation updated
  - [ ] Team trained on new patterns
  - [ ] Monitoring dashboards created

---

## Rollback Decision Matrix

| Situation | Action | Timeline |
|-----------|--------|----------|
| **CI broken (pass rate <90%)** | Emergency rollback (feature flag) | Immediate (15 min) |
| **Specific collection flaky** | Partial rollback (re-enable DisableParallelization) | Same day |
| **Performance gain <20%** | Investigate, consider rollback Week 3 | 2 days |
| **Memory issues** | Rollback Week 2-3 (pooling, shared resources) | 1 day |
| **Deadlocks in production** | Full rollback to Week 0 | Immediate (30 min) |

---

## Success Metrics Dashboard

**Create GitHub Issue with this template**:

```markdown
## Test Optimization Progress

### Week 1: Parallelization
- [ ] Collections split: ___/5
- [ ] Tests migrated: ___/150
- [ ] Time reduction: ___% (target: 40%)
- [ ] Pass rate: ___% (target: 100%)

### Week 2: Database Optimization
- [ ] Batch cleanup implemented: ___
- [ ] Shared users implemented: ___
- [ ] Tests using shared users: ___/100
- [ ] Time reduction: ___% (target: 60% cumulative)

### Week 3: Fixture & CI
- [ ] DbContext pooling: ___
- [ ] Container reuse: ___
- [ ] CI image pre-pull: ___
- [ ] Time reduction: ___% (target: 70% cumulative)

### Week 4: Monitoring
- [ ] Instrumentation: ___
- [ ] Regression prevention: ___
- [ ] Documentation: ___
- [ ] Final time: ___ minutes (target: 5-7 min)

### Final Results
**Before**: 15 minutes
**After**: ___ minutes
**Improvement**: ___% (target: 60-70%)
**Pass Rate**: ___% (target: 100%)
```

---

## Notes & Observations

### Week 1 Notes
_Record any issues, surprises, or learnings here_

### Week 2 Notes
_Record any issues, surprises, or learnings here_

### Week 3 Notes
_Record any issues, surprises, or learnings here_

### Week 4 Notes
_Record any issues, surprises, or learnings here_

---

**Last Updated**: 2025-11-10
**Status**: 📋 Planning Phase
**Next Action**: Team review of test-optimization-strategy.md
