/**
 * Library Section Layout
 * Issue #5039 — User Route Consolidation
 * Issue #5042 — Library + ActionBar
 *
 * Registers MiniNav tabs and FloatingActionBar for the /library section.
 * Active tab is determined by URL search param ?tab=
 */

'use client';

import { type ReactNode } from 'react';

import { BookOpen, Download, FileText, Filter, Heart, Lock, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function LibraryLayout({ children }: { children: ReactNode }) {
  const router = useRouter();

  useSetNavConfig({
    miniNav: [
      { id: 'games', label: 'I miei giochi', href: '/library', icon: BookOpen },
      { id: 'wishlist', label: 'Wishlist', href: '/library?tab=wishlist', icon: Heart },
      { id: 'private', label: 'Privati', href: '/library?tab=private', icon: Lock },
    ],
    actionBar: [
      {
        id: 'add-game',
        label: 'Aggiungi gioco',
        icon: Plus,
        variant: 'primary',
        onClick: () => {
          router.push('/discover/add');
        },
      },
      {
        id: 'import-bgg',
        label: 'Importa da BGG',
        icon: Download,
        variant: 'ghost',
        onClick: () => {
          router.push('/discover/add?source=bgg');
        },
      },
      {
        id: 'import-pdf',
        label: 'Carica PDF',
        icon: FileText,
        variant: 'ghost',
        onClick: () => {
          router.push('/library?action=upload-pdf');
        },
      },
      {
        id: 'filter',
        label: 'Filtra',
        icon: Filter,
        variant: 'ghost',
        onClick: () => {
          // Filter panel toggle — handled by LibraryPageClient
          document.dispatchEvent(new CustomEvent('library:toggle-filter'));
        },
      },
    ],
  });

  return <>{children}</>;
}
