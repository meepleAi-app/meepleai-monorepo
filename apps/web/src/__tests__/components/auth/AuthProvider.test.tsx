/**
 * Unit tests for AuthProvider
 * Comprehensive coverage of authentication state management
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { AuthProvider, useAuth } from '@/components/auth/AuthProvider';
import { api } from '@/lib/api';
import React, { PropsWithChildren } from 'react';

// Mock api module
jest.mock('@/lib/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
  },
}));

const mockApi = api as jest.Mocked<typeof api>;

describe('AuthProvider', () => {
  const wrapper = ({ children }: PropsWithChildren) => (
    <AuthProvider>{children}</AuthProvider>
  );

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Initialization', () => {
    it('loads user on mount', async () => {
      const mockUser = {
        id: 'user-1',
        email: 'test@example.com',
        displayName: 'Test User',
        role: 'User',
      };

      mockApi.get.mockResolvedValueOnce({ user: mockUser });

      const { result } = renderHook(() => useAuth(), { wrapper });

      // Initially loading
      expect(result.current.loading).toBe(true);
      expect(result.current.user).toBeNull();

      // Wait for load to complete
      await waitFor(() => expect(result.current.loading).toBe(false));

      expect(result.current.user).toEqual(mockUser);
      expect(mockApi.get).toHaveBeenCalledWith('/api/v1/auth/me');
    });

    it('handles user not found on mount', async () => {
      mockApi.get.mockRejectedValueOnce(new Error('Not authenticated'));

      const { result } = renderHook(() => useAuth(), { wrapper });

      await waitFor(() => expect(result.current.loading).toBe(false));

      expect(result.current.user).toBeNull();
      expect(result.current.error).toBeNull(); // Error is only set for explicit operations
    });
  });

  describe('login', () => {
    it('successfully logs in user', async () => {
      const mockUser = {
        id: 'user-1',
        email: 'test@example.com',
        displayName: 'Test User',
        role: 'User',
      };

      mockApi.get.mockResolvedValueOnce(null); // Initial load
      mockApi.post.mockResolvedValueOnce({ user: mockUser });

      const { result } = renderHook(() => useAuth(), { wrapper });

      await waitFor(() => expect(result.current.loading).toBe(false));

      let returnedUser;
      await act(async () => {
        returnedUser = await result.current.login('test@example.com', 'password123');
      });

      expect(returnedUser).toEqual(mockUser);
      expect(result.current.user).toEqual(mockUser);
      expect(result.current.error).toBeNull();
      expect(mockApi.post).toHaveBeenCalledWith('/api/v1/auth/login', {
        email: 'test@example.com',
        password: 'password123',
      });
    });

    it('handles login failure', async () => {
      mockApi.get.mockResolvedValueOnce(null); // Initial load
      mockApi.post.mockRejectedValueOnce(new Error('Invalid credentials'));

      const { result } = renderHook(() => useAuth(), { wrapper });

      await waitFor(() => expect(result.current.loading).toBe(false));

      await act(async () => {
        try {
          await result.current.login('test@example.com', 'wrongpassword');
        } catch (err) {
          // Expected to throw
        }
      });

      expect(result.current.user).toBeNull();
      expect(result.current.error).toBe('Invalid credentials');
    });
  });

  describe('register', () => {
    it('successfully registers user', async () => {
      const mockUser = {
        id: 'user-2',
        email: 'newuser@example.com',
        displayName: 'New User',
        role: 'User',
      };

      mockApi.get.mockResolvedValueOnce(null); // Initial load
      mockApi.post.mockResolvedValueOnce({ user: mockUser });

      const { result } = renderHook(() => useAuth(), { wrapper });

      await waitFor(() => expect(result.current.loading).toBe(false));

      let returnedUser;
      await act(async () => {
        returnedUser = await result.current.register({
          email: 'newuser@example.com',
          password: 'password123',
          displayName: 'New User',
        });
      });

      expect(returnedUser).toEqual(mockUser);
      expect(result.current.user).toEqual(mockUser);
      expect(result.current.error).toBeNull();
    });

    it('handles registration failure', async () => {
      mockApi.get.mockResolvedValueOnce(null); // Initial load
      mockApi.post.mockRejectedValueOnce(new Error('Email already exists'));

      const { result } = renderHook(() => useAuth(), { wrapper });

      await waitFor(() => expect(result.current.loading).toBe(false));

      await act(async () => {
        try {
          await result.current.register({
            email: 'existing@example.com',
            password: 'password123',
          });
        } catch (err) {
          // Expected to throw
        }
      });

      expect(result.current.user).toBeNull();
      expect(result.current.error).toBe('Email already exists');
    });
  });

  describe('logout', () => {
    it('successfully logs out user', async () => {
      const mockUser = {
        id: 'user-1',
        email: 'test@example.com',
        displayName: 'Test User',
        role: 'User',
      };

      mockApi.get.mockResolvedValueOnce({ user: mockUser }); // Initial load
      mockApi.post.mockResolvedValueOnce({});

      const { result } = renderHook(() => useAuth(), { wrapper });

      await waitFor(() => expect(result.current.user).toEqual(mockUser));

      await act(async () => {
        await result.current.logout();
      });

      expect(result.current.user).toBeNull();
      expect(mockApi.post).toHaveBeenCalledWith('/api/v1/auth/logout');
    });

    it('handles logout failure gracefully', async () => {
      const mockUser = {
        id: 'user-1',
        email: 'test@example.com',
        displayName: 'Test User',
        role: 'User',
      };

      mockApi.get.mockResolvedValueOnce({ user: mockUser }); // Initial load
      mockApi.post.mockRejectedValueOnce(new Error('Logout failed'));

      const { result } = renderHook(() => useAuth(), { wrapper });

      await waitFor(() => expect(result.current.user).toEqual(mockUser));

      await act(async () => {
        try {
          await result.current.logout();
        } catch (err) {
          // Expected to throw
        }
      });

      expect(result.current.user).toBeNull(); // User still cleared even if API call fails
    });
  });

  describe('refreshUser', () => {
    it('reloads current user', async () => {
      const initialUser = {
        id: 'user-1',
        email: 'test@example.com',
        displayName: 'Test User',
        role: 'User',
      };

      const updatedUser = {
        ...initialUser,
        displayName: 'Updated Name',
      };

      mockApi.get.mockResolvedValueOnce({ user: initialUser }); // Initial load

      const { result } = renderHook(() => useAuth(), { wrapper });

      await waitFor(() => expect(result.current.user).toEqual(initialUser));

      mockApi.get.mockResolvedValueOnce({ user: updatedUser });

      await act(async () => {
        await result.current.refreshUser();
      });

      expect(result.current.user).toEqual(updatedUser);
    });
  });

  describe('clearError', () => {
    it('clears error message', async () => {
      mockApi.get.mockResolvedValueOnce(null); // Initial load
      mockApi.post.mockRejectedValueOnce(new Error('Login failed'));

      const { result } = renderHook(() => useAuth(), { wrapper });

      await waitFor(() => expect(result.current.loading).toBe(false));

      // Trigger error
      await act(async () => {
        try {
          await result.current.login('test@example.com', 'wrong');
        } catch (err) {
          // Expected
        }
      });

      expect(result.current.error).toBe('Login failed');

      // Clear error
      act(() => {
        result.current.clearError();
      });

      expect(result.current.error).toBeNull();
    });
  });

  describe('error handling', () => {
    it('throws error if useAuth used outside AuthProvider', () => {
      // Suppress console.error for this test
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

      expect(() => {
        renderHook(() => useAuth());
      }).toThrow('useAuth must be used within AuthProvider');

      consoleSpy.mockRestore();
    });
  });
});
