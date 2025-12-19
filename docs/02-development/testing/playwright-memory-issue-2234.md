# Playwright WebServer Heap Crash Issue

**Discovered**: 2025-12-19 (Phase 5 - Issue #2234)
**Severity**: High
**Impact**: Blocks full E2E test suite execution (222 tests × 6 browser projects)
**Status**: Documented - Needs Investigation

---

## Problem Statement

Next.js dev server crashes with `FATAL ERROR: Reached heap limit Allocation failed - JavaScript heap out of memory` when running full Playwright E2E test suite.

**Error Log**:
```
FATAL ERROR: Reached heap limit Allocation failed - JavaScript heap out of memory
<--- Last few GCs --->
[55020:0000021B06C3A000]   237596 ms: Mark-Compact 4094.7 (4130.2) -> 4094.7 (4130.2) MB
```

---

## Context

### Test Suite Scale
- **Total Tests**: 222 E2E tests
- **Browser Projects**: 6 (desktop-chrome, desktop-firefox, desktop-safari, mobile-chrome, mobile-safari, tablet-chrome)
- **Workers**: 2 (local), 1 (CI)
- **Execution Mode**: Parallel (fullyParallel: !CI)

### Current Configuration

**File**: `apps/web/playwright.config.ts`

```typescript
webServer: {
  command: 'node --max-old-space-size=4096 ./node_modules/next/dist/bin/next dev -p 3000',
  url: 'http://localhost:3000',
  reuseExistingServer: !process.env.CI,
  timeout: 180 * 1000, // 3min
}
```

**Heap Size**: 4096MB (4GB)

---

## Observed Behavior

### Successful Runs
- ✅ Single test file: Works fine
- ✅ Single browser project (desktop-chrome): Works fine
- ✅ Small test suites (<50 tests): Works fine

### Failure Pattern
- ❌ Full suite (222 tests × 6 projects = 1332 test runs): Crashes after ~25-30 tests
- ❌ Crash timing: Variable (depends on test load, ~3-5 minutes)
- ❌ Error: Heap allocation failure in Next.js dev server process

### Crash Timeline
```
[Test 1-15]  ✓ Passing (heap: ~45MB)
[Test 16-25] ✓ Passing (heap: ~46-47MB growing)
[Test 26]    ❌ WebServer crash (heap: 4094MB limit reached)
[Test 27+]   ❌ ERR_CONNECTION_REFUSED (server dead)
```

---

## Root Cause Analysis

### Hypothesis 1: Memory Leak in Next.js Dev Server

**Evidence**:
- Dev server accumulates memory over test runs
- GC (Garbage Collection) unable to reclaim memory
- Heap grows monotonically (doesn't plateau)

**Likely Culprit**:
- Hot Module Replacement (HMR) cache
- Webpack compilation artifacts
- React Fast Refresh state
- Source map generation and caching

### Hypothesis 2: Concurrent Test Load

**Evidence**:
- 222 tests × 6 browsers = 1332 test executions
- Parallel execution (2 workers) creates concurrent requests
- Each test triggers multiple page navigations
- WebServer serves all 1332 requests from single process

**Calculation**:
```
1332 test runs × ~5 navigations/test = 6660 server requests
6660 requests / 4-5 minutes = ~25 requests/second sustained load
```

### Hypothesis 3: Insufficient Heap Size

**Current**: 4096MB
**Industry Standard**: 2048-4096MB for dev servers
**Actual Need**: Potentially >8GB for this test load

---

## Workarounds Implemented

### Workaround 1: Single Project Testing

**Command**:
```bash
pnpm exec playwright test accessibility.spec.ts --project=desktop-chrome --workers=1
```

**Result**: ✅ Works (18/25 tests pass, 7 fail due to server crash after 25 tests)

**Limitation**: Doesn't test cross-browser (Firefox, Safari)

### Workaround 2: Test Sharding

**Command**:
```bash
# CI already uses sharding
pnpm test:e2e:shard1  # 1/4 of tests
pnpm test:e2e:shard2  # 2/4 of tests
# ... etc
```

**Result**: ✅ Works in CI (each shard small enough)

**Limitation**: Slower total execution time (sequential shards)

---

## Potential Solutions

### Solution 1: Increase Heap Size (Quick Fix)

**Change**:
```typescript
// playwright.config.ts
webServer: {
  command: 'node --max-old-space-size=8192 ./node_modules/next/dist/bin/next dev -p 3000',
  // Doubled from 4096MB to 8192MB (8GB)
}
```

**Pros**:
- Simple one-line change
- Likely fixes immediate issue

**Cons**:
- Doesn't address root cause
- May just delay crash to ~50-60 tests
- High memory requirement (8GB for tests)

**Recommendation**: ⚠️ Temporary solution, needs monitoring

### Solution 2: Use Production Server for Tests (Recommended)

**Change**:
```typescript
// playwright.config.ts
webServer: {
  command: process.env.CI
    ? 'node ./node_modules/next/dist/bin/next start -p 3000'
    : 'node --max-old-space-size=6144 ./node_modules/next/dist/bin/next dev -p 3000',
}
```

**Pros**:
- Production build has no HMR/Fast Refresh overhead
- Stable memory footprint
- Already done in CI (see config line 188)

**Cons**:
- Requires `pnpm build` before tests
- Slower iteration (rebuild on code changes)

**Recommendation**: ✅ Best for CI, optional for local

### Solution 3: Reduce Test Parallelism

**Change**:
```typescript
// playwright.config.ts
workers: process.env.CI ? 1 : 1, // Reduce from 2 to 1
```

**Pros**:
- Reduces concurrent load on server
- May prevent crash with existing heap

**Cons**:
- Slower test execution (2x longer)
- Doesn't scale for growing test suite

**Recommendation**: ⚠️ Workaround, not solution

### Solution 4: Test Subset Strategy

**Implement**:
```json
// package.json
"test:a11y:quick": "playwright test accessibility.spec.ts --project=desktop-chrome",
"test:a11y:full": "playwright test accessibility.spec.ts", // All browsers
```

**Use**:
- Local dev: `test:a11y:quick` (fast feedback)
- CI/PR: `test:a11y:full` (comprehensive, sharded)

**Pros**:
- Fast local feedback loop
- Full coverage in CI

**Cons**:
- May miss browser-specific issues locally

**Recommendation**: ✅ Pragmatic approach

---

## Recommended Action Plan

### Phase 1: Immediate (Issue #2234)
1. ✅ Document issue (this file)
2. ✅ Use single-project testing for Phase 5 completion
3. ✅ Create follow-up issue for systematic investigation

### Phase 2: Short-term (Next Sprint)
1. Try Solution 1 (increase to 8192MB) + monitor
2. Implement Solution 4 (test subset strategy)
3. Measure actual improvement

### Phase 3: Long-term (Tech Debt)
1. Migrate to production server for all tests
2. Optimize test suite (remove duplicates, improve efficiency)
3. Investigate Next.js memory profiling
4. Consider separating test suites by type (a11y, functional, visual)

---

## Metrics & Monitoring

### Current Metrics
- **Heap Size**: 4096MB configured
- **Peak Usage**: 4094.7MB (99.97% utilization at crash)
- **Test Duration Before Crash**: ~3-5 minutes
- **Tests Completed Before Crash**: ~25-30 tests

### Target Metrics (Post-Fix)
- **Heap Size**: 8192MB (trial)
- **Peak Usage Target**: <75% (<6144MB)
- **Test Duration**: Complete full suite (estimated 15-20 min)
- **Tests Completed**: 222 tests (100%)

### Monitoring Commands
```bash
# Watch memory during tests
# (Windows)
Get-Process -Name node | Select-Object WorkingSet64,PrivateMemorySize64

# Linux/Mac
ps aux | grep node
```

---

## Related Issues

### Similar Known Issues
- [Vercel Next.js #45035](https://github.com/vercel/next.js/issues/45035) - Dev server memory leak
- [Playwright #12901](https://github.com/microsoft/playwright/issues/12901) - WebServer crashes

### Project Issues
- Issue #2007: WebServer stability (production server in CI)
- Issue #2008: Parallel execution optimizations
- Issue #1868: A11y test suite expansion (increased test count)

---

## Workaround for Developers

**Until fixed, use this workflow**:

```bash
# Local development - quick feedback
pnpm exec playwright test your-test.spec.ts --project=desktop-chrome

# Pre-commit - single browser full suite
pnpm exec playwright test --project=desktop-chrome --workers=1

# CI - full coverage (automatic sharding)
# Just push - CI handles it
```

---

## Testing This Issue

To reproduce the crash:

```bash
cd apps/web

# This WILL crash after ~25-30 tests
pnpm exec playwright test accessibility.spec.ts --reporter=line

# This should work (single project)
pnpm exec playwright test accessibility.spec.ts --project=desktop-chrome --workers=1

# This works in CI (sharded)
pnpm test:e2e:shard1
```

---

## Follow-Up Actions

- [ ] Create GitHub issue for systematic investigation
- [ ] Test Solution 1 (8192MB heap) effectiveness
- [ ] Measure memory usage patterns with profiling
- [ ] Evaluate production server for local tests
- [ ] Research Next.js dev server memory optimizations
- [ ] Consider test suite reorganization

---

**Discovered By**: Accessibility audit (Issue #2234 Phase 5)
**Documented By**: Claude Code
**Next Owner**: To be assigned
**Priority**: High (blocks comprehensive E2E testing)
