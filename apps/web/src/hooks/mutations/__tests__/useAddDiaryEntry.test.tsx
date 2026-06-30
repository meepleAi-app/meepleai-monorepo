/**
 * Tests for useAddDiaryEntry (Issue #2575).
 *
 * Covers:
 *   - mutate calls api.liveSessions.addDiary with the correct sessionId + { text }
 *   - returns the new entry id
 *   - success → invalidates liveSessionKeys.detail(sessionId)
 *   - error → does NOT invalidate queries
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import type { JSX, ReactNode } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { useAddDiaryEntry } from '../useAddDiaryEntry';
import { liveSessionKeys } from '@/hooks/queries/useLiveSession';

// ---------------------------------------------------------------------------
// Mock @/lib/api
// ---------------------------------------------------------------------------

const addDiaryMock = vi.fn<[string, { text: string }], Promise<string>>();

vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      addDiary: (sessionId: string, request: { text: string }) => addDiaryMock(sessionId, request),
    },
  },
}));

function makeWrapper(qc: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }): JSX.Element {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  };
}

const SESSION_ID = '00000000-0000-4000-8000-000000002575';
const NEW_ENTRY_ID = '11111111-1111-4111-8111-111111112575';

describe('useAddDiaryEntry (#2575)', () => {
  beforeEach(() => {
    addDiaryMock.mockReset();
  });

  it('calls api.liveSessions.addDiary with the sessionId and { text }, returning the new id', async () => {
    addDiaryMock.mockResolvedValueOnce(NEW_ENTRY_ID);

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useAddDiaryEntry(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    let returned: string | undefined;
    await act(async () => {
      returned = await result.current.mutateAsync({ text: 'A diary note' });
    });

    expect(addDiaryMock).toHaveBeenCalledOnce();
    expect(addDiaryMock).toHaveBeenCalledWith(SESSION_ID, { text: 'A diary note' });
    expect(returned).toBe(NEW_ENTRY_ID);
  });

  it('invalidates liveSessionKeys.detail on success', async () => {
    addDiaryMock.mockResolvedValueOnce(NEW_ENTRY_ID);

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');

    const { result } = renderHook(() => useAddDiaryEntry(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      await result.current.mutateAsync({ text: 'note' });
    });

    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({ queryKey: liveSessionKeys.detail(SESSION_ID) })
    );
  });

  it('does NOT invalidate queries on error', async () => {
    addDiaryMock.mockRejectedValueOnce(new Error('Network error'));

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');

    const { result } = renderHook(() => useAddDiaryEntry(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      try {
        await result.current.mutateAsync({ text: 'note' });
      } catch {
        // expected
      }
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(invalidateSpy).not.toHaveBeenCalled();
  });
});
