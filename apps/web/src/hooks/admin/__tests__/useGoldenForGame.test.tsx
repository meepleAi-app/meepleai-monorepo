/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useGoldenForGame } from '../useGoldenForGame';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getGolden: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';

const mockGetGolden = api.admin.getGolden as ReturnType<typeof vi.fn>;

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

describe('useGoldenForGame', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns golden-set data on success', async () => {
    const mockResponse = {
      sharedGameId: '11111111-1111-1111-1111-111111111111',
      versionHash: 'abc123',
      claims: [],
      bggTags: [],
    };
    mockGetGolden.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useGoldenForGame('11111111-1111-1111-1111-111111111111'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(mockResponse);
    expect(mockGetGolden).toHaveBeenCalledWith('11111111-1111-1111-1111-111111111111');
  });

  it('does not fetch when sharedGameId is null', () => {
    const { result } = renderHook(() => useGoldenForGame(null), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockGetGolden).not.toHaveBeenCalled();
  });
});
