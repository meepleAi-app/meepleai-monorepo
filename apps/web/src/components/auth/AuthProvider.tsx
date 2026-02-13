/**
 * AuthProvider - Global authentication state management
 *
 * Manages:
 * - User authentication state
 * - Login/logout/register operations
 * - Session management
 * - HyperDX user identification (Issue #1566)
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

import { api } from '@/lib/api';
import { identifyUser } from '@/lib/hyperdx';
import { logger } from '@/lib/logger';
import { AuthUser } from '@/types';

// ============================================================================
// Types
// ============================================================================

export interface RegisterData {
  email: string;
  password: string;
  displayName?: string;
  role?: string;
}

/**
 * Login result supporting 2FA flow
 */
export interface LoginResult {
  /** The authenticated user (null if 2FA is required) */
  user: AuthUser | null;
  /** Whether 2FA verification is required */
  requiresTwoFactor: boolean;
  /** Temporary session token for 2FA verification */
  tempSessionToken?: string | null;
}

export interface AuthContextValue {
  user: AuthUser | null;
  loading: boolean;
  error: string | null;
  login: (email: string, password: string) => Promise<LoginResult>;
  register: (data: RegisterData) => Promise<AuthUser>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
  clearError: () => void;
}

// ============================================================================
// Context
// ============================================================================

const AuthContext = createContext<AuthContextValue | null>(null);

// Export for testing/Storybook mocking
export { AuthContext };

// ============================================================================
// Provider Component
// ============================================================================

export function AuthProvider({ children }: PropsWithChildren) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(false);
  const [_initialLoading, setInitialLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadCurrentUser = useCallback(async () => {
    setLoading(true);
    setInitialLoading(true);
    setError(null);
    try {
      const user = await api.auth.getMe();
      setUser(user);
      if (user) {
        // User authenticated - set flag for optimization
        localStorage.setItem('meepleai_has_session', 'true');
      } else {
        localStorage.removeItem('meepleai_has_session');
      }
    } catch (_err) {
      // 401 expected for anonymous users - not an error
      setUser(null);
      localStorage.removeItem('meepleai_has_session');
    } finally {
      setLoading(false);
      setInitialLoading(false);
    }
  }, []);

  // Load user on mount
  useEffect(() => {
    void loadCurrentUser();
  }, [loadCurrentUser]);

  // Issue #1566: Identify user in HyperDX for session tracking
  // This connects user sessions to telemetry data for better debugging
  // Note: Only user ID is sent (GDPR-compliant), not email
  useEffect(() => {
    if (user) {
      identifyUser(user.id);
    }
  }, [user]);

  const login = useCallback(async (email: string, password: string): Promise<LoginResult> => {
    setLoading(true);
    setError(null);
    try {
      const loginResponse = await api.auth.login({ email, password });

      // Check if 2FA is required
      if (loginResponse.requiresTwoFactor) {
        return {
          user: null,
          requiresTwoFactor: true,
          tempSessionToken: loginResponse.tempSessionToken,
        };
      }

      // Normal login - set user and return result
      const user = loginResponse.user ?? null;
      setUser(user);
      if (user) {
        localStorage.setItem('meepleai_has_session', 'true');
      }
      return {
        user,
        requiresTwoFactor: false,
      };
    } catch (err) {
      logger.error('Login failed', err instanceof Error ? err : new Error(String(err)), {
        component: 'AuthProvider',
      });
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
      const authUser = await api.auth.register(data);
      setUser(authUser);
      localStorage.setItem('meepleai_has_session', 'true');
      return authUser;
    } catch (err) {
      logger.error('Registration failed', err instanceof Error ? err : new Error(String(err)), {
        component: 'AuthProvider',
      });
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
      await api.auth.logout();
    } catch (err) {
      logger.error('Logout failed', err instanceof Error ? err : new Error(String(err)), {
        component: 'AuthProvider',
      });
      setError(err instanceof Error ? err.message : 'Logout failed');
    } finally {
      // Always clear user, even if API call fails
      setUser(null);
      localStorage.removeItem('meepleai_has_session');
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