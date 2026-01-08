/**
 * Tests for useAuthUser Hook (Issue #2340)
 *
 * Coverage target: 90%+
 * Tests: Auth user fetching, loading states, error handling
 */

import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';
import { useAuthUser } from '../useAuthUser';
import { getCurrentUser } from '@/actions/auth';
import type { AuthUser } from '@/types/auth';

// Mock server action
vi.mock('@/actions/auth');
const mockGetCurrentUser = getCurrentUser as MockedFunction<typeof getCurrentUser>;

describe('useAuthUser', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should start with loading=true', () => {
    mockGetCurrentUser.mockImplementation(() => new Promise(() => {})); // Never resolves

    const { result } = renderHook(() => useAuthUser());

    expect(result.current.loading).toBe(true);
    expect(result.current.user).toBeNull();
    expect(result.current.error).toBeNull();
  });

  it('should load user successfully', async () => {
    const mockUser: AuthUser = {
      id: 'user-123',
      email: 'test@example.com',
      displayName: 'Test User',
      role: 'User',
      isActive: true,
      requirePasswordChange: false,
      isTwoFactorEnabled: false,
    };

    mockGetCurrentUser.mockResolvedValueOnce({
      success: true,
      user: mockUser,
    });

    const { result } = renderHook(() => useAuthUser());

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.user).toEqual(mockUser);
    expect(result.current.error).toBeNull();
  });

  it('should handle user not found', async () => {
    mockGetCurrentUser.mockResolvedValueOnce({
      success: false,
      error: { message: 'User not authenticated' },
    });

    const { result } = renderHook(() => useAuthUser());

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.user).toBeNull();
    expect(result.current.error).toBe('User not authenticated');
  });

  it('should handle error without message', async () => {
    mockGetCurrentUser.mockResolvedValueOnce({
      success: false,
    });

    const { result } = renderHook(() => useAuthUser());

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.user).toBeNull();
    expect(result.current.error).toBe('Failed to get user');
  });

  it('should handle exceptions', async () => {
    mockGetCurrentUser.mockRejectedValueOnce(new Error('Network error'));

    const { result } = renderHook(() => useAuthUser());

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.user).toBeNull();
    expect(result.current.error).toBe('Network error');
  });

  it('should handle non-Error exceptions', async () => {
    mockGetCurrentUser.mockRejectedValueOnce('String error');

    const { result } = renderHook(() => useAuthUser());

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.user).toBeNull();
    expect(result.current.error).toBe('Unknown error');
  });
});
