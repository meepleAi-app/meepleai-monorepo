/**
 * Active Session Layout — /sessions/[id]
 *
 * Responsibilities:
 * 1. Register tabs (Punteggi · Strumenti · Chat · Note) on the MiniNavSlot
 * 2. Load session data on mount
 * 3. Own LiveScoreSheet state and render the inline session header
 *
 * Issue #5041 — Sessions Redesign Phase 2
 * #2158 (Fix #2 codemod): migrated from legacy PageHeader to MiniNavSlot via
 * useMiniNavConfig. The session name + "Aggiungi Punteggio" CTA + back link
 * now live in an inline header above {children}, keeping the LiveScoreSheet
 * trigger inside this layout (it owns the sheet state).
 */

'use client';

import { type ReactNode, useEffect, useState, use } from 'react';

import Link from 'next/link';
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

  // Derive active tab from pathname
  const deriveActiveTab = (): string => {
    if (pathname?.endsWith('/tools')) return 'tools';
    if (pathname?.endsWith('/chat')) return 'chat';
    if (pathname?.endsWith('/notes')) return 'notes';
    return 'scores';
  };

  const sessionName = activeSession?.gameName ?? 'Sessione';

  useMiniNavConfig({
    breadcrumb: `Sessioni · ${sessionName}`,
    tabs: [
      { id: 'scores', label: 'Punteggi', href: `/sessions/${id}` },
      { id: 'tools', label: 'Strumenti', href: `/sessions/${id}/tools` },
      { id: 'chat', label: 'Chat', href: `/sessions/${id}/chat` },
      { id: 'notes', label: 'Note', href: `/sessions/${id}/notes` },
    ],
    activeTabId: deriveActiveTab(),
  });

  // Derive scoring context from store (same source as page.tsx for consistency)
  const players = activeSession?.players ?? [];
  const roundNumbers = scores.map(s => s.round);
  const currentRound = roundNumbers.length > 0 ? Math.max(1, ...roundNumbers) : 1;
  const dimensions = activeSession?.scoringConfig.enabledDimensions ?? ['default'];

  return (
    <>
      {/* #2158 (Fix #2): inline session header — title + back link + CTA. */}
      <header className="mx-auto flex w-full max-w-[1200px] items-center justify-between gap-4 px-4 pt-4 sm:px-6">
        <div className="min-w-0">
          <Link
            href="/sessions"
            className="inline-flex items-center gap-1 text-sm font-medium text-foreground/70 hover:text-foreground"
          >
            <span aria-hidden="true">←</span>
            <span>Sessioni</span>
          </Link>
          <h1 className="mt-0.5 truncate font-quicksand text-2xl font-bold tracking-tight text-foreground">
            {sessionName}
          </h1>
        </div>
        <button
          type="button"
          onClick={() => setScoreSheetOpen(true)}
          className="inline-flex shrink-0 items-center gap-2 rounded-md bg-[hsl(25,95%,38%)] px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
        >
          Aggiungi Punteggio
        </button>
      </header>
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
