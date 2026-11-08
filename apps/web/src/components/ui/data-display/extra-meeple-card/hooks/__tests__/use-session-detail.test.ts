/**
 * useSessionDetail — #4156 (D4): the drawer loads a GameSession from `GET /api/v1/sessions/{id}`
 * and carries the id of the correlated LiveGameSession, under which the session pages live.
 */

import { renderHook, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { useSessionDetail } from '../use-session-detail';

function respondWith(json: Record<string, unknown>) {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve(json) })
  );
}

describe('useSessionDetail — liveSessionId', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('reads the GameSession and carries the id of its live session', async () => {
    respondWith({ id: 'game-session-1', liveSessionId: 'live-1', status: 'InProgress' });
    const { result } = renderHook(() => useSessionDetail('game-session-1'));
    await waitFor(() => expect(result.current.data).not.toBeNull());
    expect(fetch).toHaveBeenCalledWith('/api/v1/sessions/game-session-1', expect.anything());
    expect(result.current.data?.id).toBe('game-session-1');
    expect(result.current.data?.liveSessionId).toBe('live-1');
  });

  it('leaves liveSessionId absent when the GameSession has no live session', async () => {
    respondWith({ id: 'game-session-2', liveSessionId: null, status: 'Completed' });
    const { result } = renderHook(() => useSessionDetail('game-session-2'));
    await waitFor(() => expect(result.current.data).not.toBeNull());
    expect(result.current.data?.liveSessionId).toBeUndefined();
  });
});
