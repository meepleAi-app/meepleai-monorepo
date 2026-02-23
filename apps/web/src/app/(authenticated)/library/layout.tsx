/**
 * Library Section Layout
 * Issue #5039 — User Route Consolidation
 * Issue #5042 — Library + ActionBar
 * Issue #5167 — Tab rename: Games (personal) / Collection (shared catalog)
 *
 * Registers MiniNav tabs and FloatingActionBar for the /library section.
 * Active tab is determined by URL search param ?tab=
 */

'use client';

import { type ReactNode, useEffect } from 'react';

import { Download, FileText, Filter, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function LibraryLayout({ children }: { children: ReactNode }) {
  const router = useRouter();
  const setNavConfig = useSetNavConfig();

  useEffect(() => {
    setNavConfig({
      // miniNav is registered by LibraryNavConfig (NavConfig.tsx) in page.tsx — single source of truth
      actionBar: [
        {
          id: 'add-game',
          label: 'Aggiungi gioco',
          icon: Plus,
          variant: 'primary',
          onClick: () => {
            router.push('/library?action=add');
          },
        },
        {
          id: 'import-bgg',
          label: 'Importa da BGG',
          icon: Download,
          variant: 'ghost',
          onClick: () => {
            router.push('/library?action=import-bgg');
          },
        },
        {
          id: 'import-pdf',
          label: 'Carica PDF',
          icon: FileText,
          variant: 'ghost',
          onClick: () => {
            router.push('/library?action=import-pdf');
          },
        },
        {
          id: 'filter',
          label: 'Filtra',
          icon: Filter,
          variant: 'ghost',
          onClick: () => {
            // Filter panel toggle — handled by GamesPageClient
            document.dispatchEvent(new CustomEvent('library:toggle-filter'));
          },
        },
      ],
    });
  }, [setNavConfig, router]);

  return <>{children}</>;
}
