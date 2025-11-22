# Test Patterns Reference

Quick pattern lookup for writing tests. Use in conjunction with `test-writing-guide.md`.

**Goal**: Find the right pattern in <30 seconds

---

## Overview

This document provides copy-pasteable patterns extracted from MeepleAI production tests. Each pattern includes:
- Real code from the codebase
- File path for reference
- When to use it
- Key implementation details

---

## Component Testing Patterns

### Pattern: Grouped Assertions by Feature

**When to use**: Testing a component with multiple related behaviors. Organize tests by feature with nested `describe()` blocks.

**File**: `apps/web/src/components/loading/__tests__/LoadingButton.test.tsx`

```typescript
describe('LoadingButton', () => {
  describe('Basic rendering', () => {
    it('should render children when not loading', () => {
      render(<LoadingButton>Click me</LoadingButton>);
      expect(screen.getByRole('button')).toHaveTextContent('Click me');
    });
  });

  describe('Loading state', () => {
    it('should show spinner when isLoading is true', () => {
      const { container } = render(<LoadingButton isLoading={true}>Submit</LoadingButton>);
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
    });

    it('should hide children when loading and no loadingText provided', () => {
      render(<LoadingButton isLoading={true}>Original Text</LoadingButton>);
      expect(screen.queryByText('Original Text')).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have aria-busy attribute when loading', () => {
      render(<LoadingButton isLoading={true}>Submit</LoadingButton>);
      expect(screen.getByRole('button')).toHaveAttribute('aria-busy', 'true');
    });
  });
});
```

**Key Points**:
- Group related tests with `describe()` blocks
- One assertion per test where possible
- Use semantic role queries first: `getByRole()`
- Test accessibility attributes explicitly

---

### Pattern: State Transitions & Callbacks

**When to use**: Testing components that respond to user interactions and call callbacks.

**File**: `apps/web/src/components/editor/__tests__/ViewModeToggle.test.tsx`

```typescript
describe('ViewModeToggle', () => {
  const mockOnModeChange = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('calls onModeChange with "rich" when rich button is clicked', () => {
    render(<ViewModeToggle mode="json" onModeChange={mockOnModeChange} />);

    const richButton = screen.getByText(/📝 Editor Visuale/);
    fireEvent.click(richButton);

    expect(mockOnModeChange).toHaveBeenCalledWith('rich');
    expect(mockOnModeChange).toHaveBeenCalledTimes(1);
  });

  it('highlights active mode with correct styling', () => {
    render(<ViewModeToggle mode="rich" onModeChange={mockOnModeChange} />);

    const richButton = screen.getByText(/📝 Editor Visuale/);
    expect(richButton).toHaveStyle({
      background: 'white',
      color: '#0070f3',
      fontWeight: 'bold'
    });
  });
});
```

**Key Points**:
- Create mock callbacks before tests
- Clear mocks in `beforeEach()`
- Verify callback arguments match expectations
- Verify callback call count to prevent silent failures
- Use `toHaveStyle()` for visual state validation

---

### Pattern: Snapshot Testing for UI Variants

**When to use**: When component has multiple visual states (normal, loading, error, disabled).

**File**: `apps/web/src/components/loading/__tests__/LoadingButton.test.tsx`

```typescript
describe('Snapshot tests', () => {
  it('should match snapshot in default state', () => {
    const { container } = render(<LoadingButton>Submit</LoadingButton>);
    expect(container.firstChild).toMatchSnapshot();
  });

  it('should match snapshot in loading state', () => {
    const { container } = render(
      <LoadingButton isLoading={true} loadingText="Processing...">
        Submit
      </LoadingButton>
    );
    expect(container.firstChild).toMatchSnapshot();
  });
});
```

**Key Points**:
- Use snapshots to detect unintended visual changes
- Create separate snapshots for each major state
- Review snapshot diffs carefully when updating
- Don't use snapshots for dynamic content (timestamps, IDs)

---

## Hook Testing Patterns

### Pattern: Hook State Management

**When to use**: Testing custom hooks that manage state (useState, useReducer).

**File**: `apps/web/src/hooks/__tests__/useToast.test.ts`

```typescript
describe('useToast', () => {
  beforeEach(() => {
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  });

  describe('Toast Creation', () => {
    it('should create a success toast', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('Success Title', 'Success message');
      });

      expect(result.current.toasts).toHaveLength(1);
      expect(result.current.toasts[0]).toMatchObject({
        type: 'success',
        title: 'Success Title',
        message: 'Success message',
      });
    });

    it('should return unique toast ID on creation', () => {
      const { result } = renderHook(() => useToast());

      let id1: string = '';
      let id2: string = '';

      act(() => {
        id1 = result.current.success('Toast 1');
        id2 = result.current.success('Toast 2');
      });

      expect(id1).not.toBe(id2);
      expect(id1).toMatch(/^toast-\d+$/);
    });
  });
});
```

**Key Points**:
- Use `renderHook()` for custom hooks
- Wrap state updates in `act()` (prevents console warnings)
- Use `jest.useFakeTimers()` for time-dependent logic
- Always restore with `jest.useRealTimers()` in cleanup
- Test immutability: new array references on state change

---

### Pattern: Hook Function Stability

**When to use**: Verifying hook functions don't change identity on rerenders (prevents infinite loops).

**File**: `apps/web/src/hooks/__tests__/useToast.test.ts`

```typescript
it('should maintain stable function references', () => {
  const { result, rerender } = renderHook(() => useToast());

  const initialSuccess = result.current.success;
  const initialError = result.current.error;
  const initialDismiss = result.current.dismiss;

  act(() => {
    result.current.success('Toast');
  });

  rerender();

  expect(result.current.success).toBe(initialSuccess);
  expect(result.current.error).toBe(initialError);
  expect(result.current.dismiss).toBe(initialDismiss);
});
```

**Key Points**:
- Store initial function references
- Trigger state changes with `act()`
- Call `rerender()` to simulate parent rerender
- Verify functions are same reference (===)

---

## API Mocking Patterns

### Pattern: Global Fetch Mocking

**When to use**: Testing code that calls `fetch()` without additional HTTP library.

**File**: `apps/web/src/lib/__tests__/api.test.ts`

```typescript
describe('api', () => {
  let originalFetch: typeof global.fetch;
  let fetchMock: jest.MockedFunction<typeof fetch>;

  const setFetchResponse = (status: number, payload?: unknown) => {
    fetchMock.mockResolvedValue({
      ok: status >= 200 && status < 300,
      status,
      json: async () => payload,
      headers: new Headers()
    } as Response);
  };

  beforeEach(() => {
    originalFetch = global.fetch;
    fetchMock = jest.fn() as jest.MockedFunction<typeof fetch>;
    global.fetch = fetchMock;
  });

  afterEach(() => {
    global.fetch = originalFetch;
    fetchMock.mockReset();
  });

  it('returns parsed JSON for successful get', async () => {
    const data = { id: 'game' };
    setFetchResponse(200, data);

    const result = await api.get<typeof data>('/games');

    expect(result).toEqual(data);
    expect(fetchMock).toHaveBeenCalledWith('http://localhost:8080/games', {
      method: 'GET',
      credentials: 'include'
    });
  });

  it('returns null for 401 get', async () => {
    setFetchResponse(401);

    const result = await api.get('/games');

    expect(result).toBeNull();
  });

  it('throws for unexpected get status', async () => {
    setFetchResponse(500);

    await expect(api.get('/games')).rejects.toThrow('API /games 500');
  });
});
```

**Key Points**:
- Save original `global.fetch` before mocking
- Create helper `setFetchResponse()` to reduce repetition
- Always restore in `afterEach()`
- Mock `Response.json()` as async function
- Test both success (200, 201) and error cases (401, 500)

---

### Pattern: Module Mocking for API

**When to use**: Testing components that use your API client module.

**File**: `apps/web/__tests__/pages/chat.test.tsx`

```typescript
import * as api from '@/lib/api';

jest.mock('@/lib/api');

describe('ChatPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('displays chat messages from API', async () => {
    const mockMessages = [
      { id: '1', content: 'Hello', role: 'user' },
      { id: '2', content: 'Hi there!', role: 'assistant' }
    ];

    jest.spyOn(api, 'getChatMessages').mockResolvedValue(mockMessages);

    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByText('Hello')).toBeInTheDocument();
      expect(screen.getByText('Hi there!')).toBeInTheDocument();
    });
  });

  it('handles API errors gracefully', async () => {
    jest.spyOn(api, 'getChatMessages').mockRejectedValue(new Error('Network error'));

    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByText(/error loading messages/i)).toBeInTheDocument();
    });
  });
});
```

**Key Points**:
- Mock entire module with `jest.mock()`
- Use `jest.spyOn()` on specific functions
- Mock both success and error paths
- Use `mockResolvedValue()` for promises
- Clear mocks in `beforeEach()` to prevent pollution

---

## Edge Case Testing Patterns

### Pattern: Empty and Null Data Handling

**When to use**: Testing components with optional or empty data.

**File**: `apps/web/src/hooks/__tests__/useToast.test.ts`

```typescript
describe('Edge Cases', () => {
  it('should handle empty string title', () => {
    const { result } = renderHook(() => useToast());

    act(() => {
      result.current.success('');
    });

    expect(result.current.toasts[0].title).toBe('');
  });

  it('should handle empty string message', () => {
    const { result } = renderHook(() => useToast());

    act(() => {
      result.current.success('Title', '');
    });

    expect(result.current.toasts[0].message).toBe('');
  });

  it('should handle very long toast titles', () => {
    const { result } = renderHook(() => useToast());
    const longTitle = 'A'.repeat(1000);

    act(() => {
      result.current.success(longTitle);
    });

    expect(result.current.toasts[0].title).toBe(longTitle);
  });
});
```

**Key Points**:
- Test empty strings (not just null/undefined)
- Test boundary values (zero, negative, very large)
- Test special characters and HTML-like content
- Verify graceful handling without errors

---

### Pattern: Authorization and Authentication Edge Cases

**When to use**: Testing pages/components with role-based access or auth checks.

**File**: `apps/web/src/__tests__/pages/upload.edge-cases.test.tsx`

```typescript
describe('UploadPage - Edge Cases', () => {
  describe('Given user does not have required role', () => {
    describe('When user has Viewer role', () => {
      it('Then access is blocked with role requirement message', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({
            userId: 'viewer-1',
            email: 'viewer@example.com',
            role: 'Viewer',
            displayName: 'Viewer User'
          })
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() =>
          expect(screen.getByText(/You need an Editor or Admin role/i)).toBeInTheDocument()
        );
      });
    });
  });

  describe('Given user is not authenticated', () => {
    describe('When page initializes', () => {
      it('Then shows login requirement message', async () => {
        const mockFetch = setupUploadMocks({ auth: null });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => {
          expect(screen.getByText(/You need to be logged in/i)).toBeInTheDocument();
        });
      });
    });
  });
});
```

**Key Points**:
- Use BDD-style `Given/When/Then` for clarity
- Test each role separately
- Test unauthenticated state (null auth)
- Verify permission-denied messages clearly
- Use fixture helpers to reduce mock setup complexity

---

### Pattern: Rapid Operations and Race Conditions

**When to use**: Testing operations that happen in quick succession.

**File**: `apps/web/src/hooks/__tests__/useToast.test.ts`

```typescript
describe('Multiple Toasts', () => {
  it('should handle rapid toast creation', () => {
    const { result } = renderHook(() => useToast());

    act(() => {
      for (let i = 0; i < 10; i++) {
        result.current.success(`Toast ${i}`);
      }
    });

    expect(result.current.toasts).toHaveLength(10);
    expect(result.current.toasts.map((t) => t.title)).toEqual(
      Array.from({ length: 10 }, (_, i) => `Toast ${i}`)
    );
  });

  it('should maintain toast order (FIFO)', () => {
    const { result } = renderHook(() => useToast());

    act(() => {
      result.current.success('First');
      result.current.error('Second');
      result.current.info('Third');
    });

    expect(result.current.toasts[0].title).toBe('First');
    expect(result.current.toasts[1].title).toBe('Second');
    expect(result.current.toasts[2].title).toBe('Third');
  });
});
```

**Key Points**:
- Batch related operations in single `act()` block
- Verify collection length matches expected count
- Verify order is preserved (FIFO/LIFO)
- Generate test data programmatically

---

## Pattern Selection Guide

**Choose pattern based on what you're testing**:

| What You're Testing | Pattern | File Path |
|---|---|---|
| Component renders correctly | Grouped Assertions | LoadingButton |
| Component responds to user input | State Transitions | ViewModeToggle |
| Multiple UI states exist | Snapshot Testing | LoadingButton |
| Custom hook with state | Hook State Management | useToast |
| Hook functions don't recreate | Hook Function Stability | useToast |
| Component calls API | API Mocking | ChatPage |
| Page with API integration | Module Mocking | chat.test |
| Empty/null/boundary values | Empty Data | useToast |
| Role-based access control | Auth Edge Cases | upload.edge-cases |
| Rapid successive operations | Race Conditions | useToast |

---

## Backend Patterns

### Pattern: Encryption/Decryption Testing

**When to use**: Testing symmetric operations (encrypt/decrypt, hash verify).

**File**: `apps/api/tests/Api.Tests/EncryptionServiceTests.cs`

```csharp
public class EncryptionServiceTests
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly EncryptionService _service;

    public EncryptionServiceTests(ITestOutputHelper output)
    {
        // Use EphemeralDataProtectionProvider for testing (in-memory)
        _dataProtectionProvider = new EphemeralDataProtectionProvider();
        _service = new EncryptionService(_dataProtectionProvider, Mock.Of<ILogger<EncryptionService>>());
    }

    [Fact]
    public async Task EncryptAsync_ValidPlaintext_ReturnsEncrypted()
    {
        // Arrange
        var plaintext = "secret-token-12345";

        // Act
        var result = await _service.EncryptAsync(plaintext);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().NotBe(plaintext); // Encrypted ≠ plaintext
    }

    [Fact]
    public async Task EncryptAsync_WithCustomPurpose_CanDecrypt()
    {
        // Arrange
        var plaintext = "sensitive-data";
        var purpose = "CustomPurpose";

        // Act
        var encrypted = await _service.EncryptAsync(plaintext, purpose);
        var decrypted = await _service.DecryptAsync(encrypted, purpose);

        // Assert
        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public async Task DecryptAsync_WithWrongPurpose_ThrowsException()
    {
        // Arrange
        var plaintext = "sensitive-data";
        var ciphertext = await _service.EncryptAsync(plaintext, "EncryptPurpose");

        // Act & Assert
        await FluentActions.Invoking(() => _service.DecryptAsync(ciphertext, "WrongPurpose"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task EncryptAsync_NullOrEmptyPlaintext_ThrowsArgumentException(string? plaintext)
    {
        // Act & Assert
        await FluentActions.Invoking(() => _service.EncryptAsync(plaintext!))
            .Should().ThrowAsync<ArgumentException>();
    }
}
```

**Key Points**:
- Use `EphemeralDataProtectionProvider` for unit tests
- Test round-trip: encrypt then decrypt
- Test wrong parameters throw exceptions
- Use `[Theory]` with `[InlineData]` for null/empty variants
- Use FluentAssertions: `Should()`, `Should().NotBe()`, `Should().Throw()`

---

## Quick Reference

### Frontend Common Assertions

```typescript
// Rendering
expect(screen.getByRole('button')).toBeInTheDocument();
expect(screen.getByText(/pattern/i)).toBeInTheDocument();
expect(screen.queryByText('Gone')).not.toBeInTheDocument();

// State
expect(element).toBeDisabled();
expect(element).toHaveClass('active');
expect(element).toHaveAttribute('aria-busy', 'true');

// Styling
expect(element).toHaveStyle({ color: 'red' });

// Callbacks
expect(mockFn).toHaveBeenCalledWith('arg');
expect(mockFn).toHaveBeenCalledTimes(1);

// Async
await waitFor(() => expect(...).toBeTruthy());
```

### Backend Common Assertions (FluentAssertions)

```csharp
// Objects
result.Should().NotBeNull();
result.Should().Be(expected);
result.Should().BeOfType<User>();

// Collections
list.Should().HaveCount(5);
list.Should().Contain(item);
list.Should().BeEmpty();

// Exceptions
await FluentActions.Invoking(() => method())
    .Should().ThrowAsync<ArgumentException>();

// Strings
str.Should().Contain("substring");
str.Should().StartWith("prefix");
```

---

## See Also

- **Test Writing Guide**: `docs/testing/test-writing-guide.md` - Comprehensive tutorial
- **Test Execution Guide**: `docs/testing/test-execution-guide.md` - CI/CD and running tests
- **Example Tests**: `apps/web/__tests__` and `apps/api/tests` - Real production tests
