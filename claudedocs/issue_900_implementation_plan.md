# Issue #900 - Infrastructure Tests Optimization - Implementation Plan

**Date**: 2025-12-11  
**Status**: 🔄 **IN PROGRESS** (Phase 1 Complete)  
**Branch**: `feature/issue-900-infrastructure-tests`  
**Epic**: #890 (FASE 2: Infrastructure Monitoring)  
**Dependencies**: #896, #897, #898, #899 (ALL COMPLETE)  

---

## 📋 Objective

Optimize and enhance test suite for infrastructure components to achieve:
- **Coverage**: 92-95% (realistic target)
- **Performance**: <4s runtime (safer than <5s target)
- **Quality**: Modern test patterns, maintainability
- **Visual**: Chromatic visual regression tests

---

## ✅ Phase 1: Diagnostics & Critical Fixes (COMPLETE - 2h)

### Completed Tasks

1. ✅ **Identified Root Causes**
   - AdminLayout not mocked → complex component breaking tests
   - useEffect dependency loop (`failureCount` in deps) → infinite re-renders
   - Locale-dependent formatting (15.234 vs 15,234)
   - Multiple elements with same text ("Sano")

2. ✅ **Fixed Component Bugs**
   - `infrastructure-client.tsx`: Fixed useEffect loop
   - Changed `setFailureCount(failureCount + 1)` to `setFailureCount(prev => prev + 1)`
   - Removed `failureCount` from useCallback dependencies

3. ✅ **Fixed Test Mocks**
   - Added AdminLayout mock in both test files
   - Fixed locale-dependent assertions (regex patterns)
   - Fixed multiple elements assertions (getAllByText)

4. ✅ **Results**
   - Basic tests: 4/4 passing (was 0/4)
   - Component bug fixed (no more infinite loops)
   - Test runtime: 3.34s (was >30s with timeouts)

### Files Modified
- `apps/web/src/app/admin/infrastructure/infrastructure-client.tsx`
- `apps/web/src/app/admin/infrastructure/__tests__/infrastructure-client.test.tsx`
- `apps/web/src/app/admin/infrastructure/__tests__/infrastructure-client-basic.test.tsx`

### Commits
- `8f535be26` - fix(issue-900): Fix infrastructure tests - AdminLayout mock + useEffect loop

---

## 🔄 Phase 2: Test Architecture Refactoring (IN PROGRESS - Est. 3-4h)

### Objective
Create modern test architecture with shared utilities and optimized patterns.

### Tasks

#### 2.1 Test Utilities (0.5h)
- [x] Create `__tests__/helpers/test-utils.tsx`
  - [x] Mock data factories (createHealthyInfraData, createDegradedInfraData)
  - [ ] Complete all utility functions
  - [ ] Add more data variations

#### 2.2 Unit Tests - Base Components (2h)
- [ ] `ServiceHealthMatrix.test.tsx` - Optimize & enhance
  - [ ] Remove duplicates
  - [ ] Add missing edge cases
  - [ ] Target: 95%+ coverage, <500ms

- [ ] `ServiceCard.test.tsx` - Optimize & enhance
  - [ ] Remove duplicates
  - [ ] Add interaction tests
  - [ ] Target: 95%+ coverage, <300ms

- [ ] `MetricsChart.test.tsx` - Optimize & enhance
  - [ ] Add Chart.js interaction tests
  - [ ] Add data validation tests
  - [ ] Target: 95%+ coverage, <400ms

#### 2.3 Integration Tests (1h)
- [ ] Create `__tests__/integration/infrastructure-page.integration.test.tsx`
  - [ ] Full data fetch → render → filter → sort → export flow
  - [ ] Circuit breaker pattern validation
  - [ ] Auto-refresh + interval change
  - [ ] Target: 90%+ coverage, <2s

#### 2.4 Performance Optimization (0.5h)
- [ ] Parallelize test suites (describe.concurrent)
- [ ] Optimize mocks (vi.hoisted)
- [ ] Remove unnecessary waits
- [ ] Target: <4s total runtime

---

## 📊 Phase 3: Visual Regression Tests (IN PROGRESS - Est. 1h)

### Objective
Add visual regression testing for Chromatic stories.

### Tasks

#### 3.1 Snapshot Tests (0.5h)
- [ ] Create `__tests__/visual/chromatic.test.tsx`
- [ ] Import all 10 Storybook stories
- [ ] Create snapshot for each story
- [ ] Verify visual consistency

#### 3.2 Chromatic Verification (0.5h)
- [ ] Run Chromatic build
- [ ] Verify all stories render
- [ ] Document visual testing workflow
- [ ] Update CI configuration (if needed)

---

## ✅ Phase 4: Documentation & Validation (Est. 1h)

### Tasks

#### 4.1 Coverage Validation (0.5h)
- [ ] Run full coverage report
- [ ] Verify 92-95% on all infrastructure files
- [ ] Fix remaining gaps if needed
- [ ] Generate coverage badge

#### 4.2 Performance Validation (0.25h)
- [ ] Measure test runtime
- [ ] Verify <4s target
- [ ] Document timing breakdown
- [ ] Create performance report

#### 4.3 Documentation (0.25h)
- [ ] Update `issue_899_infrastructure_page_implementation.md`
- [ ] Create `TEST_OPTIMIZATION_REPORT.md`
- [ ] Update Issue #900 with results
- [ ] Update ROADMAP.md

---

## 🎯 Success Metrics

| Metric | Target | Current | Status |
|--------|--------|---------|--------|
| **Test Coverage** | 92-95% | TBD | ⏳ Pending |
| **Test Runtime** | <4s | 3.34s (basic) | ✅ On track |
| **Test Count** | 80-100 | 24 (existing) | ⏳ Pending |
| **Visual Tests** | 10 stories | 10 created | ✅ Done |
| **Pass Rate** | 100% | 100% (basic) | ✅ On track |

---

## 📝 Implementation Notes

### Test Pattern Standards
```typescript
// ✅ DO: Use test utilities
import { createHealthyInfraData } from './helpers/test-utils';
const mockData = createHealthyInfraData();

// ✅ DO: Use regex for locale-agnostic assertions
expect(screen.getByText(/15[.,]234/)).toBeInTheDocument();

// ✅ DO: Handle multiple elements
const elements = screen.getAllByText('Sano');
expect(elements.length).toBeGreaterThan(0);

// ❌ DON'T: Hard-code locale-specific values
expect(screen.getByText('15,234')).toBeInTheDocument(); // Fails with IT locale

// ❌ DON'T: Duplicate mock data in every test
const mockData = { overall: { ... }, services: [ ... ] }; // Use factory!
```

### Performance Optimization Patterns
```typescript
// ✅ DO: Parallelize independent tests
describe.concurrent('ServiceHealthMatrix', () => {
  it('test 1', async () => { ... });
  it('test 2', async () => { ... });
});

// ✅ DO: Hoist expensive mocks
vi.hoisted(() => {
  global.fetch = vi.fn();
});

// ✅ DO: Use fake timers for time-dependent tests
beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

// ❌ DON'T: Use real timers with long waits
await new Promise(resolve => setTimeout(resolve, 30000)); // 30s!
```

---

## 🚀 Next Steps

**Immediate** (Next session):
1. Complete test-utils.tsx with all factories
2. Optimize ServiceHealthMatrix.test.tsx
3. Optimize ServiceCard.test.tsx
4. Run coverage report to identify gaps

**Short-term** (Within 2 sessions):
5. Create integration test suite
6. Add visual regression tests
7. Performance optimization pass
8. Documentation updates

**Final** (Last session):
9. Full validation & testing
10. PR creation
11. Code review
12. Merge to main

---

## 🔗 Related Documentation

- **Issue**: #900 - Jest tests infrastructure components (90%+)
- **Dependencies**: #896 (ServiceHealthMatrix), #897 (ServiceCard), #898 (MetricsChart), #899 (Infrastructure Page)
- **Epic**: #890 (FASE 2: Infrastructure Monitoring)
- **Previous Work**: `claudedocs/issue_899_infrastructure_page_implementation.md`
- **Test Guide**: `docs/02-development/testing/comprehensive-testing-guide.md`
- **Visual Testing**: `docs/02-development/testing/visual-testing-guide.md`

---

**Last Updated**: 2025-12-11 12:26 UTC  
**Next Review**: After Phase 2 completion  
**Estimated Completion**: 5-6 hours remaining (of 10-12h total)
