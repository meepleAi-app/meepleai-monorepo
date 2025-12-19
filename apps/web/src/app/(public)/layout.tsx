/**
 * Public Route Group Layout
 * Issue #2233 - Phase 4: Route Groups
 *
 * Applies PublicLayout to all pages in (public) group:
 * - / (home)
 * - /games
 * - /dashboard
 * - /settings
 * - /sessions
 * - /giochi
 * - /board-game-ai
 *
 * Features:
 * - PublicHeader with user context
 * - PublicFooter
 * - Responsive container
 * - Dark mode support
 */

'use client';

import { ReactNode } from 'react';
import { PublicLayout } from '@/components/layouts/PublicLayout';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useRouter } from 'next/navigation';

export default function PublicRootLayout({ children }: { children: ReactNode }) {
  const { data: user, isLoading } = useCurrentUser();
  const router = useRouter();

  const handleLogout = async () => {
    try {
      const response = await fetch('/api/v1/auth/logout', {
        method: 'POST',
        credentials: 'include',
      });

      if (response.ok) {
        router.push('/login');
        router.refresh();
      }
    } catch (error) {
      console.error('Logout failed:', error);
    }
  };

  // Show loading state without layout shift
  if (isLoading) {
    return <div className="min-h-screen bg-background">{children}</div>;
  }

  return (
    <PublicLayout
      user={
        user
          ? {
              name: user.displayName || user.email,
              email: user.email,
              avatar: undefined,
            }
          : undefined
      }
      onLogout={handleLogout}
      showNewsletter={false}
    >
      {children}
    </PublicLayout>
  );
}
