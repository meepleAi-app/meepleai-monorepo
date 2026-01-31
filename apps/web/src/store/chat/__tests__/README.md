# Chat Store Tests

Comprehensive test coverage for the Zustand-based chat store (Issue #1083).

## Test Files

### ✅ uiSlice.test.ts
**Coverage: 100%** (44 tests, all passing)

Comprehensive tests for UI state management:
- **Loading States**: All 8 loading flags (chats, messages, sending, creating, updating, deleting, games, agents)
- **Error Management**: setError, clearError actions
- **Input State**: Message input value management
- **Message Editing**: startEdit, setEditContent, cancelEdit, saveEdit workflow
- **Search Mode**: Vector, Keyword, Hybrid mode toggle
- **Integration**: Multi-action workflows and state independence

### ✅ Streaming Tests Migrated
**Note**: Streaming functionality has been migrated to `useChatStreaming` (Issue #1451).

Streaming tests are now located at:
- `apps/web/src/lib/hooks/__tests__/useChatStreaming.test.ts` - Real SSE streaming tests
- `apps/web/src/lib/hooks/__tests__/useMockStreaming.test.ts` - Mock streaming tests
- `apps/web/src/lib/hooks/__tests__/useChatStreaming.unified.test.ts` - Mode switching tests

See `docs/04-frontend/streaming-hooks.md` for complete documentation.

## Running Tests

### Run all store tests
```bash
npm test -- src/store/chat
```

### Run specific test file
```bash
npm test -- src/store/chat/slices/__tests__/uiSlice.test.ts
```

### Run with coverage
```bash
npm test -- src/store/chat/slices/__tests__/uiSlice.test.ts --coverage --collectCoverageFrom="src/store/chat/slices/uiSlice.ts"
```

## Coverage Results

### uiSlice.ts
- **Statements**: 100%
- **Branches**: 100%
- **Functions**: 100%
- **Lines**: 100%

### Streaming Tests (Migrated)
Streaming test coverage is now maintained in:
- `useChatStreaming.test.ts` - Real SSE: ~90%+ coverage
- `useMockStreaming.test.ts` - Mock mode: ~90%+ coverage
- `useChatStreaming.unified.test.ts` - Mode switching: 100% coverage

## Test Organization

### Test Structure Pattern
```typescript
describe('Component - Category', () => {
  describe('action/feature', () => {
    it('should [expected behavior]', () => {
      // AAA Pattern: Arrange, Act, Assert
    });
  });
});
```

### Test Categories
1. **Initial State**: Validates default state values
2. **Actions**: Tests each action in isolation
3. **Integration**: Tests multi-action workflows
4. **Edge Cases**: Validates error conditions and unusual inputs
5. **Cleanup**: Ensures proper resource cleanup

## Key Testing Patterns

### Mock Store Setup
```typescript
const createTestStore = () => {
  return create<UISlice>()(
    subscribeWithSelector(
      immer((set, get) => ({
        ...createUISlice(set, get, {} as any),
      }))
    )
  );
};
```

### Async Testing
```typescript
await act(async () => {
  await result.current.startStream('Test message');
  await waitForStreamComplete(result);
});
```

### State Validation
```typescript
expect(result.current.loading).toEqual({
  chats: false,
  messages: false,
  sending: false,
  // ... all 8 flags
});
```

## Coverage Improvements

### Before (Issue #1083)
- uiSlice.ts: 35.48%

### After (Issue #1083)
- uiSlice.ts: **100%** ✅

### Total Improvement
- **+54.52%** for uiSlice.ts
- **Target: 90%+ achieved**

## Notes

### Streaming Tests (Issue #1451)
Streaming functionality has been migrated to unified `useChatStreaming` hook with separate helpers:
- `useRealStreaming` - Real SSE via fetch + ReadableStream
- `useMockStreaming` - Mock simulation with setTimeout

See `docs/04-frontend/streaming-hooks.md` for complete documentation.

### Test Timeouts
Some streaming tests may require increased timeouts due to async behavior:
```bash
npm test -- --testTimeout=30000
```

### Mocking Strategy
- **useChatStore**: Mocked for isolation
- **setTimeout**: Real timers for accurate async behavior testing
- **AbortController**: Real implementation for cancellation testing

## Future Enhancements

### Additional Test Coverage
- Performance testing for large message streams
- Memory leak detection during long streaming sessions
- Concurrent stream handling
- Network condition simulation (slow, offline, intermittent)

## Maintenance

### Running on CI
Tests are configured to run with:
- `--maxWorkers=1` for stability
- `--forceExit` to prevent hanging
- Cleanup scripts for Windows test processes

### Debugging Tests
```bash
# Run with verbose output
npm test -- src/store/chat/slices/__tests__/uiSlice.test.ts --verbose

# Run specific test
npm test -- --testNamePattern="should set loading state"

# Run in watch mode
npm test:watch -- src/store/chat
```

## References

- Issue #1083: Chat Store Zustand Migration
- Issue #1451: Streaming Hooks Consolidation
- CLAUDE.md: Testing standards and coverage requirements
- docs/02-development/testing/testing-guide.md: Comprehensive testing guide
- docs/04-frontend/streaming-hooks.md: Streaming hooks documentation
