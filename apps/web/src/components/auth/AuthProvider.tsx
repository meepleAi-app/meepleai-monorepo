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

import React, { createContext, useContext, useState, useCallback, useMemo, useEffect, PropsWithChildren } from 'react';
import { AuthUser } from '@/types';
import { loginAction, registerAction, logoutAction, getCurrentUser } from '@/actions/auth';

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
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Load user on mount
  useEffect(() => {
    void loadCurrentUser();
  }, []);

  const loadCurrentUser = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await getCurrentUser();
      setUser(result.user ?? null);
    } catch (err) {
      console.error('Failed to load current user:', err);
      setUser(null);
    } finally {
      setLoading(false);
    }
  }, []);

  const login = useCallback(async (email: string, password: string): Promise<AuthUser> => {
    setLoading(true);
    setError(null);
    try {
      const formData = new FormData();
      formData.append('email', email);
      formData.append('password', password);

      const result = await loginAction({ success: false }, formData);

      if (!result.success || !result.user) {
        const errorMessage = result.error?.message || 'Login fallito';
        setError(errorMessage);
        throw new Error(errorMessage);
      }

      setUser(result.user);
      return result.user;
    } catch (err) {
      console.error('Login failed:', err);
      const errorMessage = err instanceof Error ? err.message : 'Login fallito';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  const register = useCallback(async (data: RegisterData): Promise<AuthUser> => {
    setLoading(true);
    setError(null);
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
        setError(errorMessage);
        throw new Error(errorMessage);
      }

      setUser(result.user);
      return result.user;
    } catch (err) {
      console.error('Registration failed:', err);
      const errorMessage = err instanceof Error ? err.message : 'Registrazione fallita';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  const logout = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      await logoutAction();
    } catch (err) {
      console.error('Logout failed:', err);
      setError(err instanceof Error ? err.message : 'Logout fallito');
    } finally {
      // Always clear user, even if API call fails
      setUser(null);
      setLoading(false);
    }
  }, []);

  const refreshUser = useCallback(async () => {
    await loadCurrentUser();
  }, [loadCurrentUser]);

  const clearError = useCallback(() => {
    setError(null);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
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
