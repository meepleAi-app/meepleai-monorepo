/**
 * useSessionQuota Hook Tests
 * Issue #3005: Frontend Test Coverage Improvement
 *
 * Tests for session quota TanStack Query hooks.
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';

import { api } from '@/lib/api';
import { useAuth } from '@/hooks/useAuth';
import type { SessionQuotaResponse } from '@/lib/api/schemas/session-quota.schemas';

import {
  useSessionQuota,
  useSessionQuotaWithStatus,
  usePrefetchSessionQuota,
  useInvalidateSessionQuota,
  sessionQuotaKeys,
} from '../useSessionQuota';

// Mock the API
vi.mock('@/lib/api', () => ({
  api: {
    sessions: {
      getQuota: vi.fn(),
    },
  },
}));

// Mock useAuth
vi.mock('@/hooks/useAuth', () => ({
  useAuth: vi.fn(),
}));

// Create wrapper with QueryClient
const createWrapper = (queryClient?: QueryClient) => {
  const client = queryClient ?? new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

  return ({ children }: { children: React.ReactNode }) => (
    React.createElement(QueryClientProvider, { client }, children)
  );
};

// Test fixtures
const createMockQuota = (overrides?: Partial<SessionQuotaResponse>): SessionQuotaResponse => ({
  currentSessions: 2,
  maxSessions: 5,
  remainingSlots: 3,
  canCreateNew: true,
  isUnlimited: false,
  userTier: 'normal',
  ...overrides,
});

describe('sessionQuotaKeys', () => {
  it('should generate correct key for all quota', () => {
    expect(sessionQuotaKeys.all).toEqual(['session-quota']);
  });

  it('should generate correct key for user quota', () => {
    expect(sessionQuotaKeys.user('user-123')).toEqual(['session-quota', 'user-123']);
  });
});

describe('useSessionQuota', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should fetch session quota for authenticated user', async () => {
    const mockQuota = createMockQuota();
    vi.mocked(useAuth).mockReturnValue({
      user: { id: 'user-123' },
      isAuthenticated: true,
    } as ReturnType<typeof useAuth>);
    vi.mocked(api.sessions.getQuota).mockResolvedValueOnce(mockQuota);

    const { result } = renderHook(() => useSessionQuota(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data).toEqual(mockQuota);
    expect(api.sessions.getQuota).toHaveBeenCalledWith('user-123');
  });

  it('should not fetch when user is not authenticated', () => {
    vi.mocked(useAuth).mockReturnValue({
      user: null,
      isAuthenticated: false,
    } as ReturnType<typeof useAuth>);

    const { result } = renderHook(() => useSessionQuota(), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(api.sessions.getQuota).not.toHaveBeenCalled();
  });

  it('should not fetch when disabled', () => {
    vi.mocked(useAuth).mockReturnValue({
      user: { id: 'user-123' },
      isAuthenticated: true,
    } as ReturnType<typeof useAuth>);

    const { result } = renderHook(() => useSessionQuota(false), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(api.sessions.getQuota).not.toHaveBeenCalled();
  });

  it('should handle fetch error', async () => {
    vi.mocked(useAuth).mockReturnValue({
      user: { id: 'user-123' },
      isAuthenticated: true,
    } as ReturnType<typeof useAuth>);
    vi.mocked(api.sessions.getQuota).mockRejectedValueOnce(new Error('Failed to fetch quota'));

    const { result } = renderHook(() => useSessionQuota(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    expect(result.current.error?.message).toBe('Failed to fetch quota');
  });

  it('should use user-specific query key', async () => {
    const mockQuota = createMockQuota();
    vi.mocked(useAuth).mockReturnValue({
      user: { id: 'user-456' },
      isAuthenticated: true,
    } as ReturnType<typeof useAuth>);
    vi.mocked(api.sessions.getQuota).mockResolvedValueOnce(mockQuota);

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });

    const { result } = renderHook(() => useSessionQuota(), {
      wrapper: createWrapper(queryClient),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    // Check that data is cached under the correct key
    const cachedData = queryClient.getQueryData(sessionQuotaKeys.user('user-456'));
    expect(cachedData).toEqual(mockQuota);
  });
});

describe('useSessionQuotaWithStatus', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should fetch quota with computed status values', async () => {
    // 4/5 = 80% which is warning level (75-89%)
    const mockQuota = createMockQuota({
      currentSessions: 4,
      maxSessions: 5,
      remainingSlots: 1,
      canCreateNew: true,
    });
    vi.mocked(useAuth).mockReturnValue({
      user: { id: 'user-123' },
      isAuthenticated: true,
    } as ReturnType<typeof useAuth>);
    vi.mocked(api.sessions.getQuota).mockResolvedValueOnce(mockQuota);

    const { result } = renderHook(() => useSessionQuotaWithStatus(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.currentSessions).toBe(4);
    expect(result.current.data?.maxSessions).toBe(5);
    expect(result.current.data?.percentageUsed).toBe(80);
    expect(result.current.data?.warningLevel).toBe('warning'); // 75-89% = warning
  });

  it('should return none warning level for low usage', async () => {
    const mockQuota = createMockQuota({
      currentSessions: 1,
      maxSessions: 10,
      remainingSlots: 9,
      canCreateNew: true,
    });
    vi.mocked(useAuth).mockReturnValue({
      user: { id: 'user-123' },
      isAuthenticated: true,
    } as ReturnType<typeof useAuth>);
    vi.mocked(api.sessions.getQuota).mockResolvedValueOnce(mockQuota);

    const { result } = renderHook(() => useSessionQuotaWithStatus(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.percentageUsed).toBe(10);
    expect(result.current.data?.warningLevel).toBe('none');
  });

  it('should return full warning level when quota exhausted', async () => {
    const mockQuota = createMockQuota({
      currentSessions: 5,
      maxSessions: 5,
      remainingSlots: 0,
      canCreateNew: false,
    });
    vi.mocked(useAuth).mockReturnValue({
      user: { id: 'user-123' },
      isAuthenticated: true,
    } as ReturnType<typeof useAuth>);
    vi.mocked(api.sessions.getQuota).mockResolvedValueOnce(mockQuota);

    const { result } = renderHook(() => useSessionQuotaWithStatus(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data?.percentageUsed).toBe(100);
    expect(result.current.data?.warningLevel).toBe('full');
  });

  it('should not fetch when disabled', () => {
    vi.mocked(useAuth).mockReturnValue({
      user: { id: 'user-123' },
      isAuthenticated: true,
    } as ReturnType<typeof useAuth>);

    const { result } = renderHook(() => useSessionQuotaWithStatus(false), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(api.sessions.getQuota).not.toHaveBeenCalled();
  });

  it('should not fetch when user is not authenticated', () => {
    vi.mocked(useAuth).mockReturnValue({
      user: null,
      isAuthenticated: false,
    } as ReturnType<typeof useAuth>);

    const { result } = renderHook(() => useSessionQuotaWithStatus(), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(api.sessions.getQuota).not.toHaveBeenCalled();
  });
});

describe('usePrefetchSessionQuota', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should return a prefetch function', () => {
    vi.mocked(useAuth).mockReturnValue({
      user: { id: 'user-123' },
      isAuthenticated: true,
    } as ReturnType<typeof useAuth>);

    const { result } = renderHook(() => usePrefetchSessionQuota(), {
      wrapper: createWrapper(),
    });

    expect(typeof result.current).toBe('function');
  });

  it('should prefetch quota when called', async () => {
    const mockQuota = createMockQuota();
    vi.mocked(useAuth).mockReturnValue({
      user: { id: 'user-123' },
      isAuthenticated: true,
    } as ReturnType<typeof useAuth>);
    vi.mocked(api.sessions.getQuota).mockResolvedValueOnce(mockQuota);

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });

    const { result } = renderHook(() => usePrefetchSessionQuota(), {
      wrapper: createWrapper(queryClient),
    });

    act(() => {
      result.current();
    });

    await waitFor(() => {
      expect(api.sessions.getQuota).toHaveBeenCalledWith('user-123');
    });
  });

  it('should not prefetch when user is not authenticated', () => {
    vi.mocked(useAuth).mockReturnValue({
      user: null,
      isAuthenticated: false,
    } as ReturnType<typeof useAuth>);

    const { result } = renderHook(() => usePrefetchSessionQuota(), {
      wrapper: createWrapper(),
    });

    act(() => {
      result.current();
    });

    expect(api.sessions.getQuota).not.toHaveBeenCalled();
  });
});

describe('useInvalidateSessionQuota', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should return an invalidate function', () => {
    const { result } = renderHook(() => useInvalidateSessionQuota(), {
      wrapper: createWrapper(),
    });

    expect(typeof result.current).toBe('function');
  });

  it('should invalidate quota queries when called', async () => {
    const mockQuota = createMockQuota();
    vi.mocked(useAuth).mockReturnValue({
      user: { id: 'user-123' },
      isAuthenticated: true,
    } as ReturnType<typeof useAuth>);
    vi.mocked(api.sessions.getQuota).mockResolvedValue(mockQuota);

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });

    // First, fetch quota
    const { result: quotaResult } = renderHook(() => useSessionQuota(), {
      wrapper: createWrapper(queryClient),
    });

    await waitFor(() => {
      expect(quotaResult.current.isSuccess).toBe(true);
    });

    expect(api.sessions.getQuota).toHaveBeenCalledTimes(1);

    // Then, invalidate
    const { result: invalidateResult } = renderHook(() => useInvalidateSessionQuota(), {
      wrapper: createWrapper(queryClient),
    });

    act(() => {
      invalidateResult.current();
    });

    // Query should be refetched after invalidation
    await waitFor(() => {
      expect(api.sessions.getQuota).toHaveBeenCalledTimes(2);
    });
  });
});
