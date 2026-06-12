/**
 * Phase E F4 — useWikidataEnrichmentEvents hook unit tests. Issue #1823.
 */

import { renderHook, act } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useWikidataEnrichmentEvents } from '../useWikidataEnrichmentEvents';

type EventListener = (event: MessageEvent) => void;

class MockEventSource {
  static instances: MockEventSource[] = [];
  url: string;
  withCredentials: boolean;
  readyState = 0; // CONNECTING
  onopen: ((e: Event) => void) | null = null;
  onerror: ((e: Event) => void) | null = null;
  private listeners: Record<string, EventListener[]> = {};

  constructor(url: string, init?: EventSourceInit) {
    this.url = url;
    this.withCredentials = init?.withCredentials ?? false;
    MockEventSource.instances.push(this);
  }

  addEventListener(type: string, listener: EventListener) {
    (this.listeners[type] ??= []).push(listener);
  }

  removeEventListener(type: string, listener: EventListener) {
    const arr = this.listeners[type];
    if (!arr) return;
    const idx = arr.indexOf(listener);
    if (idx >= 0) arr.splice(idx, 1);
  }

  emitEvent(type: string, data: string) {
    const msg = { data, type } as MessageEvent;
    for (const l of this.listeners[type] ?? []) l(msg);
  }

  emitOpen() {
    this.readyState = 1;
    this.onopen?.(new Event('open'));
  }

  emitError() {
    this.onerror?.(new Event('error'));
  }

  close() {
    this.readyState = 2;
  }
}

describe('useWikidataEnrichmentEvents (Phase E F4)', () => {
  beforeEach(() => {
    MockEventSource.instances = [];
    vi.stubGlobal('EventSource', MockEventSource as unknown as typeof EventSource);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('opens an EventSource against the BE SSE endpoint with credentials', () => {
    renderHook(() => useWikidataEnrichmentEvents());

    expect(MockEventSource.instances).toHaveLength(1);
    expect(MockEventSource.instances[0]!.url).toBe('/api/v1/admin/wikidata/enrichment/events');
    expect(MockEventSource.instances[0]!.withCredentials).toBe(true);
  });

  it('transitions to open on `onopen` and exposes the last attempt-recorded payload', () => {
    const { result } = renderHook(() => useWikidataEnrichmentEvents());
    const source = MockEventSource.instances[0]!;

    expect(result.current.state).toBe('connecting');

    act(() => source.emitOpen());
    expect(result.current.state).toBe('open');

    const payload = {
      attemptId: 'a1',
      sharedGameId: 'g1',
      outcome: 'DeadLetter',
      reason: 'r2-upload-error',
      attemptedAt: '2026-06-12T10:00:00Z',
      retryCount: 3,
      nextRetryAt: null,
      deadLetteredAt: '2026-06-12T10:00:00Z',
    };
    act(() => source.emitEvent('attempt-recorded', JSON.stringify(payload)));

    expect(result.current.lastEvent).toEqual(payload);
  });

  it('flips state back to connecting on `onerror` (UA auto-reconnect)', () => {
    const { result } = renderHook(() => useWikidataEnrichmentEvents());
    const source = MockEventSource.instances[0]!;
    act(() => source.emitOpen());
    expect(result.current.state).toBe('open');

    act(() => source.emitError());
    expect(result.current.state).toBe('connecting');
  });

  it('silently ignores malformed `attempt-recorded` payloads', () => {
    const { result } = renderHook(() => useWikidataEnrichmentEvents());
    const source = MockEventSource.instances[0]!;

    act(() => source.emitEvent('attempt-recorded', '{not valid json'));

    expect(result.current.lastEvent).toBeNull();
  });

  it('closes the EventSource on unmount', () => {
    const { unmount } = renderHook(() => useWikidataEnrichmentEvents());
    const source = MockEventSource.instances[0]!;
    const closeSpy = vi.spyOn(source, 'close');

    unmount();
    expect(closeSpy).toHaveBeenCalledTimes(1);
  });
});
