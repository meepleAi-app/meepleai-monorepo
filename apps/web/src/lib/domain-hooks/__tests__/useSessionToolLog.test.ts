import { act, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { useSessionToolLog } from '../useSessionToolLog';

describe('useSessionToolLog', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('POSTs a ToolAction event with correct body', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true } as Response);
    vi.stubGlobal('fetch', fetchMock);

    const { result } = renderHook(() => useSessionToolLog('sess-abc'));

    act(() => {
      result.current.logToolAction('dice', 'roll', 'D6 → 4');
    });

    // fire-and-forget: wait for microtask
    await Promise.resolve();

    expect(fetchMock).toHaveBeenCalledOnce();
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain('/api/v1/game-sessions/sess-abc/events');
    expect(init.method).toBe('POST');

    const body = JSON.parse(init.body as string) as Record<string, unknown>;
    expect(body.eventType).toBe('ToolAction');
    expect(body.source).toBe('toolkit');

    const payload = JSON.parse(body.payload as string) as Record<string, unknown>;
    expect(payload.toolType).toBe('dice');
    expect(payload.action).toBe('roll');
    expect(payload.result).toBe('D6 → 4');
  });

  it('does not throw when fetch rejects', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('network')));
    const { result } = renderHook(() => useSessionToolLog('sess-abc'));

    expect(() =>
      act(() => {
        result.current.logToolAction('timer', 'start', '60');
      })
    ).not.toThrow();

    await Promise.resolve();
  });

  it('encodes sessionId in URL', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true } as Response);
    vi.stubGlobal('fetch', fetchMock);

    const { result } = renderHook(() => useSessionToolLog('id with spaces'));

    act(() => {
      result.current.logToolAction('card', 'draw', 'As');
    });

    await Promise.resolve();

    const [url] = fetchMock.mock.calls[0] as [string];
    expect(url).toContain(encodeURIComponent('id with spaces'));
  });

  it('returns stable logToolAction reference across rerenders', () => {
    const { result, rerender } = renderHook(() => useSessionToolLog('sess-abc'));
    const first = result.current.logToolAction;
    rerender();
    expect(result.current.logToolAction).toBe(first);
  });
});
