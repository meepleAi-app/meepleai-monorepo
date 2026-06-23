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
        body: makeReadableStream([
          'data: {"type":"token","content":"Ciao"}\n\n',
          'data: {"type":"complete","threadId":"t1"}\n\n',
        ]),
      } as unknown as Response)
    );

    const { result } = renderHook(() => useSessionAgentChat('game-sess-1', 'agent-1'));

    await act(async () => {
      await result.current.ask('Come funziona?');
    });

    expect(result.current.messages[0].role).toBe('user');
    expect(result.current.messages[0].content).toBe('Come funziona?');
  });

  it('ask estrae le citazioni dal payload complete', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        body: makeReadableStream([
          'data: {"type":"token","content":"Posiziona la plancia."}\n\n',
          'data: {"type":"complete","threadId":"t1","citations":[{"source":"Regolamento Azul","pageNumber":7,"copyrightTier":"full","snippet":"Posiziona la plancia al centro."}]}\n\n',
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
    expect(lastMsg.citations![0]).toEqual({
      documentName: 'Regolamento Azul',
      pages: [7],
      excerpt: 'Posiziona la plancia al centro.',
    });
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

      // Mock getThreadById to return a thread with one assistant message with citationsJson
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
            citationsJson:
              '[{"source":"Regolamento Towers","pageNumber":3,"copyrightTier":"full","snippet":"Piazza la torre sulla casella."}]',
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
        documentName: 'Regolamento Towers',
        pages: [3],
        excerpt: 'Piazza la torre sulla casella.',
      });
    });

    it('AC-CHAT-1 persist: after SSE complete with chatThreadId, sessionStorage is populated', async () => {
      vi.stubGlobal(
        'fetch',
        vi.fn().mockResolvedValue({
          ok: true,
          body: makeReadableStream([
            'data: {"type":"token","content":"Risposta"}\n\n',
            `data: {"type":"complete","threadId":"${THREAD_ID}"}\n\n`,
          ]),
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
          body: makeReadableStream([
            'data: {"type":"token","content":"Risposta"}\n\n',
            `data: {"type":"complete","threadId":"${THREAD_ID}"}\n\n`,
          ]),
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
  // AC-CHAT-3: isNonGrounded flag — SSE live flow + historic hydration
  // ─────────────────────────────────────────────────────────────────────────

  describe('AC-CHAT-3: isNonGrounded flag', () => {
    it('SSE complete with 0 citations → last assistant message has isNonGrounded:true', async () => {
      vi.stubGlobal(
        'fetch',
        vi.fn().mockResolvedValue({
          ok: true,
          body: makeReadableStream([
            'data: {"type":"token","content":"Risposta senza fonti."}\n\n',
            'data: {"type":"complete","threadId":"t-ng-1"}\n\n',
          ]),
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
            'data: {"type":"token","content":"Risposta con fonti."}\n\n',
            'data: {"type":"complete","threadId":"t-g-1","citations":[{"source":"Reg","pageNumber":3,"copyrightTier":"full","snippet":"testo"}]}\n\n',
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

    it('user messages never have isNonGrounded set', async () => {
      vi.stubGlobal(
        'fetch',
        vi.fn().mockResolvedValue({
          ok: true,
          body: makeReadableStream([
            'data: {"type":"token","content":"Risposta."}\n\n',
            'data: {"type":"complete","threadId":"t-user-1"}\n\n',
          ]),
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
            citationsJson:
              '[{"source":"Reg","pageNumber":5,"copyrightTier":"full","snippet":"testo"}]',
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
