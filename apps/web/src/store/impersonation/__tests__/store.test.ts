/**
 * Impersonation Store Tests (Issue #3700)
 *
 * Tests for the Zustand impersonation store:
 * - Initial state
 * - startImpersonation (success/failure)
 * - endImpersonation (success/failure)
 * - clearError
 * - reset
 * - Selectors
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act } from '@testing-library/react';

import {
  useImpersonationStore,
  selectIsImpersonating,
  selectImpersonatedUser,
  selectImpersonationLoading,
  selectImpersonationError,
} from '../store';

// Mock the API
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      impersonateUser: vi.fn(),
      endImpersonation: vi.fn(),
    },
  },
}));

// Mock the logger
vi.mock('@/lib/logger', () => ({
  logger: {
    info: vi.fn(),
    error: vi.fn(),
  },
}));

// Import mocked modules
import { api } from '@/lib/api';

// Save original location
const originalLocation = window.location;

describe('ImpersonationStore', () => {
  beforeEach(() => {
    // Reset store state
    act(() => {
      useImpersonationStore.getState().reset();
    });

    // Reset mocks
    vi.clearAllMocks();

    // Mock document.cookie
    Object.defineProperty(document, 'cookie', {
      writable: true,
      value: 'session_token=original-admin-token',
    });

    // Mock window.location
    Object.defineProperty(window, 'location', {
      writable: true,
      value: {
        ...originalLocation,
        reload: vi.fn(),
        href: '',
      },
    });
  });

  afterEach(() => {
    Object.defineProperty(window, 'location', {
      writable: true,
      value: originalLocation,
    });
  });

  describe('initial state', () => {
    it('should have correct initial state', () => {
      const state = useImpersonationStore.getState();

      expect(state.isImpersonating).toBe(false);
      expect(state.impersonatedUser).toBeNull();
      expect(state.sessionId).toBeNull();
      expect(state.originalSessionToken).toBeNull();
      expect(state.expiresAt).toBeNull();
      expect(state.isLoading).toBe(false);
      expect(state.error).toBeNull();
    });
  });

  describe('startImpersonation', () => {
    it('should start impersonation successfully', async () => {
      const mockResponse = {
        sessionToken: 'impersonation-token-abc',
        impersonatedUserId: 'user-123',
        expiresAt: '2026-02-12T00:00:00Z',
      };
      vi.mocked(api.admin.impersonateUser).mockResolvedValue(mockResponse);

      const userInfo = { displayName: 'John Doe', email: 'john@example.com' };

      let result: boolean;
      await act(async () => {
        result = await useImpersonationStore.getState().startImpersonation('user-123', userInfo);
      });

      expect(result!).toBe(true);
      expect(api.admin.impersonateUser).toHaveBeenCalledWith('user-123');

      const state = useImpersonationStore.getState();
      expect(state.isImpersonating).toBe(true);
      expect(state.impersonatedUser).toEqual({
        id: 'user-123',
        displayName: 'John Doe',
        email: 'john@example.com',
      });
      expect(state.originalSessionToken).toBe('original-admin-token');
      expect(state.expiresAt).toBe('2026-02-12T00:00:00Z');
      expect(state.isLoading).toBe(false);
      expect(state.error).toBeNull();
    });

    it('should set loading state during impersonation', async () => {
      let resolvePromise: (value: unknown) => void;
      const pendingPromise = new Promise((resolve) => {
        resolvePromise = resolve;
      });
      vi.mocked(api.admin.impersonateUser).mockReturnValue(pendingPromise as never);

      // Start the impersonation (don't await)
      act(() => {
        useImpersonationStore.getState().startImpersonation('user-123', {
          displayName: 'John',
          email: 'john@test.com',
        });
      });

      // Should be loading
      expect(useImpersonationStore.getState().isLoading).toBe(true);

      // Resolve
      await act(async () => {
        resolvePromise!({
          sessionToken: 'token',
          impersonatedUserId: 'user-123',
          expiresAt: '2026-02-12T00:00:00Z',
        });
      });
    });

    it('should handle API error during impersonation', async () => {
      vi.mocked(api.admin.impersonateUser).mockRejectedValue(
        new Error('User not found')
      );

      let result: boolean;
      await act(async () => {
        result = await useImpersonationStore.getState().startImpersonation('user-999', {
          displayName: 'Unknown',
          email: 'unknown@test.com',
        });
      });

      expect(result!).toBe(false);
      const state = useImpersonationStore.getState();
      expect(state.isImpersonating).toBe(false);
      expect(state.isLoading).toBe(false);
      expect(state.error).toBe('User not found');
    });

    it('should handle non-Error exceptions', async () => {
      vi.mocked(api.admin.impersonateUser).mockRejectedValue('unexpected error');

      let result: boolean;
      await act(async () => {
        result = await useImpersonationStore.getState().startImpersonation('user-123', {
          displayName: 'Test',
          email: 'test@test.com',
        });
      });

      expect(result!).toBe(false);
      expect(useImpersonationStore.getState().error).toBe('Failed to start impersonation');
    });
  });

  describe('endImpersonation', () => {
    beforeEach(async () => {
      // Set up an active impersonation state
      act(() => {
        useImpersonationStore.setState({
          isImpersonating: true,
          impersonatedUser: { id: 'user-123', displayName: 'John', email: 'john@test.com' },
          sessionId: 'user-123',
          originalSessionToken: 'original-admin-token',
          expiresAt: '2026-02-12T00:00:00Z',
          isLoading: false,
          error: null,
        });
      });
    });

    it('should end impersonation successfully', async () => {
      vi.mocked(api.admin.endImpersonation).mockResolvedValue({
        success: true,
        message: 'Impersonation ended',
      });

      let result: boolean;
      await act(async () => {
        result = await useImpersonationStore.getState().endImpersonation();
      });

      expect(result!).toBe(true);
      expect(api.admin.endImpersonation).toHaveBeenCalledWith('user-123');

      const state = useImpersonationStore.getState();
      expect(state.isImpersonating).toBe(false);
      expect(state.impersonatedUser).toBeNull();
      expect(state.sessionId).toBeNull();
    });

    it('should restore original session token', async () => {
      vi.mocked(api.admin.endImpersonation).mockResolvedValue({
        success: true,
        message: 'Done',
      });

      await act(async () => {
        await useImpersonationStore.getState().endImpersonation();
      });

      expect(document.cookie).toContain('session_token=original-admin-token');
    });

    it('should do nothing if not impersonating', async () => {
      act(() => {
        useImpersonationStore.getState().reset();
      });

      let result: boolean;
      await act(async () => {
        result = await useImpersonationStore.getState().endImpersonation();
      });

      expect(result!).toBe(true);
      expect(api.admin.endImpersonation).not.toHaveBeenCalled();
    });

    it('should handle API error during end', async () => {
      vi.mocked(api.admin.endImpersonation).mockRejectedValue(
        new Error('Session expired')
      );

      let result: boolean;
      await act(async () => {
        result = await useImpersonationStore.getState().endImpersonation();
      });

      expect(result!).toBe(false);
      expect(useImpersonationStore.getState().error).toBe('Session expired');
    });
  });

  describe('clearError', () => {
    it('should clear the error state', () => {
      act(() => {
        useImpersonationStore.setState({ error: 'Some error' });
      });

      expect(useImpersonationStore.getState().error).toBe('Some error');

      act(() => {
        useImpersonationStore.getState().clearError();
      });

      expect(useImpersonationStore.getState().error).toBeNull();
    });
  });

  describe('reset', () => {
    it('should reset all state to initial values', () => {
      act(() => {
        useImpersonationStore.setState({
          isImpersonating: true,
          impersonatedUser: { id: '1', displayName: 'Test', email: 'test@test.com' },
          sessionId: 'session-1',
          originalSessionToken: 'token-abc',
          expiresAt: '2026-12-31',
          isLoading: true,
          error: 'some error',
        });
      });

      act(() => {
        useImpersonationStore.getState().reset();
      });

      const state = useImpersonationStore.getState();
      expect(state.isImpersonating).toBe(false);
      expect(state.impersonatedUser).toBeNull();
      expect(state.sessionId).toBeNull();
      expect(state.originalSessionToken).toBeNull();
      expect(state.expiresAt).toBeNull();
      expect(state.isLoading).toBe(false);
      expect(state.error).toBeNull();
    });
  });

  describe('selectors', () => {
    it('selectIsImpersonating should return correct value', () => {
      expect(selectIsImpersonating(useImpersonationStore.getState())).toBe(false);

      act(() => {
        useImpersonationStore.setState({ isImpersonating: true });
      });

      expect(selectIsImpersonating(useImpersonationStore.getState())).toBe(true);
    });

    it('selectImpersonatedUser should return correct value', () => {
      expect(selectImpersonatedUser(useImpersonationStore.getState())).toBeNull();

      const user = { id: '1', displayName: 'Test', email: 'test@test.com' };
      act(() => {
        useImpersonationStore.setState({ impersonatedUser: user });
      });

      expect(selectImpersonatedUser(useImpersonationStore.getState())).toEqual(user);
    });

    it('selectImpersonationLoading should return correct value', () => {
      expect(selectImpersonationLoading(useImpersonationStore.getState())).toBe(false);

      act(() => {
        useImpersonationStore.setState({ isLoading: true });
      });

      expect(selectImpersonationLoading(useImpersonationStore.getState())).toBe(true);
    });

    it('selectImpersonationError should return correct value', () => {
      expect(selectImpersonationError(useImpersonationStore.getState())).toBeNull();

      act(() => {
        useImpersonationStore.setState({ error: 'test error' });
      });

      expect(selectImpersonationError(useImpersonationStore.getState())).toBe('test error');
    });
  });
});
