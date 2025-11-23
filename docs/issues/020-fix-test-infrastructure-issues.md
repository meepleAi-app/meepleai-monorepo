# Issue #020: Fix Test Infrastructure Issues

**Priority:** 🟢 LOW
**Category:** Testing / Quality Assurance
**Estimated Effort:** 2-3 days
**Sprint:** MEDIUM-TERM (3-6 months)

## Summary

4 test files have known issues documented in TODO comments. These affect test reliability, performance, or functionality but don't block current development.

## Test Issues Identified

### 1. Zustand Subscription Infinite Loop (HIGH SEVERITY)

**File:** `apps/web/src/hooks/__tests__/useChatOptimistic.test.tsx:142`

**Issue:**
```typescript
// TODO: Fix Zustand subscription infinite loop in test
it.skip('should handle optimistic updates correctly', () => {
  // Test causes infinite loop in Zustand subscriptions
})
```

**Symptoms:**
- Test enters infinite loop
- Never completes
- May cause test timeout or hang entire test suite

**Root Cause (Hypothesis):**
- Zustand store updates trigger re-subscriptions
- Test mocks may not properly handle cleanup
- State updates in test create circular dependency

**Investigation Steps:**
1. [ ] Add debug logging to Zustand store subscriptions
2. [ ] Check if mock cleanup is called
3. [ ] Review useChatOptimistic hook implementation
4. [ ] Compare with working Zustand tests

**Fix Approaches:**
- **Option A:** Mock Zustand store differently (use `create` with custom storage)
- **Option B:** Use `act()` wrapper for all state updates
- **Option C:** Reset store state between tests more thoroughly
- **Option D:** Rewrite test without relying on subscriptions

**Priority:** HIGH (affects chat functionality testing)

---

### 2. MockBroadcastChannel Performance (MEDIUM SEVERITY)

**File:** `apps/web/src/hooks/__tests__/useUploadQueue.worker.test.ts:59`

**Issue:**
```typescript
// TODO: Fix MockBroadcastChannel.clearAll() performance (takes >30s)
afterEach(() => {
  // MockBroadcastChannel.clearAll() // ⚠️ Disabled - too slow
})
```

**Symptoms:**
- `clearAll()` method takes >30 seconds
- Slows down entire test suite
- Currently disabled, may cause test pollution

**Root Cause (Hypothesis):**
- Mock implementation has inefficient cleanup logic
- Large number of listeners accumulated
- Synchronous iteration over large array/map

**Investigation Steps:**
1. [ ] Profile MockBroadcastChannel.clearAll() method
2. [ ] Check number of channels/listeners created
3. [ ] Review mock implementation code
4. [ ] Compare with native BroadcastChannel behavior

**Fix Approaches:**
- **Option A:** Optimize clearAll() implementation (async cleanup, batching)
- **Option B:** Replace with different mocking strategy
- **Option C:** Create new mock instance per test instead of clearing
- **Option D:** Use real BroadcastChannel in tests (if available in jsdom)

**Priority:** MEDIUM (workaround in place but not ideal)

---

### 3. HTML5 Form Validation Issues (LOW SEVERITY)

**File:** `apps/web/src/components/ui/__tests__/form.test.tsx`

**Issues:**
```typescript
// Line 174: TODO: Fix - HTML5 validation not triggering in test
it.skip('should display error for invalid email', () => {})

// Line 215: TODO: Fix - Timing issue with validation
it.skip('should clear errors when input becomes valid', () => {})

// Line 303: TODO: Fix - Event propagation issue
it.skip('should handle form submission with enter key', () => {})
```

**Symptoms:**
- HTML5 validation doesn't fire in JSDOM environment
- Race conditions in validation state updates
- Keyboard events don't propagate correctly

**Root Cause (Hypothesis):**
- JSDOM doesn't fully support HTML5 form validation
- React Hook Form validation timing differs in tests
- Event simulation doesn't match browser behavior

**Investigation Steps:**
1. [ ] Check React Hook Form test utilities
2. [ ] Review JSDOM form validation support
3. [ ] Test with Playwright (real browser) instead
4. [ ] Review form component implementation

**Fix Approaches:**
- **Option A:** Mock HTML5 validation API in tests
- **Option B:** Use React Hook Form's validation instead of relying on HTML5
- **Option C:** Move tests to E2E suite (Playwright)
- **Option D:** Add explicit validation triggers and `await` for async updates

**Priority:** LOW (form validation works in production, just test issue)

---

### 4. Accessibility Tests Disabled (LOW SEVERITY)

**File:** `apps/web/e2e/accessibility.spec.ts`

**Issues:**
```typescript
// Line 156: TODO: Enable when modal accessibility improved
test.skip('Modal should be keyboard navigable', async ({ page }) => {})

// Line 225: TODO: Enable when focus trap fixed
test.skip('Focus should remain within modal', async ({ page }) => {})
```

**Symptoms:**
- Modal keyboard navigation incomplete
- Focus trap not working correctly
- Tests skipped, no accessibility coverage for modals

**Root Cause:**
- Modal component needs accessibility improvements
- Focus management not implemented
- Possibly missing ARIA attributes

**Investigation Steps:**
1. [ ] Review modal component for ARIA roles
2. [ ] Check if focus trap library is used
3. [ ] Test manually with screen reader
4. [ ] Review Radix UI Modal documentation (if using Shadcn)

**Fix Approaches:**
- **Option A:** Implement proper focus trap in modal
- **Option B:** Add keyboard navigation handlers
- **Option C:** Use Radix UI Dialog component (if not already)
- **Option D:** Add ARIA attributes and test with axe

**Priority:** LOW (but important for accessibility compliance)

---

## Implementation Plan

### Week 1: Investigation
- [ ] Reproduce each issue in isolation
- [ ] Profile performance issues
- [ ] Document findings
- [ ] Prioritize fixes

### Week 2: Zustand Subscription Fix
- [ ] Implement fix approach (highest priority)
- [ ] Verify test passes
- [ ] Check for regressions in other chat tests
- [ ] Document solution

### Week 3: MockBroadcastChannel Optimization
- [ ] Profile and optimize clearAll()
- [ ] Benchmark improvement
- [ ] Re-enable in tests
- [ ] Verify upload tests stable

### Week 4: Form Validation & A11y
- [ ] Fix form validation tests or move to E2E
- [ ] Implement modal accessibility improvements
- [ ] Enable skipped tests
- [ ] Run full test suite

---

## Testing After Fixes

### Verify Fixes
- [ ] All previously skipped tests now pass
- [ ] No new failures introduced
- [ ] Test suite runtime improved (BroadcastChannel fix)
- [ ] Coverage maintained or improved

### Performance Benchmarks
- [ ] Measure test suite runtime before/after
- [ ] Target: <5 seconds for useUploadQueue tests
- [ ] Target: <30 seconds for full test suite

### Accessibility Verification
- [ ] Run axe DevTools on modal
- [ ] Test with keyboard only (no mouse)
- [ ] Test with screen reader (NVDA/JAWS)
- [ ] Verify WCAG 2.1 AA compliance

---

## Success Criteria

- [ ] Zero skipped tests (all `.skip` removed)
- [ ] Zustand subscription test passes consistently
- [ ] MockBroadcastChannel cleanup <1 second
- [ ] Form validation tests pass
- [ ] Modal accessibility tests pass
- [ ] Test coverage at 90%+ maintained
- [ ] No flaky tests introduced

---

## Related Issues

- Issue #1083: Zustand migration (may affect subscription tests)
- Issue #012: Backward compatibility (Zustand related)
- WCAG 2.1 AA compliance requirements

## References

- Zustand test: `apps/web/src/hooks/__tests__/useChatOptimistic.test.tsx:142`
- BroadcastChannel mock: `apps/web/src/hooks/__tests__/useUploadQueue.worker.test.ts:59`
- Form tests: `apps/web/src/components/ui/__tests__/form.test.tsx:174,215,303`
- A11y tests: `apps/web/e2e/accessibility.spec.ts:156,225`
- Legacy code analysis: Section 11 (Testing Infrastructure Issues)

## Technical Resources

**Zustand Testing:**
- https://github.com/pmndrs/zustand/blob/main/docs/guides/testing.md
- Use `create` with custom storage for test isolation

**BroadcastChannel:**
- Consider using `vitest` mock utilities
- Alternative: `@testing-library/react` custom render with channel context

**Form Validation:**
- React Hook Form testing: https://react-hook-form.com/advanced-usage#TestingForm
- Use `waitFor` for async validation

**Accessibility:**
- Radix UI Dialog: https://www.radix-ui.com/primitives/docs/components/dialog
- Focus trap: `focus-trap-react` or built-in Radix behavior
- Axe-core: `jest-axe` for automated testing

---

## Estimated Impact

**Time Saved:**
- MockBroadcastChannel fix: ~30s per test run × 100 runs/day = ~50min/day
- Zustand test fix: Enables testing of critical chat functionality
- A11y improvements: Compliance with accessibility standards

**Risk Level:** LOW
- Tests are already skipped, fixes only improve coverage
- No production code changes required (mostly test infrastructure)
- Modal a11y improvements benefit end users

## Notes

**Test Skipping is Technical Debt:**
- Skipped tests reduce confidence in codebase
- May hide regressions
- Should be resolved before production release

**Prioritization:**
1. Zustand subscription (HIGH) - Critical chat functionality
2. MockBroadcastChannel (MEDIUM) - Performance impact
3. Form validation (LOW) - Works in production
4. Accessibility (LOW but important) - User experience & compliance
