'use client';

/**
 * LibraryNavConfig — Single source of truth for /library MiniNav + ActionBar
 *
 * Tabs: Collection · Games · Wishlist · Proposals (with icons + badge counts)
 * ActionBar: Add Game (primary) · Import BGG · Import PDF · Filter
 *
 * Include in library/page.tsx:
 *   <LibraryNavConfig />
 */

import { useEffect } from 'react';

import {
  BookOpen,
  Download,
  FileText,
  Filter,
  Gamepad2,
  Plus,
  SendHorizontal,
  Star,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useLibraryQuota } from '@/hooks/queries/useLibrary';
import { useShareRequests } from '@/hooks/queries/useShareRequests';
import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function LibraryNavConfig() {
  const setNavConfig = useSetNavConfig();
  const router = useRouter();

  // Badge data
  const { data: quota } = useLibraryQuota();
  const { data: proposals } = useShareRequests({ status: 'Pending', pageSize: 1 });

  const collectionCount = quota?.currentCount;
  const pendingProposals = proposals?.totalCount || undefined;

  useEffect(() => {
    setNavConfig({
      miniNav: [
        {
          id: 'collection',
          label: 'Collection',
          href: '/library',
          icon: BookOpen,
          badge: collectionCount ?? undefined,
        },
        {
          id: 'private',
          label: 'Games',
          href: '/library?tab=private',
          icon: Gamepad2,
        },
        {
          id: 'wishlist',
          label: 'Wishlist',
          href: '/library?tab=wishlist',
          icon: Star,
        },
        {
          id: 'proposals',
          label: 'Proposals',
          href: '/library?tab=proposals',
          icon: SendHorizontal,
          badge: pendingProposals,
        },
      ],
      actionBar: [
        {
          id: 'add-game',
          label: 'Aggiungi gioco',
          icon: Plus,
          variant: 'primary',
          onClick: () => router.push('/library?action=add'),
        },
        {
          id: 'import-bgg',
          label: 'Importa da BGG',
          icon: Download,
          variant: 'ghost',
          onClick: () => router.push('/library?action=import-bgg'),
        },
        {
          id: 'import-pdf',
          label: 'Carica PDF',
          icon: FileText,
          variant: 'ghost',
          onClick: () => router.push('/library?action=import-pdf'),
        },
        {
          id: 'filter',
          label: 'Filtra',
          icon: Filter,
          variant: 'ghost',
          onClick: () => {
            document.dispatchEvent(new CustomEvent('library:toggle-filter'));
          },
        },
      ],
    });
  }, [setNavConfig, router, collectionCount, pendingProposals]);

  return null;
}
