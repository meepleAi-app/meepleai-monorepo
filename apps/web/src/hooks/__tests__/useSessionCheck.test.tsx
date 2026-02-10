/**
 * useSessionCheck Hook Tests
 */
import { renderHook, act, waitFor } from '@testing-library/react';
import { useSessionCheck } from '../useSessionCheck';
import { api } from '@/lib/api';

// Mock api
vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      getSessionStatus: vi.fn(),
    },
  },
}));

// Mock useAuth to provide authenticated user context
vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({
    user: { id: 'user-1', email: 'test@test.com', displayName: 'Test', role: 'User' },
    loading: false,
  }),
}));

describe('useSessionCheck', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should check session on mount', async () => {
    vi.mocked(api.auth.getSessionStatus).mockResolvedValueOnce({
      remainingMinutes: 30,
    });

    const { result } = renderHook(() => useSessionCheck());

    await act(async () => {
      await vi.runOnlyPendingTimersAsync();
    });

    expect(result.current.remainingMinutes).toBe(30);
    expect(result.current.isNearExpiry).toBe(false);
  });

  it('should set isNearExpiry when less than 5 minutes', async () => {
    vi.mocked(api.auth.getSessionStatus).mockResolvedValueOnce({
      remainingMinutes: 3,
    });

    const { result } = renderHook(() => useSessionCheck());

    await act(async () => {
      await vi.runOnlyPendingTimersAsync();
    });

    expect(result.current.remainingMinutes).toBe(3);
    expect(result.current.isNearExpiry).toBe(true);
  });

  it('should handle null session (not authenticated)', async () => {
    vi.mocked(api.auth.getSessionStatus).mockResolvedValueOnce(null);

    const { result } = renderHook(() => useSessionCheck());

    await act(async () => {
      await vi.runOnlyPendingTimersAsync();
    });

    expect(result.current.remainingMinutes).toBeNull();
    expect(result.current.isNearExpiry).toBe(false);
  });

  it('should handle errors gracefully', async () => {
    // When API call throws, the hook catches the error
    vi.mocked(api.auth.getSessionStatus).mockRejectedValueOnce(new Error('Network error'));

    const { result } = renderHook(() => useSessionCheck());

    await act(async () => {
      await vi.runOnlyPendingTimersAsync();
    });

    // Hook should have caught the error
    expect(result.current.error).toBeInstanceOf(Error);
    // Could be either the original error or a wrapper
    expect(result.current.error).not.toBeNull();
  });

  it('should allow manual check with checkNow', async () => {
    vi.mocked(api.auth.getSessionStatus)
      .mockResolvedValueOnce({ remainingMinutes: 30 })
      .mockResolvedValueOnce({ remainingMinutes: 20 });

    const { result } = renderHook(() => useSessionCheck());

    // Wait for initial check
    await act(async () => {
      await vi.runOnlyPendingTimersAsync();
    });

    // Now manually check again
    await act(async () => {
      await result.current.checkNow();
    });

    // Should have the second value
    expect(result.current.remainingMinutes).toBe(20);
  });
});
