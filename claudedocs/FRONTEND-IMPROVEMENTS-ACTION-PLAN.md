# Frontend Improvements - Action Plan

**Based on**: Frontend Architecture Review (2025-10-24)
**Full Report**: `claudedocs/frontend-architecture-review-2025-10-24.md` (60KB)

---

## 🎯 Quick Wins (High Impact, Low Effort)

### 1. Fix Remaining Test Failures (2 hours)
**Current**: 15 tests failing in 3 suites
**Files**:
- `upload.continuation.test.tsx` (4 polling tests)
- `versions.test.tsx` (some tests)
- `chat-test-utils.ts` (utility error)

**Action**:
```bash
# Investigate and fix polling tests
cd apps/web && pnpm test upload.continuation.test.tsx --no-coverage

# Fix versions tests
pnpm test versions.test.tsx --no-coverage
```

### 2. Centralize Type Definitions (4 hours)
**Current**: Types scattered across files
**Target**: Create `src/types/` directory

**Action**:
```typescript
// Create src/types/index.ts
export type { AuthUser, AuthResponse } from './auth';
export type { Game, Agent, Chat, Message } from './domain';
export type { UploadStatus, ProcessingStatus } from './upload';
```

### 3. Add Error Display Component (4 hours)
**Current**: Inline error handling
**Target**: Reusable `<ErrorDisplay />` component

**Benefits**:
- Consistent error UX
- Better accessibility
- Reduced code duplication

### 4. Implement Loading Skeletons (6 hours)
**Current**: Generic loading states
**Target**: Content-specific skeletons

**Files to Update**:
- `pages/chat.tsx` - Chat list skeleton
- `pages/upload.tsx` - Upload queue skeleton
- `components/ProcessingProgress.tsx` - Progress skeleton

---

## 🚀 Priority 1: Component Decomposition (2-3 weeks)

### Chat Page Refactoring (Week 1-2)

**Current State**:
```
chat.tsx: 1640 lines, 15 state variables, complexity: 80
```

**Target State**:
```typescript
<ChatPage> (200 lines)
  ├─ <ChatSidebar /> (150 lines)
  │   ├─ <GameSelector />
  │   ├─ <AgentSelector />
  │   └─ <ChatHistory />
  ├─ <ChatContent /> (200 lines)
  │   ├─ <MessageList /> (with virtualization)
  │   ├─ <StreamingResponse />
  │   └─ <MessageInput />
  └─ <ChatProvider /> (state management)
```

**Benefits**:
- 40% reduction in cognitive load
- Isolated testing (each component <300 lines)
- Reusable components
- Better code navigation

**Estimated Effort**: 16 hours

### Upload Page Refactoring (Week 2-3)

**Current State**:
```
upload.tsx: 1570 lines, multi-step wizard, complex state
```

**Target State**:
```typescript
<UploadPage>
  ├─ <WizardProvider /> (state machine)
  ├─ <StepUpload />
  ├─ <StepParse />
  ├─ <StepReview />
  └─ <StepPublish />
```

**Benefits**:
- Clear separation of concerns
- Easier to add/remove steps
- Better testability
- Reduced bundle size (lazy load steps)

**Estimated Effort**: 12 hours

---

## 🔧 Priority 2: State Management (1-2 weeks)

### Replace Map-based State with Reducer

**Current Problem**:
```typescript
// Complex Map state in chat.tsx
const [chatStatesByGame, setChatStatesByGame] = useState<Map<string, GameChatState>>(new Map());

// Manual synchronization
setChatStatesByGame(prev => {
  const newMap = new Map(prev);
  const currentState = newMap.get(gameId) || defaultState;
  newMap.set(gameId, { ...currentState, messages: newMessages });
  return newMap;
});
```

**Recommended Solution**:
```typescript
// Clean reducer pattern
const chatReducer = (state: ChatState, action: ChatAction): ChatState => {
  switch (action.type) {
    case 'MESSAGE_ADDED':
      return {
        ...state,
        chats: {
          ...state.chats,
          [action.payload.gameId]: {
            ...state.chats[action.payload.gameId],
            messages: [...state.chats[action.payload.gameId].messages, action.payload.message]
          }
        }
      };
    // ... other actions
  }
};

// Usage
const [state, dispatch] = useReducer(chatReducer, initialState);
dispatch({ type: 'MESSAGE_ADDED', payload: { gameId, message } });
```

**Benefits**:
- Predictable state updates
- Easier debugging (time-travel debugging)
- Better testing (pure functions)
- Type-safe actions

**Estimated Effort**: 20 hours

---

## 📊 Priority 3: Performance Optimization (1 week)

### 1. Message List Virtualization

**Current**: Renders all messages (performance issue with 100+ messages)
**Target**: Virtualize with `react-window` or `@tanstack/react-virtual`

**Code Example**:
```typescript
import { useVirtualizer } from '@tanstack/react-virtual';

function MessageList({ messages }: { messages: Message[] }) {
  const parentRef = useRef<HTMLDivElement>(null);

  const virtualizer = useVirtualizer({
    count: messages.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => 80, // estimated message height
  });

  return (
    <div ref={parentRef} style={{ height: '100%', overflow: 'auto' }}>
      <div style={{ height: `${virtualizer.getTotalSize()}px`, position: 'relative' }}>
        {virtualizer.getVirtualItems().map(virtualRow => (
          <div
            key={virtualRow.index}
            style={{
              position: 'absolute',
              top: 0,
              left: 0,
              width: '100%',
              height: `${virtualRow.size}px`,
              transform: `translateY(${virtualRow.start}px)`,
            }}
          >
            <MessageItem message={messages[virtualRow.index]} />
          </div>
        ))}
      </div>
    </div>
  );
}
```

**Benefits**:
- 50% faster rendering (100+ messages)
- Smooth scrolling
- Lower memory usage

**Estimated Effort**: 8 hours

### 2. Code Splitting

**Current**: Large bundle size
**Target**: Route-based + component-based splitting

```typescript
// Route-based splitting
const ChatPage = dynamic(() => import('./pages/chat'), { ssr: false });
const UploadPage = dynamic(() => import('./pages/upload'), { ssr: false });

// Component-based splitting
const Editor = dynamic(() => import('./components/Editor'), {
  loading: () => <EditorSkeleton />,
  ssr: false
});
```

**Benefits**:
- 40% smaller initial bundle
- Faster page load
- Better Core Web Vitals

**Estimated Effort**: 6 hours

### 3. Memoization Strategy

**Apply React.memo to**:
- `MessageItem` component
- `ChatHistoryItem` component
- `UploadQueueItem` component

**Example**:
```typescript
export const MessageItem = React.memo<MessageItemProps>(
  ({ message, onFeedback }) => {
    // Component logic
  },
  (prev, next) => {
    // Custom equality check
    return prev.message.id === next.message.id &&
           prev.message.feedback === next.message.feedback;
  }
);
```

**Estimated Effort**: 4 hours

---

## 🧪 Priority 4: Test Architecture (1 week)

### 1. Complete Fixture Migration

**Current**: Mix of inline mocks and fixtures
**Target**: All tests use centralized fixtures

**Action**:
```typescript
// Migrate all tests to use fixtures from common-fixtures.ts
import {
  createMockUser,
  createMockGame,
  createMockChat,
  createMockAuthResponse
} from '@/__tests__/fixtures/common-fixtures';
```

**Estimated Effort**: 8 hours

### 2. Implement MSW for API Mocking

**Current**: Manual `jest.fn()` mocks
**Target**: MSW (Mock Service Worker) for consistent API mocking

**Setup**:
```typescript
// src/__tests__/mocks/handlers.ts
import { http, HttpResponse } from 'msw';

export const handlers = [
  http.get('/api/v1/auth/me', () => {
    return HttpResponse.json(createMockAuthResponse());
  }),
  http.get('/api/v1/games', () => {
    return HttpResponse.json([createMockGame()]);
  }),
  // ... more handlers
];

// src/__tests__/mocks/server.ts
import { setupServer } from 'msw/node';
import { handlers } from './handlers';

export const server = setupServer(...handlers);
```

**Benefits**:
- Consistent API mocking across tests
- Better integration testing
- Easier to maintain
- Works in both tests and Storybook

**Estimated Effort**: 12 hours

---

## 📋 Implementation Roadmap

### Phase 1: Foundation (Week 1-2)
- ✅ Fix remaining 15 test failures
- ✅ Centralize type definitions
- ✅ Create error display component
- ✅ Add loading skeletons
- ✅ Setup MSW for API mocking

**Total**: ~40 hours

### Phase 2: Chat Refactoring (Week 3-4)
- ✅ Extract ChatSidebar component
- ✅ Extract ChatContent component
- ✅ Implement reducer pattern
- ✅ Add virtualization to MessageList
- ✅ Update tests

**Total**: ~32 hours

### Phase 3: Upload Refactoring (Week 5-6)
- ✅ Extract wizard step components
- ✅ Implement state machine
- ✅ Add code splitting
- ✅ Update tests

**Total**: ~24 hours

### Phase 4: Performance & Polish (Week 7-8)
- ✅ Add memoization
- ✅ Optimize bundle size
- ✅ Improve accessibility
- ✅ Final test coverage push (100%)

**Total**: ~24 hours

### Phase 5: Documentation (Week 9)
- ✅ Update component documentation
- ✅ Create architecture diagrams
- ✅ Write migration guide
- ✅ Team knowledge sharing

**Total**: ~16 hours

---

## 📈 Success Metrics

### Before
- Test pass rate: 97.4% (1586/1627)
- Largest component: 1640 lines
- Bundle size: ~500KB (estimated)
- Chat rendering (100 msgs): ~800ms

### After (Target)
- Test pass rate: **100%** (1627/1627)
- Largest component: **<300 lines**
- Bundle size: **~300KB** (40% reduction)
- Chat rendering (100 msgs): **~400ms** (50% faster)
- Type coverage: **95%+**
- WCAG compliance: **100% AA**

---

## 🎯 Next Steps

### Immediate (This Week)
1. Review full architecture report: `claudedocs/frontend-architecture-review-2025-10-24.md`
2. Prioritize quick wins (fix tests, types, errors)
3. Create feature branch: `feature/frontend-improvements`
4. Start with test fixes (highest impact, lowest risk)

### Short-term (Next 2 Weeks)
1. Complete Phase 1 (Foundation)
2. Begin Chat page refactoring
3. Setup MSW for consistent mocking

### Medium-term (Next 2 Months)
1. Complete all component refactoring
2. Achieve 100% test pass rate
3. Performance optimization complete

---

## 📚 Resources

**Full Reports**:
- Architecture Review: `claudedocs/frontend-architecture-review-2025-10-24.md`
- Chat Fix Details: `claudedocs/chat-tests-resolution-summary.md`
- Test Suite Status: `claudedocs/test-suite-status-2025-10-24.md`

**Tools Recommended**:
- React DevTools Profiler (performance)
- Bundle Analyzer (size optimization)
- MSW (API mocking)
- @tanstack/react-virtual (virtualization)
- Storybook (component development)

---

**Created**: 2025-10-24
**Status**: Ready for implementation
**Estimated Total Effort**: 136 hours (~3.5 weeks full-time)
**Expected ROI**: High (better maintainability, performance, developer experience)
