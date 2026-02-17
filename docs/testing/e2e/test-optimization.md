# E2E Test Optimization Guide

**Issue**: #3082 Phase B - Data-Driven Test Performance Optimization

## Overview

This guide explains the E2E test optimization system for shard balancing and performance monitoring.

### Current Configuration

| Metric | Value |
|--------|-------|
| **Total Tests** | 233 files |
| **Shards** | 6 parallel shards |
| **Avg Tests/Shard** | ~39 tests |
| **Target Duration** | <10 minutes |

---

## Test Tagging System

### Tag Definitions

| Tag | Criteria | Purpose |
|-----|----------|---------|
| `@slow` | >50 test cases OR >500 lines OR complex setup | Identify performance-heavy tests |
| `@fast` | <15 test cases OR <200 lines OR simple assertions | Quick smoke tests |
| `@smoke` | Critical path tests | Must-pass tests for deployment |

### Usage

**Add to JSDoc comment at file top**:
```typescript
/**
 * E2E Test: Feature Name
 * @slow - 119 test cases, complex visual interactions
 */
```

### Tagged Tests (Top 9)

| File | Tests | Tag | Reason |
|------|-------|-----|--------|
| `rag-strategy-builder.spec.ts` | 135 | @slow | Massive test suite |
| `admin/rag-strategy-builder.spec.ts` | 119 | @slow | Drag-and-drop complexity |
| `public-layout.spec.ts` | 84 | @slow | Comprehensive layout tests |
| `library-filters-views.spec.ts` | 66 | @slow | Filter combinations |
| `auth.spec.ts` | 65 | @slow | Real backend integration |
| `admin-infrastructure.spec.ts` | 62 | @slow | Prometheus polling |
| `editor.spec.ts` | 58 | @slow | Rich text editing |
| `auth-email-registration-flow.spec.ts` | 49 | @slow | Complete user journey |
| `settings/profile-settings.spec.ts` | 44 | @slow | Settings workflows |

---

## Duration Reporter

### Purpose

Collects actual test execution metrics for data-driven optimization decisions.

### Output

**Location**: `test-results/duration-metrics-shard-{N}.json`

**Schema**:
```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "shard": 1,
  "totalShards": 6,
  "totalDuration": 450000,
  "testCount": 39,
  "tests": [
    {
      "file": "apps/web/e2e/admin/rag-strategy-builder.spec.ts",
      "duration": 45000,
      "status": "passed",
      "retries": 0
    }
  ]
}
```

### Integration

Reporter automatically activated in `playwright.config.ts`:
```typescript
reporter: [
  ['html'],
  ['@bgotink/playwright-coverage'],
  ['./e2e/reporters/duration-reporter.ts'], // Duration tracking
]
```

### Console Output

```
📊 Duration Reporter: Tracking 39 tests
   Shard: Shard 1/6

📊 Duration Metrics:
   Total Duration: 124.50s
   Test Count: 39
   Average: 3.19s per test

   🐌 Slowest Tests:
      1. rag-strategy-builder.spec.ts: 45.23s
      2. admin-infrastructure.spec.ts: 38.12s
      3. auth.spec.ts: 32.45s
```

---

## Analytics Script

### Purpose

Analyzes shard distribution balance and identifies optimization opportunities.

### Usage

```bash
# Run E2E tests with metrics collection
pnpm test:e2e:parallel

# Analyze distribution
pnpm test:e2e:analyze
```

### Output

```
================================================================================
📊 E2E SHARD DISTRIBUTION ANALYSIS
================================================================================

Per-Shard Statistics:

Shard | Tests | Total Duration | Avg Duration | % of Total
----------------------------------------------------------------------
  1   | 39    | 124.50s        | 3.19s        | 16.8%
  2   | 38    | 118.30s        | 3.11s        | 15.9%
  3   | 40    | 152.40s        | 3.81s        | 20.5%
  4   | 37    | 110.20s        | 2.98s        | 14.8%
  5   | 39    | 125.60s        | 3.22s        | 16.9%
  6   | 40    | 112.00s        | 2.80s        | 15.1%
----------------------------------------------------------------------
Total | 233   | 743.00s        | 3.19s        | 100%


Balance Metrics:

  Average Duration per Shard: 123.83s
  Standard Deviation: 14.52s
  Coefficient of Variation: 11.7% ✅ OK


🐌 Top 15 Slowest Tests:

Rank | Duration | Shard | File
----------------------------------------------------------------------
   1  | 45.23s |   3   | admin/rag-strategy-builder.spec.ts
   2  | 38.12s |   1   | admin-infrastructure.spec.ts
   3  | 32.45s |   2   | auth.spec.ts
   ...


✅ Shards are well-balanced! No action needed.

================================================================================
```

### Interpretation

**Coefficient of Variation (CV)**:
- **<20%**: Well-balanced, no action needed
- **20-30%**: Moderate imbalance, consider optimization
- **>30%**: High imbalance, rebalancing recommended

**Actions Based on CV**:
- **CV <20%**: Continue monitoring, Phase B complete
- **CV 20-30%**: Implement Phase C (custom test groups)
- **CV >30%**: Split large tests or use manual shard assignment

---

## Optimization Workflow

### Phase A: Quick Wins ✅ COMPLETE
- ✅ Increased shards: 4 → 6
- ✅ Added browser caching
- **Result**: 30-35% faster execution

### Phase B: Measure & Tag ✅ COMPLETE
- ✅ Duration reporter implemented
- ✅ Top 9 slow tests tagged
- ✅ Analytics script created
- **Result**: Foundation for data-driven decisions

### Phase C: Balance & Optimize (Future)

**Trigger**: CV >20% after data collection

**Actions**:
1. **Shard Balancing**: Custom Playwright projects with manual test distribution
2. **Test Grouping**: Separate slow/fast test execution
3. **File Splitting**: Break mega-tests (>100 cases) into smaller files
4. **Conditional Runs**: Skip slow tests on draft PRs, run on merge only

**Example - Custom Shard Groups**:
```typescript
// playwright.config.ts
projects: [
  {
    name: 'shard-1-fast',
    testMatch: /.*(?<!\.slow)\.spec\.ts$/,
    fullyParallel: true,
  },
  {
    name: 'shard-2-slow',
    testMatch: /.*\.slow\.spec\.ts$/,
    fullyParallel: false, // Serial for stability
  },
]
```

---

## NPM Scripts

| Command | Purpose |
|---------|---------|
| `pnpm test:e2e:parallel` | Run 6 shards locally with duration tracking |
| `pnpm test:e2e:analyze` | Analyze shard distribution from last run |
| `pnpm test:e2e:shard1-6` | Run individual shard |

---

## Monitoring & Maintenance

### Regular Analysis

**Frequency**: After major test additions (every 10+ new tests)

**Process**:
1. Run full suite: `pnpm test:e2e:parallel`
2. Analyze: `pnpm test:e2e:analyze`
3. Check CV: If >20%, proceed to Phase C
4. Review slowest tests: Consider splitting if >60s

### Continuous Improvement

**Monthly Review**:
- Review slowest tests list
- Identify optimization opportunities (mocking, setup optimization)
- Update tags as test suite evolves
- Monitor shard balance trends

**Quarterly Optimization**:
- Re-evaluate shard count (6 → 8 if suite grows >300 tests)
- Review test categories for better grouping
- Consider test file splitting for mega-tests
- Update browser caching strategy

---

## Best Practices

### When Writing New Tests

**✅ DO**:
- Keep test files <500 lines
- Limit to 30-40 test cases per file
- Extract common setup to fixtures
- Use Page Object Model

**❌ AVOID**:
- Mega-test files (>100 test cases)
- Duplicated setup in each test
- Long sequential test chains
- Slow animations without skip flags

### Tag Guidelines

**Add @slow if**:
- File has >40 test cases
- Involves complex animations or polling
- Uses real backend with heavy operations
- Known to take >30s execution time

**Add @fast if**:
- File has <15 test cases
- Simple assertions only
- No external dependencies
- Executes in <5s

**Add @smoke if**:
- Critical user path (login, register, checkout)
- Must pass for deployment
- Part of pre-merge quality gate

---

## Troubleshooting

### No Metrics Generated

**Symptom**: `duration-metrics-shard-*.json` not created

**Solutions**:
1. Check reporter in `playwright.config.ts`
2. Verify `test-results/` directory exists
3. Run with: `pnpm test:e2e:parallel` (not single shard)

### High Variance Detected

**Symptom**: CV >20% in analytics

**Root Causes**:
- Large tests clustered in one shard (alphabetical sorting)
- Slow tests not evenly distributed
- Backend latency affecting specific shards

**Solutions**:
1. Review slowest tests list
2. Check shard with highest duration
3. Implement Phase C custom grouping
4. Consider splitting large test files

### Analytics Script Errors

**Symptom**: "No duration metrics found"

**Solutions**:
1. Run tests first: `pnpm test:e2e:parallel`
2. Check `test-results/` for `duration-metrics-*.json` files
3. Verify reporter output in test logs

---

## Future Enhancements (Phase C)

### Smart Test Distribution
- Hash-based sharding (experimental Playwright feature)
- Duration-aware shard assignment
- Automatic balancing based on historical data

### Advanced Analytics
- Trend analysis (duration over time)
- Flakiness detection (retry rate tracking)
- Test stability scoring
- Shard performance dashboard

### Conditional Execution
- Skip @slow tests on draft PRs
- Run @smoke tests only for quick validation
- Full suite only on merge to main
- Parallel browser runs for @fast tests only

---

**Last Updated**: 2026-02-13
**Related Issues**: #3082
**Related PRs**: #4300 (Phase A), #TBD (Phase B)
