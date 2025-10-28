# ChatProvider Implementation

**Created**: 2025-10-24
**Status**: Phase 1 Complete (Setup & Infrastructure)
**Location**: `apps/web/src/components/chat/ChatProvider.tsx`
**Tests**: 22/22 passing (100%)

## Overview

ChatProvider is a centralized state management solution for the chat feature using React Context API. It manages all chat-related state including authentication, game/agent selection, chat sessions, messages, and UI state.

**Part of**: Chat Page Refactoring - Phase 1 (2 hours estimated, ~1 hour actual)

## Architecture Decision

**Chosen**: React Context API for state management
**Rationale**:
- Built-in React solution (no external dependencies)
- Good TypeScript support
- Easy to test with mocked context
- Sufficient for medium-complexity state
- Familiar to React developers

**Alternatives Considered**:
- Redux: Rejected (too complex, overkill for this use case)
- Zustand: Rejected (adds dependency, minimal benefit over Context API)
- MobX: Rejected (different paradigm, steeper learning curve)

## Files Created

### 1. ChatProvider.tsx (387 lines)

**Location**: `apps/web/src/components/chat/ChatProvider.tsx`

**Structure**:
```typescript
// Types
interface GameChatState { ... }
interface LoadingState { ... }
export interface ChatContextValue { ... }

// Context
const ChatContext = createContext<ChatContextValue | null>(null);
export function useChatContext(): ChatContextValue { ... }

// Provider Component
export function ChatProvider({ children }: ChatProviderProps) { ... }
```

**Key Responsibilities**:
1. Authentication state (`authUser`)
2. Game & Agent selection (`games`, `selectedGameId`, `agents`, `selectedAgentId`)
3. Multi-game chat state management (`chatStatesByGame` Map)
4. Message operations (send, edit, delete, feedback)
5. Chat operations (create, delete, select)
6. UI state (loading states, errors, sidebar, input)
7. Message edit state (editingMessageId, editContent)

### 2. ChatProvider.test.tsx (329 lines)

**Location**: `apps/web/src/components/chat/__tests__/ChatProvider.test.tsx`

**Test Coverage**: 22 tests (all passing)

**Test Suites**:
1. **Context Initialization** (2 tests)
   - Error when used outside provider
   - Provides context when used within provider

2. **Initial State** (5 tests)
   - Authentication state
   - Game selection state
   - Chat state
   - UI state
   - Message edit state

3. **UI Actions** (5 tests)
   - Toggle sidebar
   - Update input value
   - Start editing message
   - Cancel editing

4. **Game Selection** (2 tests)
   - Select game
   - Select agent

5. **Type Safety** (1 test)
   - Proper TypeScript types

6. **Component Integration** (2 tests)
   - Renders children
   - Multiple children access context

7. **API Stubs** (6 tests)
   - createChat stub
   - deleteChat stub
   - sendMessage stub
   - setMessageFeedback stub
   - editMessage stub
   - deleteMessage stub

## Context API

### ChatContextValue Interface

```typescript
export interface ChatContextValue {
  // Authentication
  authUser: AuthUser | null;

  // Game & Agent Selection
  games: Game[];
  selectedGameId: string | null;
  agents: Agent[];
  selectedAgentId: string | null;
  selectGame: (gameId: string) => void;
  selectAgent: (agentId: string) => void;

  // Chat Management
  chats: Chat[];
  activeChatId: string | null;
  messages: Message[];
  createChat: () => Promise<void>;
  deleteChat: (chatId: string) => Promise<void>;
  selectChat: (chatId: string) => void;

  // Messaging
  sendMessage: (content: string) => Promise<void>;
  setMessageFeedback: (messageId: string, feedback: 'helpful' | 'not-helpful') => Promise<void>;
  editMessage: (messageId: string, content: string) => Promise<void>;
  deleteMessage: (messageId: string) => Promise<void>;

  // UI State
  loading: LoadingState;
  errorMessage: string;
  sidebarCollapsed: boolean;
  toggleSidebar: () => void;

  // Message Edit State
  editingMessageId: string | null;
  editContent: string;
  startEditMessage: (messageId: string, content: string) => void;
  cancelEdit: () => void;
  saveEdit: () => Promise<void>;

  // Input State
  inputValue: string;
  setInputValue: (value: string) => void;
}
```

### Usage

```typescript
import { ChatProvider, useChatContext } from '@/components/chat/ChatProvider';

// In your page component
function ChatPage() {
  return (
    <ChatProvider>
      <ChatSidebar />
      <ChatContent />
    </ChatProvider>
  );
}

// In child components
function ChatSidebar() {
  const {
    games,
    selectedGameId,
    selectGame,
    loading
  } = useChatContext();

  return (
    <div>
      {loading.games ? (
        <SkeletonLoader variant="games" />
      ) : (
        <select onChange={(e) => selectGame(e.target.value)}>
          {games.map(game => (
            <option key={game.id} value={game.id}>{game.name}</option>
          ))}
        </select>
      )}
    </div>
  );
}
```

## State Management Design

### Multi-Game Chat State

Uses a `Map<string, GameChatState>` to maintain independent chat state for each game:

```typescript
interface GameChatState {
  chats: Chat[];
  activeChatId: string | null;
  messages: Message[];
}
```

**Benefits**:
- Each game maintains its own chat sessions
- Switching games preserves chat context
- No state loss when navigating between games

### Helper Functions

Three helper functions manage game-specific state updates:

```typescript
const setChats = useCallback((updater: React.SetStateAction<Chat[]>) => {
  if (!selectedGameId) return;
  setChatStatesByGame(prev => {
    const newMap = new Map(prev);
    const currentState = newMap.get(selectedGameId) || defaultState;
    const newChats = typeof updater === 'function' ? updater(currentState.chats) : updater;
    newMap.set(selectedGameId, { ...currentState, chats: newChats });
    return newMap;
  });
}, [selectedGameId]);

const setActiveChatId = useCallback((chatId: string | null) => { ... });
const setMessages = useCallback((updater: React.SetStateAction<Message[]>) => { ... });
```

**Pattern**: Updater function or direct value, similar to `useState` API

### Loading States

Granular loading states for better UX:

```typescript
interface LoadingState {
  games: boolean;        // Loading games list
  agents: boolean;       // Loading agents for selected game
  chats: boolean;        // Loading chat history
  messages: boolean;     // Loading messages for selected chat
  sending: boolean;      // Sending a new message
  creating: boolean;     // Creating a new chat
  updating: boolean;     // Updating/editing a message
  deleting: boolean;     // Deleting a message
}
```

**Usage**: Show specific loading indicators per operation

## Performance Optimizations

### 1. useMemo for Context Value

```typescript
const contextValue = useMemo<ChatContextValue>(() => ({
  // All context properties
}), [
  // All dependencies
]);
```

**Benefit**: Prevents unnecessary re-renders when context provider re-renders

### 2. useCallback for Stable References

All functions in context use `useCallback` to maintain stable references:

```typescript
const selectGame = useCallback((gameId: string) => { ... }, []);
const toggleSidebar = useCallback(() => { ... }, []);
const startEditMessage = useCallback((messageId: string, content: string) => { ... }, []);
```

**Benefit**: Child components can rely on function reference stability for optimization

### 3. Derived State

```typescript
const currentGameState = selectedGameId ? chatStatesByGame.get(selectedGameId) : undefined;
const chats = currentGameState?.chats ?? [];
const activeChatId = currentGameState?.activeChatId ?? null;
const messages = currentGameState?.messages ?? [];
```

**Benefit**: Computed values don't require separate state management

## Implementation Status

### ✅ Phase 1 Complete
- Context structure and types
- State initialization
- UI actions (sidebar, input, edit state)
- Game/Agent selection
- Helper functions for game-specific state
- Comprehensive test suite (22/22 passing)

### ✅ Phase 2 Complete (Sidebar Components)
- API integration functions (data loading):
  - `loadCurrentUser()` - Fetch authentication
  - `loadGames()` - Fetch games list
  - `loadAgents(gameId)` - Fetch agents for game
  - `loadChats(gameId)` - Fetch chat history
  - `loadChatHistory(chatId)` - Fetch messages
- Sidebar components extracted:
  - `GameSelector.tsx` (67 lines) - Game dropdown with skeleton
  - `AgentSelector.tsx` (67 lines) - Agent dropdown with disabled state
  - `ChatHistoryItem.tsx` (75 lines) - Individual chat item
  - `ChatHistory.tsx` (62 lines) - Chat list with loading states
  - `ChatSidebar.tsx` (98 lines) - Composed sidebar
- TypeScript validation passing for all new components

### ✅ Phase 3 Complete (Content Components)
- ChatProvider enhancement:
  - Added `setEditContent` to `ChatContextValue` interface
  - Exposed for message editing functionality
- Content components extracted:
  - `Message.tsx` (58 lines) - Individual message display
  - `MessageActions.tsx` (140 lines) - Edit/delete/feedback buttons
  - `MessageEditForm.tsx` (95 lines) - Message editing form
  - `MessageInput.tsx` (88 lines) - Message input with send button
  - `MessageList.tsx` (105 lines) - Scrollable message list
  - `ChatContent.tsx` (140 lines) - Composed content area
- Total: 6 new components, 626 lines of code
- TypeScript validation passing for all new components

### 🚧 TODO (Phase 3-5)
- Chat operations implementation (still stubs):
  - `createChat()` - Create new chat session
  - `deleteChat(chatId)` - Delete chat
  - `selectChat(chatId)` - Load messages for chat

- Message operations implementation:
  - `sendMessage(content)` - Send new message
  - `setMessageFeedback(messageId, feedback)` - Set helpful/not-helpful
  - `editMessage(messageId, content)` - Update message
  - `deleteMessage(messageId)` - Delete message

- Streaming integration:
  - Connect `useChatStreaming` hook
  - Handle streaming state updates
  - Update messages on stream completion

## Testing Strategy

### Unit Tests (Current)
```typescript
describe('ChatProvider', () => {
  describe('Context Initialization', () => { ... });
  describe('Initial State', () => { ... });
  describe('UI Actions', () => { ... });
  describe('Game Selection', () => { ... });
  describe('Type Safety', () => { ... });
  describe('Component Integration', () => { ... });
});

describe('ChatProvider API Stubs', () => { ... });
```

### Integration Tests (Future)
- API calls with mocked endpoints
- Data flow through context
- Complex state updates (edit → save → refresh)
- Error handling and recovery
- Loading state transitions

### E2E Tests (Future)
- Full user flows with real chat sessions
- Multi-game context switching
- Message operations lifecycle
- Streaming message integration

## Migration Path

### Phase 2: Extract Sidebar Components (Next)
1. Create `GameSelector.tsx` using `useChatContext()`
2. Create `AgentSelector.tsx` using `useChatContext()`
3. Create `ChatHistory.tsx` and `ChatHistoryItem.tsx`
4. Compose `ChatSidebar.tsx` from sub-components
5. Add tests for each component

### Phase 3: Extract Content Components
1. Create `MessageInput.tsx` using `useChatContext()`
2. Create `MessageActions.tsx`
3. Create `MessageEditForm.tsx`
4. Create `Message.tsx`
5. Create `MessageList.tsx`
6. Compose `ChatContent.tsx`
7. Add tests for each component

### Phase 4: Integration
1. Wire ChatProvider with API implementations
2. Update `pages/chat.tsx` to use ChatProvider
3. Remove old state management from chat.tsx
4. Run full test suite
5. Performance testing and optimization

## Related Documentation

- **Design Document**: `claudedocs/chat-page-refactoring-design.md`
- **Action Plan**: `claudedocs/FRONTEND-IMPROVEMENTS-ACTION-PLAN.md`
- **Session Summary**: `claudedocs/session-summary-2025-10-24.md`

## Success Metrics

### Phase 1 (Current)
- ✅ ChatProvider created (387 lines)
- ✅ 22 tests passing (100%)
- ✅ TypeScript fully typed
- ✅ No ESLint warnings
- ✅ Documentation complete

### Overall Target (All Phases)
- Reduce chat.tsx from 1639 → ~200 lines
- Each component <200 lines
- Test coverage >90%
- Initial render <1s
- Smooth scrolling (60fps)

## Benefits Delivered (Phase 1)

✅ **Foundation Complete**:
- Centralized state management infrastructure
- Type-safe API for all chat operations
- Comprehensive test coverage
- Clear separation of concerns
- Ready for component extraction

✅ **Developer Experience**:
- Easy to test components in isolation
- Type-safe context API
- Clear function signatures
- Good IDE autocomplete

✅ **Maintainability**:
- Single source of truth for chat state
- Reusable context across components
- Well-documented API
- Comprehensive test suite

---

## Overall Project Status

### ✅ Phase 1: ChatProvider (Complete)
- **Time**: ~1 hour (estimated: 2 hours)
- **Deliverables**: ChatProvider infrastructure (387 lines), 22 tests passing
- **Summary**: `claudedocs/chat-provider-implementation.md` (this document)

### ✅ Phase 2: Sidebar Components (Complete)
- **Time**: ~1.5 hours (estimated: 4 hours)
- **Deliverables**: 5 components (369 lines) - GameSelector, AgentSelector, ChatHistory, ChatHistoryItem, ChatSidebar
- **Summary**: `claudedocs/chat-refactoring-phase2-summary.md`

### ✅ Phase 3: Content Components (Complete)
- **Time**: ~1.5 hours (estimated: 6-8 hours)
- **Deliverables**: 6 components (626 lines) - Message, MessageActions, MessageEditForm, MessageInput, MessageList, ChatContent
- **Summary**: `claudedocs/chat-refactoring-phase3-summary.md`

### ✅ Phase 4: Integration (Complete)
- **Time**: ~1.5 hours (estimated: 4 hours)
- **Deliverables**: 6 API operations implemented, Message component enhanced
- **Summary**: `claudedocs/chat-refactoring-phase4-summary.md`

### ✅ Phase 5: Final Integration (Complete)
- **Time**: ~0.5 hours (estimated: 4 hours)
- **Deliverables**: pages/chat.tsx refactored (1,640 → 113 lines, 93% reduction)
- **Summary**: `claudedocs/chat-refactoring-phase5-summary.md`

---

## Final Stats

**Total Progress**: 100% complete (5 of 5 phases)
**Total Components**: 13 components (1,717 lines)
**chat.tsx Reduction**: 1,640 → 113 lines (93% reduction)
**Time Saved**: ~10-12 hours compared to estimates
**TypeScript Coverage**: 100% (all passing)
**Architecture**: Clean, composable, testable

---

**Status**: All 5 Phases Complete ✅
**Next**: Optional Phase 6 - Comprehensive Testing & Polish
