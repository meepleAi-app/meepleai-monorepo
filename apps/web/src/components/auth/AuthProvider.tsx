/**
 * AuthProvider - Global authentication state management
 *
 * Manages:
 * - User authentication state
 * - Login/logout/register operations
 * - Session management
 *
 * Designed to wrap the entire app in _app.tsx for global auth availability
 *
 * #4190: the current user has ONE source, the TanStack query `['user','current']`
 * (`useCurrentUser`). AuthProvider used to keep its own copy in `useState`, read once on mount:
 * any change outside it (a login from a form using another hook, a logout seen in another tab,
 * an invalidate) left it stale. Login, registration and logout now write the query.
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

import { useQueryClient } from '@tanstack/react-query';

import { useCurrentUser, userKeys } from '@/hooks/queries/useCurrentUser';
import { api } from '@/lib/api';
import { logger } from '@/lib/logger';
import { AuthUser } from '@/types';

// ============================================================================
// Types
// ============================================================================

export interface RegisterData {
  email: string;
  password: string;
  // #2954 F1: user's ToS acceptance, forwarded to the register API.
  termsAccepted?: boolean;
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
  const queryClient = useQueryClient();
  const currentUser = useCurrentUser();
  const user = currentUser.data ?? null;
  // A login, registration or logout in flight; the first read is the query's own loading.
  const [actionLoading, setActionLoading] = useState(false);
  const loading = currentUser.isLoading || actionLoading;
  const [error, setError] = useState<string | null>(null);

  const writeUser = useCallback(
    (next: AuthUser | null) => {
      queryClient.setQueryData(userKeys.current(), next);
    },
    [queryClient]
  );

  // Optimization flag read before React hydrates (e.g. to skip a /me call when anonymous).
  useEffect(() => {
    if (currentUser.isLoading) return;
    if (user) {
      localStorage.setItem('meepleai_has_session', 'true');
    } else {
      localStorage.removeItem('meepleai_has_session');
    }
  }, [user, currentUser.isLoading]);

  const login = useCallback(
    async (email: string, password: string): Promise<LoginResult> => {
      setActionLoading(true);
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

        // Normal login - write the user where every reader looks, and return it
        const loggedIn = loginResponse.user ?? null;
        writeUser(loggedIn);
        return {
          user: loggedIn,
          requiresTwoFactor: false,
        };
      } catch (err) {
        logger.error('Login failed', err instanceof Error ? err : new Error(String(err)), {
          component: 'AuthProvider',
        });
        setError(err instanceof Error ? err.message : 'Login failed');
        throw err;
      } finally {
        setActionLoading(false);
      }
    },
    [writeUser]
  );

  const register = useCallback(
    async (data: RegisterData): Promise<AuthUser> => {
      setActionLoading(true);
      setError(null);
      try {
        const authUser = await api.auth.register(data);
        writeUser(authUser);
        return authUser;
      } catch (err) {
        logger.error('Registration failed', err instanceof Error ? err : new Error(String(err)), {
          component: 'AuthProvider',
        });
        setError(err instanceof Error ? err.message : 'Registration failed');
        throw err;
      } finally {
        setActionLoading(false);
      }
    },
    [writeUser]
  );

  const logout = useCallback(async () => {
    setActionLoading(true);
    setError(null);
    try {
      await api.auth.logout();
    } catch (err) {
      logger.error('Logout failed', err instanceof Error ? err : new Error(String(err)), {
        component: 'AuthProvider',
      });
      setError(err instanceof Error ? err.message : 'Logout failed');
    } finally {
      // Always clear the user, even if the API call fails
      writeUser(null);
      setActionLoading(false);
    }
  }, [writeUser]);

  const refreshUser = useCallback(async () => {
    await queryClient.refetchQueries({ queryKey: userKeys.current(), exact: true });
  }, [queryClient]);

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
