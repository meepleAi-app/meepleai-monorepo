# 🔄 [Refactor] Consolidate Streaming Hooks

**Priority**: 🟢 MEDIUM
**Complexity**: Medium
**Estimated Time**: 6-8 hours
**Dependencies**: None

## 🎯 Objective

Consolidate two separate streaming hooks (`useChatStreaming` and `useChatStream`) into a single unified hook with mock/real mode support.

## 📋 Context

**Source**: Code Review Backend-Frontend Interactions
**Issue**: Two implementations for streaming (real SSE + mock setTimeout)
**Impact**: Medium - Improves maintainability, reduces code duplication

## 🔧 Current State

### Hook 1: `useChatStreaming` (Real SSE)
**Location**: `apps/web/src/lib/hooks/useChatStreaming.ts`
- Uses fetch + ReadableStream for SSE
- Real backend integration
- Event types: token, stateUpdate, citations, complete, error, heartbeat, followUpQuestions

### Hook 2: `useChatStream` (Mock)
**Location**: `apps/web/src/store/chat/useChatStream.ts`
- Uses setTimeout for word-by-word simulation
- Mock responses
- Zustand integration

## ✅ Proposed Solution

Create unified `useChatStreaming` hook with `useMock` flag:

```typescript
export function useChatStreaming(options: {
  useMock?: boolean;
  onComplete?: (...) => void;
  onError?: (error: string) => void;
}) {
  const { useMock = false } = options;

  if (useMock) {
    return useMockStreaming(options);
  }

  return useRealStreaming(options);
}
```

## ✅ Task Checklist

### Implementation
- [ ] Extract mock logic from `useChatStream` to `useMockStreaming`
- [ ] Extract real logic from `useChatStreaming` to `useRealStreaming`
- [ ] Create unified `useChatStreaming` with mode selector
- [ ] Add `useMock` flag (default: false, read from env var)
- [ ] Ensure both modes have same interface/return types
- [ ] Add environment variable `NEXT_PUBLIC_USE_MOCK_STREAMING`

### Refactoring
- [ ] Update all components using `useChatStream` to use unified hook
- [ ] Migrate `apps/web/src/app/chat/page.tsx`
- [ ] Migrate any other consumers
- [ ] Deprecate old `useChatStream` hook with JSDoc warning
- [ ] Plan removal of deprecated hook (after 2 sprints)

### Testing
- [ ] Test mock mode preserves current behavior
- [ ] Test real mode preserves current behavior
- [ ] Test mode switching via environment variable
- [ ] Test both modes with same test suite
- [ ] Integration tests for SSE endpoints

### Documentation
- [ ] Create `docs/04-frontend/streaming-hooks.md`
- [ ] Document when to use mock vs real mode
- [ ] Add examples for both modes
- [ ] Update component documentation
- [ ] Add migration guide from old hook

### Configuration
- [ ] Add `NEXT_PUBLIC_USE_MOCK_STREAMING` to `.env.example`
- [ ] Default to `false` (real SSE) in production
- [ ] Allow override for development/testing

## 📁 Files to Modify/Create

```
apps/web/src/lib/hooks/useChatStreaming.ts (MODIFY)
apps/web/src/lib/hooks/internal/useMockStreaming.ts (NEW)
apps/web/src/lib/hooks/internal/useRealStreaming.ts (NEW)
apps/web/src/store/chat/useChatStream.ts (DEPRECATE)
apps/web/src/app/chat/page.tsx (MODIFY)
apps/web/src/lib/hooks/__tests__/useChatStreaming.test.ts (MODIFY)
apps/web/.env.example (MODIFY)
docs/04-frontend/streaming-hooks.md (NEW)
```

## 🔗 References

- [React Custom Hooks Best Practices](https://react.dev/learn/reusing-logic-with-custom-hooks)
- [Server-Sent Events API](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events)
- [Fetch API Streaming](https://developer.mozilla.org/en-US/docs/Web/API/Streams_API/Using_readable_streams)

## 📊 Acceptance Criteria

- ✅ Single unified hook with mock/real modes
- ✅ Both modes have identical interface
- ✅ Environment variable controls mode
- ✅ All consumers migrated
- ✅ Test coverage >= 90% for both modes
- ✅ Documentation complete
- ✅ No breaking changes

## 🔄 Migration Timeline

### Phase 1: Refactor (Week 1)
- Extract logic to separate functions
- Create unified hook
- Test both modes

### Phase 2: Migrate Consumers (Week 2)
- Update all components
- Test integration
- Deprecate old hook

### Phase 3: Cleanup (Sprint 5)
- Remove deprecated hook
- Remove deprecation warnings
- Final testing

## 🏷️ Labels

`priority: medium`, `type: refactor`, `area: frontend`, `effort: medium`, `sprint: 4`
