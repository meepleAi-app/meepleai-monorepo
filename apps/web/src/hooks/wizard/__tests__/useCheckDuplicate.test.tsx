/**
 * useCheckDuplicate - Tests
 * Issue #4167: Duplicate check hook tests
 */

import { createElement } from 'react';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import { useCheckDuplicate } from '../useCheckDuplicate';
import { api } from '@/lib/api';

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      checkBggDuplicate: vi.fn(),
    },
  },
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });

  return function Wrapper({ children }: { children: ReactNode }) {
    return createElement(QueryClientProvider, { client: queryClient }, children);
  };
}

describe('useCheckDuplicate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should check for duplicate by BGG ID', async () => {
    const mockResult = {
      isDuplicate: true,
      existingGameId: 'game-123',
      existingGame: {
        id: 'game-123',
        title: 'Existing Catan',
        bggId: 13,
      },
      bggData: null,
    };

    vi.mocked(api.sharedGames.checkBggDuplicate).mockResolvedValue(mockResult);

    const { result } = renderHook(() => useCheckDuplicate({ bggId: 13 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(api.sharedGames.checkBggDuplicate).toHaveBeenCalledWith(13);
    expect(result.current.data?.isDuplicate).toBe(true);
    expect(result.current.data?.existingGameId).toBe('game-123');
  });

  it('should return no duplicate if game not found', async () => {
    const mockResult = {
      isDuplicate: false,
      existingGameId: null,
      existingGame: null,
      bggData: null,
    };

    vi.mocked(api.sharedGames.checkBggDuplicate).mockResolvedValue(mockResult);

    const { result } = renderHook(() => useCheckDuplicate({ bggId: 999 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.isDuplicate).toBe(false);
  });

  it('should not query if bggId is null', () => {
    const { result } = renderHook(() => useCheckDuplicate({ bggId: null }), {
      wrapper: createWrapper(),
    });

    // Query is disabled, so it should not be pending
    expect(result.current.fetchStatus).toBe('idle');
    expect(api.sharedGames.checkBggDuplicate).not.toHaveBeenCalled();
  });

  it('should not query if disabled', () => {
    const { result } = renderHook(() => useCheckDuplicate({ bggId: 13, enabled: false }), {
      wrapper: createWrapper(),
    });

    // Query is disabled, so fetch status should be idle
    expect(result.current.fetchStatus).toBe('idle');
    expect(api.sharedGames.checkBggDuplicate).not.toHaveBeenCalled();
  });

  it('should handle API errors', async () => {
    vi.mocked(api.sharedGames.checkBggDuplicate).mockRejectedValue(new Error('Network error'));

    const { result } = renderHook(() => useCheckDuplicate({ bggId: 13 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toBe('Network error');
  });
});
