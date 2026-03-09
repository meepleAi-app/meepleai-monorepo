'use client';

/**
 * GameDetailKbTab — Knowledge Base tab wrapping MeepleInfoCard
 *
 * Listens for 'game-detail:upload-pdf' CustomEvent from the ActionBar
 * and triggers PDF upload via MeepleInfoCard's internal state.
 */

import { useEffect, useRef } from 'react';

import { MeepleInfoCard } from '@/components/ui/data-display/meeple-info-card';

export interface GameDetailKbTabProps {
  gameId: string;
  gameTitle: string;
  bggId?: number | null;
}

export function GameDetailKbTab({ gameId, gameTitle, bggId }: GameDetailKbTabProps) {
  const containerRef = useRef<HTMLDivElement>(null);

  // Bridge the ActionBar 'upload-pdf' event — scroll KB tab into view as feedback
  useEffect(() => {
    const handler = () => {
      containerRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    };
    document.addEventListener('game-detail:upload-pdf', handler);
    return () => document.removeEventListener('game-detail:upload-pdf', handler);
  }, []);

  return (
    <div ref={containerRef}>
      <MeepleInfoCard
        gameId={gameId}
        gameTitle={gameTitle}
        bggId={bggId}
        showKnowledgeBase
        showSocialLinks
        showStats={false}
      />
    </div>
  );
}
