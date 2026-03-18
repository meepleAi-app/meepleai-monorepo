'use client';

/**
 * GameDetailKbTab — Knowledge Base tab wrapping MeepleInfoCard
 *
 * Listens for 'game-detail:upload-pdf' CustomEvent from the ActionBar
 * and triggers PDF upload via MeepleInfoCard's internal state.
 * Includes RulebookSection for direct rulebook upload and chat navigation.
 */

import { useEffect, useMemo, useRef } from 'react';

import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { RulebookSection } from '@/components/game/rulebook-section';
import { MeepleInfoCard } from '@/components/ui/data-display/meeple-info-card';
import { useGamesWithKb } from '@/lib/domain-hooks/use-games-with-kb';

export interface GameDetailKbTabProps {
  gameId: string;
  gameTitle: string;
  bggId?: number | null;
}

export function GameDetailKbTab({ gameId, gameTitle, bggId }: GameDetailKbTabProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const router = useRouter();
  const { user } = useAuth();

  const { data: gamesWithKb } = useGamesWithKb(user?.id ?? '');

  const rulebooks = useMemo(() => {
    const game = gamesWithKb?.find(g => g.gameId === gameId);
    return game?.rulebooks ?? [];
  }, [gamesWithKb, gameId]);

  // Bridge the ActionBar 'upload-pdf' event — scroll KB tab into view as feedback
  useEffect(() => {
    const handler = () => {
      containerRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    };
    document.addEventListener('game-detail:upload-pdf', handler);
    return () => document.removeEventListener('game-detail:upload-pdf', handler);
  }, []);

  return (
    <div ref={containerRef} className="space-y-6">
      <MeepleInfoCard
        gameId={gameId}
        gameTitle={gameTitle}
        bggId={bggId}
        showKnowledgeBase
        showSocialLinks
        showStats={false}
      />
      <RulebookSection
        gameId={gameId}
        rulebooks={rulebooks}
        onChatClick={() => router.push(`/chat/new?game=${gameId}`)}
      />
    </div>
  );
}
