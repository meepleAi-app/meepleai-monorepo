/**
 * Library Game Detail Layout
 * Issue #5042 — Library + Game Detail Hub
 *
 * Canonical route: /library/[gameId]
 * (replaces /library/games/[gameId] — permanent redirect in next.config.js)
 *
 * Registers contextual MiniNav (Dettagli · Agente · Toolkit · FAQ)
 * and FloatingActionBar (Chat · Carica PDF · Avvia Sessione) for the game hub.
 * The gameId is dynamic — read from URL params so hrefs are always correct.
 */

'use client';

import { type ReactNode } from 'react';

import { useParams, usePathname, useRouter } from 'next/navigation';

import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';

export default function LibraryGameDetailLayout({ children }: { children: ReactNode }) {
  const { gameId } = useParams<{ gameId: string }>();
  const router = useRouter();
  const pathname = usePathname();

  const activeTabId = pathname?.includes('tab=faq')
    ? 'faq'
    : pathname?.includes('tab=toolkit')
      ? 'toolkit'
      : pathname?.includes('tab=agent')
        ? 'agent'
        : 'details';

  useMiniNavConfig({
    breadcrumb: 'Gioco',
    tabs: [
      { id: 'details', label: 'Dettagli', href: `/library/${gameId}` },
      { id: 'agent', label: 'Agente', href: `/library/${gameId}?tab=agent` },
      { id: 'toolkit', label: 'Toolkit', href: `/library/${gameId}?tab=toolkit` },
      { id: 'faq', label: 'FAQ', href: `/library/${gameId}?tab=faq` },
    ],
    activeTabId,
    primaryAction: {
      label: 'Chat con Agente',
      icon: '💬',
      onClick: () => router.push(`/chat/new?gameId=${gameId}`),
    },
  });

  return <>{children}</>;
}
