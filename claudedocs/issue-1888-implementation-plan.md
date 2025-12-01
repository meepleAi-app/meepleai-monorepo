# Issue #1888 - Testing Library Query Improvements
## Implementation Plan

**Status**: In Progress
**Branch**: `fix/issue-1888-testing-library-queries`
**Scope**: 607 failing tests across 125 test files
**Approach**: Hybrid (Pattern-based + Manual)
**Estimated Timeline**: 2-3 days

---

## Error Pattern Analysis

### Pattern 1: Dialog Accessibility Issues (HIGH VOLUME)
**Error**: `DialogContent` requires a `DialogTitle` for the component to be accessible
**Occurrences**: ~50-100 tests
**Files Affected**:
- `SessionWarningModal.test.tsx`
- `ExportChatModal.test.tsx`
- `AccessibleModal.behavior.test.tsx`
- All modal/dialog components

**Fix Strategy**:
1. Add `<DialogTitle>` to all `DialogContent` components
2. Use `<VisuallyHidden>` wrapper when title should not be displayed
3. Add `aria-describedby` for descriptions

**Implementation**:
```tsx
// Before (incorrect)
<DialogContent>
  <form>...</form>
</DialogContent>

// After (correct)
<DialogContent>
  <VisuallyHidden>
    <DialogTitle>Modal Title</DialogTitle>
  </VisuallyHidden>
  <DialogDescription>Description text</DialogDescription>
  <form>...</form>
</DialogContent>
```

### Pattern 2: Auth Context Issues (MEDIUM VOLUME)
**Error**: `Cannot read properties of undefined (reading 'some')` / `useAuth must be used within AuthProvider`
**Occurrences**: ~50-80 tests
**Files Affected**:
- `RequireRole.test.tsx`
- Any component using `useAuth` hook

**Fix Strategy**:
1. Wrap test components with `<AuthProvider>`
2. Mock auth context properly
3. Add default auth state in test utils

**Implementation**:
```tsx
// Before (incorrect)
render(<RequireRole roles={['admin']} />)

// After (correct)
render(
  <AuthProvider>
    <RequireRole roles={['admin']} />
  </AuthProvider>
)
```

### Pattern 3: Multiple Elements Found (MEDIUM VOLUME)
**Error**: `Found multiple elements with text: "Button"`
**Occurrences**: ~100-150 tests
**Files Affected**: Various test files using `getByText`

**Fix Strategy**:
1. Replace `getByText` with `getAllByText` where appropriate
2. Use more specific queries: `getByRole`, `getByTestId`, `getByLabelText`
3. Add `{ name: /pattern/i }` for more specific matching

**Implementation**:
```tsx
// Before (incorrect)
const button = screen.getByText('Submit')

// After (correct - option 1: more specific)
const button = screen.getByRole('button', { name: /submit/i })

// After (correct - option 2: handle multiple)
const buttons = screen.getAllByText('Submit')
const submitButton = buttons[0]
```

### Pattern 4: Async Query Issues (MEDIUM VOLUME)
**Error**: `Unable to find element` / Element not found
**Occurrences**: ~150-200 tests
**Files Affected**: Components with async rendering

**Fix Strategy**:
1. Replace `getBy` with `findBy` for async elements
2. Add `waitFor` wrapper for complex async operations
3. Increase timeout for slow operations

**Implementation**:
```tsx
// Before (incorrect)
const element = screen.getByText('Loading complete')

// After (correct - option 1: findBy)
const element = await screen.findByText('Loading complete')

// After (correct - option 2: waitFor)
await waitFor(() => {
  expect(screen.getByText('Loading complete')).toBeInTheDocument()
})
```

### Pattern 5: Component-Specific Issues (LOW-MEDIUM VOLUME)
**Error**: Various component-specific errors
**Occurrences**: ~100-150 tests
**Files Affected**:
- `MessageInput` tests
- `ChatHistory` tests
- `GameOverviewTab` tests
- etc.

**Fix Strategy**:
1. Analyze each file individually
2. Fix component-specific mocking issues
3. Update test utilities as needed

---

## Implementation Phases

### Phase 1: High-Impact Pattern Fixes (Day 1)
**Target**: ~250-300 failures resolved

1. ✅ Dialog accessibility fixes (all modal tests)
2. ✅ Auth context provider fixes
3. ✅ Simple query pattern replacements

**Tools**: morphllm MCP for bulk pattern replacements

### Phase 2: Async Query Fixes (Day 1-2)
**Target**: ~150-200 failures resolved

1. ✅ Replace synchronous queries with async
2. ✅ Add `waitFor` wrappers
3. ✅ Fix timeout issues

**Tools**: Hybrid (morphllm + manual)

### Phase 3: Component-Specific Fixes (Day 2)
**Target**: ~100-150 failures resolved

1. ✅ Fix MessageInput tests
2. ✅ Fix ChatHistory tests
3. ✅ Fix other component-specific issues

**Tools**: Manual fixes with serena MCP

### Phase 4: Validation & Guards (Day 2-3)
**Target**: All 607 failures resolved + prevention

1. ✅ Run full test suite
2. ✅ Create ESLint rules for common patterns
3. ✅ Update testing documentation
4. ✅ Add test utilities for common scenarios

---

## Success Criteria

- [x] Batch 4 dependency verified
- [x] Feature branch created
- [ ] All 607 test failures resolved
- [ ] No new test failures introduced
- [ ] Test coverage maintained at 90%+
- [ ] ESLint rules created for prevention
- [ ] Documentation updated
- [ ] PR created and reviewed
- [ ] Issue #1888 closed

---

## Risk Mitigation

1. **Regression Risk**: Run tests after each phase
2. **Scope Creep**: Stay focused on Testing Library issues only
3. **Time Overrun**: Prioritize high-impact fixes first
4. **Breaking Changes**: Careful with provider wrapping

---

## Progress Tracking

**Current Status**: Phase 0 - Analysis & Planning
**Failures Remaining**: 607
**Failures Fixed**: 0
**Percentage Complete**: 0%

---

**Last Updated**: 2025-12-01
**Updated By**: Claude (Issue #1888 Implementation)
