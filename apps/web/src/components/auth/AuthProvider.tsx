/**
 * AuthProvider - Global authentication state management
 *
 * Manages:
 * - User authentication state
 * - Login/logout/register operations using Server Actions
 * - Session management
 *
 * Issue #1078: FE-IMP-002 — Server Actions per Auth & Export
 *
 * Designed to wrap the entire app in _app.tsx for global auth availability
 */

import React, { createContext, useContext, useCallback, useMemo, PropsWithChildren } from 'react';
import { AuthUser } from '@/types';
import { loginAction, registerAction, logoutAction } from '@/actions/auth';
import { useCurrentUser, useQueryClient } from '@/hooks/queries';

// ============================================================================
// Types
// ============================================================================

export interface RegisterData {
  email: string;
  password: string;
  displayName?: string;
}

export interface AuthContextValue {
  user: AuthUser | null;
  loading: boolean;
  error: string | null;
  login: (email: string, password: string) => Promise<AuthUser>;
  register: (data: RegisterData) => Promise<AuthUser>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
  clearError: () => void;
}

// ============================================================================
// Context
// ============================================================================

const AuthContext = createContext<AuthContextValue | null>(null);

// ============================================================================
// Provider Component
// ============================================================================

export function AuthProvider({ children }: PropsWithChildren) {
  // Issue #1079: Use TanStack Query for user data
  const { data: user, isLoading: loading, error: queryError, refetch } = useCurrentUser();
  const queryClient = useQueryClient();

  // Convert query error to string for backward compatibility
  const error = queryError ? queryError.message : null;

  const login = useCallback(async (email: string, password: string): Promise<AuthUser> => {
    try {
      const formData = new FormData();
      formData.append('email', email);
      formData.append('password', password);

      const result = await loginAction({ success: false }, formData);

      if (!result.success || !result.user) {
        const errorMessage = result.error?.message || 'Login fallito';
        throw new Error(errorMessage);
      }

      // Issue #1079: Set query data immediately so UI updates synchronously,
      // then invalidate to refetch in background (ensures cache is populated even if refetch fails)
      queryClient.setQueryData(['user', 'current'], result.user);
      await queryClient.invalidateQueries({ queryKey: ['user', 'current'] });

      return result.user;
    } catch (err) {
      console.error('Login failed:', err);
      throw err;
    }
  }, [queryClient]);

  const register = useCallback(async (data: RegisterData): Promise<AuthUser> => {
    try {
      const formData = new FormData();
      formData.append('email', data.email);
      formData.append('password', data.password);
      if (data.displayName) {
        formData.append('displayName', data.displayName);
      }

      const result = await registerAction({ success: false }, formData);

      if (!result.success || !result.user) {
        const errorMessage = result.error?.message || 'Registrazione fallita';
        throw new Error(errorMessage);
      }

      // Issue #1079: Set query data immediately so UI updates synchronously,
      // then invalidate to refetch in background (ensures cache is populated even if refetch fails)
      queryClient.setQueryData(['user', 'current'], result.user);
      await queryClient.invalidateQueries({ queryKey: ['user', 'current'] });

      return result.user;
    } catch (err) {
      console.error('Registration failed:', err);
      throw err;
    }
  }, [queryClient]);

  const logout = useCallback(async () => {
    try {
      await logoutAction();
    } catch (err) {
      console.error('Logout failed:', err);
    } finally {
      // Issue #1079: Clear user cache on logout
      queryClient.setQueryData(['user', 'current'], null);
    }
  }, [queryClient]);

  const refreshUser = useCallback(async () => {
    // Issue #1079: Refetch user data using TanStack Query
    await refetch();
  }, [refetch]);

  const clearError = useCallback(() => {
    // Error is now derived from query state, no manual clearing needed
    // Keep for backward compatibility
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user: user ?? null,
      loading,
      error,
      login,
      register,
      logout,
      refreshUser,
      clearError,
    }),
    [user, loading, error, login, register, logout, refreshUser, clearError]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// ============================================================================
// Hook
// ============================================================================

/**
 * Hook to access authentication context
 * Throws error if used outside AuthProvider
 */
export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return context;
}
