# Chat RAG Flow Bugfixes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 4 bugs (#414, #415, #416, #417) discovered during the "add shared game → chat with RAG agent" user story, so users can search the catalog, add a game, and get AI responses with citations.

**Architecture:** Frontend-backend coordination fixes. BUG-1 (#414) is a backend ID resolution fix. BUG-2 (#415) requires the frontend to call the QA stream endpoint for system agents. BUG-3 (#416) and BUG-4 (#417) are simple frontend endpoint corrections.

**Tech Stack:** .NET 9 (C# backend), Next.js (TypeScript frontend), PostgreSQL, SSE streaming

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandler.cs` | Modify | Resolve `SharedGameId` → `games.Id` when GameId FK fails |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandlerTests.cs` | Modify | Test SharedGameId resolution |
| `apps/web/src/components/chat-unified/ChatThreadView.tsx` | Modify | Add QA stream path for system agents |
| `apps/web/src/lib/api/clients/agentsClient.ts` | Modify | Fix `getUserAgentsForGame()` URL |
| `apps/web/src/hooks/queries/useAgentSlots.ts` | Modify | Graceful 404 handling |

## Dependency Order

```
Task 1 (#416) ─┐
Task 2 (#417) ─┤── independent, can run in parallel
Task 3 (#414) ─┤
Task 4 (#415) ─┘ (depends on Task 3 conceptually — needs valid thread creation first)
```

---

### Task 1: Fix agents endpoint mismatch (#416)

**Issue:** Frontend calls `GET /api/v1/agents?gameId=...&userOwned=true&activeOnly=true` (404). Backend route is `GET /api/v1/games/{id}/agents`.

**Files:**
- Modify: `apps/web/src/lib/api/clients/agentsClient.ts:97-109`
- Test: `apps/web/__tests__/lib/api/clients/agentsClient.test.ts` (if exists, otherwise manual verification)

- [ ] **Step 1: Read the current `getUserAgentsForGame` method**

Open `apps/web/src/lib/api/clients/agentsClient.ts` and locate the `getUserAgentsForGame` method (~lines 97-109). It currently builds:
```typescript
const url = `/api/v1/agents?gameId=${gameId}&userOwned=true&activeOnly=true`;
```

- [ ] **Step 2: Fix the URL to match the backend route**

Change the method to call the correct backend endpoint:

```typescript
async getUserAgentsForGame(gameId: string): Promise<AgentDto[]> {
  const response = await this.client.get<AgentDto[]>(
    `/api/v1/games/${gameId}/agents`
  );
  return response ?? [];
}
```

The backend endpoint `GET /api/v1/games/{id}/agents` (defined in `apps/api/src/Api/Routing/GameEndpoints.cs:91`) returns all active system agents plus user custom agents for the game. No query params needed — the backend already filters by active only.

- [ ] **Step 3: Verify in browser**

Navigate to `https://meepleai.app/chat/new?gameId=d1d45415-e201-4984-8bd1-172c8fc3829a`.
Expected: No 404 on agents call, no "Errore nel caricamento degli agenti" alert.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/api/clients/agentsClient.ts
git commit -m "fix(frontend): use correct /games/{id}/agents endpoint (#416)"
```

---

### Task 2: Fix agent-slots 404 (#417)

**Issue:** Frontend calls `GET /api/v1/user/agent-slots` which doesn't exist. No visible UI impact, but pollutes console.

**Files:**
- Modify: `apps/web/src/hooks/queries/useAgentSlots.ts:30-37`

- [ ] **Step 1: Read the current hook**

Open `apps/web/src/hooks/queries/useAgentSlots.ts`. The `useAgentSlots` hook (~lines 30-37) calls `api.agents.getSlots()` which hits the nonexistent endpoint.

- [ ] **Step 2: Add retry:false and suppress 404**

Update the React Query config to not retry on 404 and handle the error gracefully:

```typescript
export function useAgentSlots(enabled = true) {
  return useQuery({
    queryKey: ['agent-slots', 'user'],
    queryFn: async () => {
      try {
        return await api.agents.getSlots();
      } catch {
        // Endpoint not yet implemented — return safe defaults
        return { total: 5, used: 0, available: 5, slots: [] };
      }
    },
    enabled,
    staleTime: 30_000,
    retry: false,
  });
}
```

- [ ] **Step 3: Verify in browser**

Navigate to the library page. Expected: No 404 errors for `/user/agent-slots` in console.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/hooks/queries/useAgentSlots.ts
git commit -m "fix(frontend): graceful fallback for missing /user/agent-slots endpoint (#417)"
```

---

### Task 3: Fix shared_game_id passed to chat thread creation (#414)

**Issue:** When adding a game from the shared catalog, the frontend uses `shared_game_id` (from `user_library_entries.shared_game_id` / URL param) as `gameId` for chat thread creation. The `ChatThreads.GameId` FK references the `games` table. `shared_game_id` ≠ `games.Id`.

**Data model:**
- `games.Id = d1d45415-...` (the FK target)
- `games.SharedGameId = f26292eb-...` (the shared catalog ID)
- `user_library_entries.shared_game_id = f26292eb-...` (what the frontend has)
- Library page URL: `/library/f26292eb-...` (shared_game_id)

**Strategy:** Backend resolution — the `CreateChatThreadCommandHandler` resolves `SharedGameId` → `games.Id` when the direct lookup fails.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandler.cs:15-68`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandlerTests.cs`

- [ ] **Step 1: Read the current handler**

Open `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandler.cs`. The handler (~line 32) does:
```csharp
var effectiveGameId = command.PrivateGameId ?? command.GameId;
```
Then creates the ChatThread entity with this ID, which fails the FK if `effectiveGameId` is a shared_game_id.

- [ ] **Step 2: Write the failing test**

In the test file, add a test that passes a `SharedGameId` as `GameId` and expects successful thread creation:

```csharp
[Fact]
[Trait("Category", "Unit")]
public async Task Handle_WithSharedGameId_ResolvesToGamesId()
{
    // Arrange
    var sharedGameId = Guid.NewGuid();
    var gamesId = Guid.NewGuid();
    
    // The games table has a row where SharedGameId = sharedGameId
    var game = new GameEntity { Id = gamesId, Name = "Puerto Rico", SharedGameId = sharedGameId };
    _dbContext.Games.Add(game);
    await _dbContext.SaveChangesAsync();
    
    var command = new CreateChatThreadCommand(
        UserId: _testUserId,
        GameId: sharedGameId,  // Pass shared game ID (not games.Id)
        Title: "Test"
    );

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    var thread = await _dbContext.ChatThreads.FindAsync(result.Id);
    Assert.NotNull(thread);
    Assert.Equal(gamesId, thread!.GameId); // Should resolve to games.Id
}
```

- [ ] **Step 3: Run test to verify it fails**

```bash
cd apps/api/src/Api && dotnet test --filter "Handle_WithSharedGameId_ResolvesToGamesId"
```
Expected: FAIL — FK violation because `sharedGameId` doesn't exist in `games.Id`.

- [ ] **Step 4: Implement SharedGameId resolution in handler**

Modify `CreateChatThreadCommandHandler.Handle()` to resolve the GameId:

```csharp
public async Task<ChatThreadDto> Handle(CreateChatThreadCommand command, CancellationToken cancellationToken)
{
    var effectiveGameId = command.PrivateGameId ?? command.GameId;
    
    // Resolve SharedGameId → games.Id if the ID isn't directly in the games table
    if (effectiveGameId.HasValue)
    {
        var gameExists = await _dbContext.Games
            .AnyAsync(g => g.Id == effectiveGameId.Value, cancellationToken)
            .ConfigureAwait(false);
        
        if (!gameExists)
        {
            // Try resolving as SharedGameId
            var resolvedGame = await _dbContext.Games
                .Where(g => g.SharedGameId == effectiveGameId.Value)
                .Select(g => g.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            
            if (resolvedGame != default)
            {
                effectiveGameId = resolvedGame;
            }
            // If still not found, let the FK constraint fail naturally with a clear error
        }
    }
    
    // ... rest of handler using effectiveGameId
```

- [ ] **Step 5: Run test to verify it passes**

```bash
cd apps/api/src/Api && dotnet test --filter "Handle_WithSharedGameId_ResolvesToGamesId"
```
Expected: PASS

- [ ] **Step 6: Run existing chat thread tests to check for regressions**

```bash
cd apps/api/src/Api && dotnet test --filter "CreateChatThread"
```
Expected: All PASS

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandler.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandlerTests.cs
git commit -m "fix(backend): resolve SharedGameId to games.Id in chat thread creation (#414)"
```

---

### Task 4: Fix missing AI response streaming for system agents (#415)

**Issue:** When a system agent (Auto, Tutor, Arbitro, etc.) is selected, `thread.agentId` is null. `ChatThreadView` checks `thread.agentId` to decide SSE vs REST — since it's null, it takes the REST path which only saves the message without triggering an AI response.

**Root cause** (in `ChatThreadView.tsx` ~lines 254-282):
```typescript
// Current logic:
if (thread.agentId) {
  sendViaSSE(thread.agentId, content, thread.id);  // SSE → POST /agents/{id}/chat
} else {
  api.chat.addMessage(thread.id, { content, role: 'user' }); // REST only, no AI
}
```

**Fix:** When no custom `agentId` but a `gameId` exists, call `POST /api/v1/agents/qa/stream` for RAG-powered responses. This is the endpoint that does hybrid retrieval + LLM streaming with citations.

**Files:**
- Modify: `apps/web/src/components/chat-unified/ChatThreadView.tsx:254-282`
- Modify: `apps/web/src/lib/api/clients/chatClient.ts` (add qaStream helper)

- [ ] **Step 1: Add a `qaStream` method to the chat API client**

Open `apps/web/src/lib/api/clients/chatClient.ts` and add a method that calls the QA stream endpoint:

```typescript
/**
 * Stream a QA response via SSE for system agents (no custom agentId).
 * Endpoint: POST /api/v1/agents/qa/stream
 */
async *qaStream(request: {
  gameId: string;
  query: string;
  chatId?: string;
}): AsyncGenerator<{ type: number; data: unknown; timestamp?: string }> {
  const baseUrl = process.env.NEXT_PUBLIC_API_BASE || '';
  const response = await fetch(`${baseUrl}/api/v1/agents/qa/stream`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({
      gameId: request.gameId,
      query: request.query,
      chatId: request.chatId,
    }),
  });

  if (!response.ok) {
    throw new Error(`QA stream failed: ${response.status}`);
  }

  const reader = response.body?.getReader();
  if (!reader) throw new Error('No response body');

  const decoder = new TextDecoder();
  let buffer = '';

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split('\n');
      buffer = lines.pop() ?? '';

      for (const line of lines) {
        if (line.startsWith('data: ')) {
          const jsonStr = line.slice(6).trim();
          if (jsonStr === '[DONE]') return;
          try {
            yield JSON.parse(jsonStr);
          } catch {
            // Skip malformed JSON
          }
        }
      }
    }
  } finally {
    reader.releaseLock();
  }
}
```

- [ ] **Step 2: Modify ChatThreadView to use QA stream for system agents**

Open `apps/web/src/components/chat-unified/ChatThreadView.tsx`. Locate the `handleSendMessage` function (~lines 254-282).

Replace the `else` branch (REST fallback) with QA stream logic when a gameId is available:

```typescript
const handleSendMessage = async (content: string) => {
  if (!threadId) return;

  // Add user message to UI immediately
  const userMessage = { id: crypto.randomUUID(), content, role: 'user' as const, createdAt: new Date().toISOString() };
  setMessages(prev => [...prev, userMessage]);

  if (thread?.agentId) {
    // Custom agent: use agent-specific SSE endpoint
    sendViaSSE(thread.agentId, content, thread.id);
  } else if (thread?.gameId) {
    // System agent with game context: use QA stream for RAG retrieval
    try {
      // Save user message first
      await api.chat.addMessage(threadId, { content, role: 'user' });
      
      // Stream AI response via QA endpoint
      let fullAnswer = '';
      const assistantMsgId = crypto.randomUUID();
      setMessages(prev => [...prev, { id: assistantMsgId, content: '', role: 'assistant', createdAt: new Date().toISOString() }]);

      for await (const event of api.chat.qaStream({
        gameId: thread.gameId,
        query: content,
        chatId: threadId,
      })) {
        // Type 7 = Token (streaming text)
        if (event.type === 7 && typeof event.data === 'string') {
          fullAnswer += event.data;
          setMessages(prev =>
            prev.map(m => m.id === assistantMsgId ? { ...m, content: fullAnswer } : m)
          );
        }
        // Type 4 = Complete
        if (event.type === 4) {
          // Final response with metadata (citations, follow-ups)
          const complete = event.data as { answer?: string; citations?: unknown[]; followUpQuestions?: string[] };
          if (complete.answer) {
            setMessages(prev =>
              prev.map(m => m.id === assistantMsgId ? { ...m, content: complete.answer!, citations: complete.citations } : m)
            );
          }
        }
        // Type 5 = Error
        if (event.type === 5) {
          const errorData = event.data as { message?: string };
          setMessages(prev =>
            prev.map(m => m.id === assistantMsgId ? { ...m, content: `Errore: ${errorData.message ?? 'Risposta non disponibile'}` } : m)
          );
        }
      }
    } catch (error) {
      console.error('[Chat] QA stream error:', error);
      setMessages(prev => [...prev, {
        id: crypto.randomUUID(),
        content: 'Errore nella generazione della risposta. Riprova.',
        role: 'assistant',
        createdAt: new Date().toISOString(),
      }]);
    }
  } else {
    // No game context: just save message (no AI response possible)
    await api.chat.addMessage(threadId, { content, role: 'user' });
  }
};
```

**Important:** The exact property names (`thread.gameId`, `setMessages`, `sendViaSSE`) must match the existing code. Read the file first and adapt the variable names.

- [ ] **Step 3: Verify in browser**

1. Navigate to `https://meepleai.app/chat/new?gameId=d1d45415-e201-4984-8bd1-172c8fc3829a`
2. Select "Auto" agent, click "Inizia Chat"
3. Type "spiegami il setup del gioco" and send
4. Expected: SSE streaming response with text appearing progressively, citations visible

- [ ] **Step 4: Check console for errors**

Expected: No new errors. Network tab should show `POST /api/v1/agents/qa/stream` with `200` and `text/event-stream` content type.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/clients/chatClient.ts
git add apps/web/src/components/chat-unified/ChatThreadView.tsx
git commit -m "fix(frontend): call /agents/qa/stream for system agent chat responses (#415)"
```

---

### Task 5: Seed fix — badsworm user silent failure

**Issue:** (bonus, discovered during testing) The `SeedBadswormUserCommand` fails silently — no log output at all. The `SafeExecute` in `CoreSeedLayer` catches exceptions and logs `[Core] Seeder 'badsworm user' failed`, but this log doesn't appear. Likely a log-level filtering issue on staging.

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Seeders/Core/CoreSeedLayer.cs:71-81`

- [ ] **Step 1: Change SafeExecute log level from Warning to Error**

In `CoreSeedLayer.cs`, the `SafeExecute` method (lines 71-81) logs at `LogWarning`. If the staging log level filters warnings, failures are invisible. Change to `LogError`:

```csharp
private static async Task SafeExecute(string name, Func<Task> action, ILogger logger)
{
    try
    {
        await action().ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[Core] Seeder '{Name}' failed — continuing", name);
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/Core/CoreSeedLayer.cs
git commit -m "fix(seed): log seeder failures at Error level to ensure visibility (#414)"
```

---

### Task 6: End-to-end verification

- [ ] **Step 1: Deploy to staging**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69
cd /opt/meepleai/repo && git pull origin main-dev
cd infra && docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml up -d --build api
# Wait for frontend rebuild if needed
```

- [ ] **Step 2: Re-run the full user story**

1. Login as badsworm@alice.it
2. Go to Library — Puerto Rico should already be there
3. Click "Chat con Agente"
4. Select "Auto" agent → "Inizia Chat"
5. Send "spiegami il setup del gioco"
6. Verify: AI response streams with citations and page references
7. Check console: no 404s, no 500s

- [ ] **Step 3: Close issues on GitHub**

```bash
gh issue close 414 --repo meepleAi-app/meepleai-monorepo --reason completed
gh issue close 415 --repo meepleAi-app/meepleai-monorepo --reason completed
gh issue close 416 --repo meepleAi-app/meepleai-monorepo --reason completed
gh issue close 417 --repo meepleAi-app/meepleai-monorepo --reason completed
```
