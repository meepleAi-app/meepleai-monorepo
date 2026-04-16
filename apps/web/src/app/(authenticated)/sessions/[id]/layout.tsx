/**
 * Active Session Layout — /sessions/[id]
 *
 * Responsibilities:
 * 1. Render PageHeader with session-specific tabs (Punteggi, Strumenti, Chat, Note)
 * 2. Load session data on mount
 * 3. Own LiveScoreSheet state
 *
 * Issue #5041 — Sessions Redesign Phase 2
 */

'use client';

import { type ReactNode, useEffect, useState, use } from 'react';

import { usePathname } from 'next/navigation';

import { PageHeader } from '@/components/layout/PageHeader';
import { LiveScoreSheet } from '@/components/session';
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

  // Derive active tab from pathname
  const deriveActiveTab = (): string => {
    if (pathname?.endsWith('/tools')) return 'tools';
    if (pathname?.endsWith('/chat')) return 'chat';
    if (pathname?.endsWith('/notes')) return 'notes';
    return 'scores';
  };

  // Derive scoring context from store (same source as page.tsx for consistency)
  const players = activeSession?.players ?? [];
  const roundNumbers = scores.map(s => s.round);
  const currentRound = roundNumbers.length > 0 ? Math.max(1, ...roundNumbers) : 1;
  const dimensions = activeSession?.scoringConfig.enabledDimensions ?? ['default'];

  return (
    <>
      <PageHeader
        title={activeSession?.gameName ?? 'Sessione'}
        parentHref="/sessions"
        parentLabel="Sessioni"
        tabs={[
          { id: 'scores', label: 'Punteggi', href: `/sessions/${id}` },
          { id: 'tools', label: 'Strumenti', href: `/sessions/${id}/tools` },
          { id: 'chat', label: 'Chat', href: `/sessions/${id}/chat` },
          { id: 'notes', label: 'Note', href: `/sessions/${id}/notes` },
        ]}
        activeTabId={deriveActiveTab()}
        primaryAction={{
          label: 'Aggiungi Punteggio',
          onClick: () => setScoreSheetOpen(true),
        }}
      />
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
