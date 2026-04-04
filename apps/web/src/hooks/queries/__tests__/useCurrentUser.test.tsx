/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useCurrentUser, userKeys } from '../useCurrentUser';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('@/actions/auth', () => ({
  getCurrentUser: vi.fn(),
}));

import { getCurrentUser } from '@/actions/auth';

const mockGetCurrentUser = getCurrentUser as ReturnType<typeof vi.fn>;

// ── Helpers ───────────────────────────────────────────────────────────────────

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('userKeys', () => {
  it('returns stable query key for current user', () => {
    expect(userKeys.current()).toEqual(['user', 'current']);
  });
});

describe('useCurrentUser', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns user data on success', async () => {
    const mockUser = { id: 'u1', email: 'test@example.com', name: 'Test User' };
    mockGetCurrentUser.mockResolvedValue({ user: mockUser });

    const { result } = renderHook(() => useCurrentUser(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(mockUser);
  });

  it('returns null when user is not authenticated', async () => {
    mockGetCurrentUser.mockResolvedValue({ user: undefined });

    const { result } = renderHook(() => useCurrentUser(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toBeNull();
  });

  it('enters error state when getCurrentUser throws a 401', async () => {
    // The hook's retry function skips retries on 401 errors
    mockGetCurrentUser.mockRejectedValue(new Error('401 Unauthorized'));

    const { result } = renderHook(() => useCurrentUser(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe('401 Unauthorized');
  });

  it('starts in loading state', () => {
    mockGetCurrentUser.mockReturnValue(new Promise(() => {})); // never resolves

    const { result } = renderHook(() => useCurrentUser(), { wrapper: createWrapper() });

    expect(result.current.isLoading).toBe(true);
  });
});
