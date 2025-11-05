# TEST-ISSUE-002: Fix 125 Failing Frontend Tests

**Priority**: 🔴 CRITICAL
**Labels**: `critical`, `testing`, `bug`, `ci-cd`
**Estimated Effort**: 16-19 hours
**Failed Tests**: 125 tests across 17 suites
**Current Pass Rate**: 96.5% (should be 100%)

---

## Problem Statement

125 frontend tests are currently failing, preventing reliable CI/CD deployment. Test failures span multiple components and include React act() warnings, timeout errors, and component rendering issues. This blocks merges and reduces deployment confidence.

### Current State

```
Test Suites: 122 passed, 17 failed, 139 total (87.8% pass rate)
Tests:       3,442 passed, 125 failed, 3,567 total (96.5% pass rate)
Time:        55.133 s
```

### Impact

- **CI/CD Blocked**: Cannot reliably deploy
- **Developer Productivity**: Difficult to identify real failures
- **Code Quality**: Reduced confidence in test suite
- **Regression Risk**: Real bugs may be hidden by noise

---

## Failed Test Suites (17 Total)

| # | Test Suite | Priority | Issue Type | Est. Hours |
|---|------------|----------|------------|------------|
| 1 | timer-test-helpers.test.ts | HIGH | Act warnings | 1h |
| 2 | Message.test.tsx | HIGH | Rendering | 1h |
| 3 | UploadQueueItem.test.tsx | MEDIUM | State | 1h |
| 4 | OAuthButtons.test.tsx | CRITICAL | Auth (see ISSUE-001) | 4h |
| 5 | UploadQueue.test.tsx | MEDIUM | Queue mgmt | 1h |
| 6 | DiffToolbar.test.tsx | HIGH | Missing elements | 1h |
| 7 | MessageActions.test.tsx | MEDIUM | Interactions | 1h |
| 8 | admin-prompts-compare.test.tsx | MEDIUM | Page render | 1h |
| 9 | EditorToolbar.test.tsx | MEDIUM | Toolbar | 1h |
| 10 | ChatHistory.test.tsx | MEDIUM | History | 1h |
| 11 | DiffSearchInput.test.tsx | LOW | Search | 0.5h |
| 12 | setup.test.tsx | MEDIUM | Setup wizard | 1h |
| 13 | analytics.test.tsx | LOW | Analytics | 0.5h |
| 14 | CommentThread.test.tsx | MEDIUM | Threading | 1h |
| 15 | upload.test.tsx | HIGH | Timeout | 1h |
| 16-17 | Others | MEDIUM | Various | 2h |

---

## Common Failure Patterns

### Pattern 1: React act() Warnings (10+ tests)

**Error Message**:
```
Warning: An update to TestComponent inside a test was not wrapped in act(...)
```

**Root Cause**: Async state updates not properly wrapped

**Affected Tests**:
- useUploadQueue.test.ts (multiple tests)
- timer-test-helpers.test.ts
- Various component tests with state updates

**Example Failure**:
```typescript
// Current (causes warning)
setQueue(prev => [...prev, newItem]);

// Should be
await act(async () => {
  setQueue(prev => [...prev, newItem]);
});
```

**Fix Strategy**: Wrap all async state updates in `act()` or use `waitFor()`

---

### Pattern 2: Timeout Errors (5+ tests)

**Error Message**:
```
Exceeded timeout of 5000 ms for a test
Add a timeout value to this test to increase the timeout
```

**Root Cause**:
- Slow test execution
- Missing mocks (actual API calls)
- Heavy component rendering

**Affected Tests**:
- upload.test.tsx (validation tests)
- setup.test.tsx (wizard)
- Other integration-heavy tests

**Fix Strategy**:
- Increase timeout for legitimate long tests
- Mock slow operations
- Use fake timers

---

### Pattern 3: Missing DOM Elements (DiffToolbar suite)

**Error Message**:
```
Unable to find an element by: [aria-label="Clear search"]
```

**Root Cause**: Component doesn't have expected aria-label

**Affected Tests**:
- DiffToolbar.test.tsx
- DiffSearchInput.test.tsx

**Fix Strategy**: Update component to include aria-label or fix test selector

---

### Pattern 4: Component Rendering Failures

**Symptoms**:
- Message component not rendering
- MessageActions interactions failing
- ChatHistory not loading properly

**Root Cause**:
- Missing context providers
- Incorrect props
- Async data not awaited

**Fix Strategy**: Debug component lifecycle, add missing mocks

---

## Implementation Plan

### Phase 1: React act() Warnings (4-6 hours)

**Objective**: Fix all act() warnings to clean up test output

#### Task 1.1: Fix useUploadQueue Tests (2 hours)

**File**: `src/hooks/__tests__/useUploadQueue.test.ts`

**Current Issues**:
- State updates in upload process not wrapped
- Queue updates triggering warnings
- Multiple tests affected

**Solution**:
```typescript
// BEFORE
it('should process upload queue', async () => {
  const { result } = renderHook(() => useUploadQueue());

  act(() => {
    result.current.addToQueue(mockFile);
  });

  // This causes warning - state updates from processing
  await waitFor(() => {
    expect(result.current.queue[0].status).toBe('uploading');
  });
});

// AFTER
it('should process upload queue', async () => {
  const { result } = renderHook(() => useUploadQueue());

  await act(async () => {
    result.current.addToQueue(mockFile);
  });

  await waitFor(() => {
    expect(result.current.queue[0].status).toBe('uploading');
  }, { timeout: 10000 }); // Increase timeout if needed
});
```

**Steps**:
1. Identify all state update points in tests
2. Wrap each in `act()` or `waitFor()`
3. Verify warnings disappear
4. Ensure tests still validate correct behavior

#### Task 1.2: Fix timer-test-helpers Tests (1 hour)

**File**: `src/test-utils/__tests__/timer-test-helpers.test.ts`

**Solution**: Use `jest.useFakeTimers()` and `act()` together:
```typescript
beforeEach(() => {
  jest.useFakeTimers();
});

afterEach(() => {
  jest.runOnlyPendingTimers();
  jest.useRealTimers();
});

it('should advance timers correctly', async () => {
  await act(async () => {
    jest.advanceTimersByTime(1000);
  });
  // assertions
});
```

#### Task 1.3: Fix Other act() Warnings (1-3 hours)

Review test output for remaining act() warnings and apply similar fixes.

---

### Phase 2: Timeout Errors (3-4 hours)

#### Task 2.1: Fix upload.test.tsx Timeouts (1-2 hours)

**File**: `src/__tests__/pages/upload.test.tsx`

**Specific Test**:
```typescript
it('should display specific error messages for each validation failure', async () => {
  // This test is timing out at 5000ms
});
```

**Solutions**:

**Option 1**: Increase timeout
```typescript
it('should display specific error messages', async () => {
  // test code
}, 10000); // 10 second timeout
```

**Option 2**: Mock slow operations
```typescript
jest.mock('@/lib/pdf-validator', () => ({
  validatePDF: jest.fn().mockResolvedValue({ valid: false, errors: [...] })
}));
```

**Option 3**: Use fake timers
```typescript
beforeEach(() => {
  jest.useFakeTimers();
});

it('should display error messages', async () => {
  render(<UploadPage />);

  jest.advanceTimersByTime(5000); // Fast-forward debounce/delays

  await waitFor(() => {
    expect(screen.getByText(/error/i)).toBeInTheDocument();
  });
});
```

#### Task 2.2: Fix setup.test.tsx Timeouts (1 hour)

**File**: `src/__tests__/pages/setup.test.tsx`

Similar approach: mock slow API calls, use fake timers, or increase timeout.

#### Task 2.3: Fix Other Timeout Tests (1 hour)

Review and fix remaining timeout issues.

---

### Phase 3: DiffToolbar Tests (2 hours)

#### Task 3.1: Fix DiffToolbar.test.tsx (1 hour)

**File**: `src/components/diff/__tests__/DiffToolbar.test.tsx`

**Error**:
```typescript
const clearButton = screen.getByLabelText('Clear search');
// TestingLibraryElementError: Unable to find a label with the text of: Clear search
```

**Solution Option 1** (Fix Component):
```typescript
// In src/components/diff/DiffToolbar.tsx
<button
  onClick={onClearSearch}
  aria-label="Clear search"  // ADD THIS
  className="clear-search-button"
>
  ×
</button>
```

**Solution Option 2** (Fix Test):
```typescript
// Use more flexible selector
const clearButton = screen.getByRole('button', { name: /clear/i });
// OR
const clearButton = screen.getByTestId('clear-search-button');
```

**Recommended**: Fix the component (Option 1) for better accessibility.

#### Task 3.2: Fix DiffSearchInput.test.tsx (1 hour)

**File**: `src/components/diff/__tests__/DiffSearchInput.test.tsx`

Apply similar fixes for missing accessibility labels.

---

### Phase 4: OAuth Tests (4 hours)

**Note**: This overlaps with TEST-ISSUE-001. Coordinate with that issue.

**File**: `src/components/auth/__tests__/OAuthButtons.test.tsx`

**See TEST-ISSUE-001** for detailed implementation plan.

---

### Phase 5: Message & Chat Tests (3 hours)

#### Task 5.1: Fix Message.test.tsx (1 hour)

**File**: `src/__tests__/components/chat/Message.test.tsx`

**Common Issues**:
- Missing ChatContext provider
- Missing user authentication context
- Props not matching component requirements

**Solution Template**:
```typescript
import { ChatProvider } from '@/contexts/ChatContext';
import { AuthProvider } from '@/contexts/AuthContext';

const renderWithProviders = (ui: React.ReactElement) => {
  return render(
    <AuthProvider value={mockAuthContext}>
      <ChatProvider value={mockChatContext}>
        {ui}
      </ChatProvider>
    </AuthProvider>
  );
};

it('should render message', () => {
  renderWithProviders(<Message message={mockMessage} />);
  expect(screen.getByText(mockMessage.content)).toBeInTheDocument();
});
```

#### Task 5.2: Fix MessageActions.test.tsx (1 hour)

**File**: `src/components/chat/__tests__/MessageActions.test.tsx`

- Test edit action
- Test delete action
- Test copy action
- Mock API calls properly

#### Task 5.3: Fix ChatHistory.test.tsx (1 hour)

**File**: `src/__tests__/components/chat/ChatHistory.test.tsx`

- Mock history loading
- Mock pagination
- Test infinite scroll

---

### Phase 6: Remaining Tests (4-6 hours)

#### Task 6.1: Fix UploadQueueItem.test.tsx (1 hour)
**File**: `src/__tests__/components/UploadQueueItem.test.tsx`

#### Task 6.2: Fix UploadQueue.test.tsx (1 hour)
**File**: `src/__tests__/components/UploadQueue.test.tsx`

#### Task 6.3: Fix admin-prompts-compare.test.tsx (1 hour)
**File**: `src/__tests__/pages/admin-prompts-compare.test.tsx`

#### Task 6.4: Fix EditorToolbar.test.tsx (1 hour)
**File**: `src/__tests__/components/editor/EditorToolbar.test.tsx`

#### Task 6.5: Fix CommentThread.test.tsx (1 hour)
**File**: `src/__tests__/components/CommentThread.test.tsx`

#### Task 6.6: Fix analytics.test.tsx (0.5 hour)
**File**: `src/__tests__/pages/analytics.test.tsx`

#### Task 6.7: Fix Remaining Suites (1-2 hours)
Review and fix any remaining failures.

---

## Testing Strategy

### Before Starting

```bash
# Run tests to get baseline
cd apps/web
pnpm test 2>&1 | tee test-failures-before.log

# Count failures
grep "FAIL" test-failures-before.log | wc -l
# Should show 17 failed suites
```

### During Implementation

```bash
# Test specific file you're fixing
pnpm test Message.test.tsx

# Run with verbose output
pnpm test Message.test.tsx --verbose

# Watch mode for iterative development
pnpm test Message.test.tsx --watch
```

### After Each Phase

```bash
# Run all tests
pnpm test 2>&1 | tee test-failures-current.log

# Compare with before
diff test-failures-before.log test-failures-current.log

# Verify no regressions
pnpm test:coverage
```

---

## Acceptance Criteria

### Test Execution
- [ ] All 125 failing tests fixed
- [ ] 100% test pass rate (3,567/3,567)
- [ ] Test suites: 139/139 passing
- [ ] Execution time: < 60 seconds total

### Test Output
- [ ] No act() warnings in output
- [ ] No timeout errors
- [ ] No accessibility warnings
- [ ] Clean test output

### Quality
- [ ] All tests validate actual behavior (not just pass)
- [ ] No flaky tests introduced
- [ ] Test descriptions clear and accurate
- [ ] Proper mocks and fixtures used

### CI/CD
- [ ] CI pipeline green
- [ ] No merge blockers
- [ ] Coverage maintained (90%+)
- [ ] No regressions in passing tests

---

## Common Patterns and Solutions

### Pattern: Async State Updates

```typescript
// ❌ Wrong - causes act() warning
it('should update state', () => {
  const { result } = renderHook(() => useMyHook());
  result.current.updateState();
  expect(result.current.state).toBe('updated');
});

// ✅ Right - properly wrapped
it('should update state', async () => {
  const { result } = renderHook(() => useMyHook());
  await act(async () => {
    await result.current.updateState();
  });
  expect(result.current.state).toBe('updated');
});
```

### Pattern: Waiting for Elements

```typescript
// ❌ Wrong - may timeout
it('should show message', () => {
  render(<Component />);
  expect(screen.getByText('Success')).toBeInTheDocument();
});

// ✅ Right - waits for element
it('should show message', async () => {
  render(<Component />);
  await waitFor(() => {
    expect(screen.getByText('Success')).toBeInTheDocument();
  });
});
```

### Pattern: Testing with Providers

```typescript
// ❌ Wrong - missing context
it('should render', () => {
  render(<ComponentNeedingContext />);
  // Fails - context undefined
});

// ✅ Right - includes providers
it('should render', () => {
  render(
    <RequiredProvider value={mockValue}>
      <ComponentNeedingContext />
    </RequiredProvider>
  );
  // Works correctly
});
```

### Pattern: Mocking API Calls

```typescript
// ❌ Wrong - undefined returns
jest.mock('@/lib/api');

// ✅ Right - defined returns
jest.mock('@/lib/api', () => ({
  api: {
    get: jest.fn().mockResolvedValue({ data: mockData }),
    post: jest.fn().mockResolvedValue({ success: true }),
  }
}));
```

---

## Dependencies

- **TEST-ISSUE-001**: OAuthButtons.test.tsx overlaps with auth coverage issue
- **No other blockers**: Can start immediately on other suites

---

## Risk Assessment

### High Risk Areas
1. **Regression Risk**: Fixing one test may break others
2. **Timeout Adjustments**: May hide real performance issues
3. **Mock Changes**: May mask actual bugs

### Mitigation
- Run full suite after each fix
- Review actual component behavior, not just test pass
- Document timeout increases with reasoning
- Code review all changes

---

## Progress Tracking

Create a checklist to track progress:

```markdown
### Phase 1: Act Warnings (4-6h)
- [ ] useUploadQueue.test.ts fixed
- [ ] timer-test-helpers.test.ts fixed
- [ ] Other act() warnings resolved

### Phase 2: Timeouts (3-4h)
- [ ] upload.test.tsx fixed
- [ ] setup.test.tsx fixed
- [ ] Other timeouts resolved

### Phase 3: DiffToolbar (2h)
- [ ] DiffToolbar.test.tsx fixed
- [ ] DiffSearchInput.test.tsx fixed

### Phase 4: OAuth (4h)
- [ ] Coordinate with TEST-ISSUE-001

### Phase 5: Message/Chat (3h)
- [ ] Message.test.tsx fixed
- [ ] MessageActions.test.tsx fixed
- [ ] ChatHistory.test.tsx fixed

### Phase 6: Remaining (4-6h)
- [ ] All remaining suites fixed
- [ ] 100% pass rate achieved
```

---

## Success Metrics

### Before
```
Test Suites: 122 passed, 17 failed, 139 total
Tests:       3,442 passed, 125 failed, 3,567 total
Pass Rate:   96.5%
```

### After (Target)
```
Test Suites: 139 passed, 0 failed, 139 total
Tests:       3,567 passed, 0 failed, 3,567 total
Pass Rate:   100% ✅
```

### Additional Metrics
- No act() warnings
- No timeout errors
- Clean test output
- CI/CD green
- Coverage maintained at 90%+

---

## Definition of Done

- [ ] All 6 phases completed
- [ ] All 17 test suites passing
- [ ] 100% test pass rate (3,567/3,567)
- [ ] No warnings in test output
- [ ] No timeout errors
- [ ] CI/CD pipeline green
- [ ] Coverage maintained (90%+)
- [ ] Code reviewed and approved
- [ ] Documentation updated (if patterns discovered)
- [ ] Merged to main branch

---

## Related Issues

- **TEST-ISSUE-001**: Auth Component Coverage (overlaps with OAuthButtons)
- **TEST-ISSUE-003**: Test Infrastructure (may help with shared utilities)
- Future: Any new test failures discovered

---

## References

- [Jest Documentation](https://jestjs.io/docs/getting-started)
- [React Testing Library](https://testing-library.com/docs/react-testing-library/intro/)
- [Testing Async Code](https://kentcdodds.com/blog/common-mistakes-with-react-testing-library)
- [act() in Depth](https://kentcdodds.com/blog/fix-the-not-wrapped-in-act-warning)

---

**Created**: 2025-11-05
**Status**: Ready for Assignment
**Assignee**: TBD
**Due Date**: Within 2-3 days (CRITICAL)
**Estimated Effort**: 16-19 hours
