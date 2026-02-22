/**
 * Admin Users Hub Layout
 * Issue #5040 — Admin Route Consolidation
 */

'use client';

import { type ReactNode } from 'react';

import { BarChart2, Users } from 'lucide-react';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function AdminUsersLayout({ children }: { children: ReactNode }) {
  useSetNavConfig({
    miniNav: [
      { id: 'users', label: 'Utenti', href: '/admin/users', icon: Users },
      { id: 'analytics', label: 'Analytics', href: '/admin/users?tab=analytics', icon: BarChart2 },
    ],
  });

  return <>{children}</>;
}
