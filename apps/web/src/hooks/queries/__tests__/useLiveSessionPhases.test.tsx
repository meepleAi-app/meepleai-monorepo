/**
 * @vitest-environment jsdom
 *
 * useLiveSessionPhases unit tests — #2787 Catan flavor turn/phase header.
 *
 * Coverage:
 *   - delegates to api.liveSessions.getPhases with the sessionId
 *   - exposes the TurnPhasesDto on success
 *   - defers the request when enabled=false / sessionId empty
 *   - surfaces a generic API error
 *   - maps a real HTTP 404 (NotFoundError) to null
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { NotFoundError } from '@/lib/api/core/errors';
import type { TurnPhasesDto } from '@/lib/api/schemas/live-sessions.schemas';

import { useLiveSessionPhases } from '../useLiveSessionPhases';

const getPhasesMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({
  api: { liveSessions: { getPhases: getPhasesMock } },
}));

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

const SESSION_ID = '11111111-1111-1111-1111-111111111111';

const SAMPLE_PHASES = {
  currentTurnIndex: 3,
  currentPhaseIndex: 1,
  currentPhaseName: 'Costruisci',
  phaseNames: ['Produzione', 'Costruisci', 'Commercio'],
  totalPhases: 3,
  hasPhases: true,
} as unknown as TurnPhasesDto;

describe('useLiveSessionPhases', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('delegates to api.liveSessions.getPhases and exposes the dto', async () => {
    getPhasesMock.mockResolvedValue(SAMPLE_PHASES);
    const { result } = renderHook(() => useLiveSessionPhases(SESSION_ID), {
      wrapper: createWrapper(),
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(getPhasesMock).toHaveBeenCalledWith(SESSION_ID);
    expect(result.current.data).toEqual(SAMPLE_PHASES);
  });

  it('does not fire when enabled is false', () => {
    renderHook(() => useLiveSessionPhases(SESSION_ID, false), { wrapper: createWrapper() });
    expect(getPhasesMock).not.toHaveBeenCalled();
  });

  it('does not fire when sessionId is empty', () => {
    renderHook(() => useLiveSessionPhases(''), { wrapper: createWrapper() });
    expect(getPhasesMock).not.toHaveBeenCalled();
  });

  it('surfaces the rejection on a generic API error', async () => {
    getPhasesMock.mockRejectedValue(new Error('phases service unavailable'));
    const { result } = renderHook(() => useLiveSessionPhases(SESSION_ID), {
      wrapper: createWrapper(),
    });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toBe('phases service unavailable');
  });

  it('maps a real HTTP 404 (NotFoundError) to null', async () => {
    getPhasesMock.mockRejectedValue(new NotFoundError({ message: 'no phases for session' }));
    const { result } = renderHook(() => useLiveSessionPhases(SESSION_ID), {
      wrapper: createWrapper(),
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toBeNull();
  });
});
