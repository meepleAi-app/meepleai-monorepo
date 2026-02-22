/**
 * Admin Content Hub Layout
 * Issue #5040 — Admin Route Consolidation
 *
 * Consolidates: games, shared-games, faqs, pdfs/kb, game-sessions, share-requests
 */

'use client';

import { type ReactNode } from 'react';

import { BookOpen, Database, FileText, Gamepad2, Globe, Play } from 'lucide-react';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function AdminContentLayout({ children }: { children: ReactNode }) {
  useSetNavConfig({
    miniNav: [
      { id: 'games', label: 'Giochi', href: '/admin/content', icon: Gamepad2 },
      { id: 'shared', label: 'Catalogo community', href: '/admin/content?tab=shared', icon: Globe },
      { id: 'faqs', label: 'FAQ', href: '/admin/content?tab=faqs', icon: FileText },
      { id: 'kb', label: 'Knowledge Base', href: '/admin/content?tab=kb', icon: Database },
      { id: 'sessions', label: 'Sessioni', href: '/admin/content?tab=sessions', icon: Play },
    ],
  });

  return <>{children}</>;
}
