/**
 * Unit tests for AuthProvider
 * Comprehensive coverage of authentication state management
 *
 * Issue #1078: FE-IMP-002 — Server Actions per Auth & Export
 * Updated to mock Server Actions instead of direct API calls
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { AuthProvider, useAuth } from '@/components/auth/AuthProvider';
import * as authActions from '@/actions/auth';
import React, { PropsWithChildren } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// Mock auth actions module
jest.mock('@/actions/auth', () => ({
  getCurrentUser: jest.fn(),
  loginAction: jest.fn(),
  registerAction: jest.fn(),
  logoutAction: jest.fn(),
}));

const mockGetCurrentUser = authActions.getCurrentUser as jest.MockedFunction<typeof authActions.getCurrentUser>;
const mockLoginAction = authActions.loginAction as jest.MockedFunction<typeof authActions.loginAction>;
const mockRegisterAction = authActions.registerAction as jest.MockedFunction<typeof authActions.registerAction>;
const mockLogoutAction = authActions.logoutAction as jest.MockedFunction<typeof authActions.logoutAction>;

describe('AuthProvider', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    jest.clearAllMocks();
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false, staleTime: 0 },
      },
      logger: {
        log: () => {},
        warn: () => {},
        error: () => {},
      },
    });
  });

  afterEach(() => {
    queryClient.clear();
  });

  const wrapper = ({ children }: PropsWithChildren) => (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>{children}</AuthProvider>
    </QueryClientProvider>
  );

  describe('Initialization', () => {
    it('loads user on mount', async () => {
      const mockUser = {
        id: 'user-1',
        email: 'test@example.com',
        displayName: 'Test User',
        role: 'User',
      };

      mockGetCurrentUser.mockResolvedValueOnce({
        success: true,
        user: mockUser
      });

      const { result } = renderHook(() => useAuth(), { wrapper });

      // Initially loading
      expect(result.current.loading).toBe(true);
      expect(result.current.user).toBeNull();

      // Wait for load to complete
      await waitFor(() => expect(result.current.loading).toBe(false));

      expect(result.current.user).toEqual(mockUser);
      expect(mockGetCurrentUser).toHaveBeenCalled();
    });

    it('handles user not found on mount', async () => {
      mockGetCurrentUser.mockResolvedValueOnce({
        success: false,
        user: undefined
      });

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

      mockGetCurrentUser.mockResolvedValueOnce({ success: false }); // Initial load
      mockLoginAction.mockResolvedValueOnce({
        success: true,
        user: mockUser,
        message: 'Login effettuato con successo!'
      });

      const { result } = renderHook(() => useAuth(), { wrapper });

      await waitFor(() => expect(result.current.loading).toBe(false));

      let returnedUser;
      await act(async () => {
        returnedUser = await result.current.login('test@example.com', 'password123');
      });

      expect(returnedUser).toEqual(mockUser);
      expect(result.current.user).toEqual(mockUser);
      expect(result.current.error).toBeNull();
      expect(mockLoginAction).toHaveBeenCalled();

      // Verify FormData was passed correctly
      const callArgs = mockLoginAction.mock.calls[0];
      const formData = callArgs[1] as FormData;
      expect(formData.get('email')).toBe('test@example.com');
      expect(formData.get('password')).toBe('password123');
    });

    it('handles login failure', async () => {
      mockGetCurrentUser.mockResolvedValueOnce({ success: false }); // Initial load
      mockLoginAction.mockResolvedValueOnce({
        success: false,
        error: {
          type: 'auth',
          message: 'Email o password non corretti.'
        }
      });

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
      expect(result.current.error).toBe('Email o password non corretti.');
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

      mockGetCurrentUser.mockResolvedValueOnce({ success: false }); // Initial load
      mockRegisterAction.mockResolvedValueOnce({
        success: true,
        user: mockUser,
        message: 'Registrazione completata! Benvenuto su MeepleAI.'
      });

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
      expect(mockRegisterAction).toHaveBeenCalled();

      // Verify FormData was passed correctly
      const callArgs = mockRegisterAction.mock.calls[0];
      const formData = callArgs[1] as FormData;
      expect(formData.get('email')).toBe('newuser@example.com');
      expect(formData.get('password')).toBe('password123');
      expect(formData.get('displayName')).toBe('New User');
    });

    it('handles registration failure', async () => {
      mockGetCurrentUser.mockResolvedValueOnce({ success: false }); // Initial load
      mockRegisterAction.mockResolvedValueOnce({
        success: false,
        error: {
          type: 'conflict',
          message: 'Questa email è già registrata. Prova con un\'altra email o effettua il login.',
          field: 'email'
        }
      });

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
      expect(result.current.error).toContain('già registrata');
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

      mockGetCurrentUser.mockResolvedValueOnce({ success: true, user: mockUser }); // Initial load
      mockLogoutAction.mockResolvedValueOnce({
        success: true,
        message: 'Logout effettuato.'
      });

      const { result } = renderHook(() => useAuth(), { wrapper });

      await waitFor(() => expect(result.current.user).toEqual(mockUser));

      await act(async () => {
        await result.current.logout();
      });

      expect(result.current.user).toBeNull();
      expect(mockLogoutAction).toHaveBeenCalled();
    });

    it('handles logout failure gracefully', async () => {
      const mockUser = {
        id: 'user-1',
        email: 'test@example.com',
        displayName: 'Test User',
        role: 'User',
      };

      mockGetCurrentUser.mockResolvedValueOnce({ success: true, user: mockUser }); // Initial load
      mockLogoutAction.mockRejectedValueOnce(new Error('Logout failed'));

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

      mockGetCurrentUser.mockResolvedValueOnce({ success: true, user: initialUser }); // Initial load

      const { result } = renderHook(() => useAuth(), { wrapper });

      await waitFor(() => expect(result.current.user).toEqual(initialUser));

      mockGetCurrentUser.mockResolvedValueOnce({ success: true, user: updatedUser });

      await act(async () => {
        await result.current.refreshUser();
      });

      expect(result.current.user).toEqual(updatedUser);
    });
  });

  describe('clearError', () => {
    it('clears error message', async () => {
      mockGetCurrentUser.mockResolvedValueOnce({ success: false }); // Initial load
      mockLoginAction.mockResolvedValueOnce({
        success: false,
        error: {
          type: 'auth',
          message: 'Login fallito'
        }
      });

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

      expect(result.current.error).toBe('Login fallito');

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
