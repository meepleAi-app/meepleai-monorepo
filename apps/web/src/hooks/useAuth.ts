/**
 * useAuth Hook - Centralized Authentication Logic
 *
 * Manages authentication state and operations:
 * - User session loading and persistence
 * - Login and registration
 * - Logout
 * - Session hydration on mount
 *
 * This hook consolidates auth logic previously scattered across
 * index.tsx, login.tsx, and ChatProvider.tsx
 */

import { useState, useEffect, useCallback } from 'react';

import { useRouter } from 'next/navigation';

import { userKeys } from '@/hooks/queries/useCurrentUser';
import { api } from '@/lib/api';
import { getQueryClient } from '@/lib/queryClient';

// ============================================================================
// Types
// ============================================================================

export interface AuthUser {
  id: string;
  email: string;
  displayName?: string | null;
  role: string;
}

export interface RegisterData {
  email: string;
  password: string;
  displayName?: string;
  role?: string;
}

export interface LoginData {
  email: string;
  password: string;
}

export interface UseAuthReturn {
  user: AuthUser | null;
  loading: boolean;
  error: string;
  login: (data: LoginData) => Promise<AuthUser>;
  register: (data: RegisterData) => Promise<AuthUser>;
  logout: () => Promise<void>;
  loadCurrentUser: () => Promise<void>;
  clearError: () => void;
}

// ============================================================================
// Hook Implementation
// ============================================================================

export function useAuth(): UseAuthReturn {
  const router = useRouter();
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>('');

  /**
   * Load current user session from API
   * Called on mount to hydrate auth state
   */
  const loadCurrentUser = useCallback(async () => {
    try {
      setLoading(true);
      const user = await api.auth.getMe();
      setUser(user);
    } catch (_err) {
      // Session not found or expired - this is expected for logged-out users
      setUser(null);
    } finally {
      setLoading(false);
    }
  }, []);

  /**
   * Login with email and password
   * Returns user on success, throws on failure
   *
   * After successful login, invalidates TanStack Query cache to ensure
   * dashboard and other components fetch fresh user data (fixes #XXXX)
   */
  const login = useCallback(async (data: LoginData): Promise<AuthUser> => {
    setLoading(true);
    setError('');

    try {
      const authUser = await api.auth.login({
        email: data.email,
        password: data.password,
      });

      setUser(authUser);

      // Invalidate TanStack Query cache to ensure fresh data on dashboard
      // This fixes the desync between useAuth (local state) and useCurrentUser (TanStack Query)
      const queryClient = getQueryClient();
      await queryClient.invalidateQueries({ queryKey: userKeys.current() });

      return authUser;
    } catch (err: unknown) {
      const errorMessage =
        err instanceof Error ? err.message : 'Login failed. Please check your credentials.';
      setError(errorMessage);
      throw new Error(errorMessage);
    } finally {
      setLoading(false);
    }
  }, []);

  /**
   * Register new user account
   * Returns user on success, throws on failure
   *
   * After successful registration, invalidates TanStack Query cache to ensure
   * dashboard shows user data instead of "Nessun Utente" error (fixes #XXXX)
   */
  const register = useCallback(async (data: RegisterData): Promise<AuthUser> => {
    setLoading(true);
    setError('');

    try {
      const authUser = await api.auth.register({
        email: data.email,
        password: data.password,
        displayName: data.displayName || undefined,
        role: data.role || 'User',
      });

      setUser(authUser);

      // Invalidate TanStack Query cache to ensure fresh data on dashboard
      // This fixes the desync between useAuth (local state) and useCurrentUser (TanStack Query)
      const queryClient = getQueryClient();
      await queryClient.invalidateQueries({ queryKey: userKeys.current() });

      return authUser;
    } catch (err: unknown) {
      const errorMessage =
        err instanceof Error ? err.message : 'Registration failed. Please try again.';
      setError(errorMessage);
      throw new Error(errorMessage);
    } finally {
      setLoading(false);
    }
  }, []);

  /**
   * Logout current user
   * Redirects to home page on success
   *
   * After logout, invalidates TanStack Query cache to clear user data
   */
  const logout = useCallback(async () => {
    setLoading(true);
    setError('');

    try {
      await api.auth.logout();
      setUser(null);

      // Invalidate TanStack Query cache to clear stale user data
      const queryClient = getQueryClient();
      await queryClient.invalidateQueries({ queryKey: userKeys.current() });

      await router.push('/');
    } catch (_err: unknown) {
      // Clear user state even if API call fails
      setUser(null);

      // Still invalidate cache on logout failure
      const queryClient = getQueryClient();
      await queryClient.invalidateQueries({ queryKey: userKeys.current() });

      await router.push('/');
    } finally {
      setLoading(false);
    }
  }, [router]);

  /**
   * Clear error message
   */
  const clearError = useCallback(() => {
    setError('');
  }, []);

  /**
   * Load user session on mount
   */
  useEffect(() => {
    void loadCurrentUser();
  }, [loadCurrentUser]);

  return {
    user,
    loading,
    error,
    login,
    register,
    logout,
    loadCurrentUser,
    clearError,
  };
}
