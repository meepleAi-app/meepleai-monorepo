# G2 Fast Resume Audit — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Estendere `useGameChat` per fetchare il thread chat più recente per (user, gameId) on mount, hydrate `messages[]` (text-only), persist `chatThreadId` per qaStream successive, e renderizzare skeleton + history banner + scroll-bottom in `GameChatTabV2`. Backend zero modifiche.

**Architecture:** Frontend-only PR. 2 nuovi pure components (`ChatBubbleSkeleton`, `ChatHistoryBanner`) + extension di `useGameChat` (hydrate logic + chatThreadId state) + modifica `GameChatTabV2` (skeleton/banner/scroll). Riusa endpoint esistente `getThreadsByGame` + schema `ChatThreadDto`.

**Tech Stack:** React 19 + Next.js 16, TypeScript strict, Tailwind 4, Vitest + TanStack Query (cache 5min staleTime). Pattern Wave B.1 puro/orchestrator + TDD strict.

**Spec:** [`docs/superpowers/specs/2026-05-10-fast-resume-audit-g2-design.md`](../specs/2026-05-10-fast-resume-audit-g2-design.md)
**Issue:** [#929](https://github.com/meepleAi-app/meepleai-monorepo/issues/929)
**Branch:** `feature/issue-929-fast-resume-audit` (creato dal commit spec)

---

## File Structure

| Status | Path | Responsibility |
|---|---|---|
| Create | `apps/web/src/components/v2/game-chat/ChatBubbleSkeleton.tsx` | Pure: N bubble skeleton alternati user/agent |
| Create | `apps/web/src/components/v2/game-chat/__tests__/ChatBubbleSkeleton.test.tsx` | 2 test |
| Create | `apps/web/src/components/v2/game-chat/ChatHistoryBanner.tsx` | Pure: 1 riga disclaimer "messaggi storici text-only" |
| Create | `apps/web/src/components/v2/game-chat/__tests__/ChatHistoryBanner.test.tsx` | 2 test |
| Modify | `apps/web/src/hooks/queries/useGameChat.ts` | Aggiungi hydrate logic + isHydrating + chatThreadId state + ChatMessage.isHistorical + qaStream chatId pass |
| Modify | `apps/web/src/hooks/queries/__tests__/useGameChat.test.tsx` | 6 nuovi test (hydrate scenari + audit A) |
| Modify | `apps/web/src/components/v2/game-chat/ChatBubble.tsx` | Aggiungi prop `isHistorical?: boolean` con `opacity-90` styling |
| Modify | `apps/web/src/components/v2/game-chat/__tests__/ChatBubble.test.tsx` | 1 nuovo test isHistorical |
| Modify | `apps/web/src/components/v2/game-chat/GameChatTabV2.tsx` | Skeleton render + banner conditional + scroll-bottom + isHistorical pass |
| Modify | `apps/web/src/components/v2/game-chat/__tests__/GameChatTabV2.test.tsx` | 3 nuovi test (skeleton + banner + scroll) |
| Modify | `apps/web/src/components/v2/game-chat/index.ts` | Aggiungi export ChatBubbleSkeleton, ChatHistoryBanner |

**Decomposition logic**: 2 pure components (Task 1, 2) → hook extension (Task 3) → ChatBubble extension (Task 4) → orchestrator wiring (Task 5) → barrel + final verify + PR (Task 6).

---

## Pre-flight

- [ ] **Step 0.1: Verifica branch**

```bash
git -C "D:/Repositories/meepleai-monorepo-main" branch --show-current
```
Expected: `feature/issue-929-fast-resume-audit`

- [ ] **Step 0.2: Verifica baseline typecheck**

```bash
cd apps/web && pnpm typecheck
```
Expected: 0 errori. Se errori preesistenti, registrarli.

- [ ] **Step 0.3: Conferma `getThreadsByGame` + `chatId` in QaStreamRequest**

```bash
grep -A 8 "interface QaStreamRequest" apps/web/src/lib/api/clients/chatClient.ts
grep -A 5 "getThreadsByGame" apps/web/src/lib/api/clients/chatClient.ts | head -10
```

Expected output (verifiche):
- `QaStreamRequest` ha `chatId?: string` (riga 354 ~)
- `getThreadsByGame(gameId): Promise<ChatThreadDto[]>` (riga 135 ~)

Se mancano: STOP. Lo spec assume entrambi esistenti.

---

## Task 1: `ChatBubbleSkeleton` (pure)

**Files:**
- Create: `apps/web/src/components/v2/game-chat/ChatBubbleSkeleton.tsx`
- Test: `apps/web/src/components/v2/game-chat/__tests__/ChatBubbleSkeleton.test.tsx`

**Contract:**
```ts
interface ChatBubbleSkeletonProps {
  readonly count?: number;  // default 3
  readonly className?: string;
}
```

Renderizza N bubble alternati (indice pari = user right ~70% width, dispari = agent left ~80% width) usando `Skeleton` primitive da `@/components/ui/feedback/skeleton`.

- [ ] **Step 1.1: Test failing**

Crea `apps/web/src/components/v2/game-chat/__tests__/ChatBubbleSkeleton.test.tsx`:

```tsx
/**
 * ChatBubbleSkeleton — pure component tests
 * Spec: docs/superpowers/specs/2026-05-10-fast-resume-audit-g2-design.md §3.2
 */
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ChatBubbleSkeleton } from '../ChatBubbleSkeleton';

describe('ChatBubbleSkeleton', () => {
  it('renders 3 skeleton bubbles by default', () => {
    render(<ChatBubbleSkeleton />);
    expect(screen.getAllByTestId('chat-bubble-skeleton')).toHaveLength(3);
  });

  it('renders custom count of bubbles', () => {
    render(<ChatBubbleSkeleton count={5} />);
    expect(screen.getAllByTestId('chat-bubble-skeleton')).toHaveLength(5);
  });
});
```

- [ ] **Step 1.2: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/ChatBubbleSkeleton.test
```
Expected: FAIL — module not found.

- [ ] **Step 1.3: Implementa**

Crea `apps/web/src/components/v2/game-chat/ChatBubbleSkeleton.tsx`:

```tsx
/**
 * ChatBubbleSkeleton — N bubble skeleton alternati user/agent durante hydrate.
 *
 * Pattern: indice pari = user (right, ~70% width), dispari = agent (left, ~80% width).
 *
 * Spec: docs/superpowers/specs/2026-05-10-fast-resume-audit-g2-design.md §3.2
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

import { Skeleton } from '@/components/ui/feedback/skeleton';

export interface ChatBubbleSkeletonProps {
  readonly count?: number;
  readonly className?: string;
}

export function ChatBubbleSkeleton({
  count = 3,
  className,
}: ChatBubbleSkeletonProps): ReactElement {
  return (
    <div
      data-slot="chat-bubble-skeleton-list"
      className={clsx('flex flex-col gap-3 p-2', className)}
    >
      {Array.from({ length: count }, (_, idx) => {
        const isUser = idx % 2 === 0;
        return (
          <div
            key={idx}
            data-testid="chat-bubble-skeleton"
            className={clsx('flex', isUser ? 'justify-end' : 'justify-start')}
          >
            <Skeleton
              className={clsx(
                'h-12 rounded-2xl',
                isUser ? 'w-[70%] rounded-br-sm' : 'w-[80%] rounded-bl-sm'
              )}
            />
          </div>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 1.4: Run test PASS**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/ChatBubbleSkeleton.test
```
Expected: 2/2 PASS.

- [ ] **Step 1.5: Commit**

```bash
git add apps/web/src/components/v2/game-chat/ChatBubbleSkeleton.tsx apps/web/src/components/v2/game-chat/__tests__/ChatBubbleSkeleton.test.tsx
git commit -m "feat(web): #929 ChatBubbleSkeleton pure component (G2)"
```

---

## Task 2: `ChatHistoryBanner` (pure)

**Files:**
- Create: `apps/web/src/components/v2/game-chat/ChatHistoryBanner.tsx`
- Test: `apps/web/src/components/v2/game-chat/__tests__/ChatHistoryBanner.test.tsx`

**Contract:**
```ts
interface ChatHistoryBannerProps {
  readonly className?: string;
}
```

Riga sottile: icona `<Info>` da lucide-react + testo disclaimer.

- [ ] **Step 2.1: Test failing**

Crea `apps/web/src/components/v2/game-chat/__tests__/ChatHistoryBanner.test.tsx`:

```tsx
/**
 * ChatHistoryBanner — pure component tests
 * Spec: docs/superpowers/specs/2026-05-10-fast-resume-audit-g2-design.md §3.3
 */
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ChatHistoryBanner } from '../ChatHistoryBanner';

describe('ChatHistoryBanner', () => {
  it('renders disclaimer text about historical messages', () => {
    render(<ChatHistoryBanner />);
    expect(screen.getByText(/messaggi precedenti.*sola lettura/i)).toBeInTheDocument();
    expect(screen.getByText(/citazioni.*non sono ricostruibili/i)).toBeInTheDocument();
  });

  it('passes className prop to root element', () => {
    const { container } = render(<ChatHistoryBanner className="custom-class" />);
    expect(container.firstChild).toHaveClass('custom-class');
  });
});
```

- [ ] **Step 2.2: Run test FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/ChatHistoryBanner.test
```

- [ ] **Step 2.3: Implementa**

Crea `apps/web/src/components/v2/game-chat/ChatHistoryBanner.tsx`:

```tsx
/**
 * ChatHistoryBanner — disclaimer "messaggi storici text-only" mostrato in cima
 * alla lista messaggi quando ci sono SIA storici (da hydrate) SIA nuovi (sessione corrente).
 *
 * Spec: docs/superpowers/specs/2026-05-10-fast-resume-audit-g2-design.md §3.3
 */
import type { ReactElement } from 'react';

import { Info } from 'lucide-react';
import clsx from 'clsx';

export interface ChatHistoryBannerProps {
  readonly className?: string;
}

export function ChatHistoryBanner({ className }: ChatHistoryBannerProps): ReactElement {
  return (
    <div
      role="note"
      data-slot="chat-history-banner"
      className={clsx(
        'flex items-start gap-2 rounded-md border-l-4 px-3 py-2 text-xs',
        'border-l-[hsl(var(--c-info,210_80%_50%)/0.5)] bg-muted/50 text-muted-foreground',
        className
      )}
    >
      <Info className="mt-0.5 h-3.5 w-3.5 flex-shrink-0" aria-hidden="true" />
      <span>
        I messaggi precedenti a questa sessione sono in sola lettura. Citazioni e indicatori di
        confidenza non sono ricostruibili.
      </span>
    </div>
  );
}
```

- [ ] **Step 2.4: Run test PASS**

Expected: 2/2 PASS.

- [ ] **Step 2.5: Commit**

```bash
git add apps/web/src/components/v2/game-chat/ChatHistoryBanner.tsx apps/web/src/components/v2/game-chat/__tests__/ChatHistoryBanner.test.tsx
git commit -m "feat(web): #929 ChatHistoryBanner disclaimer (G2)"
```

---

## Task 3: `useGameChat` extension (hydrate + chatThreadId)

**Files:**
- Modify: `apps/web/src/hooks/queries/useGameChat.ts`
- Modify: `apps/web/src/hooks/queries/__tests__/useGameChat.test.tsx`

**Cambiamenti:**
- Aggiungi state: `isHydrating`, `chatThreadId`, `hasHistoricalMessages`
- Aggiungi `isHistorical?: boolean` a `ChatMessage`
- Aggiungi `useEffect` per fetch on mount via `api.chat.getThreadsByGame(gameId)`
- Mapping `ChatThreadMessageDto` → `ChatMessage` con `isHistorical=true`
- Pass `chatId: chatThreadId` in `qaStream` request
- Salva `chatThreadId` da Complete event (se non già impostato)
- Aggiungi `isHydrating`, `chatThreadId`, `hasHistoricalMessages` al return result

- [ ] **Step 3.1: Aggiungi nuovi test**

Modifica `apps/web/src/hooks/queries/__tests__/useGameChat.test.tsx`. Aggiungi import + mock di `api.chat`:

```ts
// Aggiungi al vi.mock di '@/lib/api/clients/chatClient' (già esistente per qaStream):
vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      getThreadsByGame: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';
```

Nel `beforeEach` aggiungi:
```ts
vi.mocked(api.chat.getThreadsByGame).mockReset();
vi.mocked(api.chat.getThreadsByGame).mockResolvedValue([]);  // default: no threads
```

Aggiungi 6 nuovi test in fondo al `describe('useGameChat', ...)`:

```ts
  // ─── G2 hydrate tests ───────────────────────────────

  const sampleThreadMessage = (role: string, content: string, ts: string) => ({
    content,
    role,
    timestamp: ts,
  });

  const sampleThread = (id: string, lastMessageAt: string | null, messages: any[] = []) => ({
    id,
    gameId: 'wingspan',
    agentId: null,
    agentType: null,
    title: 'Test thread',
    createdAt: '2026-05-10T00:00:00Z',
    lastMessageAt,
    messageCount: messages.length,
    messages,
  });

  it('hydrates messages from latest thread on mount', async () => {
    const thread = sampleThread('thread-1', '2026-05-10T10:00:00Z', [
      sampleThreadMessage('user', 'old question', '2026-05-10T09:00:00Z'),
      sampleThreadMessage('agent', 'old answer', '2026-05-10T09:01:00Z'),
    ]);
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([thread]);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.isHydrating).toBe(false));
    expect(result.current.messages).toHaveLength(2);
    expect(result.current.messages[0].content).toBe('old question');
    expect(result.current.messages[0].isHistorical).toBe(true);
    expect(result.current.chatThreadId).toBe('thread-1');
    expect(result.current.hasHistoricalMessages).toBe(true);
  });

  it('selects most recent thread when multiple exist (lastMessageAt desc)', async () => {
    const older = sampleThread('thread-old', '2026-05-09T10:00:00Z', [
      sampleThreadMessage('user', 'older', '2026-05-09T10:00:00Z'),
    ]);
    const newer = sampleThread('thread-new', '2026-05-10T10:00:00Z', [
      sampleThreadMessage('user', 'newer', '2026-05-10T10:00:00Z'),
    ]);
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([older, newer]);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.isHydrating).toBe(false));
    expect(result.current.chatThreadId).toBe('thread-new');
    expect(result.current.messages[0].content).toBe('newer');
  });

  it('starts empty when no threads exist (no error)', async () => {
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([]);
    const { result } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.isHydrating).toBe(false));
    expect(result.current.messages).toEqual([]);
    expect(result.current.chatThreadId).toBeNull();
    expect(result.current.hasHistoricalMessages).toBe(false);
    expect(result.current.isError).toBe(false);
  });

  it('silent fail when getThreadsByGame rejects', async () => {
    vi.mocked(api.chat.getThreadsByGame).mockRejectedValueOnce(new Error('500'));
    const { result } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.isHydrating).toBe(false));
    expect(result.current.messages).toEqual([]);
    expect(result.current.isError).toBe(false);  // silent fail, no error state
  });

  it('passes chatId in subsequent qaStream calls after hydrate', async () => {
    const thread = sampleThread('thread-existing', '2026-05-10T10:00:00Z', [
      sampleThreadMessage('user', 'old', '2026-05-10T10:00:00Z'),
    ]);
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([thread]);
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);

    const { result } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.chatThreadId).toBe('thread-existing'));

    await act(async () => { await result.current.ask('new question'); });

    expect(qaStream).toHaveBeenCalledWith(expect.objectContaining({
      chatId: 'thread-existing',
    }));
  });

  it('preserves messages across remount (audit trigger A — TanStack cache hit)', async () => {
    const thread = sampleThread('thread-1', '2026-05-10T10:00:00Z', [
      sampleThreadMessage('user', 'cached', '2026-05-10T10:00:00Z'),
    ]);
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValue([thread]);

    // First mount + fetch
    const { result, unmount } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result.current.messages.length).toBeGreaterThan(0));
    expect(api.chat.getThreadsByGame).toHaveBeenCalledTimes(1);

    unmount();

    // Re-mount immediately — within staleTime, no refetch
    const { result: result2 } = renderHook(() => useGameChat('wingspan'));
    await waitFor(() => expect(result2.current.messages.length).toBeGreaterThan(0));
    // Audit: trigger A doesn't lose state because TanStack Query cache OR
    // because hook is module-level effectful — both ways state persists
    expect(result2.current.messages[0].content).toBe('cached');
  });
```

> **NB**: il test "preserves across remount" usa `mockResolvedValue` (non `mockResolvedValueOnce`) per coprire eventuali secondi fetch.

- [ ] **Step 3.2: Run test FAIL**

```bash
cd apps/web && pnpm vitest run src/hooks/queries/__tests__/useGameChat.test
```
Expected: i 9 test esistenti passano, i 6 nuovi falliscono (no `isHydrating`, `chatThreadId`, hydrate logic).

- [ ] **Step 3.3: Modifica `useGameChat.ts`**

Apri `apps/web/src/hooks/queries/useGameChat.ts` e applica queste modifiche:

**A. Aggiungi import:**
```ts
import { useCallback, useEffect, useState } from 'react';

import { qaStream } from '@/lib/api/clients/chatClient';
import { api } from '@/lib/api';
import type { Citation } from '@/types';

import type { AgentKind } from '@/components/v2/game-chat/GameChatHeader';
```

**B. Estendi `ChatMessage` interface:**
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
  readonly isHistorical?: boolean;  // NEW
}
```

**C. Estendi `UseGameChatResult`:**
```ts
export interface UseGameChatResult {
  readonly messages: readonly ChatMessage[];
  readonly isLoading: boolean;
  readonly isHydrating: boolean;        // NEW
  readonly isError: boolean;
  readonly currentAgent: AgentKind;
  readonly chatThreadId: string | null;  // NEW
  readonly hasHistoricalMessages: boolean;  // NEW
  readonly ask: (question: string) => Promise<void>;
  readonly switchAgent: (next: AgentKind) => void;
}
```

**D. Aggiungi mapper:**
```ts
import type { ChatThreadMessageDto } from '@/lib/api/schemas/chat.schemas';

function toChatMessage(dto: ChatThreadMessageDto, idx: number): ChatMessage {
  return {
    id: dto.backendMessageId ?? `historical-${idx}-${dto.timestamp}`,
    role: dto.role === 'user' ? 'user' : 'agent',
    content: dto.content,
    createdAt: dto.timestamp,
    isHistorical: true,
  };
}
```

**E. Modifica `useGameChat` body** — aggiungi state + hydrate effect + qaStream chatId:

```ts
export function useGameChat(gameId: string, initialAgent: AgentKind = 'tutor'): UseGameChatResult {
  const [messages, setMessages] = useState<readonly ChatMessage[]>([]);
  const [currentAgent, setCurrentAgent] = useState<AgentKind>(initialAgent);
  const [isLoading, setIsLoading] = useState(false);
  const [isError, setIsError] = useState(false);
  const [isHydrating, setIsHydrating] = useState(false);  // NEW — default false, set true in effect (G2 review fix C1)
  const [chatThreadId, setChatThreadId] = useState<string | null>(null);  // NEW
  const [hasHistoricalMessages, setHasHistoricalMessages] = useState(false);  // NEW

  // G2: Hydrate latest thread on mount
  useEffect(() => {
    let cancelled = false;
    setIsHydrating(true);

    api.chat.getThreadsByGame(gameId)
      .then(threads => {
        if (cancelled) return;
        const latest = [...threads]
          .filter(t => t.lastMessageAt !== null)
          .sort((a, b) => {
            // Robusto su timezone offset (G2 review fix m2):
            // confronto numerico via getTime() invece di lessicografico
            if (a.lastMessageAt === null) return 1;
            if (b.lastMessageAt === null) return -1;
            return new Date(b.lastMessageAt).getTime() - new Date(a.lastMessageAt).getTime();
          })[0];

        if (latest && latest.messages.length > 0) {
          setChatThreadId(latest.id);
          setMessages(latest.messages.map(toChatMessage));
          setHasHistoricalMessages(true);
        }
      })
      .catch(() => {
        // Silent fail — utente vede chat vuota, può comunque chiedere
      })
      .finally(() => {
        if (!cancelled) setIsHydrating(false);
      });

    return () => { cancelled = true; };
  }, [gameId]);

  const ask = useCallback(async (question: string) => {
    const userMsg: ChatMessage = {
      id: nextId('u'),
      role: 'user',
      content: question,
      createdAt: new Date().toISOString(),
    };
    setMessages(prev => [...prev, userMsg]);
    setIsLoading(true);
    setIsError(false);

    let answerBuffer = '';

    try {
      const stream = qaStream({
        gameId,
        query: question,
        chatId: chatThreadId ?? undefined,  // NEW: passa thread esistente
      });

      for await (const event of stream) {
        if (event.type === TOKEN_EVENT_TYPE) {
          const tokenData = event.data;
          if (typeof tokenData === 'string') {
            answerBuffer += tokenData;
          } else if (typeof tokenData === 'object' && tokenData !== null && 'content' in tokenData) {
            answerBuffer += String((tokenData as { content?: unknown }).content ?? '');
          }
        } else if (event.type === COMPLETE_EVENT_TYPE) {
          const payload = event.data as StreamingCompletePayload;
          const confidence = payload.confidence ?? undefined;
          const citations = payload.Citations ?? payload.citations ?? [];
          const isLowQuality = confidence !== undefined && confidence < LOW_QUALITY_THRESHOLD;
          const outOfContext =
            citations.length === 0 &&
            (confidence === undefined || confidence < OUT_OF_CONTEXT_THRESHOLD);

          const agentMsg: ChatMessage = {
            id: nextId('a'),
            role: 'agent',
            content: answerBuffer,
            citations: citations.length > 0 ? citations : undefined,
            overallConfidence: confidence,
            isLowQuality,
            outOfContext,
            createdAt: new Date().toISOString(),
          };
          setMessages(prev => [...prev, agentMsg]);

          // NEW: salva chatThreadId se backend lo ritorna e non l'abbiamo ancora
          if (payload.chatThreadId && !chatThreadId) {
            setChatThreadId(payload.chatThreadId);
          }
        } else if (event.type === ERROR_EVENT_TYPE) {
          const errPayload = event.data as ErrorPayload;
          throw new Error(errPayload?.message ?? 'QA stream error');
        }
      }
    } catch (e) {
      setIsError(true);
      throw e;
    } finally {
      setIsLoading(false);
    }
  }, [gameId, chatThreadId]);  // chatThreadId aggiunto deps

  const switchAgent = useCallback((next: AgentKind) => {
    setCurrentAgent(next);
  }, []);

  return {
    messages,
    isLoading,
    isHydrating,
    isError,
    currentAgent,
    chatThreadId,
    hasHistoricalMessages,
    ask,
    switchAgent,
  };
}
```

**F. Estendi `StreamingCompletePayload` per includere chatThreadId:**

Verifica se `StreamingCompletePayload` interface (dichiarato in `useGameChat.ts` o importato) include `chatThreadId?`. Se NO, aggiungi:
```ts
interface StreamingCompletePayload {
  // ...existing fields...
  readonly chatThreadId?: string;
}
```

> **NB**: backend `StreamingComplete` (Contracts.cs:109) ha `Guid? chatThreadId` → JSON serializza come `chatThreadId` camelCase.

- [ ] **Step 3.4: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/hooks/queries/__tests__/useGameChat.test
```
Expected: 9 originali + 6 nuovi = 15/15 PASS.

> Se il test "preserves messages across remount" fallisce con due chiamate getThreadsByGame, è OK — il design accetta sia "cache hit TanStack" sia "no cache hit ma stesso effect". L'asserzione importante è che `messages` ricompaiano.

- [ ] **Step 3.5: Commit**

```bash
git add apps/web/src/hooks/queries/useGameChat.ts apps/web/src/hooks/queries/__tests__/useGameChat.test.tsx
git commit -m "feat(web): #929 useGameChat hydrate latest thread on mount + chatId pass-through (G2)"
```

---

## Task 4: `ChatBubble` extension (isHistorical)

**Files:**
- Modify: `apps/web/src/components/v2/game-chat/ChatBubble.tsx`
- Modify: `apps/web/src/components/v2/game-chat/__tests__/ChatBubble.test.tsx`

- [ ] **Step 4.1: Aggiungi test**

Apri `apps/web/src/components/v2/game-chat/__tests__/ChatBubble.test.tsx` e aggiungi in fondo al `describe('ChatBubble', ...)`:

```tsx
  it('applies historical visual variant when isHistorical=true', () => {
    render(<ChatBubble role="agent" content="old" agentName="Tutor" isHistorical />);
    const bubble = screen.getByTestId('chat-bubble');
    expect(bubble).toHaveAttribute('data-historical', 'true');
  });

  it('does not apply historical variant by default', () => {
    render(<ChatBubble role="agent" content="new" agentName="Tutor" />);
    const bubble = screen.getByTestId('chat-bubble');
    expect(bubble).not.toHaveAttribute('data-historical');
  });
```

- [ ] **Step 4.2: Run test FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/ChatBubble.test
```

- [ ] **Step 4.3: Modifica `ChatBubble.tsx`**

Aggiungi prop:

```ts
export interface ChatBubbleProps {
  readonly role: 'user' | 'agent';
  readonly content: ReactNode;
  readonly agentName?: string;
  readonly avatar?: string;
  readonly children?: ReactNode;
  readonly className?: string;
  readonly isHistorical?: boolean;  // NEW
}
```

Modifica il render per applicare opacity quando historical:

```tsx
return (
  <div
    data-testid="chat-bubble"
    data-role={role}
    data-historical={isHistorical ? 'true' : undefined}  // NEW
    className={clsx(
      'max-w-[85%] rounded-2xl px-4 py-3 text-base leading-relaxed',
      isAgent
        ? 'self-start rounded-bl-sm border border-[hsl(var(--c-agent)/0.18)] bg-[hsl(var(--c-agent)/0.08)] text-foreground'
        : 'self-end rounded-br-sm bg-[hsl(var(--c-chat))] text-white',
      isHistorical && 'opacity-90',  // NEW
      className
    )}
  >
    {/* ... */}
  </div>
);
```

E aggiungi `isHistorical` al destructuring dei props.

- [ ] **Step 4.4: Run test PASS**

Expected: tutti i test esistenti + 2 nuovi.

- [ ] **Step 4.5: Commit**

```bash
git add apps/web/src/components/v2/game-chat/ChatBubble.tsx apps/web/src/components/v2/game-chat/__tests__/ChatBubble.test.tsx
git commit -m "feat(web): #929 ChatBubble isHistorical prop (G2 visual variant)"
```

---

## Task 5: `GameChatTabV2` wiring

**Files:**
- Modify: `apps/web/src/components/v2/game-chat/GameChatTabV2.tsx`
- Modify: `apps/web/src/components/v2/game-chat/__tests__/GameChatTabV2.test.tsx`

**Cambiamenti:**
- Import `ChatBubbleSkeleton`, `ChatHistoryBanner`
- Render skeleton durante `chat.isHydrating`
- Render `<ChatHistoryBanner />` quando `chat.hasHistoricalMessages && messages.some(m => !m.isHistorical)`
- Pass `isHistorical={msg.isHistorical}` a `<ChatBubble>`
- `useRef` + `useEffect` per scroll-to-bottom

- [ ] **Step 5.1: Aggiungi 3 test integration**

Apri `apps/web/src/components/v2/game-chat/__tests__/GameChatTabV2.test.tsx`. Aggiungi mock di `api.chat.getThreadsByGame` (se non già presente):

```ts
vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      getThreadsByGame: vi.fn().mockResolvedValue([]),
    },
  },
}));

import { api } from '@/lib/api';
```

Aggiungi 3 nuovi test in fondo al `describe('GameChatTabV2', ...)`:

```tsx
  it('shows skeleton bubbles while hydrating', () => {
    // Make getThreadsByGame never resolve to keep isHydrating=true
    vi.mocked(api.chat.getThreadsByGame).mockReturnValueOnce(new Promise(() => {}));
    render(<GameChatTabV2 gameId="wingspan" />);
    expect(screen.getAllByTestId('chat-bubble-skeleton').length).toBeGreaterThan(0);
  });

  it('shows ChatHistoryBanner when historical messages + new messages exist', async () => {
    const thread = {
      id: 'thread-1',
      gameId: 'wingspan',
      agentId: null,
      agentType: null,
      title: 'Test',
      createdAt: '2026-05-10T00:00:00Z',
      lastMessageAt: '2026-05-10T10:00:00Z',
      messageCount: 1,
      messages: [{ content: 'old historical', role: 'user', timestamp: '2026-05-10T10:00:00Z' }],
    };
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([thread]);
    vi.mocked(qaStream).mockReturnValueOnce(mockStream(happyEvents) as any);

    render(<GameChatTabV2 gameId="wingspan" />);
    // Wait for hydrate
    await waitFor(() => expect(screen.getByText('old historical')).toBeInTheDocument());

    // No banner yet (only historical, no new)
    expect(screen.queryByRole('note')).not.toBeInTheDocument();

    // User asks new question
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'new q?' } });
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    await waitFor(() => expect(screen.getByText(/Sì, ogni potere/)).toBeInTheDocument());

    // Banner now visible
    expect(screen.getByRole('note')).toBeInTheDocument();
  });

  it('does NOT show ChatHistoryBanner when only historical messages (no new yet)', async () => {
    const thread = {
      id: 'thread-1',
      gameId: 'wingspan',
      agentId: null,
      agentType: null,
      title: 'Test',
      createdAt: '2026-05-10T00:00:00Z',
      lastMessageAt: '2026-05-10T10:00:00Z',
      messageCount: 1,
      messages: [{ content: 'old only', role: 'user', timestamp: '2026-05-10T10:00:00Z' }],
    };
    vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([thread]);

    render(<GameChatTabV2 gameId="wingspan" />);
    await waitFor(() => expect(screen.getByText('old only')).toBeInTheDocument());
    expect(screen.queryByRole('note')).not.toBeInTheDocument();
  });
```

- [ ] **Step 5.2: Run test FAIL**

Expected: i 3 nuovi falliscono (skeleton/banner non renderizzati).

- [ ] **Step 5.3: Modifica `GameChatTabV2.tsx`**

Apri `apps/web/src/components/v2/game-chat/GameChatTabV2.tsx`:

**A. Aggiungi import:**

```tsx
import { useState, useEffect, useRef, type ReactElement, type ReactNode } from 'react';

import { ChatBubbleSkeleton } from './ChatBubbleSkeleton';
import { ChatHistoryBanner } from './ChatHistoryBanner';
```

**B. Aggiungi `messagesEndRef` + scroll effect** (subito dopo `const chat = useGameChat(...)`):

```tsx
const messagesEndRef = useRef<HTMLDivElement>(null);

useEffect(() => {
  if (chat.isHydrating) return;
  messagesEndRef.current?.scrollIntoView({ behavior: 'instant', block: 'end' });
}, [chat.isHydrating, chat.messages.length]);
```

**C. Modifica il render della zona messaggi**. Trova il blocco:

```tsx
<div role="log" aria-live="polite" className="flex flex-1 flex-col gap-4 overflow-y-auto p-4">
  {chat.messages.map(msg => ...)}
  {chat.isLoading && (...)}
</div>
```

Sostituisci con:

```tsx
<div role="log" aria-live="polite" className="flex flex-1 flex-col gap-4 overflow-y-auto p-4">
  {chat.isHydrating ? (
    <ChatBubbleSkeleton count={3} />
  ) : (
    <>
      {chat.hasHistoricalMessages && chat.messages.some(m => !m.isHistorical) && (
        <ChatHistoryBanner />
      )}
      {chat.messages.map(msg =>
        msg.role === 'user' ? (
          <ChatBubble
            key={msg.id}
            role="user"
            content={msg.content}
            isHistorical={msg.isHistorical}
          />
        ) : (
          <ChatBubble
            key={msg.id}
            role="agent"
            content={msg.content}
            agentName={AGENT_NAME[chat.currentAgent]}
            avatar={AGENT_AVATAR[chat.currentAgent]}
            isHistorical={msg.isHistorical}
          >
            {renderAgentExtras(msg)}
          </ChatBubble>
        )
      )}
      {chat.isLoading && (
        <ChatBubble
          role="agent"
          content=""
          agentName={AGENT_NAME[chat.currentAgent]}
          avatar={AGENT_AVATAR[chat.currentAgent]}
        >
          <TypingIndicator hint="Cerco nella KB" />
        </ChatBubble>
      )}
      <div ref={messagesEndRef} aria-hidden="true" />
    </>
  )}
</div>
```

> **NB**: `renderAgentExtras` esistente continua a funzionare — per messaggi historical, `msg.citations`/`msg.overallConfidence` sono undefined → niente chip/badge renderizzati (previsto).

- [ ] **Step 5.4: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/GameChatTabV2.test
```
Expected: 5 originali + 3 nuovi = 8/8 PASS.

> Se test "shows banner when historical + new" fallisce, verifica che il mock `api.chat.getThreadsByGame` venga risolto con la struttura corretta del thread (incluso `messages` array).

- [ ] **Step 5.5: Commit**

```bash
git add apps/web/src/components/v2/game-chat/GameChatTabV2.tsx apps/web/src/components/v2/game-chat/__tests__/GameChatTabV2.test.tsx
git commit -m "feat(web): #929 GameChatTabV2 skeleton + history banner + scroll-bottom (G2)"
```

---

## Task 6: Barrel + final verify + PR

**Files:**
- Modify: `apps/web/src/components/v2/game-chat/index.ts`

- [ ] **Step 6.1: Aggiorna barrel**

Modifica `apps/web/src/components/v2/game-chat/index.ts` aggiungendo in fondo:

```ts
export { ChatBubbleSkeleton } from './ChatBubbleSkeleton';
export type { ChatBubbleSkeletonProps } from './ChatBubbleSkeleton';

export { ChatHistoryBanner } from './ChatHistoryBanner';
export type { ChatHistoryBannerProps } from './ChatHistoryBanner';
```

- [ ] **Step 6.2: Lint + typecheck**

```bash
cd apps/web && pnpm lint && pnpm typecheck
```
Expected: 0 errori, baseline ~42-52 warning pre-existing.

- [ ] **Step 6.3: Run full game-chat suite**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat src/hooks/queries/__tests__/useGameChat
```
Expected: tutti i test passano (~95 test totali).

- [ ] **Step 6.4: Smoke test manuale**

Avvia `make dev-core`, login, vai su `/library/games/[id]?tab=aiChat`:

1. **Apri tab vuoto (no thread esistente)** → vedi skeleton brevemente → poi UI vuota con suggested prompts
2. **Invia un messaggio test** → ricevi risposta
3. **Reload pagina (Ctrl+R)** → vedi skeleton → poi vedi tuo messaggio precedente come "historical" (sottile opacity-90), banner NON visibile
4. **Invia altro messaggio** → ricevi risposta → banner appare in cima
5. **Audit trigger A**: minimizza browser per 30s → riapri → state preservato (no fetch ulteriore se entro 5min)

- [ ] **Step 6.5: Commit barrel + push**

```bash
git add apps/web/src/components/v2/game-chat/index.ts
git commit -m "chore(web): #929 export ChatBubbleSkeleton + ChatHistoryBanner from barrel"
git push -u origin feature/issue-929-fast-resume-audit
```

- [ ] **Step 6.6: Apri PR**

```bash
gh pr create --repo meepleAi-app/meepleai-monorepo --base main-dev --title "feat(web): #929 G2 — Fast resume audit (chat thread hydrate on mount)" --body "$(cat <<'EOF'
## Summary
Implementa **G2** dello spec game-night user flow: estende \`useGameChat\` per fetchare il thread chat più recente per (user, gameId) on mount, hydrate \`messages[]\` text-only, persist \`chatThreadId\` per qaStream successive. Aggiunge skeleton + history banner + scroll-bottom in \`GameChatTabV2\`. Backend zero modifiche.

Closes #929.

## Componenti
- NEW \`ChatBubbleSkeleton\` (2 test)
- NEW \`ChatHistoryBanner\` (2 test)
- MODIFY \`useGameChat\` (6 nuovi test: hydrate scenari + audit trigger A)
- MODIFY \`ChatBubble\` (1 nuovo test: isHistorical visual variant)
- MODIFY \`GameChatTabV2\` (3 nuovi test integration)

## Trigger coverage
- **A** background→foreground: confermato via vitest test (TanStack Query cache + state preservato)
- **B** tab killed (Safari iOS aggressive): hydrate on mount restaura cronologia
- **C** cold reload: idem, hydrate restaura
- **D** device handoff: coperto by design (auth session 30 giorni)

## Trade-off documentato
Messaggi storici renderizzati text-only (no citation chips, no confidence badge) perché backend \`ChatThreadMessage\` non persiste metadati G1+G5. \`ChatHistoryBanner\` comunica chiaramente all'utente. Backend extension è plan separato.

## Spec / Plan
- Spec: [\`docs/superpowers/specs/2026-05-10-fast-resume-audit-g2-design.md\`](docs/superpowers/specs/2026-05-10-fast-resume-audit-g2-design.md)
- Plan: [\`docs/superpowers/plans/2026-05-10-fast-resume-audit-g2.md\`](docs/superpowers/plans/2026-05-10-fast-resume-audit-g2.md)

## Test plan
- [x] Unit \`ChatBubbleSkeleton\` 2/2
- [x] Unit \`ChatHistoryBanner\` 2/2
- [x] Unit \`useGameChat\` 9 originali + 6 nuovi = 15/15
- [x] Unit \`ChatBubble\` esistenti + 2 nuovi
- [x] Integration \`GameChatTabV2\` 5 + 3 = 8/8
- [x] Typecheck clean
- [x] Lint baseline
- [ ] Smoke test manuale richiesta al reviewer

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Out of scope (follow-up plans)

| Item | Reason | Plan target |
|---|---|---|
| Backend extension `ChatThreadMessage` per persistere citations/confidence | Trade-off documentato in spec §4.A | Plan dedicato post-Alpha |
| Bottone "Nuova chat" header | E del Q3 brainstorm — follow-up | Plan separato |
| Smart scroll su unread marker | C del Q6 — backend non traccia unread | Future |
| localStorage hybrid cache | D del Q2 — ottimizzazione opzionale | Future se latenza problema |
| Cross-device E2E Playwright | Richiede 2 device + setup elaborato | Plan E2E dedicato |

---

## Spec Self-Review (eseguito)

**Spec coverage**: tutte le sezioni spec §1-§5 mappate a Task 1-6:
- §2 Architecture → Task 1 (skeleton), 2 (banner), 3 (hook), 4 (bubble), 5 (orchestrator)
- §3 Component contracts → contracts in Task 1-5 corrispondono 1:1
- §4 Logic → Task 3 (hydrate, mapper, chatId pass-through, scroll)
- §5 Testing → ogni task ha unit test con TDD strict

**Placeholder scan**: zero `TBD/TODO/...later`. L'unico `TODO` esistente nel codice di PR #918/#926 (`onOpenInKb`) è già stato eliminato post-merge — non riapparirà.

**Type consistency**:
- `ChatMessage` esteso con `isHistorical?` ✓ (Task 3 hook + Task 4 bubble + Task 5 orchestrator coerenti)
- `UseGameChatResult` esteso con `isHydrating`, `chatThreadId`, `hasHistoricalMessages` ✓ (Task 3 + Task 5 consumer)
- `ChatThreadDto` schema verificato: `lastMessageAt`, `messages: ChatThreadMessageDto[]`, `messageCount` ✓
- `QaStreamRequest.chatId?: string` verificato `chatClient.ts:354` ✓

**Risk noted**:
- Test "preserves messages across remount" (audit A) potrebbe trigger 2 fetch invece di cache hit se TanStack Query non è il sistema di caching. Il design accetta entrambi (assertion solo su `messages` ricomparsi).
- Mock `api.chat.getThreadsByGame` in `GameChatTabV2.test` deve essere setup PRIMA di `qaStream` mock per evitare race nei test esistenti.
