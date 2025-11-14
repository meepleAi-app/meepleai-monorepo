# SPRINT-3: ChatThread DDD Integration

**Issue**: #858 - Chat UI with Thread Sidebar
**Backend Dependency**: #1126 - KnowledgeBase ChatThread API Endpoints
**Status**: ✅ Frontend Complete | ⏳ Awaiting Backend #1126
**Date**: 2025-11-14

## Overview

Integration of DDD-based ChatThread system from KnowledgeBase bounded context (Issue #924) into frontend chat UI.

## Changes Implemented

### 1. Type Definitions

**New Types** (`src/types/domain.ts`):
```typescript
export interface ChatThread {
  id: string;
  gameId: string | null;
  title: string | null;
  createdAt: string;
  lastMessageAt: string;
  messageCount: number;
  messages: ChatThreadMessage[];
}

export interface ChatThreadMessage {
  content: string;
  role: string;
  timestamp: string;
}
```

**API Contract Types** (`src/lib/api.ts`):
```typescript
export interface ChatThreadDto {
  id: string;
  gameId: string | null;
  title: string | null;
  createdAt: string;
  lastMessageAt: string;
  messageCount: number;
  messages: ChatThreadMessageDto[];
}

export interface CreateChatThreadRequest {
  gameId?: string | null;
  title?: string | null;
  initialMessage?: string | null;
}

export interface AddMessageRequest {
  content: string;
  role: string;
}
```

### 2. API Client Methods

**New `api.chatThreads` namespace**:
- `getByGame(gameId: string)`: Get all threads for a game
- `getById(threadId: string)`: Get single thread with messages
- `create(request: CreateChatThreadRequest)`: Create new thread
- `addMessage(threadId: string, request: AddMessageRequest)`: Add message to thread

### 3. ChatProvider Integration

**Migrated from**:
- `/api/v1/chats` (legacy ChatService)
- DTOs: `Chat`, `ChatMessage`

**Migrated to**:
- `/api/v1/knowledge-base/chat-threads` (DDD KnowledgeBase)
- DTOs: `ChatThread`, `ChatThreadMessage`

**Key Changes**:
- `loadChats()`: Uses `api.chatThreads.getByGame()`
- `loadMessages()`: Uses `api.chatThreads.getById()` and converts to UI format
- `createChat()`: Uses `api.chatThreads.create()`
- `sendMessage()`: Uses `api.chatThreads.addMessage()`

### 4. Backward Compatibility

- Maintained `ChatState` structure (serializable for localStorage)
- UI components continue to work with existing interfaces
- Messages converted from backend DTOs to UI `Message` format
- Temporary message IDs generated until backend provides them

## Backend Dependency: Issue #1126

### Required Endpoints

Backend team must implement in `KnowledgeBaseEndpoints.cs`:

1. **GET `/api/v1/knowledge-base/chat-threads?gameId={guid}`**
   - Handler: `GetChatThreadsByGameQuery`
   - Returns: `IReadOnlyList<ChatThreadDto>`

2. **GET `/api/v1/knowledge-base/chat-threads/{id}`**
   - Handler: `GetChatThreadByIdQuery`
   - Returns: `ChatThreadDto`

3. **POST `/api/v1/knowledge-base/chat-threads`**
   - Handler: `CreateChatThreadCommand`
   - Body: `CreateChatThreadRequest`
   - Returns: `ChatThreadDto`

4. **POST `/api/v1/knowledge-base/chat-threads/{id}/messages`**
   - Handler: `AddMessageCommand`
   - Body: `AddMessageRequest`
   - Returns: `ChatThreadDto`

### DDD Handlers (Already Exist from #924)

✅ All handlers complete:
- `GetChatThreadsByGameQueryHandler`
- `GetChatThreadByIdQueryHandler`
- `CreateChatThreadCommandHandler`
- `AddMessageCommandHandler`

**Backend Implementation**: Only routing layer needed (~2-3 hours)

## Testing Status

### Unit Tests
- ⚠️ Some existing upload tests failing (unrelated to ChatThread changes)
- ✅ ChatProvider compiles successfully with new types
- ⏳ ChatProvider-specific tests need update when backend ready

### Integration Tests
- ⏳ Requires backend #1126 completion
- Will test full flow: create thread → add message → load threads

### E2E Tests
- ⏳ Planned after backend integration
- Will validate: UI → API → DDD handlers → UI

## Migration Notes

### What Changed
- **State**: `Chat[]` → `ChatThread[]`
- **API Calls**: Legacy endpoints → DDD endpoints
- **DTOs**: Old structure → DDD structure

### What Stayed the Same
- UI Components (ChatSidebar, ChatHistory, ChatContent)
- localStorage persistence
- Message format for UI display
- Provider context interface

### Known Limitations
1. Message IDs are temporary (generated client-side)
2. AI response streaming not yet implemented
3. Message edit/delete uses old endpoints (separate issue)
4. Delete thread not yet implemented in DDD

## Future Enhancements

**After Backend #1126 Completes**:
1. Add proper message IDs from backend
2. Implement AI response streaming
3. Migrate message edit/delete to DDD
4. Add delete thread functionality
5. Add thread title editing
6. Implement thread search/filtering

**Integration with Other Sprint-3 Issues**:
- #857: Game-Specific Chat Context (filters by gameId)
- #859: PDF Citation Display (enhance message DTOs)
- #860: Chat Export (export entire thread)

## References

- **Frontend Issue**: #858
- **Backend Issue**: #1126
- **DDD Foundation**: #924 (CLOSED)
- **Architecture**: `docs/architecture/board-game-ai-architecture-overview.md`
- **API Spec**: `docs/api/board-game-ai-api-specification.md`
- **DDD Status**: `docs/refactoring/ddd-status-and-roadmap.md`

## Files Modified

```
apps/web/src/
├── lib/api.ts                          # Added chatThreads API methods
├── types/
│   ├── domain.ts                       # Added ChatThread types
│   └── index.ts                        # Exported new types
└── components/chat/
    └── ChatProvider.tsx                # Integrated DDD endpoints
```

## Commit History

- `76de5200`: feat(frontend): Integrate DDD ChatThread API (#858)

---

**Status**: ✅ **Frontend Ready** | ⏳ **Awaiting Backend #1126**
**Next Steps**: Backend team implements 4 endpoints → E2E testing → Merge to main
