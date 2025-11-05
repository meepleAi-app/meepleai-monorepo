# TEST-ISSUE-003: Improve Test Infrastructure Coverage

**Priority**: 🟡 HIGH
**Labels**: `high-priority`, `testing`, `infrastructure`, `test-quality`
**Estimated Effort**: 6-8 hours
**Current Coverage**: 49-70% (should be 90%+)
**Target Coverage**: 90%+ for all test utilities

---

## Problem Statement

Test utilities and shared fixtures have critically low coverage, with **0% branch coverage** in multiple files. Test infrastructure should have very high coverage (90%+) as it supports all other tests and incorrect test utilities can lead to false positives or false negatives across the entire test suite.

### Current State

| File/Directory | Statements | Branches | Functions | Status |
|----------------|------------|----------|-----------|--------|
| **__tests__/pages/chat/shared** | 49.33% (37/75) | 0% (0/62) ⚠️ | 25% (3/12) | CRITICAL |
| **__tests__/utils** | 70.58% (12/17) | 0% (0/1) ⚠️ | 62.5% (5/8) | NEEDS WORK |
| **lib/__tests__/test-utils.tsx** | 56.66% (17/30) | 64.7% (11/17) | 58.33% (7/12) | NEEDS WORK |

### Impact

- **Test Reliability**: Untested utilities may have bugs
- **False Positives**: Tests may pass when they shouldn't
- **False Negatives**: Tests may fail incorrectly
- **Developer Confidence**: Can't trust test infrastructure
- **Maintenance**: Hard to refactor without tests

---

## Affected Files

### Critical: Chat Shared Utilities (0% branches)

**Location**: `src/__tests__/pages/chat/shared/`

**Files**:
- `fixtures.ts` - Chat test fixtures and mock data
- `setup-helpers.ts` - Chat environment setup utilities
- `mock-generators.ts` - Dynamic mock data generators
- Other shared utilities

**Issues**:
- 38 uncovered statements (out of 75)
- **0 branches tested** (0/62) - ALL conditionals untested
- 9 untested functions (out of 12)

**Risk**: High - affects all chat tests

---

### High: General Test Utilities (0% branches)

**Location**: `src/__tests__/utils/`

**Issues**:
- 5 uncovered statements (out of 17)
- **0 branches tested** (0/1) - conditional paths untested
- 3 untested functions (out of 8)

**Risk**: High - affects multiple test suites

---

### Medium: Test Utils (64.7% branches)

**Location**: `src/lib/__tests__/test-utils.tsx`

**Issues**:
- 13 uncovered statements (out of 30)
- 6 untested branches (out of 17)
- 5 untested functions (out of 12)

**Risk**: Medium - but widely used

---

## Implementation Tasks

### Task 1: Chat Shared Utilities Tests (3 hours)

#### Subtask 1.1: Test `setupFullChatEnvironment` (1 hour)

**File to Create**: `src/__tests__/pages/chat/shared/__tests__/setup-helpers.test.ts`

**Test Coverage Needed**:

```typescript
import { setupFullChatEnvironment } from '../setup-helpers';

describe('setupFullChatEnvironment', () => {
  describe('Basic Setup', () => {
    it('should create complete chat environment with defaults', () => {
      const env = setupFullChatEnvironment();

      expect(env.mockFetch).toBeDefined();
      expect(env.mockRouter).toBeDefined();
      expect(env.mockGame).toBeDefined();
      expect(env.mockUser).toBeDefined();
    });

    it('should use custom options when provided', () => {
      const customGame = { id: 'custom-1', name: 'Custom Game' };
      const env = setupFullChatEnvironment({ game: customGame });

      expect(env.mockGame).toEqual(customGame);
    });
  });

  describe('Conditional Branches', () => {
    it('should handle authenticated user scenario', () => {
      const env = setupFullChatEnvironment({ authenticated: true });

      expect(env.mockUser).not.toBeNull();
      expect(env.mockFetch).toHaveBeenCalledWith('/api/v1/auth/me');
    });

    it('should handle unauthenticated user scenario', () => {
      const env = setupFullChatEnvironment({ authenticated: false });

      expect(env.mockUser).toBeNull();
      // Should not call auth endpoint
    });

    it('should handle admin user scenario', () => {
      const env = setupFullChatEnvironment({
        authenticated: true,
        userRole: 'Admin'
      });

      expect(env.mockUser.role).toBe('Admin');
    });

    it('should handle error scenarios', () => {
      const env = setupFullChatEnvironment({ simulateError: true });

      expect(env.mockFetch).toReject();
    });
  });

  describe('Cleanup', () => {
    it('should provide cleanup function', () => {
      const env = setupFullChatEnvironment();

      expect(env.cleanup).toBeDefined();
      expect(typeof env.cleanup).toBe('function');
    });

    it('should cleanup mocks when cleanup is called', () => {
      const env = setupFullChatEnvironment();

      env.cleanup();

      expect(jest.isMockFunction(global.fetch)).toBe(false);
    });
  });
});
```

#### Subtask 1.2: Test Chat Fixtures (1 hour)

**File to Create**: `src/__tests__/pages/chat/shared/__tests__/fixtures.test.ts`

**Test Coverage Needed**:

```typescript
import {
  createMockMessage,
  createMockChatSession,
  createMockGame,
  createMockUser,
} from '../fixtures';

describe('Chat Fixtures', () => {
  describe('createMockMessage', () => {
    it('should create default mock message', () => {
      const message = createMockMessage();

      expect(message).toHaveProperty('id');
      expect(message).toHaveProperty('content');
      expect(message).toHaveProperty('role');
      expect(message).toHaveProperty('timestamp');
    });

    it('should accept custom properties', () => {
      const custom = {
        content: 'Custom message',
        role: 'assistant',
      };

      const message = createMockMessage(custom);

      expect(message.content).toBe('Custom message');
      expect(message.role).toBe('assistant');
    });

    it('should handle all message types', () => {
      const userMsg = createMockMessage({ role: 'user' });
      const assistantMsg = createMockMessage({ role: 'assistant' });
      const systemMsg = createMockMessage({ role: 'system' });

      expect(userMsg.role).toBe('user');
      expect(assistantMsg.role).toBe('assistant');
      expect(systemMsg.role).toBe('system');
    });
  });

  describe('createMockChatSession', () => {
    it('should create chat session with messages', () => {
      const session = createMockChatSession();

      expect(session.messages).toBeInstanceOf(Array);
      expect(session.messages.length).toBeGreaterThan(0);
    });

    it('should allow empty message array', () => {
      const session = createMockChatSession({ messages: [] });

      expect(session.messages).toEqual([]);
    });
  });

  describe('createMockGame', () => {
    it('should create valid game mock', () => {
      const game = createMockGame();

      expect(game.id).toBeDefined();
      expect(game.name).toBeDefined();
      expect(game.ruleSpecId).toBeDefined();
    });
  });

  describe('createMockUser', () => {
    it('should create user with role', () => {
      const admin = createMockUser({ role: 'Admin' });
      const editor = createMockUser({ role: 'Editor' });
      const user = createMockUser({ role: 'User' });

      expect(admin.role).toBe('Admin');
      expect(editor.role).toBe('Editor');
      expect(user.role).toBe('User');
    });
  });
});
```

#### Subtask 1.3: Test Mock Generators (1 hour)

**File to Create**: `src/__tests__/pages/chat/shared/__tests__/mock-generators.test.ts`

**Test scenarios**:
- Test dynamic mock generation
- Test generator options
- Test edge cases (empty, null, undefined)
- Test performance (large datasets)

---

### Task 2: General Test Utilities (2 hours)

#### Subtask 2.1: Test Custom Render Functions (1 hour)

**File to Create**: `src/__tests__/utils/__tests__/render-utils.test.ts`

```typescript
import { renderWithProviders, renderWithRouter } from '../render-utils';

describe('Test Utilities', () => {
  describe('renderWithProviders', () => {
    it('should render component with all providers', () => {
      const { getByText } = renderWithProviders(<TestComponent />);

      expect(getByText('Test')).toBeInTheDocument();
    });

    it('should accept custom provider values', () => {
      const customAuth = { user: mockUser, isAuthenticated: true };

      const { getByText } = renderWithProviders(
        <TestComponent />,
        { auth: customAuth }
      );

      expect(getByText(mockUser.name)).toBeInTheDocument();
    });

    it('should handle provider errors gracefully', () => {
      // Test error boundary behavior
      expect(() => {
        renderWithProviders(<ComponentThatThrows />);
      }).not.toThrow();
    });
  });

  describe('renderWithRouter', () => {
    it('should render with mock router', () => {
      const { getByText } = renderWithRouter(<TestComponent />);

      expect(getByText('Test')).toBeInTheDocument();
    });

    it('should accept custom route', () => {
      const { getByText } = renderWithRouter(
        <TestComponent />,
        { route: '/custom' }
      );

      // Verify router.pathname is '/custom'
    });
  });
});
```

#### Subtask 2.2: Test Helper Functions (1 hour)

**File**: Enhance existing or create new tests for:
- Async test helpers
- Wait utilities
- Mock factories
- Cleanup utilities

---

### Task 3: Enhance lib/__tests__/test-utils.tsx (1-2 hours)

**File to Enhance**: `src/lib/__tests__/test-utils.test.tsx`

**Additional Coverage Needed**:

```typescript
describe('test-utils', () => {
  describe('customRender', () => {
    it('should render with default options', () => {
      const { container } = customRender(<div>Test</div>);
      expect(container).toBeInTheDocument();
    });

    it('should apply custom wrapper', () => {
      const CustomWrapper = ({ children }) => (
        <div data-testid="wrapper">{children}</div>
      );

      const { getByTestId } = customRender(<div>Test</div>, {
        wrapper: CustomWrapper
      });

      expect(getByTestId('wrapper')).toBeInTheDocument();
    });
  });

  describe('waitForLoadingToFinish', () => {
    it('should wait for loading spinner to disappear', async () => {
      render(<ComponentWithLoading />);

      await waitForLoadingToFinish();

      expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    });

    it('should timeout if loading never finishes', async () => {
      render(<ComponentThatNeverLoads />);

      await expect(
        waitForLoadingToFinish({ timeout: 1000 })
      ).rejects.toThrow('Timeout');
    });
  });

  describe('Branch Coverage', () => {
    it('should handle all conditional paths', () => {
      // Test each if/else branch in test-utils
      // This ensures 100% branch coverage
    });
  });
});
```

---

### Task 4: Documentation (1 hour)

Create documentation for test utilities usage.

**File to Create**: `docs/testing/test-utilities-api.md`

**Contents**:
```markdown
# Test Utilities API

## Overview
This document describes the test utilities available in the MeepleAI test suite.

## Rendering Utilities

### renderWithProviders
Renders a component with all required providers.

**Usage**:
```typescript
import { renderWithProviders } from '@/__tests__/utils';

const { getByText } = renderWithProviders(<MyComponent />);
```

### renderWithRouter
Renders a component with Next.js router mock.

## Chat Test Utilities

### setupFullChatEnvironment
Sets up complete chat testing environment.

**Usage**:
```typescript
import { setupFullChatEnvironment } from '@/__tests__/pages/chat/shared';

const env = setupFullChatEnvironment({
  authenticated: true,
  userRole: 'Admin'
});

// Use env.mockFetch, env.mockRouter, etc.
env.cleanup(); // Clean up after test
```

## Mock Generators

### createMockMessage
Generates mock chat message.

### createMockChatSession
Generates mock chat session with messages.

## Best Practices
1. Always use utility functions for consistent test setup
2. Clean up after tests using provided cleanup functions
3. Use fixtures for predictable test data
4. Document custom test utilities
```

---

## Testing Strategy

### Test Structure

```typescript
describe('TestUtility', () => {
  describe('Happy Path', () => {
    // Test normal usage
  });

  describe('Edge Cases', () => {
    // Test boundary conditions
  });

  describe('Error Handling', () => {
    // Test error scenarios
  });

  describe('Cleanup', () => {
    // Test resource cleanup
  });
});
```

### Coverage Goals

- **Statements**: 90%+ (currently 49-70%)
- **Branches**: 90%+ (currently 0%)
- **Functions**: 90%+ (currently 25-62%)
- **Lines**: 90%+ (currently varies)

---

## Acceptance Criteria

### Coverage Metrics
- [ ] Chat shared utilities: 49% → 90%+
- [ ] General test utils: 70% → 90%+
- [ ] lib/test-utils: 57% → 90%+
- [ ] **Branch coverage: 0% → 90%+ (CRITICAL)**

### Functional Testing
- [ ] All utility functions tested
- [ ] All conditional branches covered
- [ ] Error scenarios tested
- [ ] Edge cases covered
- [ ] Cleanup functions validated

### Documentation
- [ ] API documentation created
- [ ] Usage examples provided
- [ ] Best practices documented
- [ ] Common patterns explained

### Quality
- [ ] All tests passing
- [ ] No flaky tests
- [ ] Clear test descriptions
- [ ] Proper assertions

---

## Success Metrics

### Before
```
__tests__/pages/chat/shared: 49.33% statements, 0% branches
__tests__/utils:            70.58% statements, 0% branches
lib/__tests__/test-utils:   56.66% statements, 64.7% branches
```

### After (Target)
```
__tests__/pages/chat/shared: 90%+ statements, 90%+ branches ✅
__tests__/utils:             90%+ statements, 90%+ branches ✅
lib/__tests__/test-utils:    90%+ statements, 90%+ branches ✅
```

---

## Common Patterns

### Testing Utility Functions

```typescript
// Test function with default behavior
it('should work with defaults', () => {
  const result = utilityFunction();
  expect(result).toBeDefined();
});

// Test function with custom options
it('should work with custom options', () => {
  const result = utilityFunction({ option: 'custom' });
  expect(result.option).toBe('custom');
});

// Test all branches
it('should handle condition A', () => {
  const result = utilityFunction({ condition: 'A' });
  expect(result.path).toBe('A');
});

it('should handle condition B', () => {
  const result = utilityFunction({ condition: 'B' });
  expect(result.path).toBe('B');
});
```

### Testing Cleanup Functions

```typescript
it('should clean up resources', () => {
  const env = setupEnvironment();

  // Verify setup worked
  expect(env.resource).toBeDefined();

  // Clean up
  env.cleanup();

  // Verify cleanup worked
  expect(global.resource).toBeUndefined();
});
```

---

## Dependencies

- No blocking dependencies
- Can start immediately
- May help with TEST-ISSUE-002 (failing tests may use these utilities)

---

## Risk Assessment

### Risks
1. **Breaking Changes**: Fixing utilities may break existing tests
2. **False Sense of Security**: Tests may pass but utility still has bugs
3. **Over-Testing**: Testing implementation details vs behavior

### Mitigation
- Run full test suite after each utility change
- Test behavior, not implementation
- Focus on how utilities are actually used
- Coordinate with TEST-ISSUE-002 to fix any broken tests

---

## Definition of Done

- [ ] All 4 tasks completed
- [ ] Coverage targets met (90%+ all metrics)
- [ ] All branches tested (0% → 90%+)
- [ ] Documentation created
- [ ] All tests passing
- [ ] No regressions in other tests
- [ ] Code reviewed and approved
- [ ] Merged to main branch

---

## Related Issues

- **TEST-ISSUE-002**: Fix Failing Tests (may use these utilities)
- **TEST-ISSUE-004**: Chat Coverage (uses chat shared utilities)
- **TEST-ISSUE-007**: Documentation (related to documentation task)

---

## References

- [Jest Best Practices](https://jestjs.io/docs/best-practices)
- [Testing Utilities Pattern](https://kentcdodds.com/blog/test-isolation-with-react)
- [Don't Test Implementation Details](https://kentcdodds.com/blog/testing-implementation-details)

---

**Created**: 2025-11-05
**Status**: Ready for Assignment
**Assignee**: TBD
**Due Date**: Within 7 days (HIGH priority)
**Estimated Effort**: 6-8 hours
