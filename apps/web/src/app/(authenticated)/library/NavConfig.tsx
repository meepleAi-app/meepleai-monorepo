'use client';

/**
 * LibraryNavConfig — Registers MiniNav tabs + ActionBar actions for /library
 * Issue #5042 — Library + Game Detail MiniNav + ActionBar
 *
 * Tabs: Games · Wishlist · Private · History
 * ActionBar: Add Game (primary) · Import BGG · Import PDF · Filter
 *
 * Include in library/page.tsx or library/layout.tsx:
 *   <LibraryNavConfig />
 */

import { useEffect } from 'react';

import { Download, FileText, Filter, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function LibraryNavConfig() {
  const setNavConfig = useSetNavConfig();
  const router = useRouter();

  useEffect(() => {
    setNavConfig({
      miniNav: [
        { id: 'games',    label: 'Games',    href: '/library' },
        { id: 'wishlist', label: 'Wishlist', href: '/library?tab=wishlist' },
        { id: 'private',  label: 'Private',  href: '/library?tab=private' },
        { id: 'history',  label: 'History',  href: '/library?tab=history' },
      ],
      actionBar: [
        {
          id: 'add-game',
          label: 'Add Game',
          icon: Plus,
          variant: 'primary',
          onClick: () => router.push('/library?action=add'),
        },
        {
          id: 'import-bgg',
          label: 'Import BGG',
          icon: Download,
          onClick: () => router.push('/library?action=import-bgg'),
        },
        {
          id: 'import-pdf',
          label: 'Import PDF',
          icon: FileText,
          onClick: () => router.push('/library?action=import-pdf'),
        },
        {
          id: 'filter',
          label: 'Filter',
          icon: Filter,
          onClick: () => router.push('/library?action=filter'),
        },
      ],
    });
  }, [setNavConfig, router]);

  return null;
}
