import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useSessionAgentChat } from '../useSessionAgentChat';
import { useSessionStore } from '@/stores/session/store';

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

describe('useSessionAgentChat', () => {
  beforeEach(() => {
    localStorage.clear();
    useSessionStore.getState().reset();
    vi.restoreAllMocks();
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
});
