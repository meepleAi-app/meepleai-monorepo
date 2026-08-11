import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { useSessionAgentChat } from '../useSessionAgentChat';
import { useSessionStore } from '@/stores/session/store';
import { api } from '@/lib/api';

// Mock the api module so we can spy on api.chat.getThreadById
vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      getThreadById: vi.fn(),
    },
  },
}));

const mockGetThreadById = vi.mocked(api.chat.getThreadById);

// Mock fetch for streaming
const makeReadableStream = (chunks: string[]) => {
  let i = 0;
  return new ReadableStream({
    pull(controller) {
      if (i < chunks.length) {
        controller.enqueue(new TextEncoder().encode(chunks[i++]));
      } else {
        controller.close();
      }
    },
  });
};

// sessionStorage key helper — must match the hook implementation
const SESSION_STORAGE_KEY = (gameSessionId: string) =>
  `meepleai:live-agent-thread:${gameSessionId}`;

// ─── Real wire SSE helpers ────────────────────────────────────────────────────
// C2 (#2500): BE emits {"type":<int>,"data":{...},"timestamp":"...Z"}
// Token=7, Complete=4, Error=5

function sseToken(token: string): string {
  return `data: ${JSON.stringify({ type: 7, data: { token }, timestamp: new Date().toISOString() })}\n\n`;
}

function sseComplete(
  chatThreadId: string,
  citations?: unknown[],
  groundingStatus?: string
): string {
  return `data: ${JSON.stringify({
    type: 4,
    data: {
      chatThreadId,
      citations: citations ?? [],
      totalTokens: 10,
      confidence: 0.9,
      groundingStatus,
    },
    timestamp: new Date().toISOString(),
  })}\n\n`;
}

/** Real CitationDto wire shape (BE Contracts.cs:137-144) */
function makeCitationDto(overrides?: Record<string, unknown>) {
  return {
    documentId: 'doc-uuid-001',
    pageNumber: 7,
    relevanceScore: 0.92,
    snippetPreview: 'Posiziona la plancia al centro.',
    copyrightTier: 'full',
    paraphrasedSnippet: null,
    isPublic: true,
    ...overrides,
  };
}

describe('useSessionAgentChat', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    useSessionStore.getState().reset();
    vi.restoreAllMocks();
    mockGetThreadById.mockReset();
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('messages inizialmente è vuoto', () => {
    const { result } = renderHook(() => useSessionAgentChat('game-sess-1', 'agent-1'));
    expect(result.current.messages).toHaveLength(0);
  });

  it('isLoading è true durante lo streaming', async () => {
    useSessionStore.getState().startSession({
      sessionId: 'sess-1',
      gameId: 'game-1',
      gameTitle: 'Catan',
      participants: [{ id: 'p1', displayName: 'Marco', isGuest: false }],
    });

    // Fetch that never resolves during the test
    const fetchMock = vi.fn().mockReturnValue(
      new Promise(() => {}) // never resolves
    );
    vi.stubGlobal('fetch', fetchMock);

    const { result } = renderHook(() => useSessionAgentChat('game-sess-1', 'agent-session-1'));

    act(() => {
      void result.current.ask('Come si piazzano i ladri?');
    });

    expect(result.current.isLoading).toBe(true);
  });

  it('ask aggiunge messaggio utente immediatamente', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        body: makeReadableStream([sseToken('Ciao'), sseComplete('t1')]),
      } as unknown as Response)
    );

    const { result } = renderHook(() => useSessionAgentChat('game-sess-1', 'agent-1'));

    await act(async () => {
      await result.current.ask('Come funziona?');
    });

    expect(result.current.messages[0].role).toBe('user');
    expect(result.current.messages[0].content).toBe('Come funziona?');
  });

  // ─── C2 wire format contract test ──────────────────────────────────────────
  // This is the key anti-false-green test: asserts the parser reads
  // {type:4,data:{chatThreadId,citations}} (numeric type, nested data).

  it('C2 wire contract: token type=7 + complete type=4 with chatThreadId + citations', async () => {
    const citationDto = makeCitationDto();

    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        body: makeReadableStream([
          sseToken('Posiziona'),
          sseToken(' la plancia.'),
          sseComplete('thread-wire-001', [citationDto]),
        ]),
      } as unknown as Response)
    );

    const { result } = renderHook(() =>
      useSessionAgentChat('game-sess-wire', 'agent-wire', { persistHistory: true })
    );

    await act(async () => {
      await result.current.ask('Come si fa il setup?');
    });

    const lastMsg = result.current.messages[result.current.messages.length - 1];
    expect(lastMsg.role).toBe('assistant');
    // Token accumulation works
    expect(lastMsg.content).toBe('Posiziona la plancia.');
    // Citations mapped from real wire shape (snippetPreview → excerpt, documentId → documentName)
    expect(lastMsg.citations).toBeDefined();
    expect(lastMsg.citations).toHaveLength(1);
    expect(lastMsg.citations![0]).toEqual({
      documentName: 'doc-uuid-001', // real wire has no `source` → falls back to documentId
      pages: [7],
      excerpt: 'Posiziona la plancia al centro.',
    });
    // threadId persisted to sessionStorage
    expect(sessionStorage.getItem(SESSION_STORAGE_KEY('game-sess-wire'))).toBe('thread-wire-001');
  });

  it('ask estrae le citazioni dal payload complete (wire format)', async () => {
    const citationDto = makeCitationDto({ snippetPreview: 'Posiziona la plancia al centro.' });

    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        body: makeReadableStream([
          sseToken('Posiziona la plancia.'),
          sseComplete('t1', [citationDto]),
        ]),
      } as unknown as Response)
    );

    const { result } = renderHook(() => useSessionAgentChat('game-sess-1', 'agent-1'));

    await act(async () => {
      await result.current.ask('come si fa il setup?');
    });

    const lastMsg = result.current.messages[result.current.messages.length - 1];
    expect(lastMsg.role).toBe('assistant');
    expect(lastMsg.citations).toBeDefined();
    expect(lastMsg.citations).toHaveLength(1);
    expect(lastMsg.citations![0].pages).toEqual([7]);
    expect(lastMsg.citations![0].excerpt).toBe('Posiziona la plancia al centro.');
  });

  // ─────────────────────────────────────────────────────────────────────────
  // AC-CHAT-1: persistHistory opt-in — history hydration with citations
  // ─────────────────────────────────────────────────────────────────────────

  describe('persistHistory opt-in', () => {
    const GAME_SESSION_ID = 'game-sess-persist-1';
    const THREAD_ID = 'thread-uuid-0001';
    const STORAGE_KEY = SESSION_STORAGE_KEY(GAME_SESSION_ID);

    it('AC-CHAT-1 happy: mounts with persisted threadId and hydrates messages with citations', async () => {
      // Pre-seed sessionStorage with a saved threadId
      sessionStorage.setItem(STORAGE_KEY, THREAD_ID);

      // Mock getThreadById to return a thread with one assistant message with citationsJson.
      // citationsJson stores the real CitationDto wire shape (snippetPreview field).
      mockGetThreadById.mockResolvedValueOnce({
        id: THREAD_ID,
        gameId: null,
        agentId: null,
        agentType: null,
        title: null,
        createdAt: '2026-06-23T10:00:00Z',
        lastMessageAt: '2026-06-23T10:01:00Z',
        messageCount: 1,
        messages: [
          {
            content: 'Le torri si piazzano così.',
            role: 'assistant',
            timestamp: '2026-06-23T10:01:00Z',
            backendMessageId: 'msg-uuid-001',
            citationsJson: JSON.stringify([
              {
                documentId: 'doc-towers-001',
                pageNumber: 3,
                relevanceScore: 0.88,
                snippetPreview: 'Piazza la torre sulla casella.',
                copyrightTier: 'full',
                paraphrasedSnippet: null,
                isPublic: true,
              },
            ]),
          },
        ],
      });

      const { result } = renderHook(() =>
        useSessionAgentChat(GAME_SESSION_ID, 'agent-1', { persistHistory: true })
      );

      // Wait for async mount effect
      await act(async () => {
        await new Promise(resolve => setTimeout(resolve, 0));
      });

      expect(result.current.messages).toHaveLength(1);
      const msg = result.current.messages[0];
      expect(msg.role).toBe('assistant');
      expect(msg.content).toBe('Le torri si piazzano così.');
      expect(msg.citations).toBeDefined();
      expect(msg.citations).toHaveLength(1);
      expect(msg.citations![0]).toEqual({
        documentName: 'doc-towers-001', // no `source` in wire → falls back to documentId
        pages: [3],
        excerpt: 'Piazza la torre sulla casella.',
      });
    });

    it('AC-CHAT-1 persist: after SSE complete with chatThreadId, sessionStorage is populated', async () => {
      vi.stubGlobal(
        'fetch',
        vi.fn().mockResolvedValue({
          ok: true,
          body: makeReadableStream([sseToken('Risposta'), sseComplete(THREAD_ID)]),
        } as unknown as Response)
      );

      const { result } = renderHook(() =>
        useSessionAgentChat(GAME_SESSION_ID, 'agent-1', { persistHistory: true })
      );

      await act(async () => {
        await result.current.ask('Domanda di test');
      });

      expect(sessionStorage.getItem(STORAGE_KEY)).toBe(THREAD_ID);
    });

    it('AC-CHAT-1 opt-in OFF (default): no sessionStorage access, no getThreadById, messages empty', async () => {
      sessionStorage.setItem(STORAGE_KEY, THREAD_ID);

      const { result } = renderHook(() => useSessionAgentChat(GAME_SESSION_ID, 'agent-1'));

      // Wait briefly for any async effect
      await act(async () => {
        await new Promise(resolve => setTimeout(resolve, 0));
      });

      // getThreadById must NOT be called when persistHistory is off
      expect(mockGetThreadById).not.toHaveBeenCalled();
      // messages must remain empty
      expect(result.current.messages).toHaveLength(0);
    });

    it('AC-CHAT-1 opt-in explicit false: no getThreadById called', async () => {
      sessionStorage.setItem(STORAGE_KEY, THREAD_ID);

      const { result } = renderHook(() =>
        useSessionAgentChat(GAME_SESSION_ID, 'agent-1', { persistHistory: false })
      );

      await act(async () => {
        await new Promise(resolve => setTimeout(resolve, 0));
      });

      expect(mockGetThreadById).not.toHaveBeenCalled();
      expect(result.current.messages).toHaveLength(0);
    });

    it('AC-CHAT-1 graceful: getThreadById rejects → no throw, messages remains empty', async () => {
      sessionStorage.setItem(STORAGE_KEY, THREAD_ID);
      mockGetThreadById.mockRejectedValueOnce(new Error('Network error'));

      const { result } = renderHook(() =>
        useSessionAgentChat(GAME_SESSION_ID, 'agent-1', { persistHistory: true })
      );

      await act(async () => {
        await new Promise(resolve => setTimeout(resolve, 0));
      });

      // Must not throw, messages must stay empty on failure
      expect(result.current.messages).toHaveLength(0);
    });

    it('AC-CHAT-1 graceful: no persisted threadId → no fetch, messages empty', async () => {
      // sessionStorage is empty (no STORAGE_KEY)
      const { result } = renderHook(() =>
        useSessionAgentChat(GAME_SESSION_ID, 'agent-1', { persistHistory: true })
      );

      await act(async () => {
        await new Promise(resolve => setTimeout(resolve, 0));
      });

      expect(mockGetThreadById).not.toHaveBeenCalled();
      expect(result.current.messages).toHaveLength(0);
    });

    it('AC-CHAT-1 mapping: citationsJson null on messages → citations undefined', async () => {
      sessionStorage.setItem(STORAGE_KEY, THREAD_ID);

      mockGetThreadById.mockResolvedValueOnce({
        id: THREAD_ID,
        gameId: null,
        agentId: null,
        agentType: null,
        title: null,
        createdAt: '2026-06-23T10:00:00Z',
        lastMessageAt: '2026-06-23T10:01:00Z',
        messageCount: 2,
        messages: [
          {
            content: 'Come funziona?',
            role: 'user',
            timestamp: '2026-06-23T10:00:00Z',
            backendMessageId: 'msg-uuid-002',
            citationsJson: null,
          },
          {
            content: 'Funziona così.',
            role: 'assistant',
            timestamp: '2026-06-23T10:01:00Z',
            backendMessageId: 'msg-uuid-003',
            citationsJson: null,
          },
        ],
      });

      const { result } = renderHook(() =>
        useSessionAgentChat(GAME_SESSION_ID, 'agent-1', { persistHistory: true })
      );

      await act(async () => {
        await new Promise(resolve => setTimeout(resolve, 0));
      });

      expect(result.current.messages).toHaveLength(2);
      expect(result.current.messages[0].citations).toBeUndefined();
      expect(result.current.messages[1].citations).toBeUndefined();
    });

    it('AC-CHAT-1 persist opt-in OFF: SSE complete does NOT write to sessionStorage', async () => {
      vi.stubGlobal(
        'fetch',
        vi.fn().mockResolvedValue({
          ok: true,
          body: makeReadableStream([sseToken('Risposta'), sseComplete(THREAD_ID)]),
        } as unknown as Response)
      );

      const { result } = renderHook(
        () => useSessionAgentChat(GAME_SESSION_ID, 'agent-1')
        // no persistHistory
      );

      await act(async () => {
        await result.current.ask('Domanda di test');
      });

      // sessionStorage must remain empty when opt-in is off
      expect(sessionStorage.getItem(STORAGE_KEY)).toBeNull();
    });
  });

  // ─────────────────────────────────────────────────────────────────────────
  // I1: gameContext explicit injection
  // ─────────────────────────────────────────────────────────────────────────

  describe('I1: gameContext explicit injection', () => {
    it('uses explicit gameContext in fetch body when provided', async () => {
      let capturedBody: Record<string, unknown> | undefined;
      vi.stubGlobal(
        'fetch',
        vi.fn().mockImplementation(async (_url: string, init: RequestInit) => {
          capturedBody = JSON.parse(init.body as string) as Record<string, unknown>;
          return {
            ok: true,
            body: makeReadableStream([sseToken('ok'), sseComplete('t-ctx-1')]),
          };
        })
      );

      const { result } = renderHook(() =>
        useSessionAgentChat('game-sess-ctx', 'agent-ctx', {
          gameContext: {
            gameId: 'game-uuid-123',
            gameTitle: 'Azul',
            players: ['Alice', 'Bob'],
            currentTurn: 3,
          },
        })
      );

      await act(async () => {
        await result.current.ask('Domanda con contesto');
      });

      expect(capturedBody).toBeDefined();
      expect(capturedBody?.gameContext).toEqual({
        gameId: 'game-uuid-123',
        gameTitle: 'Azul',
        players: ['Alice', 'Bob'],
        currentTurn: 3,
        responseLanguage: 'it',
      });
    });

    it('falls back to session store when gameContext not provided (RulesExplainer compat)', async () => {
      useSessionStore.getState().startSession({
        sessionId: 'sess-rules',
        gameId: 'game-rules-1',
        gameTitle: 'Wingspan',
        participants: [{ id: 'p1', displayName: 'Luca', isGuest: false }],
      });

      let capturedBody: Record<string, unknown> | undefined;
      vi.stubGlobal(
        'fetch',
        vi.fn().mockImplementation(async (_url: string, init: RequestInit) => {
          capturedBody = JSON.parse(init.body as string) as Record<string, unknown>;
          return {
            ok: true,
            body: makeReadableStream([sseToken('ok'), sseComplete('t-rules-1')]),
          };
        })
      );

      const { result } = renderHook(
        () => useSessionAgentChat('game-sess-rules', 'agent-rules')
        // no gameContext — uses store
      );

      await act(async () => {
        await result.current.ask('Domanda rules explainer');
      });

      expect((capturedBody?.gameContext as Record<string, unknown>)?.gameId).toBe('game-rules-1');
      expect((capturedBody?.gameContext as Record<string, unknown>)?.gameTitle).toBe('Wingspan');
    });

    it('sends no gameContext when store is empty and no explicit context provided', async () => {
      let capturedBody: Record<string, unknown> | undefined;
      vi.stubGlobal(
        'fetch',
        vi.fn().mockImplementation(async (_url: string, init: RequestInit) => {
          capturedBody = JSON.parse(init.body as string) as Record<string, unknown>;
          return {
            ok: true,
            body: makeReadableStream([sseToken('ok'), sseComplete('t-empty-1')]),
          };
        })
      );

      const { result } = renderHook(() => useSessionAgentChat('game-sess-empty', 'agent-empty'));

      await act(async () => {
        await result.current.ask('Domanda senza contesto');
      });

      expect(capturedBody?.gameContext).toBeUndefined();
    });
  });

  // ─────────────────────────────────────────────────────────────────────────
  // AC-CHAT-3: isNonGrounded flag — SSE live flow + historic hydration
  // ─────────────────────────────────────────────────────────────────────────

  describe('AC-CHAT-3: isNonGrounded flag', () => {
    it('SSE complete with 0 citations → last assistant message has isNonGrounded:true', async () => {
      vi.stubGlobal(
        'fetch',
        vi.fn().mockResolvedValue({
          ok: true,
          body: makeReadableStream([sseToken('Risposta senza fonti.'), sseComplete('t-ng-1')]),
        } as unknown as Response)
      );

      const { result } = renderHook(() => useSessionAgentChat('game-sess-ng', 'agent-ng'));

      await act(async () => {
        await result.current.ask('Domanda senza citazioni');
      });

      const lastMsg = result.current.messages[result.current.messages.length - 1];
      expect(lastMsg.role).toBe('assistant');
      expect(lastMsg.isNonGrounded).toBe(true);
    });

    it('SSE complete with ≥1 citation → isNonGrounded is falsy', async () => {
      vi.stubGlobal(
        'fetch',
        vi.fn().mockResolvedValue({
          ok: true,
          body: makeReadableStream([
            sseToken('Risposta con fonti.'),
            sseComplete('t-g-1', [makeCitationDto({ snippetPreview: 'testo' })]),
          ]),
        } as unknown as Response)
      );

      const { result } = renderHook(() => useSessionAgentChat('game-sess-g', 'agent-g'));

      await act(async () => {
        await result.current.ask('Domanda con citazioni');
      });

      const lastMsg = result.current.messages[result.current.messages.length - 1];
      expect(lastMsg.role).toBe('assistant');
      expect(lastMsg.isNonGrounded).toBeFalsy();
    });

    // ─────────────────────────────────────────────────────────────────────
    // Task 3 (#3388): server-authoritative groundingStatus on the SSE Complete
    // event now drives isNonGrounded instead of the citations heuristic.
    // ─────────────────────────────────────────────────────────────────────

    it('SSE complete groundingStatus="Ungrounded" → message carries groundingStatus + isNonGrounded:true', async () => {
      vi.stubGlobal(
        'fetch',
        vi.fn().mockResolvedValue({
          ok: true,
          body: makeReadableStream([
            sseToken('Risposta.'),
            sseComplete('t-gs-ungrounded', [], 'Ungrounded'),
          ]),
        } as unknown as Response)
      );

      const { result } = renderHook(() => useSessionAgentChat('game-sess-gs-1', 'agent-gs-1'));

      await act(async () => {
        await result.current.ask('Domanda');
      });

      const lastMsg = result.current.messages[result.current.messages.length - 1];
      expect(lastMsg.role).toBe('assistant');
      expect(lastMsg.groundingStatus).toBe('Ungrounded');
      expect(lastMsg.isNonGrounded).toBe(true);
    });

    it('SSE complete groundingStatus="Grounded" (with citations) → groundingStatus set + isNonGrounded falsy', async () => {
      vi.stubGlobal(
        'fetch',
        vi.fn().mockResolvedValue({
          ok: true,
          body: makeReadableStream([
            sseToken('Risposta.'),
            sseComplete('t-gs-grounded', [makeCitationDto()], 'Grounded'),
          ]),
        } as unknown as Response)
      );

      const { result } = renderHook(() => useSessionAgentChat('game-sess-gs-2', 'agent-gs-2'));

      await act(async () => {
        await result.current.ask('Domanda');
      });

      const lastMsg = result.current.messages[result.current.messages.length - 1];
      expect(lastMsg.role).toBe('assistant');
      expect(lastMsg.groundingStatus).toBe('Grounded');
      expect(lastMsg.isNonGrounded).toBeFalsy();
    });

    it('user messages never have isNonGrounded set', async () => {
      vi.stubGlobal(
        'fetch',
        vi.fn().mockResolvedValue({
          ok: true,
          body: makeReadableStream([sseToken('Risposta.'), sseComplete('t-user-1')]),
        } as unknown as Response)
      );

      const { result } = renderHook(() => useSessionAgentChat('game-sess-u', 'agent-u'));

      await act(async () => {
        await result.current.ask('Domanda utente');
      });

      const userMsg = result.current.messages[0];
      expect(userMsg.role).toBe('user');
      expect(userMsg.isNonGrounded).toBeFalsy();
    });

    it('historic assistant message without citations → isNonGrounded:true', async () => {
      const THREAD_ID_NG = 'thread-ng-history-001';
      const STORAGE_KEY_NG = `meepleai:live-agent-thread:game-sess-ng-hist`;
      sessionStorage.setItem(STORAGE_KEY_NG, THREAD_ID_NG);

      mockGetThreadById.mockResolvedValueOnce({
        id: THREAD_ID_NG,
        gameId: null,
        agentId: null,
        agentType: null,
        title: null,
        createdAt: '2026-06-23T10:00:00Z',
        lastMessageAt: '2026-06-23T10:01:00Z',
        messageCount: 1,
        messages: [
          {
            content: 'Risposta storica senza citazioni.',
            role: 'assistant',
            timestamp: '2026-06-23T10:01:00Z',
            backendMessageId: 'msg-hist-ng-001',
            citationsJson: null,
          },
        ],
      });

      const { result } = renderHook(() =>
        useSessionAgentChat('game-sess-ng-hist', 'agent-ng-hist', { persistHistory: true })
      );

      await act(async () => {
        await new Promise(resolve => setTimeout(resolve, 0));
      });

      expect(result.current.messages).toHaveLength(1);
      const msg = result.current.messages[0];
      expect(msg.role).toBe('assistant');
      expect(msg.isNonGrounded).toBe(true);
    });

    it('historic assistant message WITH citations → isNonGrounded is falsy', async () => {
      const THREAD_ID_G = 'thread-g-history-001';
      const STORAGE_KEY_G = `meepleai:live-agent-thread:game-sess-g-hist`;
      sessionStorage.setItem(STORAGE_KEY_G, THREAD_ID_G);

      mockGetThreadById.mockResolvedValueOnce({
        id: THREAD_ID_G,
        gameId: null,
        agentId: null,
        agentType: null,
        title: null,
        createdAt: '2026-06-23T10:00:00Z',
        lastMessageAt: '2026-06-23T10:01:00Z',
        messageCount: 1,
        messages: [
          {
            content: 'Risposta con citazioni storiche.',
            role: 'assistant',
            timestamp: '2026-06-23T10:01:00Z',
            backendMessageId: 'msg-hist-g-001',
            citationsJson: JSON.stringify([
              {
                documentId: 'doc-hist-001',
                pageNumber: 5,
                relevanceScore: 0.85,
                snippetPreview: 'testo storico',
                copyrightTier: 'full',
                paraphrasedSnippet: null,
                isPublic: true,
              },
            ]),
          },
        ],
      });

      const { result } = renderHook(() =>
        useSessionAgentChat('game-sess-g-hist', 'agent-g-hist', { persistHistory: true })
      );

      await act(async () => {
        await new Promise(resolve => setTimeout(resolve, 0));
      });

      expect(result.current.messages).toHaveLength(1);
      const msg = result.current.messages[0];
      expect(msg.role).toBe('assistant');
      expect(msg.isNonGrounded).toBeFalsy();
    });
  });
});
