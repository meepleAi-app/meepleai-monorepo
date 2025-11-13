/**
 * Re-export useAuth from AuthProvider for backward compatibility
 *
 * This maintains compatibility with components importing from @/hooks/useAuth
 * instead of @/components/auth/AuthProvider
 */

export { useAuth, type AuthContextValue } from '@/components/auth/AuthProvider';
