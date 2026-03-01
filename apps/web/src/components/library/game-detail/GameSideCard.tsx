/**
 * GameSideCard Component (Issue #3513)
 * Issue #4858: Replaced with MeepleInfoCard
 *
 * @deprecated Use `MeepleInfoCard` directly instead.
 * This wrapper delegates to MeepleInfoCard for backward compatibility.
 * @see {@link @/components/ui/data-display/meeple-info-card}
 */

'use client';

import { MeepleInfoCard } from '@/components/ui/data-display/meeple-info-card';

export interface GameSideCardProps {
  gameId: string;
  gameTitle: string;
  /** Optional BGG ID for external link */
  bggId?: number | null;
}

/**
 * @deprecated Use `MeepleInfoCard` directly.
 */
export function GameSideCard({ gameId, gameTitle, bggId }: GameSideCardProps) {
  return (
    <MeepleInfoCard
      gameId={gameId}
      gameTitle={gameTitle}
      bggId={bggId}
      showKnowledgeBase
      showSocialLinks
      data-testid="game-side-card"
    />
  );
}
