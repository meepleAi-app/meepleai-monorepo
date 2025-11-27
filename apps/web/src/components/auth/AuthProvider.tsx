/**
 * AuthProvider - Global authentication state management
 *
 * Manages:
 * - User authentication state
 * - Login/logout/register operations
 * - Session management
 *
 * Designed to wrap the entire app in _app.tsx for global auth availability
 */

import React, {
  createContext,
  useContext,
  useState,
  useCallback,
  useMemo,
  useEffect,
  PropsWithChildren,
} from 'react';
import { AuthUser } from '@/types';
import { api } from '@/lib/api';

// ============================================================================
// Types
// ============================================================================

interface AuthResponse {
  user: AuthUser;
}

export interface RegisterData {
  email: string;
  password: string;
  displayName?: string;
  role?: string;
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
  const [loading, setLoading] = useState(false);
  const [initialLoading, setInitialLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadCurrentUser = useCallback(async () => {
    setInitialLoading(true);
    setError(null);
    try {
      const res = await api.get<AuthResponse>('/api/v1/auth/me');
      setUser(res?.user ?? null);
    } catch (err) {
      console.error('Failed to load current user:', err);
      setUser(null);
    } finally {
      setInitialLoading(false);
    }
  }, []);

  // Load user on mount
  useEffect(() => {
    void loadCurrentUser();
  }, [loadCurrentUser]);

  const login = useCallback(async (email: string, password: string): Promise<AuthUser> => {
    setLoading(true);
    setError(null);
    try {
      const res = await api.post<AuthResponse>('/api/v1/auth/login', { email, password });
      const authUser = res?.user ?? null;
      setUser(authUser);
      if (!authUser) throw new Error('Login response missing user data');
      return authUser;
    } catch (err) {
      console.error('Login failed:', err);
      setError(err instanceof Error ? err.message : 'Login failed');
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  const register = useCallback(async (data: RegisterData): Promise<AuthUser> => {
    setLoading(true);
    setError(null);
    try {
      const res = await api.post<AuthResponse>('/api/v1/auth/register', data);
      const authUser = res?.user ?? null;
      setUser(authUser);
      if (!authUser) throw new Error('Registration response missing user data');
      return authUser;
    } catch (err) {
      console.error('Registration failed:', err);
      setError(err instanceof Error ? err.message : 'Registration failed');
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  const logout = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      await api.post('/api/v1/auth/logout');
    } catch (err) {
      console.error('Logout failed:', err);
      setError(err instanceof Error ? err.message : 'Logout failed');
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

/**
 * Convenience hook to access just the user and loading state
 * Useful for components that only need user data
 */
export function useAuthUser(): { user: AuthUser | null; loading: boolean } {
  const { user, loading } = useAuth();
  return { user, loading };
}
