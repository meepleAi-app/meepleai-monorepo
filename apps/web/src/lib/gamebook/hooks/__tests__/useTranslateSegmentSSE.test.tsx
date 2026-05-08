import { renderHook, act } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';

import { useTranslateSegmentSSE } from '../useTranslateSegmentSSE';

// ---------------------------------------------------------------------------
// Fake EventSource
// ---------------------------------------------------------------------------

interface FakeEventSource {
  url: string;
  withCredentials: boolean;
  onmessage: ((ev: MessageEvent<string>) => void) | null;
  onerror: (() => void) | null;
  close: ReturnType<typeof vi.fn>;
  /** Simulate an incoming SSE message */
  simulateMessage: (data: unknown) => void;
  /** Simulate an error event */
  simulateError: () => void;
}

let lastInstance: FakeEventSource | null = null;

function FakeEventSourceConstructor(
  url: string,
  opts?: { withCredentials?: boolean }
): FakeEventSource {
  const instance: FakeEventSource = {
    url,
    withCredentials: opts?.withCredentials ?? false,
    onmessage: null,
    onerror: null,
    close: vi.fn(),
    simulateMessage(data: unknown) {
      this.onmessage?.({ data: JSON.stringify(data) } as MessageEvent<string>);
    },
    simulateError() {
      this.onerror?.();
    },
  };
  lastInstance = instance;
  return instance;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

const CAMPAIGN_ID = '11111111-1111-4111-a111-111111111111';
const PHOTO_ID = '22222222-2222-4222-a222-222222222222';
const PARA_ID = '33333333-3333-4333-a333-333333333333';

describe('useTranslateSegmentSSE', () => {
  beforeEach(() => {
    lastInstance = null;
    vi.stubGlobal('EventSource', FakeEventSourceConstructor);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  it('starts with empty initial state', () => {
    const { result } = renderHook(() => useTranslateSegmentSSE());
    expect(result.current.partialText).toBe('');
    expect(result.current.isComplete).toBe(false);
    expect(result.current.appliedTerms).toEqual([]);
    expect(result.current.error).toBeUndefined();
  });

  it('accumulates delta chunks', () => {
    const { result } = renderHook(() => useTranslateSegmentSSE());

    act(() => result.current.start(CAMPAIGN_ID, PHOTO_ID, 3));

    act(() => lastInstance!.simulateMessage({ delta: 'Ciao ', isComplete: false }));
    act(() => lastInstance!.simulateMessage({ delta: 'mondo', isComplete: false }));

    expect(result.current.partialText).toBe('Ciao mondo');
    expect(result.current.isComplete).toBe(false);
  });

  it('marks complete and captures paragraphId + appliedTerms on final chunk', () => {
    const { result } = renderHook(() => useTranslateSegmentSSE());

    act(() => result.current.start(CAMPAIGN_ID, PHOTO_ID, 3));
    act(() => lastInstance!.simulateMessage({ delta: 'Sei in una foresta', isComplete: false }));
    act(() =>
      lastInstance!.simulateMessage({
        delta: '.',
        isComplete: true,
        paragraphId: PARA_ID,
        appliedTerms: ['Dragon', 'Forest'],
      })
    );

    expect(result.current.partialText).toBe('Sei in una foresta.');
    expect(result.current.isComplete).toBe(true);
    expect(result.current.paragraphId).toBe(PARA_ID);
    expect(result.current.appliedTerms).toEqual(['Dragon', 'Forest']);
    expect(lastInstance!.close).toHaveBeenCalled();
  });

  it('sets error state on chunk with error field', () => {
    const { result } = renderHook(() => useTranslateSegmentSSE());

    act(() => result.current.start(CAMPAIGN_ID, PHOTO_ID, 1));
    act(() => lastInstance!.simulateMessage({ error: 'translation_failed' }));

    expect(result.current.error).toBe('translation_failed');
    expect(lastInstance!.close).toHaveBeenCalled();
  });

  it('sets stream_error on EventSource onerror', () => {
    const { result } = renderHook(() => useTranslateSegmentSSE());

    act(() => result.current.start(CAMPAIGN_ID, PHOTO_ID, 2));
    act(() => lastInstance!.simulateError());

    expect(result.current.error).toBe('stream_error');
  });

  it('stop() closes EventSource', () => {
    const { result } = renderHook(() => useTranslateSegmentSSE());

    act(() => result.current.start(CAMPAIGN_ID, PHOTO_ID, 5));
    const captured = lastInstance;
    act(() => result.current.stop());

    expect(captured?.close).toHaveBeenCalled();
  });

  it('resets state on new start() call', () => {
    const { result } = renderHook(() => useTranslateSegmentSSE());

    act(() => result.current.start(CAMPAIGN_ID, PHOTO_ID, 1));
    act(() => lastInstance!.simulateMessage({ delta: 'old text', isComplete: false }));

    act(() => result.current.start(CAMPAIGN_ID, PHOTO_ID, 2));

    expect(result.current.partialText).toBe('');
    expect(result.current.isComplete).toBe(false);
  });

  it('builds correct SSE URL with encoded params', () => {
    renderHook(() => useTranslateSegmentSSE());
    const { result } = renderHook(() => useTranslateSegmentSSE());

    act(() => result.current.start(CAMPAIGN_ID, PHOTO_ID, 7));

    expect(lastInstance?.url).toContain(`/campaigns/${CAMPAIGN_ID}/photos/translate`);
    expect(lastInstance?.url).toContain(`photoId=${PHOTO_ID}`);
    expect(lastInstance?.url).toContain('paragraphNumber=7');
  });
});
