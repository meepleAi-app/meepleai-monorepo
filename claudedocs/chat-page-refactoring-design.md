# Chat Page Refactoring - Design Document

**Created**: 2025-10-24
**Status**: Design Phase
**Effort Estimate**: 16 hours (2 weeks)
**Target**: Reduce from 1639 lines to ~600 lines across 8 components

## Current State Analysis

### Complexity Metrics
- **Lines of Code**: 1,639 lines
- **React Hooks**: 29 hooks (useState, useEffect, useCallback, useMemo, useRef)
- **State Variables**: 19+ state variables
- **Functions**: 20+ handler functions
- **Type Definitions**: 10+ inline types (can use centralized types)

### State Variables Breakdown

#### Authentication & User (1 variable)
- `authUser` - Current authenticated user

#### Game & Agent Selection (4 variables)
- `games` - Available games list
- `selectedGameId` - Currently selected game
- `agents` - Agents for selected game
- `selectedAgentId` - Currently selected agent

#### Chat Management (2 variables)
- `chatStatesByGame` - Map of game-specific chat states (complex!)
- Per-game state includes: `chats`, `activeChatId`, `messages`

#### UI State (12 variables)
- `inputValue` - Message input text
- `errorMessage` - Error display
- `sidebarCollapsed` - Sidebar visibility
- `showExportModal` - Export dialog state
- `isLoadingGames` - Games loading state
- `isLoadingAgents` - Agents loading state
- `isLoadingChats` - Chats loading state
- `isLoadingMessages` - Messages loading state
- `isSendingMessage` - Send message loading state
- `isCreatingChat` - New chat loading state
- `editingMessageId` - Message being edited
- `editContent` - Edit input content
- `deleteConfirmMessageId` - Delete confirmation state
- `isUpdatingMessage` - Update loading state

### Major Functions (20+)

#### Data Loading (5 functions)
- `loadCurrentUser()` - Fetch current user
- `loadGames()` - Fetch available games
- `loadAgents(gameId)` - Fetch agents for game
- `loadChats(gameId)` - Fetch chat history
- `loadChatHistory(chatId)` - Fetch chat messages

#### Chat Operations (3 functions)
- `createNewChat()` - Create new chat session
- `deleteChat(chatId)` - Delete chat
- `sendMessage(e)` - Send chat message

#### Message Operations (6 functions)
- `setFeedback(messageId, feedback)` - Set message feedback
- `startEditMessage(messageId, content)` - Begin editing
- `cancelEdit()` - Cancel edit
- `saveEdit(chatId, messageId)` - Save edited message
- `startDeleteMessage(messageId)` - Begin delete confirmation
- `confirmDelete(chatId, messageId)` - Confirm message deletion

#### Helper Functions (6 functions)
- `setChats(updater)` - Update chats for current game
- `setActiveChatId(chatId)` - Set active chat
- `setMessages(updater)` - Update messages for current game
- `formatSnippets(snippets)` - Format RAG snippets
- `getSnippetLabel(snippet)` - Get snippet display text
- `formatChatPreview(chat)` - Format chat preview text

## Target Architecture

### Component Hierarchy
```
<ChatPage> (~200 lines)
├─ <ChatProvider> (state management)
│   ├─ Authentication state
│   ├─ Game/Agent selection state
│   ├─ Chat management state
│   └─ UI state (loading, errors)
│
├─ <ChatSidebar> (~150 lines)
│   ├─ <GameSelector /> (~50 lines)
│   ├─ <AgentSelector /> (~50 lines)
│   └─ <ChatHistory /> (~80 lines)
│       └─ <ChatHistoryItem /> (~30 lines)
│
└─ <ChatContent> (~200 lines)
    ├─ <MessageList /> (~150 lines)
    │   ├─ <Message /> (~80 lines)
    │   ├─ <MessageActions /> (~40 lines)
    │   └─ <MessageEditForm /> (~50 lines)
    ├─ <StreamingResponse /> (existing)
    └─ <MessageInput /> (~80 lines)
        └─ <FollowUpQuestions /> (existing)
```

### File Structure
```
src/
├─ pages/
│   └─ chat.tsx (~200 lines - page component)
├─ components/
│   └─ chat/
│       ├─ ChatProvider.tsx (~150 lines)
│       ├─ ChatSidebar.tsx (~50 lines - composition)
│       ├─ GameSelector.tsx (~50 lines)
│       ├─ AgentSelector.tsx (~50 lines)
│       ├─ ChatHistory.tsx (~80 lines)
│       ├─ ChatHistoryItem.tsx (~30 lines)
│       ├─ ChatContent.tsx (~50 lines - composition)
│       ├─ MessageList.tsx (~150 lines)
│       ├─ Message.tsx (~80 lines)
│       ├─ MessageActions.tsx (~40 lines)
│       ├─ MessageEditForm.tsx (~50 lines)
│       └─ MessageInput.tsx (~80 lines)
└─ types/
    └─ index.ts (already centralized)
```

## State Management Strategy

### ChatProvider Context API

```typescript
interface ChatContextValue {
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
  loading: {
    games: boolean;
    agents: boolean;
    chats: boolean;
    messages: boolean;
    sending: boolean;
  };
  errorMessage: string;
  sidebarCollapsed: boolean;
  toggleSidebar: () => void;
}
```

### Benefits of Context API
1. **Centralized State**: All chat state in one place
2. **Type Safety**: Full TypeScript support
3. **Performance**: Use `useMemo` and `useCallback` to prevent unnecessary re-renders
4. **Testability**: Easy to mock context for testing
5. **Reusability**: Context can be reused across chat-related pages

## Component Specifications

### 1. ChatProvider (`ChatProvider.tsx`)

**Responsibility**: Centralized state management for entire chat feature

**State**:
- All current chat.tsx state variables
- Derived state (current chat, selected agent, etc.)

**Methods**:
- Data loading functions
- Chat operations (create, delete, select)
- Message operations (send, edit, delete, feedback)
- Game/Agent selection

**Optimizations**:
- `useMemo` for derived state
- `useCallback` for stable function references
- Debounced API calls where appropriate

### 2. ChatSidebar (`ChatSidebar.tsx`)

**Responsibility**: Sidebar layout and composition

**Props**:
```typescript
interface ChatSidebarProps {
  collapsed: boolean;
  onToggle: () => void;
}
```

**Children**:
- GameSelector
- AgentSelector
- ChatHistory

**Features**:
- Collapsible sidebar
- Responsive design
- Accessibility (keyboard navigation)

### 3. GameSelector (`GameSelector.tsx`)

**Responsibility**: Game selection dropdown

**Context Used**:
- `games`, `selectedGameId`, `selectGame`, `loading.games`

**Features**:
- Loading skeleton
- Empty state
- Error handling

### 4. AgentSelector (`AgentSelector.tsx`)

**Responsibility**: Agent selection dropdown

**Context Used**:
- `agents`, `selectedAgentId`, `selectAgent`, `loading.agents`

**Features**:
- Disabled when no game selected
- Loading skeleton
- Agent type badges

### 5. ChatHistory (`ChatHistory.tsx`)

**Responsibility**: List of chat sessions

**Context Used**:
- `chats`, `activeChatId`, `selectChat`, `deleteChat`, `loading.chats`

**Features**:
- Virtualization for large chat lists (react-window)
- Delete confirmation
- Chat preview with last message
- Loading skeleton

### 6. ChatHistoryItem (`ChatHistoryItem.tsx`)

**Responsibility**: Individual chat item in history

**Props**:
```typescript
interface ChatHistoryItemProps {
  chat: Chat;
  isActive: boolean;
  onSelect: () => void;
  onDelete: () => void;
}
```

**Features**:
- Highlight active chat
- Delete button with confirmation
- Timestamp formatting

### 7. ChatContent (`ChatContent.tsx`)

**Responsibility**: Main chat content area layout

**Props**:
```typescript
interface ChatContentProps {
  // None - uses context
}
```

**Children**:
- MessageList
- MessageInput

**Features**:
- Empty state when no chat selected
- Error display

### 8. MessageList (`MessageList.tsx`)

**Responsibility**: Scrollable list of messages

**Context Used**:
- `messages`, `loading.messages`

**Features**:
- Auto-scroll to bottom on new messages
- Loading skeleton
- Empty state
- Virtualization for large message lists (react-window)

### 9. Message (`Message.tsx`)

**Responsibility**: Individual message display

**Props**:
```typescript
interface MessageProps {
  message: Message;
  isEditing: boolean;
  onEdit: (content: string) => void;
  onDelete: () => void;
  onFeedback: (feedback: 'helpful' | 'not-helpful') => void;
}
```

**Features**:
- User vs Assistant styling
- Snippet display
- Follow-up questions
- Feedback buttons
- Edit/Delete actions

### 10. MessageActions (`MessageActions.tsx`)

**Responsibility**: Message action buttons (edit, delete, feedback)

**Props**:
```typescript
interface MessageActionsProps {
  message: Message;
  onEdit: () => void;
  onDelete: () => void;
  onFeedback: (feedback: 'helpful' | 'not-helpful') => void;
}
```

**Features**:
- Conditional visibility (only for user messages)
- Feedback states
- Accessibility

### 11. MessageEditForm (`MessageEditForm.tsx`)

**Responsibility**: Inline message editing form

**Props**:
```typescript
interface MessageEditFormProps {
  initialContent: string;
  onSave: (content: string) => void;
  onCancel: () => void;
  loading: boolean;
}
```

**Features**:
- Auto-focus input
- Save/Cancel buttons
- Loading state
- Keyboard shortcuts (Enter to save, Esc to cancel)

### 12. MessageInput (`MessageInput.tsx`)

**Responsibility**: Message composition input

**Context Used**:
- `sendMessage`, `loading.sending`, `selectedAgentId`

**Features**:
- Multi-line input (textarea)
- Send button with loading state
- Follow-up question integration
- Character count (optional)
- Disabled when sending or no agent selected

## Migration Strategy

### Phase 1: Setup & Infrastructure (2 hours)
1. Create `src/components/chat/` directory
2. Create centralized types (use existing `@/types`)
3. Create ChatProvider skeleton
4. Setup tests for ChatProvider

### Phase 2: Extract Sidebar Components (4 hours)
1. Extract GameSelector component
2. Extract AgentSelector component
3. Extract ChatHistory component
4. Extract ChatHistoryItem component
5. Compose ChatSidebar component
6. Add tests for each component

### Phase 3: Extract Content Components (6 hours)
1. Extract MessageInput component
2. Extract MessageActions component
3. Extract MessageEditForm component
4. Extract Message component
5. Extract MessageList component
6. Compose ChatContent component
7. Add tests for each component

### Phase 4: Integration & Testing (3 hours)
1. Wire ChatProvider to all components
2. Update chat.tsx to use new architecture
3. Run full test suite
4. Fix any integration issues
5. Performance testing and optimization

### Phase 5: Cleanup & Documentation (1 hour)
1. Remove old code from chat.tsx
2. Update documentation
3. Add component stories (optional)
4. Final code review

## Testing Strategy

### Unit Tests
- Each component isolated with mocked context
- Test all user interactions
- Test error states and edge cases

### Integration Tests
- Full ChatProvider with all components
- Test data flow through context
- Test complex interactions (edit, delete, feedback)

### E2E Tests
- Full user flows (select game → select agent → chat → edit → delete)
- Streaming message tests
- Multi-chat session management

## Risk Assessment

### High Risks
1. **Breaking existing functionality**
   - Mitigation: Comprehensive test coverage before refactoring
   - Mitigation: Feature flags for gradual rollout

2. **Performance degradation**
   - Mitigation: Use React.memo, useMemo, useCallback
   - Mitigation: Virtualization for large lists
   - Mitigation: Performance profiling before/after

3. **State synchronization bugs**
   - Mitigation: Centralized state management
   - Mitigation: Strict TypeScript types
   - Mitigation: Integration tests for state flows

### Medium Risks
1. **Increased bundle size**
   - Mitigation: Code splitting with dynamic imports
   - Mitigation: Tree shaking unused components

2. **Context re-render issues**
   - Mitigation: Split context if needed (auth, data, UI)
   - Mitigation: Use React DevTools Profiler

### Low Risks
1. **Developer onboarding complexity**
   - Mitigation: Comprehensive documentation
   - Mitigation: Clear component hierarchy
   - Mitigation: TypeScript type safety

## Performance Optimizations

### Virtualization
- Use `react-window` for ChatHistory (if >20 chats)
- Use `react-window` for MessageList (if >50 messages)
- Lazy load message content (images, snippets)

### Memoization
- Memoize expensive computations (formatSnippets, formatChatPreview)
- Memoize derived state (currentChat, selectedAgent)
- Memoize callback functions (useCallback)

### Code Splitting
- Lazy load ExportChatModal
- Lazy load MessageEditForm (only when editing)
- Dynamic import for heavy dependencies

## Accessibility

### Keyboard Navigation
- Tab through all interactive elements
- Enter/Space to activate buttons
- Escape to close modals/cancel edits
- Arrow keys for list navigation

### Screen Reader Support
- Proper ARIA labels
- Live regions for new messages
- Role attributes (list, listitem, etc.)
- Focus management (auto-focus on modals)

### Visual Accessibility
- High contrast colors
- Focus indicators
- Loading states announced
- Error messages visible and announced

## Success Metrics

### Code Quality
- ✅ Reduce chat.tsx from 1639 lines to ~200 lines
- ✅ Each component <200 lines
- ✅ Test coverage >90%
- ✅ No TypeScript errors
- ✅ No ESLint warnings

### Performance
- ✅ Initial render <1s
- ✅ Message send response <500ms
- ✅ Smooth scrolling (60fps)
- ✅ No memory leaks

### Developer Experience
- ✅ Clear component responsibilities
- ✅ Easy to add new features
- ✅ Simple to test components in isolation
- ✅ Good TypeScript autocomplete

## Next Steps

1. **Review & Approval**: Get stakeholder approval for design
2. **Create Tickets**: Break down into individual tasks
3. **Setup Branch**: Create feature branch `feat/chat-page-refactoring`
4. **Begin Phase 1**: Start with ChatProvider infrastructure

## Related Documentation

- **Frontend Improvements Action Plan**: `claudedocs/FRONTEND-IMPROVEMENTS-ACTION-PLAN.md`
- **Centralized Types**: `claudedocs/centralized-types-structure.md`
- **Current chat.tsx**: `apps/web/src/pages/chat.tsx` (1639 lines)
