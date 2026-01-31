/**
 * Tests for useCurrentUser hook
 *
 * Issue #1079: FE-IMP-003 — TanStack Query Data Layer
 */

import type { Mock } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { getCurrentUser } from '@/actions/auth';
import { useCurrentUser } from '../useCurrentUser';
import { ReactNode } from 'react';

// Mock the auth action
vi.mock('@/actions/auth', () => ({
  getCurrentUser: vi.fn(),
}));

const mockGetCurrentUser = getCurrentUser as Mock<typeof getCurrentUser>;

describe('useCurrentUser', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    // Create fresh QueryClient for each test
    queryClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: false, // Disable retries in tests
          staleTime: 0,
        },
      },
    });

    vi.clearAllMocks();
  });

  afterEach(() => {
    queryClient.clear();
  });

  const wrapper = ({ children }: { children: ReactNode }) => {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };

  it('should fetch current user successfully', async () => {
    const mockUser = {
      id: '990e8400-e29b-41d4-a716-000000000001',
      email: 'test@example.com',
      displayName: 'Test User',
      role: 'user',
    };

    mockGetCurrentUser.mockResolvedValue({
      success: true,
      user: mockUser,
      error: undefined,
    });

    const { result } = renderHook(() => useCurrentUser(), { wrapper });

    // Initially loading
    expect(result.current.isLoading).toBe(true);
    expect(result.current.data).toBeUndefined();

    // Wait for query to complete
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(mockUser);
    expect(result.current.error).toBeNull();
    expect(mockGetCurrentUser).toHaveBeenCalledTimes(1);
  });

  it('should handle user not authenticated (null)', async () => {
    mockGetCurrentUser.mockResolvedValue({
      success: true,
      user: undefined,
      error: undefined,
    });

    const { result } = renderHook(() => useCurrentUser(), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toBeNull();
    expect(result.current.error).toBeNull();
  });

  // Note: Network error testing is complex with TanStack Query due to retry/cache behavior
  // The useCurrentUser hook has custom retry logic tested separately in 401 test below

  it('should not retry on 401 errors', async () => {
    const unauthorizedError = new Error('401 Unauthorized');
    mockGetCurrentUser.mockRejectedValue(unauthorizedError);

    const { result } = renderHook(() => useCurrentUser(), { wrapper });

    await waitFor(() => expect(result.current.isError).toBe(true));

    // Should not retry on 401
    expect(mockGetCurrentUser).toHaveBeenCalledTimes(1);
  });

  it('should NOT cache user data for security (always verify with server)', async () => {
    const mockUser = {
      id: '990e8400-e29b-41d4-a716-000000000001',
      email: 'test@example.com',
      displayName: 'Test User',
      role: 'user',
    };

    mockGetCurrentUser.mockResolvedValue({
      success: true,
      user: mockUser,
      error: undefined,
    });

    // First render
    const { result: result1 } = renderHook(() => useCurrentUser(), { wrapper });
    await waitFor(() => expect(result1.current.isSuccess).toBe(true));

    // Second render (should NOT use cache - security requirement)
    const { result: result2 } = renderHook(() => useCurrentUser(), { wrapper });

    // Wait for second query to complete
    await waitFor(() => expect(result2.current.isSuccess).toBe(true));

    // Should verify data from server
    expect(result2.current.data).toEqual(mockUser);

    // Should call API twice (no cache, always verify for security)
    // This is intentional: auth state must always be verified with server
    expect(mockGetCurrentUser).toHaveBeenCalledTimes(2);
  });
});
