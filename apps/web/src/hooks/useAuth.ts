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
import { useRouter } from 'next/navigation'; // App Router
import { api } from '@/lib/api';
import { AuthUser } from '@/types/auth';
import { isApiError } from '@/types/api';

// ============================================================================
// Types
// ============================================================================

export interface AuthResponse {
  user: AuthUser;
  expiresAt?: string;
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

export interface DemoLoginData {
  email: string;
}

export interface UseAuthReturn {
  user: AuthUser | null;
  loading: boolean;
  error: string;
  login: (data: LoginData) => Promise<AuthUser>;
  demoLogin: (data: DemoLoginData) => Promise<AuthUser>;
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
      const res = await api.get<AuthResponse>('/api/v1/auth/me');
      if (res?.user) {
        setUser(res.user);
      } else {
        setUser(null);
      }
    } catch (err) {
      // Session not found or expired - this is expected for logged-out users
      setUser(null);
    } finally {
      setLoading(false);
    }
  }, []);

  /**
   * Login with email and password
   * Returns user on success, throws on failure
   */
  const login = useCallback(async (data: LoginData): Promise<AuthUser> => {
    setLoading(true);
    setError('');

    try {
      const res = await api.post<AuthResponse>('/api/v1/auth/login', {
        email: data.email,
        password: data.password,
      });

      if (!res?.user) {
        throw new Error('Login response missing user data');
      }

      setUser(res.user);
      return res.user;
    } catch (err) {
      const errorMessage = isApiError(err)
        ? err.message
        : err instanceof Error
          ? err.message
          : 'Login failed. Please check your credentials.';
      setError(errorMessage);
      throw new Error(errorMessage);
    } finally {
      setLoading(false);
    }
  }, []);

  /**
   * Demo login with email only (no password required)
   * Only works for users marked as demo accounts
   * Returns user on success, throws on failure
   */
  const demoLogin = useCallback(async (data: DemoLoginData): Promise<AuthUser> => {
    setLoading(true);
    setError('');

    try {
      const res = await api.post<AuthResponse>('/api/v1/auth/demo-login', {
        email: data.email,
      });

      if (!res?.user) {
        throw new Error('Demo login response missing user data');
      }

      setUser(res.user);
      return res.user;
    } catch (err) {
      const errorMessage = isApiError(err)
        ? err.message
        : err instanceof Error
          ? err.message
          : 'Demo login failed. Please check the email address.';
      setError(errorMessage);
      throw new Error(errorMessage);
    } finally {
      setLoading(false);
    }
  }, []);

  /**
   * Register new user account
   * Returns user on success, throws on failure
   */
  const register = useCallback(async (data: RegisterData): Promise<AuthUser> => {
    setLoading(true);
    setError('');

    try {
      const payload = {
        email: data.email,
        password: data.password,
        displayName: data.displayName || undefined,
        role: data.role || 'User',
      };

      const res = await api.post<AuthResponse>('/api/v1/auth/register', payload);

      if (!res?.user) {
        throw new Error('Registration response missing user data');
      }

      setUser(res.user);
      return res.user;
    } catch (err) {
      const errorMessage = isApiError(err)
        ? err.message
        : err instanceof Error
          ? err.message
          : 'Registration failed. Please try again.';
      setError(errorMessage);
      throw new Error(errorMessage);
    } finally {
      setLoading(false);
    }
  }, []);

  /**
   * Logout current user
   * Redirects to home page on success
   */
  const logout = useCallback(async () => {
    setLoading(true);
    setError('');

    try {
      await api.post('/api/v1/auth/logout');
      setUser(null);
      await router.push('/');
    } catch (err) {
      console.error('Logout error:', err);
      // Clear user state even if API call fails
      setUser(null);
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
    demoLogin,
    register,
    logout,
    loadCurrentUser,
    clearError,
  };
}