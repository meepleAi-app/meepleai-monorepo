'use client';

/**
 * ChatNavConfig — Registers MiniNav tabs + ActionBar actions for /chat (list)
 * Issue #5044 — Chat MiniNav + ActionBar
 *
 * Tabs: Recent · All Threads
 * ActionBar: New Chat (primary) · Search
 *
 * Include in chat/page.tsx:
 *   <ChatNavConfig />
 */

import { useEffect } from 'react';

import { Plus, Search } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function ChatNavConfig() {
  const setNavConfig = useSetNavConfig();
  const router = useRouter();

  useEffect(() => {
    setNavConfig({
      miniNav: [
        { id: 'recent',     label: 'Recent',      href: '/chat' },
        { id: 'all-threads', label: 'All Threads', href: '/chat?tab=all' },
      ],
      actionBar: [
        {
          id: 'new-chat',
          label: 'New Chat',
          icon: Plus,
          variant: 'primary',
          onClick: () => router.push('/chat/new'),
        },
        {
          id: 'search',
          label: 'Search',
          icon: Search,
          onClick: () => router.push('/chat?action=search'),
        },
      ],
    });
  }, [setNavConfig, router]);

  return null;
}
