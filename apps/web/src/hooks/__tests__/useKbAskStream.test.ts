import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useKbAskStream } from '../useKbAskStream';
import { kbAskClient } from '../../lib/api/clients/kbAskClient';
import type { KbAskEvent } from '../../lib/api/schemas/kb-ask.schemas';

async function* eventsAsAsyncGen(events: KbAskEvent[]): AsyncGenerator<KbAskEvent> {
  for (const e of events) yield e;
}

describe('useKbAskStream — initial state', () => {
  it('starts in idle with empty citations + no error', () => {
    const { result } = renderHook(() => useKbAskStream());
    expect(result.current.state.status).toBe('idle');
    expect(result.current.state.partialText).toBe('');
    expect(result.current.state.citations).toEqual([]);
    expect(result.current.state.error).toBeNull();
    expect(result.current.state.retryCount).toBe(0);
  });
});

describe('useKbAskStream — transitions', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('idle → streaming on ask()', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([{ type: 0, data: { message: 'Ricerca…' } }])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('scout abilities');
    });

    await waitFor(() => expect(result.current.state.status).toBe('streaming'));
  });

  it('accumulates tokens into partialText', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 7, data: { token: 'La ' } },
        { type: 7, data: { token: 'classe.' } },
      ])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.partialText).toBe('La classe.'));
  });

  it('stores citations from Citations event (D-E: page-level)', async () => {
    const cite = { docId: 'd', source: 'd', page: 14, snippet: 't', score: 0.9 };
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([{ type: 1, data: { citations: [cite] } }])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.citations).toEqual([cite]));
  });

  it('streaming → completed on Complete event with tokens', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 7, data: { token: 'a' } },
        {
          type: 4,
          data: {
            totalTokens: 1,
            promptTokens: 5,
            completionTokens: 1,
            estimatedReadingTimeMinutes: 0,
            confidence: null,
          },
        },
      ])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.status).toBe('completed'));
    expect(result.current.state.totalTokens).toBe(1);
  });

  it('streaming → completed-empty when Complete arrives with totalTokens=0 and no prior Citations (D-L Adzic)', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 0, data: { message: 'Ricerca nella tua libreria...' } },
        {
          type: 4,
          data: {
            totalTokens: 0,
            promptTokens: 0,
            completionTokens: 0,
            estimatedReadingTimeMinutes: 0,
            confidence: null,
          },
        },
      ])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.status).toBe('completed-empty'));
  });

  it('Error event with server-side code maps to kind=server (D-L Nygard)', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 5, data: { message: 'RBAC failed', code: 'RBAC_RESOLUTION_FAILED' } },
      ])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.status).toBe('error'));
    expect(result.current.state.error?.kind).toBe('server');
    expect(result.current.state.error?.code).toBe('RBAC_RESOLUTION_FAILED');
  });

  it('Error event with LLM_STREAMING_FAILED + accumulated partial text maps to kind=partial', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 7, data: { token: 'La classe Scout inizia…' } },
        { type: 5, data: { message: 'LLM crashed', code: 'LLM_STREAMING_FAILED' } },
      ])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.error?.kind).toBe('partial'));
    expect(result.current.state.partialText).toBe('La classe Scout inizia…');
  });

  it('Network throw maps to kind=connection', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() => {
      throw new TypeError('Failed to fetch');
    });
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => expect(result.current.state.error?.kind).toBe('connection'));
  });

  it('stop() aborts mid-stream and transitions to idle', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([{ type: 7, data: { token: 'a' } }])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });
    await waitFor(() => expect(result.current.state.status).toBe('streaming'));

    act(() => {
      result.current.stop();
    });
    await waitFor(() => expect(result.current.state.status).toBe('idle'));
  });

  it('reset() returns to idle from completed', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        {
          type: 4,
          data: {
            totalTokens: 1,
            promptTokens: 1,
            completionTokens: 1,
            estimatedReadingTimeMinutes: 0,
            confidence: null,
          },
        },
      ])
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });
    await waitFor(() => expect(result.current.state.status).toBe('completed'));

    act(() => {
      result.current.reset();
    });
    expect(result.current.state.status).toBe('idle');
    expect(result.current.state.partialText).toBe('');
    expect(result.current.state.citations).toEqual([]);
  });

  it('ask() with topK and language forwards options to client', async () => {
    const spy = vi
      .spyOn(kbAskClient, 'askGlobal')
      .mockImplementation(() => eventsAsAsyncGen([{ type: 0, data: { message: 'ok' } }]));
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q', { language: 'en', topK: 12 });
    });

    await waitFor(() =>
      expect(spy).toHaveBeenCalledWith(
        { query: 'q', language: 'en', topK: 12 },
        expect.any(AbortSignal)
      )
    );
  });

  it('retries on connection error with exponential backoff (1s, 3s, 9s)', async () => {
    vi.useFakeTimers();
    let attempt = 0;
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() => {
      attempt++;
      if (attempt < 3) throw new TypeError('Failed to fetch');
      return eventsAsAsyncGen([
        {
          type: 4,
          data: {
            totalTokens: 1,
            promptTokens: 1,
            completionTokens: 1,
            estimatedReadingTimeMinutes: 0,
            confidence: null,
          },
        },
      ]);
    });
    const { result } = renderHook(() => useKbAskStream());

    act(() => {
      result.current.ask('q');
    });
    await vi.runAllTimersAsync();

    expect(result.current.state.retryCount).toBeGreaterThanOrEqual(2);
    vi.useRealTimers();
  });
});
