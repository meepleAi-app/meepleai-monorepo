# TEST-ISSUE-007: Test Documentation & Maintenance

**Priority**: 🔵 LOW
**Labels**: `low-priority`, `documentation`, `testing`, `developer-experience`
**Estimated Effort**: 4-6 hours
**Type**: Documentation, tooling, and maintenance
**Timeline**: Ongoing

---

## Problem Statement

While coverage metrics are good, test maintainability and developer onboarding could be improved with better documentation, tooling, and established patterns. This improves long-term test quality and reduces time for new developers to write effective tests.

---

## Current State

### What Exists ✅
- Coverage reports generated
- CI enforcement in place (90% backend, 80% frontend)
- Test execution scripts
- Basic test infrastructure

### What's Missing ⚠️
- Test writing guide for new developers
- Common test patterns documentation
- Coverage trend tracking
- Coverage badges in README
- Test maintenance scripts
- Example tests for common scenarios

---

## Implementation Tasks

### Task 1: Test Writing Guide (2 hours)

**File to Create**: `docs/testing/test-writing-guide.md`

**Contents**:

```markdown
# Test Writing Guide

## Overview
Guide for writing effective tests in the MeepleAI project.

## Test Types

### Unit Tests (Jest)
Test individual components or functions in isolation.

**Example - Component Test**:
```typescript
import { render, screen } from '@testing-library/react';
import { MyComponent } from '../MyComponent';

describe('MyComponent', () => {
  it('should render with default props', () => {
    render(<MyComponent />);
    expect(screen.getByText('Hello')).toBeInTheDocument();
  });

  it('should handle user interaction', async () => {
    const onClickMock = jest.fn();
    render(<MyComponent onClick={onClickMock} />);

    await userEvent.click(screen.getByRole('button'));

    expect(onClickMock).toHaveBeenCalledTimes(1);
  });
});
```

**Example - Hook Test**:
```typescript
import { renderHook, act } from '@testing-library/react';
import { useMyHook } from '../useMyHook';

describe('useMyHook', () => {
  it('should update state on action', async () => {
    const { result } = renderHook(() => useMyHook());

    await act(async () => {
      await result.current.performAction();
    });

    expect(result.current.state).toBe('updated');
  });
});
```

### Integration Tests
Test interactions between components and services.

### E2E Tests (Playwright)
Test complete user workflows.

**Example**:
```typescript
import { test, expect } from '@playwright/test';

test('user can log in', async ({ page }) => {
  await page.goto('/login');
  await page.fill('[name="email"]', 'user@example.com');
  await page.fill('[name="password"]', 'password');
  await page.click('[type="submit"]');

  await expect(page).toHaveURL('/dashboard');
  await expect(page.getByText('Welcome')).toBeVisible();
});
```

## Testing Patterns

### AAA Pattern (Arrange-Act-Assert)
```typescript
it('should add item to cart', () => {
  // Arrange
  const cart = new Cart();
  const item = { id: 1, name: 'Product' };

  // Act
  cart.addItem(item);

  // Assert
  expect(cart.items).toHaveLength(1);
  expect(cart.items[0]).toEqual(item);
});
```

### Test Naming Convention
Format: `should [expected behavior] when [condition]`

Examples:
- ✅ `should show error message when API call fails`
- ✅ `should disable submit button when form is invalid`
- ❌ `test1` (not descriptive)
- ❌ `it works` (not specific)

## Mocking Strategies

### Mock API Calls
```typescript
jest.mock('@/lib/api', () => ({
  fetchData: jest.fn().mockResolvedValue({ data: mockData }),
}));
```

### Mock Router
```typescript
const mockPush = jest.fn();
jest.mock('next/router', () => ({
  useRouter: () => ({
    push: mockPush,
    pathname: '/test',
  }),
}));
```

### Mock Date/Time
```typescript
beforeEach(() => {
  jest.useFakeTimers();
  jest.setSystemTime(new Date('2025-01-01'));
});

afterEach(() => {
  jest.useRealTimers();
});
```

## Common Pitfalls

### Pitfall 1: Not Wrapping Async Updates
❌ Wrong: Causes act() warnings
✅ Right: Use act() or waitFor()

### Pitfall 2: Testing Implementation Details
❌ Wrong: Testing internal state
✅ Right: Testing user-visible behavior

### Pitfall 3: Incomplete Mocks
❌ Wrong: Mocking without return values
✅ Right: Complete mock with expected returns

## Best Practices

1. **Test Behavior, Not Implementation**
2. **Write Clear Test Descriptions**
3. **Use Proper Cleanup**
4. **Mock External Dependencies**
5. **Keep Tests Fast and Focused**
6. **Avoid Test Interdependencies**
7. **Use Test Utilities Consistently**

## Resources
- [Jest Documentation](https://jestjs.io/)
- [React Testing Library](https://testing-library.com/)
- [Playwright Documentation](https://playwright.dev/)
```

---

### Task 2: Coverage Monitoring Tools (1-2 hours)

#### Subtask 2.1: Coverage Trend Script

**File to Create**: `tools/coverage-trends.sh`

```bash
#!/bin/bash
# Track coverage trends over time

TIMESTAMP=$(date +%Y-%m-%d-%H%M%S)
COVERAGE_DIR="coverage-history"

mkdir -p "$COVERAGE_DIR"

# Run tests with coverage
cd apps/web
pnpm test:coverage --json --outputFile="../$COVERAGE_DIR/coverage-$TIMESTAMP.json"

# Extract key metrics
echo "Coverage Snapshot: $TIMESTAMP" >> "../$COVERAGE_DIR/trends.log"
cat coverage/coverage-summary.json | jq '.total' >> "../$COVERAGE_DIR/trends.log"

echo "Coverage snapshot saved to $COVERAGE_DIR/coverage-$TIMESTAMP.json"
```

#### Subtask 2.2: Coverage Badge Generation

**File to Update**: `README.md`

Add coverage badges:
```markdown
[![Frontend Coverage](https://img.shields.io/badge/coverage-90%25-brightgreen)]()
[![Backend Coverage](https://img.shields.io/badge/coverage-90%25-brightgreen)]()
```

---

### Task 3: Update CLAUDE.md (1 hour)

**File to Update**: `CLAUDE.md`

Add section:
```markdown
## Testing Status (Updated 2025-11-05)

### Current Coverage
- Frontend: 90.03% statements, 84.22% branches ✅
- Backend: 90%+ (enforced in CI) ✅

### Test Counts
- Frontend Unit: 3,567 tests
- Backend: 90+ tests
- E2E: 28 test files

### Known Issues
See TEST-ISSUES-SUMMARY.md for 7 prioritized issues

### Running Tests
```bash
# Frontend
cd apps/web
pnpm test              # Unit tests
pnpm test:coverage     # With coverage
pnpm test:e2e          # E2E tests

# Backend
cd apps/api
dotnet test            # All tests
dotnet test /p:CollectCoverage=true  # With coverage
```

### Test Documentation
- Test Writing Guide: `docs/testing/test-writing-guide.md`
- Test Patterns: `docs/testing/test-patterns.md`
- Coverage Guide: `docs/code-coverage.md`
```

---

### Task 4: Test Patterns Documentation (1 hour)

**File to Create**: `docs/testing/test-patterns.md`

```markdown
# Common Test Patterns

## Component Testing Patterns

### Pattern: Testing with Context Providers
Use when component needs context (Auth, Theme, etc.)

### Pattern: Testing Async State Updates
Use act() for async state changes

### Pattern: Testing User Interactions
Use userEvent from @testing-library/user-event

## Hook Testing Patterns

### Pattern: Testing Custom Hooks
Use renderHook from @testing-library/react

### Pattern: Testing Hook Dependencies
Test behavior when dependencies change

## API Mocking Patterns

### Pattern: Mock Successful Response
### Pattern: Mock Error Response
### Pattern: Mock Loading States

## Edge Case Testing Patterns

### Pattern: Empty Data
Test behavior with empty arrays, null, undefined

### Pattern: Large Data Sets
Test performance with large amounts of data

### Pattern: Special Characters
Test with special chars, unicode, emojis

## Examples

[Include examples from real tests]
```

---

## Acceptance Criteria

### Documentation
- [ ] Test writing guide created
- [ ] Test patterns documented
- [ ] CLAUDE.md updated with current test status
- [ ] Coverage monitoring tools created

### Tooling
- [ ] Coverage trend tracking script
- [ ] Coverage badges added to README
- [ ] Test maintenance scripts created

### Quality
- [ ] Documentation clear and actionable
- [ ] Examples provided for all patterns
- [ ] Links to external resources
- [ ] Easy to find and navigate

---

## Success Metrics

### Developer Experience
- New developers can write tests in < 1 hour
- Test patterns are reused consistently
- Coverage trends are visible
- Test maintenance is easier

### Documentation Quality
- All common scenarios documented
- Clear examples for each pattern
- Easy to find relevant information
- Up-to-date with codebase

---

## Deliverables

1. `docs/testing/test-writing-guide.md` - Complete guide
2. `docs/testing/test-patterns.md` - Common patterns
3. `tools/coverage-trends.sh` - Trend tracking
4. Updated `README.md` - Coverage badges
5. Updated `CLAUDE.md` - Current test status

---

## Related Issues

- All test issues (provides documentation for them)

---

**Created**: 2025-11-05
**Estimated Effort**: 4-6 hours
**Timeline**: Ongoing (low priority)
