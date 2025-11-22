/**
 * AdminAuthGuard - Shared auth protection component for admin pages
 *
 * Handles loading and unauthorized states consistently across all admin pages.
 * This component MUST be used after all hooks are called to maintain proper
 * React hook ordering.
 *
 * Usage:
 * ```tsx
 * export function AdminPageClient() {
 *   const { user, loading: authLoading } = useAuthUser();
 *   // ... all other hooks ...
 *
 *   return (
 *     <AdminAuthGuard loading={authLoading} user={user}>
 *       {// Main content}
 *     </AdminAuthGuard>
 *   );
 * }
 * ```
 */

import Link from 'next/link';
import type { AuthUser } from '@/types/auth';

interface AdminAuthGuardProps {
  children: React.ReactNode;
  loading: boolean;
  user: AuthUser | null;
  loadingMessage?: string;
  backgroundClass?: string;
}

export function AdminAuthGuard({
  children,
  loading,
  user,
  loadingMessage = 'Authenticating...',
  backgroundClass = 'min-h-dvh bg-gray-50 p-8'
}: AdminAuthGuardProps) {
  if (loading) {
    return (
      <div className={backgroundClass}>
        <div className="max-w-7xl mx-auto">
          <div className="flex items-center justify-center py-12">
            <div className="text-center">
              <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto mb-4" />
              <p className="text-gray-600">{loadingMessage}</p>
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (!user) {
    return (
      <div className={backgroundClass}>
        <div className="max-w-7xl mx-auto">
          <div className="max-w-md mx-auto mt-12">
            <div className="p-6 bg-red-50 border border-red-600 rounded-lg mb-4">
              <h2 className="text-lg font-semibold text-red-900 mb-2">
                Unauthorized Access
              </h2>
              <p className="text-red-800">
                Admin access required. Please log in with an administrator account.
              </p>
            </div>
            <Link
              href="/login"
              className="block text-center px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
            >
              Go to Login
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
