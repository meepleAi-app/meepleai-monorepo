'use client';

/**
 * DashboardNavConfig — Registers ActionBar actions for /dashboard (home)
 * Issue #5041 — Dashboard / Home ActionBar
 *
 * MiniNav: none (homepage = global entry point)
 * ActionBar: Add Game (primary) · New Chat · New Session
 *
 * Include in dashboard/page.tsx:
 *   <DashboardNavConfig />
 */

import { useEffect } from 'react';

import { MessageSquare, Play, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function DashboardNavConfig() {
  const setNavConfig = useSetNavConfig();
  const router = useRouter();

  useEffect(() => {
    setNavConfig({
      // No MiniNav on homepage — it's the global entry point
      miniNav: [],
      actionBar: [
        {
          id: 'add-game',
          label: 'Add Game',
          icon: Plus,
          variant: 'primary',
          onClick: () => router.push('/library?action=add'),
        },
        {
          id: 'new-chat',
          label: 'New Chat',
          icon: MessageSquare,
          onClick: () => router.push('/chat/new'),
        },
        {
          id: 'new-session',
          label: 'New Session',
          icon: Play,
          onClick: () => router.push('/sessions/new'),
        },
      ],
    });
  }, [setNavConfig, router]);

  return null;
}
