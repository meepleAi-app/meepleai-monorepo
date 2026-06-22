# G2 Fast Resume Audit — Design Spec

**Status**: ✅ COMPLETED 2026-05-10 (issue #929, PR #933)
**Date**: 2026-05-10
**Author**: Brainstorming session post-G4 v3
**Issue**: [#929](https://github.com/meepleAi-app/meepleai-monorepo/issues/929)
**Spec parent**: [`2026-05-09-game-night-user-flow-design.md`](./2026-05-09-game-night-user-flow-design.md) §G2
**Status**: DRAFT — pending user review

---

## 1. Contesto

Implementa il **goal G2** del flusso "serata di gioco": quando l'utente riapre l'app dopo che il browser ha killato la tab o dopo un cold reload, vede subito cronologia chat + agente attivo + gioco di contesto. Backend già pronto (`ChatThread` aggregate + `getThreadsByGame` endpoint), modifiche solo frontend.

### 1.1 Scope-frame

**In scope:**
- Estensione `useGameChat` per fetchare il thread più recente on mount (per `gameId`)
- Persist `chatThreadId` in state per riusarlo nelle qaStream successive (continua thread, non ne crea nuovo)
- 2 nuovi componenti: `ChatHistoryBanner` (disclaimer text-only) + `ChatBubbleSkeleton` (loading state)
- Modifica `GameChatTabV2` per renderizzare skeleton + banner + scroll-to-bottom
- Audit trigger A (background→foreground) via vitest test

**Out of scope:**
- Backend extension per persistere citation/overallConfidence/isLowQuality in `ChatThreadMessage` (plan separato)
- Bottone "Nuova chat" per partire pulito ignorando thread esistenti
- Cross-device E2E (richiede 2 device + Playwright)
- localStorage cache hybrid (ottimizzazione opzionale)

### 1.2 Decisioni di design (validate via brainstorming)

| # | Domanda | Scelta |
|---|---|---|
| 1 | Trigger | **B + C** (tab killed + cold reload). A confermato via audit. D coperto. |
| 2 | Source of truth | **A** Backend lazy fetch on mount (no localStorage) |
| 3 | Quale thread | **A** Più recente per (user, gameId) — `lastMessageAt` desc, top 1 |
| 4 | Citation persistence | **A + D** Resume text-only + disclaimer banner |
| 5 | Loading UX | **B** Skeleton 3 bubble alternati user/agent |
| 6 | Scroll position | **A** Bottom istantaneo on data ready |

### 1.3 Vincoli SMART

| Goal | Vincolo | Misurabile |
|---|---|---|
| **G2.1** | Resume <3s (target spec parent: ≤3s percepito) | Manual smoke + perf check (fetch p95 <500ms + render <100ms) |
| **G2.2** | Audit conferma background→foreground non perde state | Vitest test unmount/remount con TanStack Query cache hit |
| **G2.3** | Banner disclaimer mostrato solo quando ci sono messaggi nuovi DOPO storici (no banner su solo storici) | Unit test condition |
| **G2.4** | qaStream successive a hydrate passano `chatId` (continuano thread esistente) | Integration test |
| **G2.5** | 100% dei thread esistenti per (user, gameId) sono fetchabili (test su mock con N=0, 1, 5 thread) | Hook unit test |

### 1.4 Audit trigger A risolto

Trigger A (background→foreground stessa sessione) **già funziona by design**:
- React preserva state in memoria finché la tab non viene scaricata dal browser
- Su screen lock + sblocco, la tab resta attiva → niente unmount React
- Audit = 1 vitest test (no codice nuovo): unmount + remount stesso `useGameChat` → cache TanStack Query 5min staleTime evita refetch immediato

---

## 2. Architecture

```
apps/web/src/
├── hooks/queries/useGameChat.ts          ← MODIFY
│   - aggiungi useEffect per fetch on mount
│   - aggiungi state isHydrating, chatThreadId, hasHistoricalMessages
│   - aggiungi mapping ChatThreadMessageDto → ChatMessage (text-only, isHistorical=true)
│   - passa chatId in qaStream calls
│   - salva chatThreadId da Complete event payload
├── hooks/queries/__tests__/useGameChat.test.tsx  ← MODIFY (6 nuovi test)
├── components/v2/game-chat/
│   ├── ChatBubbleSkeleton.tsx            ← NEW (pure: 3 bubble alternati)
│   ├── __tests__/ChatBubbleSkeleton.test.tsx  ← NEW (2 test)
│   ├── ChatHistoryBanner.tsx             ← NEW (pure: 1 riga disclaimer)
│   ├── __tests__/ChatHistoryBanner.test.tsx   ← NEW (2 test)
│   ├── GameChatTabV2.tsx                 ← MODIFY (skeleton + banner + scroll-bottom + ChatBubble isHistorical)
│   ├── __tests__/GameChatTabV2.test.tsx  ← MODIFY (3 nuovi test)
│   ├── ChatBubble.tsx                    ← MODIFY (aggiungi prop isHistorical?)
│   └── __tests__/ChatBubble.test.tsx     ← MODIFY (1 nuovo test isHistorical visual)
├── hooks/queries/useGameChat.ts (ChatMessage type)  ← MODIFY (aggiungi isHistorical?)
└── docs/superpowers/specs/2026-05-10-fast-resume-audit-g2-design.md  ← spec
```

### Rendering tree esteso

```
GameChatTabV2
├── GameChatHeader (invariato)
├── flex
│   ├── GameChatSidebar (desktop, invariato)
│   └── messagesArea
│       ├── if (chat.isHydrating) → <ChatBubbleSkeleton count={3} />
│       ├── if (chat.hasHistoricalMessages && hasNewMessages) → <ChatHistoryBanner />
│       ├── messages.map → <ChatBubble isHistorical={msg.isHistorical} ... />
│       ├── (typing indicator if isLoading)
│       └── <div ref={messagesEndRef} /> ← sentinel scroll
├── SuggestedPrompts (invariato)
└── ChatInputBar (invariato)
```

---

## 3. Component Contracts

### 3.1 `useGameChat` extension

**Result interface esteso**:
```ts
export interface UseGameChatResult {
  readonly messages: readonly ChatMessage[];
  readonly isLoading: boolean;            // existing: submitting state
  readonly isHydrating: boolean;          // NEW: initial thread fetch
  readonly isError: boolean;
  readonly currentAgent: AgentKind;
  readonly chatThreadId: string | null;   // NEW: tracked dopo hydrate o prima qaStream
  readonly hasHistoricalMessages: boolean; // NEW: derived flag
  readonly ask: (question: string) => Promise<void>;
  readonly switchAgent: (next: AgentKind) => void;
}
```

**ChatMessage type esteso**:
```ts
export interface ChatMessage {
  readonly id: string;
  readonly role: 'user' | 'agent';
  readonly content: string;
  readonly citations?: ReadonlyArray<Citation>;
  readonly overallConfidence?: number;
  readonly isLowQuality?: boolean;
  readonly outOfContext?: boolean;
  readonly createdAt: string;
  readonly isHistorical?: boolean;        // NEW: true se viene da hydrate
}
```

### 3.2 `ChatBubbleSkeleton` (pure)

```ts
interface ChatBubbleSkeletonProps {
  readonly count?: number;  // default 3
  readonly className?: string;
}
```

Renderizza N bubble alternati user (right, ~70% width) / agent (left, ~80% width) con `Skeleton` primitive. Indici pari = user, dispari = agent.

### 3.3 `ChatHistoryBanner` (pure)

```ts
interface ChatHistoryBannerProps {
  readonly className?: string;
}
```

Riga sottile sopra la lista messaggi:
> ℹ️ I messaggi precedenti a questa sessione sono in sola lettura. Citazioni e indicatori di confidenza non sono ricostruibili.

Tailwind: `bg-muted/50 border-l-4 border-l-[hsl(var(--c-info)/0.5)] px-3 py-2 text-xs text-muted-foreground`. Icona `<Info>` da lucide-react.

### 3.4 `ChatBubble` extension

Aggiungi prop opzionale:
```ts
interface ChatBubbleProps {
  // ... esistenti
  readonly isHistorical?: boolean;
}
```

Quando `isHistorical=true`, applica `opacity-90` (sottile differenziazione visiva). NO badge per ogni bubble (banner singolo gestisce comunicazione).

### 3.5 `GameChatTabV2` extension

Aggiunge:
- Render skeleton durante `chat.isHydrating`
- `messagesEndRef` + `useEffect` scroll-to-bottom on `chat.messages.length` change e on `!isHydrating`
- Render `<ChatHistoryBanner />` quando `chat.hasHistoricalMessages && messages.some(m => !m.isHistorical)`
- Pass `isHistorical={msg.isHistorical}` a `<ChatBubble>`

---

## 4. Logic / Decisions

### 4.1 Hydrate logic on mount

```ts
useEffect(() => {
  let cancelled = false;
  setIsHydrating(true);

  api.chat.getThreadsByGame(gameId)
    .then(threads => {
      if (cancelled) return;
      const latest = [...threads]
        .filter(t => t.lastMessageAt !== null)
        .sort((a, b) => {
          if (a.lastMessageAt === null) return 1;
          if (b.lastMessageAt === null) return -1;
          return b.lastMessageAt > a.lastMessageAt ? 1 : -1;
        })[0];

      if (latest && latest.messages.length > 0) {
        setChatThreadId(latest.id);
        setMessages(latest.messages.map(toChatMessage));
        setHasHistoricalMessages(true);
      }
    })
    .catch(() => {
      // Silent fail — utente vede chat vuota, può comunque chiedere
      // (no error state — backend ChatThread non bloccante per ask)
    })
    .finally(() => {
      if (!cancelled) setIsHydrating(false);
    });

  return () => { cancelled = true; };
}, [gameId]);
```

> Nota: catch silenzioso. Se il fetch thread fallisce, l'utente vede chat vuota MA può comunque interagire (qaStream non dipende da hydrate).

### 4.2 Mapping `ChatThreadMessageDto` → `ChatMessage`

```ts
function toChatMessage(dto: ChatThreadMessageDto, idx: number): ChatMessage {
  return {
    id: dto.backendMessageId ?? `historical-${idx}-${dto.timestamp}`,
    role: dto.role === 'user' ? 'user' : 'agent',
    content: dto.content,
    createdAt: dto.timestamp,
    isHistorical: true,
    // citations, overallConfidence, isLowQuality, outOfContext: undefined (NOT persisted backend)
  };
}
```

### 4.3 chatThreadId pass-through

Modifica `ask` per passare `chatId`:

```ts
const stream = qaStream({
  gameId,
  query: question,
  chatId: chatThreadId ?? undefined,  // NEW: continua thread esistente se hydrato
});

// Nel Complete event handler:
if (payload.chatThreadId && !chatThreadId) {
  setChatThreadId(payload.chatThreadId);  // primo ask: salva il nuovo thread id
}
```

> Backend behavior: se `chatId` è passato → append al thread esistente. Se assente → crea nuovo thread, ritorna ID nel Complete payload.

### 4.4 Scroll-to-bottom logic

```ts
const messagesEndRef = useRef<HTMLDivElement>(null);

useEffect(() => {
  if (chat.isHydrating) return;
  messagesEndRef.current?.scrollIntoView({ behavior: 'instant', block: 'end' });
}, [chat.isHydrating, chat.messages.length]);
```

`<div ref={messagesEndRef} />` come ultimo child del `<div role="log">`.

### 4.5 Banner conditional render

```tsx
const hasNewMessages = chat.messages.some(m => !m.isHistorical);
const showHistoryBanner = chat.hasHistoricalMessages && hasNewMessages;

{showHistoryBanner && <ChatHistoryBanner />}
{chat.messages.map(...)}
```

Banner appare solo quando l'utente ha già aggiunto almeno 1 messaggio nuovo. Se solo storici, niente banner (non ha senso confondere l'utente che non ha ancora interagito).

### 4.6 Audit trigger A

Test vitest:
```ts
it('preserves messages across remount (background→foreground audit)', async () => {
  vi.mocked(api.chat.getThreadsByGame).mockResolvedValue([sampleThread]);
  const { result, unmount, rerender } = renderHook(() => useGameChat('wingspan'));
  await waitFor(() => expect(result.current.isHydrating).toBe(false));
  expect(result.current.messages.length).toBeGreaterThan(0);

  unmount();
  // Simulate React remount with same QueryClient cache
  const { result: result2 } = renderHook(() => useGameChat('wingspan'), { wrapper });
  // Cache hit: data appears immediately (no second fetch within staleTime)
  await waitFor(() => expect(result2.current.messages.length).toBeGreaterThan(0));
});
```

---

## 5. Testing strategy

### 5.1 Unit `useGameChat` (6 nuovi test)

- `hydrates messages from latest thread on mount`
- `selects most recent thread when multiple exist (lastMessageAt desc)`
- `starts empty when no threads exist (no error)`
- `silent fail when getThreadsByGame rejects`
- `passes chatId in subsequent qaStream calls after hydrate`
- `preserves messages across remount (audit trigger A)`

### 5.2 Unit nuovi componenti

- `ChatBubbleSkeleton` (2 test): render default count=3, custom count
- `ChatHistoryBanner` (2 test): render testo + className pass

### 5.3 Unit `ChatBubble` (1 nuovo)

- `applies isHistorical visual variant when prop true`

### 5.4 Integration `GameChatTabV2` (3 nuovi)

- `renders skeleton bubbles while hydrating`
- `shows ChatHistoryBanner when historical + new messages exist`
- `does NOT show banner when only historical (no new)`
- `auto-scrolls to bottom after hydration` (mock scrollIntoView)

### 5.5 Acceptance

- [ ] G2.1 Resume <3s: misurabile via DevTools Network (single fetch <500ms typical)
- [ ] G2.2 Audit A: 1 test passing (cache hit)
- [ ] G2.3 Banner conditional: 2 test (with/without new messages)
- [ ] G2.4 chatThreadId pass-through: 1 test integration
- [ ] G2.5 N=0/1/5 thread: 3 test scenari hydrate

---

## 6. Open questions residue (delegate al plan)

- **OQ1**: il `qaStream` request deve includere `chatId` come field optional o serve nuova firma? Verifica `QaStreamRequest` shape esistente in `chatClient.ts:351` — già ha `chatId?: string`, OK.
- **OQ2**: behavior se `getThreadsByGame` ritorna thread con `messages: []` ma `lastMessageAt != null`? Edge case backend — assumiamo non si verifica, ma il `messages.length > 0` check è defensivo.
- **OQ3**: `react-query` cache key `['game-documents', gameId]` (G4) vs `['chat-threads', gameId]` (G2) — namespace separati OK, no collision.
- **OQ4**: scroll-to-bottom su `messages.length` change — durante typing l'array cresce di 1 (user msg) poi di 1 (agent msg). Doppio scroll OK o serve debounce? Assumiamo OK per ora.

---

## 7. Riferimenti

- Spec parent: [`2026-05-09-game-night-user-flow-design.md`](./2026-05-09-game-night-user-flow-design.md) §G2
- Backend `ChatThread` aggregate: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/ChatThread.cs`
- Endpoint: `GET /api/v1/chat-threads?gameId=X` (KnowledgeBaseEndpoints.cs:92)
- Client wrapper: `apps/web/src/lib/api/clients/chatClient.ts:135` (`getThreadsByGame`)
- Schema: `ChatThreadDto`, `ChatThreadMessageDto` in `apps/web/src/lib/api/schemas/chat.schemas.ts`
- Auth backend: `Session.DefaultLifetime = TimeSpan.FromDays(30)` — auth NON è il problema in serata
- `useGameChat` corrente (PR #918 merged): `apps/web/src/hooks/queries/useGameChat.ts`
- Issue: [#929](https://github.com/meepleAi-app/meepleai-monorepo/issues/929)
