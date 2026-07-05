/**
 * @vitest-environment jsdom
 *
 * useCompleteGameNightSession unit tests — #2634 C4.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ConflictError } from '@/lib/api/core/errors';

import { useCompleteGameNightSession } from '../useCompleteGameNightSession';

const completeSessionMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api/clients/gameNightSessionClient', () => ({
  gameNightSessionClient: { completeSession: completeSessionMock },
}));

const GN = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

function wrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  };
}

describe('useCompleteGameNightSession', () => {
  beforeEach(() => vi.clearAllMocks());

  it('completes with the winner participant id', async () => {
    completeSessionMock.mockResolvedValue(undefined);
    const { result } = renderHook(() => useCompleteGameNightSession(GN), { wrapper: wrapper() });

    result.current.mutate({ winnerId: 'participant-1' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(completeSessionMock).toHaveBeenCalledWith(GN, 'participant-1');
  });

  it('completes with no winner (undefined)', async () => {
    completeSessionMock.mockResolvedValue(undefined);
    const { result } = renderHook(() => useCompleteGameNightSession(GN), { wrapper: wrapper() });

    result.current.mutate({});

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(completeSessionMock).toHaveBeenCalledWith(GN, undefined);
  });

  it('surfaces the error so the view can discriminate 409 vs 403', async () => {
    completeSessionMock.mockRejectedValue(new ConflictError({ message: 'no live session' }));
    const { result } = renderHook(() => useCompleteGameNightSession(GN), { wrapper: wrapper() });

    result.current.mutate({});

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeInstanceOf(ConflictError);
  });
});
