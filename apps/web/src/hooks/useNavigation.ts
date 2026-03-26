'use client';

import { usePathname } from 'next/navigation';

import { useNavStore, type NavTab } from '@/lib/stores/navStore';

export type { NavTab };

interface Breadcrumb {
  label: string;
  href: string;
}

const SEGMENT_LABELS: Record<string, string> = {
  dashboard: 'Dashboard',
  sessions: 'Sessioni',
  live: 'Live',
  library: 'Libreria',
  discover: 'Scopri',
  'game-nights': 'Serate',
  chat: 'Chat',
  settings: 'Impostazioni',
  scoreboard: 'Punteggi',
  players: 'Giocatori',
  collections: 'Collezioni',
  wishlist: 'Wishlist',
  'play-records': 'Partite',
  admin: 'Admin',
  new: 'Nuovo',
};

function buildBreadcrumbs(pathname: string): Breadcrumb[] {
  const segments = pathname.split('/').filter(Boolean);
  const cleanSegments = segments.filter(s => !s.startsWith('('));

  return cleanSegments.map((segment, index) => ({
    label: SEGMENT_LABELS[segment] || segment,
    href: '/' + cleanSegments.slice(0, index + 1).join('/'),
  }));
}

export function useNavigation() {
  const store = useNavStore();
  const pathname = usePathname();

  const breadcrumbs = buildBreadcrumbs(pathname);
  const showBreadcrumb = breadcrumbs.length >= 3;

  return {
    ...store,
    breadcrumbs,
    showBreadcrumb,
  };
}
