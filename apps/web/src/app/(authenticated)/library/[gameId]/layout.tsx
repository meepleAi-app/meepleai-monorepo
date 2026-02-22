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

import { Bot, FileText, HelpCircle, MessageSquare, Play, Wrench } from 'lucide-react';
import { useParams, useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function LibraryGameDetailLayout({ children }: { children: ReactNode }) {
  const { gameId } = useParams<{ gameId: string }>();
  const router = useRouter();

  useSetNavConfig({
    miniNav: [
      {
        id: 'details',
        label: 'Dettagli',
        href: `/library/${gameId}`,
      },
      {
        id: 'agent',
        label: 'Agente',
        href: `/library/${gameId}?tab=agent`,
        icon: Bot,
      },
      {
        id: 'toolkit',
        label: 'Toolkit',
        href: `/library/${gameId}?tab=toolkit`,
        icon: Wrench,
      },
      {
        id: 'faq',
        label: 'FAQ',
        href: `/library/${gameId}?tab=faq`,
        icon: HelpCircle,
      },
    ],
    actionBar: [
      {
        id: 'chat-agent',
        label: 'Chat con Agente',
        icon: MessageSquare,
        variant: 'primary',
        onClick: () => {
          router.push(`/chat/new?gameId=${gameId}`);
        },
      },
      {
        id: 'add-pdf',
        label: 'Carica PDF',
        icon: FileText,
        variant: 'ghost',
        onClick: () => {
          router.push(`/library/${gameId}?action=upload-pdf`);
        },
      },
      {
        id: 'new-session',
        label: 'Avvia Sessione',
        icon: Play,
        variant: 'ghost',
        onClick: () => {
          router.push(`/sessions/new?gameId=${gameId}`);
        },
      },
    ],
  });

  return <>{children}</>;
}
