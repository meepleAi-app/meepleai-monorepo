/**
 * PublicLayoutWrapper - Client Component Wrapper
 *
 * Wrapper client-side per PublicLayout che gestisce auth state.
 * Permette di usare PublicLayout in Server Components.
 */

'use client';

import { ReactNode } from 'react';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { PublicLayout } from './PublicLayout';
import { useRouter } from 'next/navigation';

export interface PublicLayoutWrapperProps {
  children: ReactNode;
  containerWidth?: 'sm' | 'md' | 'lg' | 'xl' | 'full';
  className?: string;
}

export function PublicLayoutWrapper({
  children,
  containerWidth,
  className,
}: PublicLayoutWrapperProps) {
  const { data: user } = useCurrentUser();
  const router = useRouter();

  const handleLogout = async () => {
    try {
      const response = await fetch('/api/auth/logout', {
        method: 'POST',
        credentials: 'include',
      });

      if (response.ok) {
        router.push('/login');
      }
    } catch (error) {
      console.error('Logout error:', error);
    }
  };

  const publicUser = user
    ? {
        name: user.displayName || user.email.split('@')[0],
        email: user.email,
        avatar: undefined,
      }
    : undefined;

  return (
    <PublicLayout
      user={publicUser}
      onLogout={handleLogout}
      containerWidth={containerWidth}
      className={className}
    >
      {children}
    </PublicLayout>
  );
}
