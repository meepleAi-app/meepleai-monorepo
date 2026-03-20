/**
 * Discover Section Layout
 * Issue #5046 — Discover (Catalog + Community) + MiniNav + ActionBar
 *
 * Consolidates: /games/catalog, /games/:id/* → /discover
 * Registers MiniNav tabs and FloatingActionBar.
 * Tabs: Catalogo · Community
 */

'use client';

import { type ReactNode, useEffect } from 'react';

import { BookOpen, FileText, Plus, Users } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { DiscoveryContextBar } from '@/components/discovery/DiscoveryContextBar';
import { ContextBarRegistrar } from '@/components/layout/ContextBar';
import { useSetNavConfig } from '@/context/NavigationContext';

export default function DiscoverLayout({ children }: { children: ReactNode }) {
  const router = useRouter();
  const setNavConfig = useSetNavConfig();

  useEffect(() => {
    setNavConfig({
      miniNav: [
        { id: 'catalog', label: 'Catalogo', href: '/discover', icon: BookOpen },
        { id: 'proposals', label: 'Proposte', href: '/discover?tab=proposals', icon: FileText },
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
  }, [setNavConfig, router]);

  return (
    <>
      <ContextBarRegistrar>
        <DiscoveryContextBar />
      </ContextBarRegistrar>
      {children}
    </>
  );
}
