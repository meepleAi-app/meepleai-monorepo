# E2E-007: Reduce Hardcoded Timeouts

**Issue**: #1493
**Priority**: 🟡 MEDIUM
**Effort**: 15-20h (phased approach)
**Status**: 🔄 IN PROGRESS (Phase 1)

---

## Problem Statement

**Current State**: 188 `waitForTimeout()` occurrences across 60 E2E test files
**Target**: 40-50 occurrences (75% reduction, ~140 timeout eliminated)
**Impact**: Fragile tests, slow execution, unreliable CI/CD

### Distribution Analysis

```
500ms:     74 (39%) - Mostly assertion-adjacent
1000ms:    63 (34%) - UI state transitions
2000ms:    19 (10%) - Network/API waits
>2000ms:    5 (3%)  - CRITICAL (6s, 5s, 3s)
<500ms:    27 (14%) - Animation/debounce
```

### Breakdown by Type

- **Mock Latencies** (~80, 42%): Inside `route.fulfill()` - simulate server lag
- **UI Waits** (~108, 58%): Outside route handlers - REPLACEABLE

---

## Solution: Opzione B (Balanced Aggressive)

### Phased Approach (3 Phases)

#### Phase 1: Eliminate Critical UI Waits (2-3h)
**Target**: -25 timeout >=2000ms

**Strategy**:
1. **error-handling.spec.ts**: 6000ms → `waitForApiResponse('/logs/client')`
2. **user-journey-upload-chat.spec.ts**: 5000ms → `waitForPdfProcessingComplete()`
3. **chat-edit-delete.spec.ts**: 3x 2000-3000ms → `waitForNetworkIdle()`
4. **auth-2fa-complete.spec.ts**: 3x 2000ms → `waitForNetworkIdle() + assertion`
5. **Other files** (15+): 2000-2500ms → WaitHelper specialized methods

**Files Modified**: ~18 high-priority files

---

#### Phase 2: Optimize Mock Latencies (3-4h)
**Target**: -30 mock timeout (reduce 2000ms→500ms where safe)

**Criteria for Reduction**:
- ✅ SSE streaming: 2000ms→500ms (response already mocked, instant)
- ✅ PDF processing mocks: 2000ms→800ms (safe for UI updates)
- ✅ OAuth redirects: 2000ms→500ms (mock callback instant)
- ❌ Race condition tests: PRESERVE timing-sensitive scenarios

**Documentation**:
- Add comments: `// MOCK: simulate slow server (500ms)`
- Mark preserved timeouts with rationale

**Files Modified**: ~20 files with mock timeouts

---

#### Phase 3: Assertion-Adjacent Waits (2-3h)
**Target**: -50 timeout 500-1000ms near `expect()`

**Pattern**:
```typescript
// ❌ Before
await page.waitForTimeout(500);
await expect(element).toBeVisible();

// ✅ After
await expect(element).toBeVisible({ timeout: 3000 }); // Playwright auto-retry
```

**Hotspot Files**:
- `chat-*.spec.ts`: ~30 occurrences
- `pdf-*.spec.ts`: ~10 occurrences
- `admin-*.spec.ts`: ~10 occurrences

**Files Modified**: ~25 files

---

## WaitHelper API

**Location**: `apps/web/e2e/helpers/WaitHelper.ts` (250 lines, 10 methods)

### Smart Wait Methods

```typescript
// SSE Streaming
await waitHelper.waitForStreamingComplete({
  messageText: 'expected response',
  timeout: 10000
});

// PDF Processing
await waitHelper.waitForPdfProcessingComplete('file.pdf', 'completed', 30000);

// OAuth Callback
await waitHelper.waitForOAuthCallback('google', 5000);

// CSS Animations
await waitHelper.waitForAnimationComplete('.fade-in', 3000);

// Network Idle
await waitHelper.waitForNetworkIdle(10000);

// DOM Stability (React setState batching)
await waitHelper.waitForDOMStable('.chat-message', 5000);

// API Response
await waitHelper.waitForApiResponse('/api/endpoint', 200, 10000);

// Element Actionable
await waitHelper.waitForActionable('button#submit', 5000);

// Generic State Predicate
await waitHelper.waitForState(() => window.appReady, true, 5000);
```

---

## Metrics Target

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| **Total timeouts** | 188 | 40-50 | -140 (-75%) |
| **UI waits** | 108 | <10 | -100 (-92%) |
| **Mock latencies** | 80 | 40-50 | -30 (-40%) |
| **Test duration** | 8-10min | 5-6min | -35% |
| **Flaky rate** | ~5% | <3% | Improved stability |

---

## Implementation Progress

### Phase 1: Critical UI Waits ✅ (PARTIAL)

**Completed** (5 timeout eliminated):
- ✅ `error-handling.spec.ts`: 6000ms → `waitForApiResponse`
- ✅ `chat-streaming.spec.ts`: 4 UI waits (100ms, 200ms, 500ms × 2) → smart waits

**Pending** (20 timeout):
- ⏳ `user-journey-upload-chat.spec.ts`: 5000ms + 3x 2000ms
- ⏳ `chat-edit-delete.spec.ts`: 2000ms, 3000ms, 2000ms
- ⏳ `auth-2fa-complete.spec.ts`: 3x 2000ms
- ⏳ `pdf-upload-journey.spec.ts`: 2x 2000ms
- ⏳ 15+ other files

### Phase 2: Mock Latencies ⏳ (PENDING)

**Target Files**:
- `chat-streaming.spec.ts`: 6 mock timeout (2000ms→500ms)
- `qa-streaming-sse.spec.ts`: 2 mock timeout
- `chat-animations.spec.ts`: 3 mock timeout (2000-2500ms)
- Others: ~20 files

### Phase 3: Assertion-Adjacent ⏳ (PENDING)

**Pattern Detection Script**:
```bash
# Find waitForTimeout followed by expect within 5 lines
grep -A5 "waitForTimeout" e2e/**/*.spec.ts | grep "expect"
```

---

## Quality Gates

- ✅ Zero test regressions (all tests passing)
- ✅ <5% flaky rate increase
- ✅ TypeScript compilation passing
- ✅ ESLint passing
- ⏳ E2E test suite run (<10min target)

---

## Testing Strategy

### Pre-Refactor Baseline
```bash
cd apps/web
pnpm test:e2e 2>&1 | tee /tmp/e2e-baseline.log
```

### Post-Phase Validation
```bash
# After each phase
pnpm test:e2e --grep "MODIFIED_FILES_PATTERN"

# Full regression
pnpm test:e2e
```

### Metrics Collection
```bash
# Count remaining timeouts
grep -r "waitForTimeout" e2e/ --include="*.spec.ts" | wc -l

# Duration comparison
grep "Test Suites:" /tmp/e2e-*.log
```

---

## Risks & Mitigations

### Risk 1: Increased Flaky Tests
**Mitigation**:
- Incremental phase approach (validate after each phase)
- Preserve timeouts for documented race conditions
- Use `timeout` parameters in assertions (3-5s auto-retry)

### Risk 2: Test Duration Increase
**Mitigation**:
- Optimize mock latencies (Phase 2)
- Use parallel test execution where possible
- Monitor duration metrics per phase

### Risk 3: Breaking Test Realism
**Mitigation**:
- Preserve mock timeouts where timing is critical
- Document rationale for preserved timeouts
- Review with team for user journey tests

---

## References

**Best Practices** (Tavily Research 2024):
- [Playwright Auto-Waiting](https://playwright.dev/docs/actionability)
- [BrowserStack Scalable Scripts](https://www.browserstack.com/guide/playwright-scripts)
- [Checkly Waits Guide](https://www.checklyhq.com/docs/learn/playwright/waits-and-timeouts/)
- [TestingMint Optimization](https://testingmint.com/chapter-17-playwright-advanced-tips-and-optimization/)

**Key Principles**:
1. **Never use static timeouts** - rely on auto-waiting or explicit conditions
2. **Web-first assertions** - `expect().toBeVisible()` has built-in retry
3. **Event-driven waits** - `waitForResponse`, `waitForLoadState('networkidle')`
4. **Stable selectors** - roles, labels, `data-testid` > brittle CSS/XPath

---

## Next Actions

### Immediate (Current Session)
1. ✅ Create `WaitHelper.ts` with 10 smart wait methods
2. ✅ Refactor 2 critical files (5 timeout eliminated)
3. ⏳ Create checkpoint commit
4. ⏳ Continue Phase 1 (remaining 20 critical timeout)

### Short-term (Next Session)
1. Complete Phase 1 (all >=2000ms UI waits)
2. Start Phase 2 (mock latency optimization)
3. Run E2E regression tests
4. Generate metrics report

### Long-term (Future PRs)
1. Complete Phase 3 (assertion-adjacent waits)
2. Final validation and metrics
3. Update testing guide with WaitHelper patterns
4. Team review and merge

---

**Last Updated**: 2025-11-29
**Author**: Engineering Team
**Version**: 1.0 (Phase 1 in progress)
