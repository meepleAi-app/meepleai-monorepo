/**
 * Discover Section Layout
 * Issue #5046 — Discover (Catalog + Community) + MiniNav + ActionBar
 *
 * Consolidates: /games/catalog, /games/:id/* → /discover
 * Registers MiniNav tabs and FloatingActionBar.
 * Tabs: Catalogo · Community
 */

'use client';

import { type ReactNode } from 'react';

import { BookOpen, Plus, Users } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function DiscoverLayout({ children }: { children: ReactNode }) {
  const router = useRouter();

  useSetNavConfig({
    miniNav: [
      { id: 'catalog', label: 'Catalogo', href: '/discover', icon: BookOpen },
      { id: 'community', label: 'Community', href: '/discover?tab=community', icon: Users },
    ],
    actionBar: [
      {
        id: 'add-game',
        label: 'Aggiungi Gioco',
        icon: Plus,
        variant: 'primary',
        onClick: () => {
          router.push('/discover/add');
        },
      },
    ],
  });

  return <>{children}</>;
}
