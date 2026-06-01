import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { useTranslateTextSSE } from '../useTranslateTextSSE';

const CAMPAIGN_ID = '11111111-1111-4111-a111-111111111111';
const BOOK_ID = '22222222-2222-4222-a222-222222222222';

function makeSseResponse(events: string[]): Response {
  const encoder = new TextEncoder();
  let i = 0;
  const stream = new ReadableStream({
    pull(controller) {
      if (i < events.length) {
        controller.enqueue(encoder.encode(`data: ${events[i++]}\n\n`));
      } else {
        controller.close();
      }
    },
  });
  return new Response(stream, { status: 200, headers: { 'Content-Type': 'text/event-stream' } });
}

describe('useTranslateTextSSE', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('starts with initial empty state', () => {
    vi.stubGlobal('fetch', vi.fn());
    const { result } = renderHook(() => useTranslateTextSSE());
    expect(result.current.partialText).toBe('');
    expect(result.current.isComplete).toBe(false);
    expect(result.current.appliedTerms).toEqual([]);
    expect(result.current.error).toBeUndefined();
  });

  it('accumulates delta chunks and emits final', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        makeSseResponse([
          JSON.stringify({ delta: 'Ciao ', isComplete: false }),
          JSON.stringify({ delta: 'mondo.', isComplete: false }),
          JSON.stringify({
            delta: '',
            isComplete: true,
            paragraphId: null,
            appliedTerms: [],
            detectedSourceLang: 'EN',
            langDetectionConfidence: null,
          }),
        ])
      );
    vi.stubGlobal('fetch', fetchMock);

    const { result } = renderHook(() => useTranslateTextSSE());
    await act(async () => {
      result.current.start(CAMPAIGN_ID, 'Hello world.', 'EN', BOOK_ID);
    });

    await waitFor(() => expect(result.current.isComplete).toBe(true));
    expect(result.current.partialText).toBe('Ciao mondo.');
    expect(result.current.detectedSourceLang).toBe('EN');
    expect(result.current.langDetectionConfidence).toBeNull();
  });

  it('builds POST request with correct body', async () => {
    const fetchMock = vi.fn().mockResolvedValue(makeSseResponse([]));
    vi.stubGlobal('fetch', fetchMock);

    const { result } = renderHook(() => useTranslateTextSSE());
    await act(async () => {
      result.current.start(CAMPAIGN_ID, 'Hello.', 'FR', BOOK_ID);
    });

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    const [url, opts] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain(`/campaigns/${CAMPAIGN_ID}/text/translate`);
    expect(opts.method).toBe('POST');
    expect(opts.credentials).toBe('include');
    expect(JSON.parse(opts.body as string)).toEqual({
      text: 'Hello.',
      sourceLang: 'FR',
      targetLang: 'IT',
      gameBookId: BOOK_ID,
    });
  });

  it('handles HTTP 400 error response', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('Validation', { status: 400 })));
    const { result } = renderHook(() => useTranslateTextSSE());
    await act(async () => {
      result.current.start(CAMPAIGN_ID, '', 'EN', BOOK_ID);
    });
    await waitFor(() => expect(result.current.error).toBeTruthy());
  });

  it('handles HTTP 403 ownership error', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 403 })));
    const { result } = renderHook(() => useTranslateTextSSE());
    await act(async () => {
      result.current.start(CAMPAIGN_ID, 'Hello.', 'EN', BOOK_ID);
    });
    await waitFor(() => expect(result.current.error).toBe('forbidden'));
  });

  it('handles fetch network error', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('Network error')));
    const { result } = renderHook(() => useTranslateTextSSE());
    await act(async () => {
      result.current.start(CAMPAIGN_ID, 'Hello.', 'EN', BOOK_ID);
    });
    await waitFor(() => expect(result.current.error).toBeTruthy());
  });

  it('stop() aborts via AbortController', async () => {
    const fetchMock = vi.fn().mockImplementation(() => new Promise(() => {})); // never resolves
    vi.stubGlobal('fetch', fetchMock);

    const { result } = renderHook(() => useTranslateTextSSE());
    await act(async () => {
      result.current.start(CAMPAIGN_ID, 'Hello.', 'EN', BOOK_ID);
    });
    act(() => result.current.stop());

    const [, opts] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect((opts.signal as AbortSignal).aborted).toBe(true);
  });
});
