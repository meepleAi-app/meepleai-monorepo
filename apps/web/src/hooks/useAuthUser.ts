/**
 * useAuthUser Hook
 *
 * Issue #1608: Frontend Route Protection
 *
 * Custom hook to get authenticated user in client components.
 * Used by components wrapped in RequireRole to access user data.
 *
 * Usage:
 * ```tsx
 * export function MyClient() {
 *   const { user, loading } = useAuthUser();
 *
 *   if (loading) return <Loading />;
 *   if (!user) return null; // RequireRole handles redirect
 *
 *   return <div>Welcome {user.email}</div>;
 * }
 * ```
 */

'use client';

import { useEffect, useState } from 'react';

import { getCurrentUser } from '@/actions/auth';
import type { AuthUser } from '@/types/auth';

interface UseAuthUserResult {
  user: AuthUser | null;
  loading: boolean;
  error: string | null;
}

export function useAuthUser(): UseAuthUserResult {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function fetchUser() {
      try {
        const result = await getCurrentUser();

        if (result.success && result.user) {
          setUser(result.user);
        } else {
          setUser(null);
          setError(result.error?.message || 'Failed to get user');
        }
      } catch (err) {
        setUser(null);
        setError(err instanceof Error ? err.message : 'Unknown error');
      } finally {
        setLoading(false);
      }
    }

    fetchUser();
  }, []);

  return { user, loading, error };
}
