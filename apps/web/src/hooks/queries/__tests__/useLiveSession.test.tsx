/**
 * @vitest-environment jsdom
 *
 * useLiveSession unit tests — ADR-083 Fase 1 (Issue #2501).
 *
 * SessionLiveView must load the canonical LiveGameSession aggregate
 * (`/api/v1/live-sessions/{id}` → LiveSessionDto), NOT the empty GameSession
 * shell (`/api/v1/sessions/{id}` → GameSessionDto). This hook is the TanStack
 * Query loader for that aggregate.
 *
 * Coverage:
 *   - delegates to api.liveSessions.getSession with the sessionId
 *   - exposes the LiveSessionDto on success
 *   - defers the request when enabled=false
 *   - defers the request when sessionId is empty
 *   - surfaces the rejection on a generic API error
 *   - maps a "Session not found" rejection to null (FSM not-found contract)
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { NotFoundError } from '@/lib/api/core/errors';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';

import { liveSessionKeys, useLiveSession } from '../useLiveSession';

// ── Mocks ─────────────────────────────────────────────────────────────────────

const getSessionMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({
  api: { liveSessions: { getSession: getSessionMock } },
}));

// ── Helpers ───────────────────────────────────────────────────────────────────

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

const SESSION_ID = '11111111-1111-1111-1111-111111111111';

const SAMPLE_LIVE_SESSION = {
  id: SESSION_ID,
  sessionCode: 'ABC123',
  gameId: '22222222-2222-2222-2222-222222222222',
  gameName: 'Mage Knight',
  gameSlug: 'mage-knight',
  status: 'InProgress',
  players: [{ id: '33333333-3333-3333-3333-333333333333', displayName: 'Alice', totalScore: 12 }],
} as unknown as LiveSessionDto;

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useLiveSession — query key factory', () => {
  it('namespaces detail keys under liveSessions', () => {
    expect(liveSessionKeys.detail(SESSION_ID)).toEqual(['liveSessions', 'detail', SESSION_ID]);
  });
});

describe('useLiveSession — loading behaviour', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('delegates to api.liveSessions.getSession with the sessionId and exposes the dto', async () => {
    getSessionMock.mockResolvedValue(SAMPLE_LIVE_SESSION);

    const { result } = renderHook(() => useLiveSession(SESSION_ID), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(getSessionMock).toHaveBeenCalledWith(SESSION_ID);
    expect(result.current.data).toEqual(SAMPLE_LIVE_SESSION);
  });

  it('does not fire when enabled is false', () => {
    renderHook(() => useLiveSession(SESSION_ID, false), {
      wrapper: createWrapper(),
    });

    expect(getSessionMock).not.toHaveBeenCalled();
  });

  it('does not fire when sessionId is empty', () => {
    renderHook(() => useLiveSession(''), {
      wrapper: createWrapper(),
    });

    expect(getSessionMock).not.toHaveBeenCalled();
  });

  it('surfaces the rejection on a generic API error', async () => {
    getSessionMock.mockRejectedValue(new Error('Live session service unavailable'));

    const { result } = renderHook(() => useLiveSession(SESSION_ID), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe('Live session service unavailable');
  });

  it('maps a "Session not found" rejection to null (401→null client path)', async () => {
    // api.liveSessions.getSession throws Error('Session not found') when
    // httpClient.get returns null (the 401 path).
    getSessionMock.mockRejectedValue(new Error('Session not found'));

    const { result } = renderHook(() => useLiveSession(SESSION_ID), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toBeNull();
  });

  it('maps a real HTTP 404 (NotFoundError) to null (FSM not-found contract)', async () => {
    // A genuine 404 surfaces as a NotFoundError (ApiError, statusCode 404) whose
    // message comes from the backend body — NOT the literal 'Session not found'.
    getSessionMock.mockRejectedValue(
      new NotFoundError({ message: 'Live session with ID 1111 was not found' })
    );

    const { result } = renderHook(() => useLiveSession(SESSION_ID), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toBeNull();
  });
});
