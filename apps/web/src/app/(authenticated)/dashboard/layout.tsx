/**
 * Dashboard Layout
 * Issue #5041 — Dashboard / Home
 *
 * Configures the FloatingActionBar for the gaming hub dashboard.
 * No MiniNav (dashboard is the global entry point).
 */

'use client';

import { type ReactNode } from 'react';

import { MessageSquare, Play, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function DashboardLayout({ children }: { children: ReactNode }) {
  const router = useRouter();

  useSetNavConfig({
    // No MiniNav — dashboard is the global entry point
    miniNav: [],
    actionBar: [
      {
        id: 'add-game',
        label: 'Aggiungi gioco',
        icon: Plus,
        variant: 'primary',
        onClick: () => {
          router.push('/discover/add');
        },
      },
      {
        id: 'new-chat',
        label: 'Nuova chat',
        icon: MessageSquare,
        variant: 'ghost',
        onClick: () => {
          router.push('/chat/new');
        },
      },
      {
        id: 'new-session',
        label: 'Nuova sessione',
        icon: Play,
        variant: 'ghost',
        onClick: () => {
          router.push('/sessions/new');
        },
      },
    ],
  });

  return <>{children}</>;
}
