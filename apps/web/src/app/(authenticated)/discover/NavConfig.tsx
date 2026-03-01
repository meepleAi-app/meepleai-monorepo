'use client';

/**
 * DiscoverNavConfig — Registers MiniNav tabs + ActionBar actions for /discover
 * Issue #5046 — Discover (Catalog + Community) MiniNav + ActionBar
 *
 * Tabs: Catalog · Proposals · Community
 * ActionBar: Search (primary) · Filter
 *
 * Include in discover/page.tsx:
 *   <DiscoverNavConfig />
 */

import { useEffect } from 'react';

import { Filter, Search } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function DiscoverNavConfig() {
  const setNavConfig = useSetNavConfig();
  const router = useRouter();

  useEffect(() => {
    setNavConfig({
      miniNav: [
        { id: 'catalog',   label: 'Catalog',   href: '/discover' },
        { id: 'proposals', label: 'Proposals', href: '/discover?tab=proposals' },
        { id: 'community', label: 'Community', href: '/discover?tab=community' },
      ],
      actionBar: [
        {
          id: 'search',
          label: 'Search',
          icon: Search,
          variant: 'primary',
          onClick: () => router.push('/discover?action=search'),
        },
        {
          id: 'filter',
          label: 'Filter',
          icon: Filter,
          onClick: () => router.push('/discover?action=filter'),
        },
      ],
    });
  }, [setNavConfig, router]);

  return null;
}
