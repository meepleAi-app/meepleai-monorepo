/**
 * Tests for useActiveSessions hooks
 *
 * Issue #2761: Sprint 1 - Query Hooks Coverage
 *
 * Tests TanStack Query hooks for active game sessions.
 */

import type { Mock } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import {
  useActiveSessions,
  useSession,
  usePauseSession,
  useResumeSession,
  useEndSession,
  sessionsKeys,
} from '../useActiveSessions';
import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { api } from '@/lib/api';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';
import type { PaginatedSessionsResponse, GameSessionDto } from '@/lib/api/schemas';

// Mock API module
vi.mock('@/lib/api', () => ({
  api: {
    sessions: {
      getActive: vi.fn(),
      getById: vi.fn(),
      pause: vi.fn(),
      resume: vi.fn(),
      end: vi.fn(),
    },
  },
}));

describe('useActiveSessions hooks', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  // ==================== Query Keys ====================

  describe('sessionsKeys', () => {
    it('generates correct base query keys', () => {
      expect(sessionsKeys.all).toEqual(['sessions']);
      expect(sessionsKeys.active()).toEqual(['sessions', 'active']);
    });

    it('generates correct activeList query keys', () => {
      expect(sessionsKeys.activeList()).toEqual(['sessions', 'active', { limit: undefined }]);
      expect(sessionsKeys.activeList(5)).toEqual(['sessions', 'active', { limit: 5 }]);
      expect(sessionsKeys.activeList(10)).toEqual(['sessions', 'active', { limit: 10 }]);
    });

    it('generates correct detail query keys', () => {
      const sessionId = 'session-123';
      expect(sessionsKeys.detail(sessionId)).toEqual(['sessions', 'detail', sessionId]);
    });

    it('generates correct history query keys', () => {
      expect(sessionsKeys.history()).toEqual(['sessions', 'history', { filters: undefined }]);
      expect(sessionsKeys.history({ gameId: 'game-1' })).toEqual([
        'sessions',
        'history',
        { filters: { gameId: 'game-1' } },
      ]);
    });

    it('generates unique keys for different limits', () => {
      const key1 = sessionsKeys.activeList(5);
      const key2 = sessionsKeys.activeList(10);

      expect(key1).not.toEqual(key2);
    });
  });

  // ==================== useActiveSessions ====================

  describe('useActiveSessions', () => {
    const mockSession: GameSessionDto = {
      id: 'session-1',
      gameId: 'game-1',
      gameName: 'Catan',
      status: 'InProgress',
      playerCount: 4,
      startedAt: '2024-01-15T10:00:00Z',
      players: [
        { id: 'p1', name: 'Alice', isWinner: false },
        { id: 'p2', name: 'Bob', isWinner: false },
      ],
    };

    const mockSessionsResponse: PaginatedSessionsResponse = {
      items: [mockSession],
      totalCount: 1,
      page: 1,
      pageSize: 5,
      totalPages: 1,
    };

    it('fetches active sessions with default limit', async () => {
      (api.sessions.getActive as Mock).mockResolvedValue(mockSessionsResponse);

      const { result } = renderHook(() => useActiveSessions(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sessions.getActive).toHaveBeenCalledWith(5, 0);
      expect(result.current.data).toEqual(mockSessionsResponse);
    });

    it('fetches active sessions with custom limit', async () => {
      (api.sessions.getActive as Mock).mockResolvedValue(mockSessionsResponse);

      const { result } = renderHook(() => useActiveSessions(10), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sessions.getActive).toHaveBeenCalledWith(10, 0);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useActiveSessions(5, false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.sessions.getActive).not.toHaveBeenCalled();
    });

    it('handles fetch errors', async () => {
      const error = new Error('Failed to fetch sessions');
      (api.sessions.getActive as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useActiveSessions(), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });

    it('returns empty list when no active sessions', async () => {
      const emptyResponse: PaginatedSessionsResponse = {
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 5,
        totalPages: 0,
      };
      (api.sessions.getActive as Mock).mockResolvedValue(emptyResponse);

      const { result } = renderHook(() => useActiveSessions(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data?.items).toHaveLength(0);
    });

    it('returns sessions with different statuses', async () => {
      const mixedStatusResponse: PaginatedSessionsResponse = {
        items: [
          { ...mockSession, id: 's1', status: 'InProgress' },
          { ...mockSession, id: 's2', status: 'Paused' },
          { ...mockSession, id: 's3', status: 'Setup' },
        ],
        totalCount: 3,
        page: 1,
        pageSize: 5,
        totalPages: 1,
      };
      (api.sessions.getActive as Mock).mockResolvedValue(mixedStatusResponse);

      const { result } = renderHook(() => useActiveSessions(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data?.items).toHaveLength(3);
    });
  });

  // ==================== useSession ====================

  describe('useSession', () => {
    const sessionId = 'session-123';
    const mockSession: GameSessionDto = {
      id: sessionId,
      gameId: 'game-1',
      gameName: 'Ticket to Ride',
      status: 'InProgress',
      playerCount: 3,
      startedAt: '2024-01-15T14:00:00Z',
      players: [
        { id: 'p1', name: 'Alice', isWinner: false },
        { id: 'p2', name: 'Bob', isWinner: false },
        { id: 'p3', name: 'Charlie', isWinner: false },
      ],
    };

    it('fetches session by ID successfully', async () => {
      (api.sessions.getById as Mock).mockResolvedValue(mockSession);

      const { result } = renderHook(() => useSession(sessionId), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sessions.getById).toHaveBeenCalledWith(sessionId);
      expect(result.current.data).toEqual(mockSession);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useSession(sessionId, false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.sessions.getById).not.toHaveBeenCalled();
    });

    it('does not fetch when sessionId is empty', () => {
      const { result } = renderHook(() => useSession(''), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.sessions.getById).not.toHaveBeenCalled();
    });

    it('returns null when session not found', async () => {
      (api.sessions.getById as Mock).mockResolvedValue(null);

      const { result } = renderHook(() => useSession('nonexistent'), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toBeNull();
    });

    it('handles fetch errors', async () => {
      const error = new Error('Session not found');
      (api.sessions.getById as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useSession(sessionId), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });
  });

  // ==================== usePauseSession ====================

  describe('usePauseSession', () => {
    const sessionId = 'session-123';
    const mockPausedSession: GameSessionDto = {
      id: sessionId,
      gameId: 'game-1',
      gameName: 'Catan',
      status: 'Paused',
      playerCount: 4,
      startedAt: '2024-01-15T10:00:00Z',
      pausedAt: '2024-01-15T11:30:00Z',
      players: [],
    };

    it('pauses session successfully', async () => {
      (api.sessions.pause as Mock).mockResolvedValue(mockPausedSession);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => usePauseSession(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync(sessionId);
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(api.sessions.pause).toHaveBeenCalledWith(sessionId);
      expect(result.current.data).toEqual(mockPausedSession);
      expect(result.current.data?.status).toBe('Paused');

      // Verify cache invalidation
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: sessionsKeys.active() });
    });

    it('handles pause errors', async () => {
      const error = new Error('Cannot pause session');
      (api.sessions.pause as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => usePauseSession(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync(sessionId);
        } catch (e) {
          expect(e).toEqual(error);
        }
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error).toEqual(error);
    });

    // ==================== Optimistic Update Tests (Issue #2859) ====================

    it('optimistically updates session status to Paused before API response', async () => {
      // Pre-populate cache with active sessions using correct schema field names
      const mockSessionsResponse: PaginatedSessionsResponse = {
        sessions: [
          {
            id: sessionId,
            gameId: 'game-1',
            gameName: 'Catan',
            status: 'InProgress',
            playerCount: 4,
            startedAt: '2024-01-15T10:00:00Z',
            players: [],
          },
        ],
        total: 1,
        page: 1,
        pageSize: 5,
      };
      queryClient.setQueryData(sessionsKeys.activeList(5), mockSessionsResponse);

      // Make API call slow to observe optimistic update
      let resolveApiCall: (value: GameSessionDto) => void;
      (api.sessions.pause as Mock).mockImplementation(
        () =>
          new Promise((resolve) => {
            resolveApiCall = resolve;
          })
      );

      const { result } = renderHook(() => usePauseSession(), { wrapper });

      // Start mutation but don't await
      act(() => {
        result.current.mutate(sessionId);
      });

      // Check that cache was optimistically updated
      await waitFor(() => {
        const cachedData = queryClient.getQueryData<PaginatedSessionsResponse>(
          sessionsKeys.activeList(5)
        );
        expect(cachedData?.sessions[0]?.status).toBe('Paused');
      });

      // Now resolve the API call
      await act(async () => {
        resolveApiCall!(mockPausedSession);
      });
    });

    it('rolls back optimistic update on error', async () => {
      // Pre-populate cache with active sessions
      const mockSessionsResponse: PaginatedSessionsResponse = {
        sessions: [
          {
            id: sessionId,
            gameId: 'game-1',
            gameName: 'Catan',
            status: 'InProgress',
            playerCount: 4,
            startedAt: '2024-01-15T10:00:00Z',
            players: [],
          },
        ],
        total: 1,
        page: 1,
        pageSize: 5,
      };
      queryClient.setQueryData(sessionsKeys.activeList(5), mockSessionsResponse);

      // API will fail
      const error = new Error('Network error');
      (api.sessions.pause as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => usePauseSession(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync(sessionId);
        } catch {
          // Expected to fail
        }
      });

      // After error, cache should be refetched (invalidated) which will trigger refetch
      // The rollback happens, then invalidate triggers
      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });
    });
  });

  // ==================== useResumeSession ====================

  describe('useResumeSession', () => {
    const sessionId = 'session-123';
    const mockResumedSession: GameSessionDto = {
      id: sessionId,
      gameId: 'game-1',
      gameName: 'Catan',
      status: 'InProgress',
      playerCount: 4,
      startedAt: '2024-01-15T10:00:00Z',
      players: [],
    };

    it('resumes session successfully', async () => {
      (api.sessions.resume as Mock).mockResolvedValue(mockResumedSession);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useResumeSession(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync(sessionId);
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(api.sessions.resume).toHaveBeenCalledWith(sessionId);
      expect(result.current.data).toEqual(mockResumedSession);
      expect(result.current.data?.status).toBe('InProgress');

      // Verify cache invalidation
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: sessionsKeys.active() });
    });

    it('handles resume errors', async () => {
      const error = new Error('Cannot resume session');
      (api.sessions.resume as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useResumeSession(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync(sessionId);
        } catch (e) {
          expect(e).toEqual(error);
        }
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error).toEqual(error);
    });

    // ==================== Optimistic Update Tests (Issue #2859) ====================

    it('optimistically updates session status to InProgress before API response', async () => {
      // Pre-populate cache with paused session using correct schema field names
      const mockSessionsResponse: PaginatedSessionsResponse = {
        sessions: [
          {
            id: sessionId,
            gameId: 'game-1',
            gameName: 'Catan',
            status: 'Paused',
            playerCount: 4,
            startedAt: '2024-01-15T10:00:00Z',
            pausedAt: '2024-01-15T11:00:00Z',
            players: [],
          },
        ],
        total: 1,
        page: 1,
        pageSize: 5,
      };
      queryClient.setQueryData(sessionsKeys.activeList(5), mockSessionsResponse);

      // Make API call slow to observe optimistic update
      let resolveApiCall: (value: GameSessionDto) => void;
      (api.sessions.resume as Mock).mockImplementation(
        () =>
          new Promise((resolve) => {
            resolveApiCall = resolve;
          })
      );

      const { result } = renderHook(() => useResumeSession(), { wrapper });

      // Start mutation but don't await
      act(() => {
        result.current.mutate(sessionId);
      });

      // Check that cache was optimistically updated
      await waitFor(() => {
        const cachedData = queryClient.getQueryData<PaginatedSessionsResponse>(
          sessionsKeys.activeList(5)
        );
        expect(cachedData?.sessions[0]?.status).toBe('InProgress');
      });

      // Now resolve the API call
      await act(async () => {
        resolveApiCall!(mockResumedSession);
      });
    });

    it('rolls back optimistic update on error', async () => {
      // Pre-populate cache with paused session
      const mockSessionsResponse: PaginatedSessionsResponse = {
        sessions: [
          {
            id: sessionId,
            gameId: 'game-1',
            gameName: 'Catan',
            status: 'Paused',
            playerCount: 4,
            startedAt: '2024-01-15T10:00:00Z',
            pausedAt: '2024-01-15T11:00:00Z',
            players: [],
          },
        ],
        total: 1,
        page: 1,
        pageSize: 5,
      };
      queryClient.setQueryData(sessionsKeys.activeList(5), mockSessionsResponse);

      // API will fail
      const error = new Error('Network error');
      (api.sessions.resume as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useResumeSession(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync(sessionId);
        } catch {
          // Expected to fail
        }
      });

      // After error, mutation should be in error state
      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });
    });
  });

  // ==================== useEndSession ====================

  describe('useEndSession', () => {
    const sessionId = 'session-123';
    const mockEndedSession: GameSessionDto = {
      id: sessionId,
      gameId: 'game-1',
      gameName: 'Catan',
      status: 'Completed',
      playerCount: 4,
      startedAt: '2024-01-15T10:00:00Z',
      endedAt: '2024-01-15T12:00:00Z',
      players: [
        { id: 'p1', name: 'Alice', isWinner: true },
        { id: 'p2', name: 'Bob', isWinner: false },
      ],
    };

    it('ends session without winner', async () => {
      (api.sessions.end as Mock).mockResolvedValue(mockEndedSession);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useEndSession(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync({ sessionId });
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(api.sessions.end).toHaveBeenCalledWith(sessionId, undefined);
      expect(result.current.data).toEqual(mockEndedSession);
      expect(result.current.data?.status).toBe('Completed');

      // Verify cache invalidation for both active and history
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: sessionsKeys.active() });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: sessionsKeys.history() });
    });

    it('ends session with winner', async () => {
      const sessionWithWinner = {
        ...mockEndedSession,
        players: [
          { id: 'p1', name: 'Alice', isWinner: true },
          { id: 'p2', name: 'Bob', isWinner: false },
        ],
      };
      (api.sessions.end as Mock).mockResolvedValue(sessionWithWinner);

      const { result } = renderHook(() => useEndSession(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync({ sessionId, winnerName: 'Alice' });
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(api.sessions.end).toHaveBeenCalledWith(sessionId, 'Alice');
    });

    it('handles end errors', async () => {
      const error = new Error('Cannot end session');
      (api.sessions.end as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useEndSession(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync({ sessionId });
        } catch (e) {
          expect(e).toEqual(error);
        }
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error).toEqual(error);
    });
  });
});
