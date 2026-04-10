/**
 * Active Session Layout — /sessions/[id]
 *
 * Responsibilities:
 * 1. Override MiniNav with session-specific tabs (Punteggi, Strumenti, Chat, Note)
 * 2. Load session data on mount
 * 3. Own LiveScoreSheet state
 *
 * Issue #5041 — Sessions Redesign Phase 2
 */

'use client';

import { type ReactNode, useEffect, useState, use } from 'react';

import { usePathname } from 'next/navigation';

import { LiveScoreSheet } from '@/components/session';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
import { useSessionStore } from '@/lib/stores/session-store';

interface SessionLayoutProps {
  children: ReactNode;
  params: Promise<{ id: string }>;
}

export default function SessionDetailLayout({ children, params }: SessionLayoutProps) {
  const { id } = use(params);
  const pathname = usePathname();
  const loadSession = useSessionStore(s => s.loadSession);
  const activeSession = useSessionStore(s => s.activeSession);
  const scores = useSessionStore(s => s.scores);

  const [scoreSheetOpen, setScoreSheetOpen] = useState(false);

  // Load session data on mount
  useEffect(() => {
    loadSession(id).catch(() => {
      // Error state is set in the store; child page renders it
    });
  }, [id, loadSession]);

  const activeTabId = pathname?.includes('/notes')
    ? 'notes'
    : pathname?.includes('/chat')
      ? 'chat'
      : pathname?.includes('/tools')
        ? 'tools'
        : 'scores';

  useMiniNavConfig({
    breadcrumb: 'Sessione',
    tabs: [
      { id: 'scores', label: 'Punteggi', href: `/sessions/${id}` },
      { id: 'tools', label: 'Strumenti', href: `/sessions/${id}/tools` },
      { id: 'chat', label: 'Chat', href: `/sessions/${id}/chat` },
      { id: 'notes', label: 'Note', href: `/sessions/${id}/notes` },
    ],
    activeTabId,
    primaryAction: {
      label: 'Aggiungi Punteggio',
      icon: '＋',
      onClick: () => setScoreSheetOpen(true),
    },
  });

  // Derive scoring context from store (same source as page.tsx for consistency)
  const players = activeSession?.players ?? [];
  const roundNumbers = scores.map(s => s.round);
  const currentRound = roundNumbers.length > 0 ? Math.max(1, ...roundNumbers) : 1;
  const dimensions = activeSession?.scoringConfig.enabledDimensions ?? ['default'];

  return (
    <>
      {children}

      <LiveScoreSheet
        open={scoreSheetOpen}
        onOpenChange={setScoreSheetOpen}
        players={players}
        currentUserId={null}
        currentRound={currentRound}
        dimensions={dimensions}
      />
    </>
  );
}
