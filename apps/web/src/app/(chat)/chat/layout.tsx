/**
 * Chat Section Layout
 * Issue #5044 — Chat Section MiniNav + ActionBar
 *
 * Registers MiniNav (single "Chat" tab) and FloatingActionBar for /chat/*.
 * All pages under /chat/ inherit this config, including the thread view.
 *
 * - /chat          → Chat list (tab active)
 * - /chat/new      → New chat form
 * - /chat/[threadId] → Thread view (tab highlights /chat root)
 */

'use client';

import { type ReactNode } from 'react';

import { MessageSquare, Plus } from 'lucide-react';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function ChatLayout({ children }: { children: ReactNode }) {
  useSetNavConfig({
    miniNav: [
      { id: 'chats', label: 'Chat', href: '/chat', icon: MessageSquare },
    ],
    actionBar: [
      {
        id: 'new-chat',
        label: 'Nuova Chat',
        icon: Plus,
        variant: 'primary',
        onClick: () => {
          window.location.href = '/chat/new';
        },
      },
    ],
  });

  return <>{children}</>;
}
