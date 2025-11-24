/**
 * useAuth Hook Tests
 *
 * Tests authentication state management and operations:
 * - User session loading and persistence
 * - Login and registration flows
 * - Logout functionality
 * - Error handling
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { useAuth } from '../useAuth';

// Mock API module
vi.mock('@/lib/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    auth: {},
  },
}));

vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({
    push: vi.fn(),
    pathname: '/',
    query: {},
  })),
}));

// Get mocked functions after module is mocked
const { api } = require('@/lib/api');
const mockedApi = api as Mocked<typeof api>;

describe('useAuth', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedApi.get.mockResolvedValue(null);
    mockedApi.post.mockResolvedValue({});
  });

  describe('initialization', () => {
    it('should initialize with null user after loading completes', async () => {
      mockedApi.get.mockResolvedValue(null);

      const { result } = renderHook(() => useAuth());

      // Wait for initial load to complete
      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.user).toBeNull();
      expect(result.current.error).toBe('');
    });

    it('should load current user on mount', async () => {
      const mockUser = {
        id: 'user-123',
        email: 'test@example.com',
        displayName: 'Test User',
        role: 'User',
      };

      mockedApi.get.mockResolvedValue({ user: mockUser });

      const { result } = renderHook(() => useAuth());

      await waitFor(() => {
        expect(result.current.user).toEqual(mockUser);
      });

      expect(mockedApi.get).toHaveBeenCalledWith('/api/v1/auth/me');
    });

    it('should handle session not found gracefully', async () => {
      mockedApi.get.mockRejectedValue(new Error('Unauthorized'));

      const { result } = renderHook(() => useAuth());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.user).toBeNull();
      expect(result.current.error).toBe('');
    });
  });

  describe('login', () => {
    it('should login successfully with valid credentials', async () => {
      const mockUser = {
        id: 'user-123',
        email: 'test@example.com',
        displayName: 'Test User',
        role: 'User',
      };

      mockedApi.get.mockResolvedValue(null); // Initial load
      mockedApi.post.mockResolvedValue({ user: mockUser });

      const { result } = renderHook(() => useAuth());

      let loginResult;
      await act(async () => {
        loginResult = await result.current.login({
          email: 'test@example.com',
          password: 'password123',
        });
      });

      expect(loginResult).toEqual(mockUser);
      expect(result.current.user).toEqual(mockUser);
      expect(result.current.error).toBe('');
      expect(mockedApi.post).toHaveBeenCalledWith('/api/v1/auth/login', {
        email: 'test@example.com',
        password: 'password123',
      });
    });

    it('should set loading state during login', async () => {
      mockedApi.get.mockResolvedValue(null);
      mockedApi.post.mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve({ user: {} }), 100))
      );

      const { result } = renderHook(() => useAuth());

      act(() => {
        void result.current.login({
          email: 'test@example.com',
          password: 'password123',
        });
      });

      expect(result.current.loading).toBe(true);

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });
    });

    it('should handle login errors', async () => {
      mockedApi.get.mockResolvedValue(null);
      mockedApi.post.mockRejectedValue(new Error('Invalid credentials'));

      const { result } = renderHook(() => useAuth());

      await act(async () => {
        try {
          await result.current.login({
            email: 'wrong@example.com',
            password: 'wrongpassword',
          });
        } catch (error) {
          expect(error).toBeInstanceOf(Error);
        }
      });

      expect(result.current.user).toBeNull();
      expect(result.current.error).toContain('Invalid credentials');
      expect(result.current.loading).toBe(false);
    });

    it('should handle missing user in login response', async () => {
      mockedApi.get.mockResolvedValue(null);
      mockedApi.post.mockResolvedValue({}); // Missing user

      const { result } = renderHook(() => useAuth());

      await act(async () => {
        try {
          await result.current.login({
            email: 'test@example.com',
            password: 'password123',
          });
        } catch (error) {
          expect(error).toBeInstanceOf(Error);
          expect((error as Error).message).toContain('missing user data');
        }
      });

      expect(result.current.user).toBeNull();
    });

    it('should clear error before new login attempt', async () => {
      mockedApi.get.mockResolvedValue(null);
      mockedApi.post
        .mockRejectedValueOnce(new Error('First error'))
        .mockResolvedValueOnce({
          user: { id: '1', email: 'test@example.com', role: 'User' },
        });

      const { result } = renderHook(() => useAuth());

      // First login fails
      await act(async () => {
        try {
          await result.current.login({
            email: 'test@example.com',
            password: 'wrong',
          });
        } catch {}
      });

      expect(result.current.error).toBe('First error');

      // Second login succeeds
      await act(async () => {
        await result.current.login({
          email: 'test@example.com',
          password: 'correct',
        });
      });

      expect(result.current.error).toBe('');
    });
  });

  describe('register', () => {
    it('should register successfully with valid data', async () => {
      const mockUser = {
        id: 'user-123',
        email: 'newuser@example.com',
        displayName: 'New User',
        role: 'User',
      };

      mockedApi.get.mockResolvedValue(null);
      mockedApi.post.mockResolvedValue({ user: mockUser });

      const { result } = renderHook(() => useAuth());

      let registerResult;
      await act(async () => {
        registerResult = await result.current.register({
          email: 'newuser@example.com',
          password: 'password123',
          displayName: 'New User',
        });
      });

      expect(registerResult).toEqual(mockUser);
      expect(result.current.user).toEqual(mockUser);
      expect(result.current.error).toBe('');
      expect(mockedApi.post).toHaveBeenCalledWith('/api/v1/auth/register', {
        email: 'newuser@example.com',
        password: 'password123',
        displayName: 'New User',
        role: 'User',
      });
    });

    it('should register with custom role', async () => {
      const mockUser = {
        id: 'admin-123',
        email: 'admin@example.com',
        displayName: 'Admin User',
        role: 'Admin',
      };

      mockedApi.get.mockResolvedValue(null);
      mockedApi.post.mockResolvedValue({ user: mockUser });

      const { result } = renderHook(() => useAuth());

      await act(async () => {
        await result.current.register({
          email: 'admin@example.com',
          password: 'password123',
          displayName: 'Admin User',
          role: 'Admin',
        });
      });

      expect(mockedApi.post).toHaveBeenCalledWith('/api/v1/auth/register', {
        email: 'admin@example.com',
        password: 'password123',
        displayName: 'Admin User',
        role: 'Admin',
      });
    });

    it('should handle registration errors', async () => {
      mockedApi.get.mockResolvedValue(null);
      mockedApi.post.mockRejectedValue(new Error('Email already exists'));

      const { result } = renderHook(() => useAuth());

      await act(async () => {
        try {
          await result.current.register({
            email: 'existing@example.com',
            password: 'password123',
          });
        } catch (error) {
          expect(error).toBeInstanceOf(Error);
        }
      });

      expect(result.current.user).toBeNull();
      expect(result.current.error).toContain('Email already exists');
    });

    it('should handle missing user in registration response', async () => {
      mockedApi.get.mockResolvedValue(null);
      mockedApi.post.mockResolvedValue({}); // Missing user

      const { result } = renderHook(() => useAuth());

      await act(async () => {
        try {
          await result.current.register({
            email: 'test@example.com',
            password: 'password123',
          });
        } catch (error) {
          expect((error as Error).message).toContain('missing user data');
        }
      });

      expect(result.current.user).toBeNull();
    });
  });

  describe('logout', () => {
    it('should logout successfully', async () => {
      const mockUser = {
        id: 'user-123',
        email: 'test@example.com',
        role: 'User',
      };

      const mockRouterPush = vi.fn();
      const { useRouter } = require('next/navigation');
      useRouter.mockReturnValue({ push: mockRouterPush });

      mockedApi.get.mockResolvedValue({ user: mockUser });
      mockedApi.post.mockResolvedValue({});

      const { result } = renderHook(() => useAuth());

      // Wait for initial user load
      await waitFor(() => {
        expect(result.current.user).toEqual(mockUser);
      });

      // Logout
      await act(async () => {
        await result.current.logout();
      });

      expect(result.current.user).toBeNull();
      expect(mockedApi.post).toHaveBeenCalledWith('/api/v1/auth/logout');
      expect(mockRouterPush).toHaveBeenCalledWith('/');
    });

    it('should clear user state even if logout API fails', async () => {
      const mockUser = {
        id: 'user-123',
        email: 'test@example.com',
        role: 'User',
      };

      const mockRouterPush = vi.fn();
      const { useRouter } = require('next/navigation');
      useRouter.mockReturnValue({ push: mockRouterPush });

      const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation();

      mockedApi.get.mockResolvedValue({ user: mockUser });
      mockedApi.post.mockRejectedValue(new Error('Logout failed'));

      const { result } = renderHook(() => useAuth());

      await waitFor(() => {
        expect(result.current.user).toEqual(mockUser);
      });

      await act(async () => {
        await result.current.logout();
      });

      expect(result.current.user).toBeNull();
      expect(mockRouterPush).toHaveBeenCalledWith('/');
      expect(consoleErrorSpy).toHaveBeenCalled();

      consoleErrorSpy.mockRestore();
    });
  });

  describe('loadCurrentUser', () => {
    it('should reload user data', async () => {
      const initialUser = {
        id: 'user-123',
        email: 'test@example.com',
        role: 'User',
      };

      const updatedUser = {
        id: 'user-123',
        email: 'test@example.com',
        displayName: 'Updated Name',
        role: 'User',
      };

      mockedApi.get
        .mockResolvedValueOnce({ user: initialUser })
        .mockResolvedValueOnce({ user: updatedUser });

      const { result } = renderHook(() => useAuth());

      await waitFor(() => {
        expect(result.current.user).toEqual(initialUser);
      });

      await act(async () => {
        await result.current.loadCurrentUser();
      });

      expect(result.current.user).toEqual(updatedUser);
    });

    it('should clear user if session expired', async () => {
      const mockUser = {
        id: 'user-123',
        email: 'test@example.com',
        role: 'User',
      };

      mockedApi.get
        .mockResolvedValueOnce({ user: mockUser })
        .mockRejectedValueOnce(new Error('Unauthorized'));

      const { result } = renderHook(() => useAuth());

      await waitFor(() => {
        expect(result.current.user).toEqual(mockUser);
      });

      await act(async () => {
        await result.current.loadCurrentUser();
      });

      expect(result.current.user).toBeNull();
    });
  });

  describe('clearError', () => {
    it('should clear error message', async () => {
      mockedApi.get.mockResolvedValue(null);
      mockedApi.post.mockRejectedValue(new Error('Login failed'));

      const { result } = renderHook(() => useAuth());

      // Cause an error
      await act(async () => {
        try {
          await result.current.login({
            email: 'test@example.com',
            password: 'wrong',
          });
        } catch {}
      });

      expect(result.current.error).toBe('Login failed');

      // Clear error
      act(() => {
        result.current.clearError();
      });

      expect(result.current.error).toBe('');
    });
  });
});
